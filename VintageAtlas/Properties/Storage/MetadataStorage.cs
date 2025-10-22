using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using VintageAtlas.Export;
using VintageAtlas.Models.Domain;
using Vintagestory.API.MathTools;

namespace VintageAtlas.Storage;

/// <summary>
/// MBTiles storage for map tiles following the MBTiles specification
/// https://github.com/mapbox/mbtiles-spec
/// Thread-safe implementation using connection string instead of shared connection
/// </summary>
public sealed class MetadataStorage : IDisposable
{
    private readonly string _connectionString;
    private readonly string _dbPath;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public MetadataStorage(string dbPath)
    {
        _dbPath = dbPath;

        // Create directory if needed (skip for :memory: databases)
        if (dbPath != ":memory:")
        {
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        // Use connection string instead of shared connection for thread safety
        // For in-memory databases, use URI format with shared cache and unique name to ensure
        // all connections see the same database while maintaining test isolation
        // Enable WAL mode for better concurrent write performance
        // Note: Busy timeout is set via PRAGMA in InitializeDatabase
        if (dbPath == ":memory:")
        {
            // Use named shared cache for in-memory database with unique name
            // This ensures each instance has its own isolated database
            var uniqueName = $"memdb_{Guid.NewGuid():N}";
            _connectionString = $"Data Source=file:{uniqueName}?mode=memory&cache=shared;";
        }
        else
        {
            _connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared;Pooling=True";
        }

        InitializeDatabase();
    }

    /// <summary>
    /// Helper to create and open a connection with proper PRAGMA settings
    /// </summary>
    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Set a busy timeout to 30 seconds (30000ms) to wait for locks
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA busy_timeout=30000;";
        cmd.ExecuteNonQuery();

        return connection;
    }

    public void AddTrader(long id, string name, string type, BlockPos pos)
    {
        using var connection = CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO traders (id, name, type, pos) VALUES (@id, @name, @type, @pos)";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@type", type);
        cmd.Parameters.AddWithValue("@pos", $"{pos.X},{pos.Y},{pos.Z}");
        cmd.ExecuteNonQuery();
    }

    public void RemoveTrader(long id)
    {
        using var connection = CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM traders WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public Task<List<Trader>> GetTraders()
    {
        using var connection = CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM traders";
        var result = cmd.ExecuteReader();
        var traders = new List<Trader>();
        while (result.Read())
        {
            var unparsedPos = result.GetString(3).Split(',');
            traders.Add(new Trader
            {
                Id = result.GetInt64(0),
                Name = result.GetString(1),
                Type = result.GetString(2),
                Pos = new BlockPos(int.Parse(unparsedPos[0]), int.Parse(unparsedPos[1]), int.Parse(unparsedPos[2]))
            });
        }
        return Task.FromResult(traders);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            // Initialize database synchronously (one-time setup)
            InitializeDatabaseInternal();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private void InitializeDatabase()
    {
        if (_initialized) return;

        _initLock.Wait();
        try
        {
            if (_initialized) return;

            InitializeDatabaseInternal();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private void InitializeDatabaseInternal()
    {
        // Actual initialization logic (called by both sync and async methods)

        using var connection = CreateConnection();
        using var cmd = connection.CreateCommand();

        // Enable WAL mode for better concurrent access
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        cmd.ExecuteNonQuery();

        // Create tables
        cmd.CommandText = """
        CREATE TABLE IF NOT EXISTS traders (
            id INTEGER PRIMARY KEY,
            name TEXT NOT NULL,
            type TEXT NOT NULL,
            pos TEXT NOT NULL
        );
        
        CREATE TABLE IF NOT EXISTS climate_data (
            layer_type TEXT NOT NULL,
            x INTEGER NOT NULL,
            z INTEGER NOT NULL,
            value REAL NOT NULL,
            real_value REAL NOT NULL,
            PRIMARY KEY (layer_type, x, z)
        );
        
        CREATE INDEX IF NOT EXISTS idx_climate_layer ON climate_data(layer_type);
        """;
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Vacuum the database to reclaim space and optimize
    /// </summary>
    public async Task VacuumAsync()
    {
        await using var connection = CreateConnection();
        await Task.CompletedTask; // Already opened in CreateConnection()

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "VACUUM";
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Get database file size in bytes
    /// </summary>
    public long GetDatabaseSize()
    {
        return File.Exists(_dbPath) ? new FileInfo(_dbPath).Length : 0;
    }

    /// <summary>
    /// Manually checkpoint the WAL to commit all pending writes to the main database file.
    /// Call this after batch operations (e.g., full export) to ensure data is persisted.
    /// </summary>
    public void CheckpointWal()
    {
        try
        {
            using var connection = CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
            // Result: 0 = success, busy = database locked
        }
        catch (Exception)
        {
            // Ignore checkpoint errors (might be locked)
        }
    }

    /// <summary>
    /// Checkpoint the WAL and dispose of resources.
    /// This ensures all data is written to the main database file.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        try
        {
            // Checkpoint WAL to ensure all data is committed to the main DB
            using var connection = CreateConnection();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            cmd.ExecuteNonQuery();
        }
        catch (Exception)
        {
            // Ignore errors during dispose
        }
        finally
        {
            _initLock?.Dispose();
        }
    }
    
    /// <summary>
    /// Store climate data points in batch for efficient database writes
    /// </summary>
    /// <param name="layerType">Layer type identifier ("temperature" or "rainfall")</param>
    /// <param name="points">Climate data points to store</param>
    public async Task StoreClimateDataAsync(string layerType, List<ClimatePoint> points)
    {
        await EnsureInitializedAsync();
        
        await using var connection = CreateConnection();
        
        // Start transaction for batch insert
        await using var transaction = await connection.BeginTransactionAsync();
        
        try
        {
            // Clear existing data for this layer type
            await using (var deleteCmd = connection.CreateCommand())
            {
                deleteCmd.Transaction = (SqliteTransaction)transaction;
                deleteCmd.CommandText = "DELETE FROM climate_data WHERE layer_type = @layerType";
                deleteCmd.Parameters.AddWithValue("@layerType", layerType);
                await deleteCmd.ExecuteNonQueryAsync();
            }
            
            // Insert new data in batches
            const int batchSize = 1000;
            for (var i = 0; i < points.Count; i += batchSize)
            {
                var batch = points.GetRange(i, Math.Min(batchSize, points.Count - i));
                
                var sql = new StringBuilder("INSERT INTO climate_data (layer_type, x, z, value, real_value) VALUES ");
                var first = true;
                
                await using var insertCmd = connection.CreateCommand();
                insertCmd.Transaction = (SqliteTransaction)transaction;
                
                for (var j = 0; j < batch.Count; j++)
                {
                    var point = batch[j];
                    if (!first) sql.Append(", ");
                    first = false;
                    
                    sql.Append($"(@layerType, @x{j}, @z{j}, @value{j}, @realValue{j})");
                    insertCmd.Parameters.AddWithValue($"@x{j}", point.X);
                    insertCmd.Parameters.AddWithValue($"@z{j}", point.Z);
                    insertCmd.Parameters.AddWithValue($"@value{j}", point.Value);
                    insertCmd.Parameters.AddWithValue($"@realValue{j}", point.RealValue);
                }
                
                insertCmd.Parameters.AddWithValue("@layerType", layerType);
                insertCmd.CommandText = sql.ToString();
                await insertCmd.ExecuteNonQueryAsync();
            }
            
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    
    /// <summary>
    /// Get climate data as GeoJSON-ready structure
    /// </summary>
    /// <param name="layerType">Layer type identifier ("temperature" or "rainfall")</param>
    public async Task<List<ClimatePoint>> GetClimateDataAsync(string layerType)
    {
        await EnsureInitializedAsync();
        
        await using var connection = CreateConnection();
        await using var cmd = connection.CreateCommand();
        
        cmd.CommandText = "SELECT x, z, value, real_value FROM climate_data WHERE layer_type = @layerType";
        cmd.Parameters.AddWithValue("@layerType", layerType);
        
        var points = new List<ClimatePoint>();
        await using var reader = await cmd.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            points.Add(new ClimatePoint
            {
                X = reader.GetInt32(0),
                Z = reader.GetInt32(1),
                Value = reader.GetFloat(2),
                RealValue = reader.GetFloat(3)
            });
        }
        
        return points;
    }
}
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace VintageAtlas.Storage;

/// <summary>
/// MBTiles storage for map tiles following the MBTiles specification
/// https://github.com/mapbox/mbtiles-spec
/// Thread-safe implementation using connection string instead of shared connection
/// </summary>
public sealed class MbTilesStorage : IDisposable
{
    private readonly string _connectionString;
    private readonly string _dbPath;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public MbTilesStorage(string dbPath)
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

        // Create a tile table
        cmd.CommandText = """

                                              CREATE TABLE IF NOT EXISTS tiles (
                                                  zoom_level INTEGER NOT NULL,
                                                  tile_column INTEGER NOT NULL,
                                                  tile_row INTEGER NOT NULL,
                                                  tile_data BLOB NOT NULL,
                                                  PRIMARY KEY (zoom_level, tile_column, tile_row)
                                              );
                                              
                                              CREATE INDEX IF NOT EXISTS tiles_zoom_idx ON tiles(zoom_level);
                                              
                                              -- Metadata table (optional but recommended)
                                              CREATE TABLE IF NOT EXISTS metadata (
                                                  name TEXT PRIMARY KEY,
                                                  value TEXT
                                              );
                                          
                              """;

        cmd.ExecuteNonQuery();

        // Set metadata
        SetMetadataInternal(connection, "name", "VintageAtlas Map");
        SetMetadataInternal(connection, "type", "baselayer");
        SetMetadataInternal(connection, "version", "1.0");
        SetMetadataInternal(connection, "description", "Vintage Story World Map");
        SetMetadataInternal(connection, "format", "png");
    }

    /// <summary>
    /// Store a tile in the database
    /// </summary>
    public async Task PutTileAsync(int zoom, int x, int y, byte[] tileData)
    {
        // NOTE: VintageAtlas uses ABSOLUTE world tile coordinates (e.g., 2000, 3000),
        // not zoom-relative coordinates (0 to 2^zoom-1).
        // Standard TMS conversion doesn't apply here - we store coordinates as-is.
        // The MBTiles spec allows this for custom coordinate systems.

        await EnsureInitializedAsync(); // CRITICAL: Initialize DB before first write

        await using var connection = CreateConnection();
        await Task.CompletedTask; // Already opened in CreateConnection()

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """

                                      INSERT OR REPLACE INTO tiles (zoom_level, tile_column, tile_row, tile_data)
                                      VALUES (@zoom, @x, @y, @data)
                                  
                          """;

        cmd.Parameters.AddWithValue("@zoom", zoom);
        cmd.Parameters.AddWithValue("@x", x);
        cmd.Parameters.AddWithValue("@y", y); // Use absolute tile coordinates
        cmd.Parameters.AddWithValue("@data", tileData);

        await cmd.ExecuteNonQueryAsync();

        // Maintain minzoom/maxzoom metadata automatically
        await UpdateZoomMetadataAsync(zoom);
    }

    /// <summary>
    /// Retrieve a tile from the database
    /// </summary>
    public async Task<byte[]?> GetTileAsync(int zoom, int x, int y)
    {
        await EnsureInitializedAsync(); // Ensure database is initialized

        // Use absolute tile coordinates (no TMS conversion needed)
        await using var connection = CreateConnection();
        await Task.CompletedTask; // Already opened in CreateConnection()

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """

                                      SELECT tile_data FROM tiles
                                      WHERE zoom_level = @zoom AND tile_column = @x AND tile_row = @y
                                  
                          """;

        cmd.Parameters.AddWithValue("@zoom", zoom);
        cmd.Parameters.AddWithValue("@x", x);
        cmd.Parameters.AddWithValue("@y", y); // Use absolute tile coordinates

        var result = await cmd.ExecuteScalarAsync();
        return result as byte[];
    }

    /// <summary>
    /// Check if a tile exists
    /// </summary>
    public async Task<bool> TileExistsAsync(int zoom, int x, int y)
    {
        await EnsureInitializedAsync(); // Ensure database is initialized

        // Use absolute tile coordinates (no TMS conversion needed)
        await using var connection = CreateConnection();
        await Task.CompletedTask; // Already opened in CreateConnection()

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """

                                      SELECT COUNT(*) FROM tiles
                                      WHERE zoom_level = @zoom AND tile_column = @x AND tile_row = @y
                                  
                          """;

        cmd.Parameters.AddWithValue("@zoom", zoom);
        cmd.Parameters.AddWithValue("@x", x);
        cmd.Parameters.AddWithValue("@y", y); // Use absolute tile coordinates

        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        return count > 0;
    }

    /// <summary>
    /// Delete a specific tile
    /// </summary>
    public async Task DeleteTileAsync(int zoom, int x, int y)
    {
        // Use absolute tile coordinates (no TMS conversion needed)
        await using var connection = CreateConnection();
        await Task.CompletedTask; // Already opened in CreateConnection()

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """

                                      DELETE FROM tiles
                                      WHERE zoom_level = @zoom AND tile_column = @x AND tile_row = @y
                                  
                          """;

        cmd.Parameters.AddWithValue("@zoom", zoom);
        cmd.Parameters.AddWithValue("@x", x);
        cmd.Parameters.AddWithValue("@y", y); // Use absolute tile coordinates

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Get tile count for a specific zoom level
    /// </summary>
    public async Task<long> GetTileCountAsync(int? zoom = null)
    {
        await EnsureInitializedAsync(); // Ensure database is initialized

        await using var connection = CreateConnection();
        await Task.CompletedTask; // Already opened in CreateConnection()

        await using var cmd = connection.CreateCommand();

        if (zoom.HasValue)
        {
            cmd.CommandText = "SELECT COUNT(*) FROM tiles WHERE zoom_level = @zoom";
            cmd.Parameters.AddWithValue("@zoom", zoom.Value);
        }
        else
        {
            cmd.CommandText = "SELECT COUNT(*) FROM tiles";
        }

        return (long)(await cmd.ExecuteScalarAsync() ?? 0L);
    }

    /// <summary>
    /// Get tile extent (min/max coordinates) for a specific zoom level
    /// </summary>
    public async Task<TileExtent?> GetTileExtentAsync(int zoom)
    {
        await EnsureInitializedAsync(); // Ensure database is initialized

        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """

                                              SELECT 
                                                  MIN(tile_column) as minX,
                                                  MAX(tile_column) as maxX,
                                                  MIN(tile_row) as minY,
                                                  MAX(tile_row) as maxY
                                              FROM tiles 
                                              WHERE zoom_level = @zoom
                                          
                              """;
            cmd.Parameters.AddWithValue("@zoom", zoom);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                // Check if the result is NULL (no tiles for this zoom level)
                if (await reader.IsDBNullAsync(0))
                {
                    Console.WriteLine($"[MBTilesStorage] No tiles found for zoom level {zoom} (NULL result)");
                    return null;
                }

                var extent = new TileExtent
                {
                    MinX = reader.GetInt32(0),
                    MaxX = reader.GetInt32(1),
                    MinY = reader.GetInt32(2),
                    MaxY = reader.GetInt32(3)
                };

                Console.WriteLine($"[MBTilesStorage] Found tile extent for zoom {zoom}: ({extent.MinX},{extent.MinY}) to ({extent.MaxX},{extent.MaxY})");

                return extent;
            }

            Console.WriteLine($"[MBTilesStorage] No tiles found for zoom level {zoom} (no rows)");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MBTilesStorage] Error querying tile extent for zoom {zoom}: {ex.Message}");
            Console.WriteLine($"[MBTilesStorage] Stack trace: {ex.StackTrace}");
            return null;
        }
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
    /// Set metadata value (internal helper with provided connection)
    /// </summary>
    private static void SetMetadataInternal(SqliteConnection connection, string name, string value)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """

                                      INSERT OR REPLACE INTO metadata (name, value)
                                      VALUES (@name, @value)
                                  
                          """;
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Get metadata value by name, or null if not set
    /// </summary>
    public async Task<string?> GetMetadataAsync(string name)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM metadata WHERE name = @name";
        cmd.Parameters.AddWithValue("@name", name);
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString();
    }

    /// <summary>
    /// Set metadata value (INSERT OR REPLACE)
    /// </summary>
    public async Task SetMetadataAsync(string name, string value)
    {
        await EnsureInitializedAsync();
        await using var connection = CreateConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """

                                      INSERT OR REPLACE INTO metadata (name, value)
                                      VALUES (@name, @value)
                                  
                          """;
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@value", value);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<int?> GetMetadataIntAsync(string name)
    {
        var s = await GetMetadataAsync(name);
        if (int.TryParse(s, out var v)) return v;
        return null;
    }

    /// <summary>
    /// Update minzoom/maxzoom metadata based on a new tile at the given zoom level
    /// </summary>
    private async Task UpdateZoomMetadataAsync(int zoom)
    {
        try
        {
            // Update minzoom
            var minZoom = await GetMetadataIntAsync("minzoom");
            if (!minZoom.HasValue || zoom < minZoom.Value)
            {
                await SetMetadataAsync("minzoom", zoom.ToString());
            }

            // Update maxzoom
            var maxZoom = await GetMetadataIntAsync("maxzoom");
            if (!maxZoom.HasValue || zoom > maxZoom.Value)
            {
                await SetMetadataAsync("maxzoom", zoom.ToString());
            }
        }
        catch (Exception)
        {
            // Metadata update is best-effort; ignore failures so tile writes are not blocked
        }
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
}

/// <summary>
/// Tile extent data (min/max tile coordinates)
/// </summary>
public class TileExtent
{
    public int MinX { get; set; }
    public int MaxX { get; set; }
    public int MinY { get; set; }
    public int MaxY { get; set; }
}


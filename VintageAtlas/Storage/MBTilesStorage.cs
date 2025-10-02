using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace VintageAtlas.Storage;

/// <summary>
/// MBTiles storage for map tiles following the MBTiles specification
/// https://github.com/mapbox/mbtiles-spec
/// </summary>
public class MBTilesStorage : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _dbPath;

    public MBTilesStorage(string dbPath)
    {
        _dbPath = dbPath;
        
        // Create directory if needed
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var cmd = _connection.CreateCommand();
        
        // Create tiles table
        cmd.CommandText = @"
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
        ";
        
        cmd.ExecuteNonQuery();
        
        // Set metadata
        SetMetadata("name", "VintageAtlas Map");
        SetMetadata("type", "baselayer");
        SetMetadata("version", "1.0");
        SetMetadata("description", "Vintage Story World Map");
        SetMetadata("format", "png");
    }

    /// <summary>
    /// Store a tile in the database
    /// </summary>
    public async Task PutTileAsync(int zoom, int x, int y, byte[] tileData)
    {
        // MBTiles uses TMS (Tile Map Service) scheme, which has Y origin at bottom
        // OpenLayers uses XYZ scheme with Y origin at top
        // Convert: tms_y = (2^zoom - 1) - xyz_y
        var tmsY = ((1 << zoom) - 1) - y;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO tiles (zoom_level, tile_column, tile_row, tile_data)
            VALUES (@zoom, @x, @y, @data)
        ";
        
        cmd.Parameters.AddWithValue("@zoom", zoom);
        cmd.Parameters.AddWithValue("@x", x);
        cmd.Parameters.AddWithValue("@y", tmsY);
        cmd.Parameters.AddWithValue("@data", tileData);
        
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Retrieve a tile from the database
    /// </summary>
    public async Task<byte[]?> GetTileAsync(int zoom, int x, int y)
    {
        // Convert to TMS coordinates
        var tmsY = ((1 << zoom) - 1) - y;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT tile_data FROM tiles
            WHERE zoom_level = @zoom AND tile_column = @x AND tile_row = @y
        ";
        
        cmd.Parameters.AddWithValue("@zoom", zoom);
        cmd.Parameters.AddWithValue("@x", x);
        cmd.Parameters.AddWithValue("@y", tmsY);
        
        var result = await cmd.ExecuteScalarAsync();
        return result as byte[];
    }

    /// <summary>
    /// Check if a tile exists
    /// </summary>
    public async Task<bool> TileExistsAsync(int zoom, int x, int y)
    {
        var tmsY = ((1 << zoom) - 1) - y;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COUNT(*) FROM tiles
            WHERE zoom_level = @zoom AND tile_column = @x AND tile_row = @y
        ";
        
        cmd.Parameters.AddWithValue("@zoom", zoom);
        cmd.Parameters.AddWithValue("@x", x);
        cmd.Parameters.AddWithValue("@y", tmsY);
        
        var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
        return count > 0;
    }

    /// <summary>
    /// Delete a specific tile
    /// </summary>
    public async Task DeleteTileAsync(int zoom, int x, int y)
    {
        var tmsY = ((1 << zoom) - 1) - y;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM tiles
            WHERE zoom_level = @zoom AND tile_column = @x AND tile_row = @y
        ";
        
        cmd.Parameters.AddWithValue("@zoom", zoom);
        cmd.Parameters.AddWithValue("@x", x);
        cmd.Parameters.AddWithValue("@y", tmsY);
        
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Get tile count for a specific zoom level
    /// </summary>
    public async Task<long> GetTileCountAsync(int? zoom = null)
    {
        using var cmd = _connection.CreateCommand();
        
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
    /// Vacuum the database to reclaim space and optimize
    /// </summary>
    public async Task VacuumAsync()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "VACUUM";
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Set metadata value
    /// </summary>
    private void SetMetadata(string name, string value)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO metadata (name, value)
            VALUES (@name, @value)
        ";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Get database file size in bytes
    /// </summary>
    public long GetDatabaseSize()
    {
        if (File.Exists(_dbPath))
        {
            return new FileInfo(_dbPath).Length;
        }
        return 0;
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}


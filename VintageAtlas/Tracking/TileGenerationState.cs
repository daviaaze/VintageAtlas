using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VintageAtlas.Tracking;

/// <summary>
/// Tracks tile generation state in a SQLite database
/// Records what tiles have been generated, when, and their status
/// </summary>
public class TileGenerationState : IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly string _dbPath;
    private SqliteConnection? _db;
    private readonly object _dbLock = new();

    public TileGenerationState(ICoreServerAPI sapi, string dataDirectory)
    {
        _sapi = sapi;
        _dbPath = Path.Combine(dataDirectory, "tile_state.db");

        Initialize();
    }

    private void Initialize()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath) ?? "");

            _db = new SqliteConnection($"Data Source={_dbPath}");
            _db.Open();

            CreateTables();

            _sapi.Logger.Notification($"[VintageAtlas] Tile state database initialized at: {_dbPath}");
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to initialize tile state database: {ex.Message}");
            throw;
        }
    }

    private void CreateTables()
    {
        if (_db == null) return;

        lock (_dbLock)
        {
            using var cmd = _db.CreateCommand();

            // Table to track tile generation status
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS tiles (
                    zoom INTEGER NOT NULL,
                    tile_x INTEGER NOT NULL,
                    tile_z INTEGER NOT NULL,
                    status TEXT NOT NULL,
                    generated_at INTEGER,
                    last_updated INTEGER,
                    file_size INTEGER,
                    generation_time_ms INTEGER,
                    source_chunks INTEGER,
                    error_count INTEGER DEFAULT 0,
                    last_error TEXT,
                    PRIMARY KEY (zoom, tile_x, tile_z)
                );
                
                CREATE INDEX IF NOT EXISTS idx_tiles_status ON tiles(status);
                CREATE INDEX IF NOT EXISTS idx_tiles_updated ON tiles(last_updated);
                
                -- Table to track chunk-to-tile mappings (for invalidation)
                CREATE TABLE IF NOT EXISTS chunk_tiles (
                    chunk_x INTEGER NOT NULL,
                    chunk_z INTEGER NOT NULL,
                    zoom INTEGER NOT NULL,
                    tile_x INTEGER NOT NULL,
                    tile_z INTEGER NOT NULL,
                    PRIMARY KEY (chunk_x, chunk_z, zoom, tile_x, tile_z)
                );
                
                CREATE INDEX IF NOT EXISTS idx_chunk_lookup ON chunk_tiles(chunk_x, chunk_z);
                
                -- Table to track generation queue
                CREATE TABLE IF NOT EXISTS generation_queue (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    zoom INTEGER NOT NULL,
                    tile_x INTEGER NOT NULL,
                    tile_z INTEGER NOT NULL,
                    priority INTEGER DEFAULT 5,
                    queued_at INTEGER NOT NULL,
                    reason TEXT,
                    UNIQUE(zoom, tile_x, tile_z)
                );
                
                CREATE INDEX IF NOT EXISTS idx_queue_priority ON generation_queue(priority DESC, queued_at ASC);
            ";

            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Record that a tile has been generated
    /// </summary>
    public void RecordTileGenerated(int zoom, int tileX, int tileZ, long generationTimeMs, int sourceChunks, long fileSize)
    {
        if (_db == null) return;

        lock (_dbLock)
        {
            try
            {
                var now = _sapi.World.ElapsedMilliseconds;

                using var cmd = _db.CreateCommand();
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO tiles 
                    (zoom, tile_x, tile_z, status, generated_at, last_updated, file_size, generation_time_ms, source_chunks, error_count)
                    VALUES (@zoom, @x, @z, 'ready', @now, @now, @size, @time, @chunks, 0)
                ";

                cmd.Parameters.AddWithValue("@zoom", zoom);
                cmd.Parameters.AddWithValue("@x", tileX);
                cmd.Parameters.AddWithValue("@z", tileZ);
                cmd.Parameters.AddWithValue("@now", now);
                cmd.Parameters.AddWithValue("@size", fileSize);
                cmd.Parameters.AddWithValue("@time", generationTimeMs);
                cmd.Parameters.AddWithValue("@chunks", sourceChunks);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to record tile generation: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Record that a tile generation failed
    /// </summary>
    public void RecordTileError(int zoom, int tileX, int tileZ, string error)
    {
        if (_db == null) return;

        lock (_dbLock)
        {
            try
            {
                var now = _sapi.World.ElapsedMilliseconds;

                using var cmd = _db.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO tiles 
                    (zoom, tile_x, tile_z, status, last_updated, error_count, last_error)
                    VALUES (@zoom, @x, @z, 'error', @now, 1, @error)
                    ON CONFLICT(zoom, tile_x, tile_z) DO UPDATE SET
                        status = 'error',
                        last_updated = @now,
                        error_count = error_count + 1,
                        last_error = @error
                ";

                cmd.Parameters.AddWithValue("@zoom", zoom);
                cmd.Parameters.AddWithValue("@x", tileX);
                cmd.Parameters.AddWithValue("@z", tileZ);
                cmd.Parameters.AddWithValue("@now", now);
                cmd.Parameters.AddWithValue("@error", error);

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to record tile error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Check if a tile exists and is up to date
    /// </summary>
    public TileStatus GetTileStatus(int zoom, int tileX, int tileZ)
    {
        if (_db == null) return new TileStatus { Status = "missing" };

        lock (_dbLock)
        {
            try
            {
                using var cmd = _db.CreateCommand();
                cmd.CommandText = @"
                    SELECT status, generated_at, last_updated, file_size, error_count, last_error
                    FROM tiles
                    WHERE zoom = @zoom AND tile_x = @x AND tile_z = @z
                ";

                cmd.Parameters.AddWithValue("@zoom", zoom);
                cmd.Parameters.AddWithValue("@x", tileX);
                cmd.Parameters.AddWithValue("@z", tileZ);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return new TileStatus
                    {
                        Status = reader.GetString(0),
                        GeneratedAt = reader.IsDBNull(1) ? 0 : reader.GetInt64(1),
                        LastUpdated = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                        FileSize = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                        ErrorCount = reader.GetInt32(4),
                        LastError = reader.IsDBNull(5) ? null : reader.GetString(5)
                    };
                }

                return new TileStatus { Status = "missing" };
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to get tile status: {ex.Message}");
                return new TileStatus { Status = "error" };
            }
        }
    }

    /// <summary>
    /// Map chunks to tiles for invalidation tracking
    /// </summary>
    public void MapChunksToTile(int zoom, int tileX, int tileZ, List<Vec2i> chunks)
    {
        if (_db == null || chunks.Count == 0) return;

        lock (_dbLock)
        {
            try
            {
                using var transaction = _db.BeginTransaction();

                foreach (var chunk in chunks)
                {
                    using var cmd = _db.CreateCommand();
                    cmd.CommandText = @"
                        INSERT OR IGNORE INTO chunk_tiles (chunk_x, chunk_z, zoom, tile_x, tile_z)
                        VALUES (@cx, @cz, @zoom, @tx, @tz)
                    ";

                    cmd.Parameters.AddWithValue("@cx", chunk.X);
                    cmd.Parameters.AddWithValue("@cz", chunk.Y);
                    cmd.Parameters.AddWithValue("@zoom", zoom);
                    cmd.Parameters.AddWithValue("@tx", tileX);
                    cmd.Parameters.AddWithValue("@tz", tileZ);

                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to map chunks to tile: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Find all tiles affected by chunk changes
    /// </summary>
    public List<TileCoordinate> GetTilesAffectedByChunks(List<Vec2i> chunks)
    {
        if (_db == null || chunks.Count == 0) return [];

        lock (_dbLock)
        {
            try
            {
                var tiles = new HashSet<TileCoordinate>();

                using var cmd = _db.CreateCommand();
                var chunkParams = string.Join(",", chunks.Select((_, i) => $"(@cx{i}, @cz{i})"));
                cmd.CommandText = $@"
                    SELECT DISTINCT zoom, tile_x, tile_z
                    FROM chunk_tiles
                    WHERE (chunk_x, chunk_z) IN ({chunkParams})
                ";

                for (var i = 0; i < chunks.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@cx{i}", chunks[i].X);
                    cmd.Parameters.AddWithValue($"@cz{i}", chunks[i].Y);
                }

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    tiles.Add(new TileCoordinate
                    {
                        Zoom = reader.GetInt32(0),
                        X = reader.GetInt32(1),
                        Z = reader.GetInt32(2)
                    });
                }

                return tiles.ToList();
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to get affected tiles: {ex.Message}");
                return new List<TileCoordinate>();
            }
        }
    }

    /// <summary>
    /// Add tiles to generation queue
    /// </summary>
    public void QueueTilesForGeneration(List<TileCoordinate> tiles, string reason, int priority = 5)
    {
        if (_db == null || tiles.Count == 0) return;

        lock (_dbLock)
        {
            try
            {
                var now = _sapi.World.ElapsedMilliseconds;

                using var transaction = _db.BeginTransaction();

                foreach (var tile in tiles)
                {
                    using var cmd = _db.CreateCommand();
                    cmd.CommandText = @"
                        INSERT OR IGNORE INTO generation_queue (zoom, tile_x, tile_z, priority, queued_at, reason)
                        VALUES (@zoom, @x, @z, @priority, @now, @reason)
                    ";

                    cmd.Parameters.AddWithValue("@zoom", tile.Zoom);
                    cmd.Parameters.AddWithValue("@x", tile.X);
                    cmd.Parameters.AddWithValue("@z", tile.Z);
                    cmd.Parameters.AddWithValue("@priority", priority);
                    cmd.Parameters.AddWithValue("@now", now);
                    cmd.Parameters.AddWithValue("@reason", reason);

                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();

                _sapi.Logger.Debug($"[VintageAtlas] Queued {tiles.Count} tiles for generation: {reason}");
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to queue tiles: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Get next batch of tiles to generate (highest priority first)
    /// </summary>
    public List<TileCoordinate> GetNextBatch(int batchSize = 10)
    {
        if (_db == null) return new List<TileCoordinate>();

        lock (_dbLock)
        {
            try
            {
                var tiles = new List<TileCoordinate>();

                using var cmd = _db.CreateCommand();
                cmd.CommandText = @"
                    SELECT id, zoom, tile_x, tile_z
                    FROM generation_queue
                    ORDER BY priority DESC, queued_at ASC
                    LIMIT @limit
                ";

                cmd.Parameters.AddWithValue("@limit", batchSize);

                var idsToDelete = new List<long>();

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    idsToDelete.Add(reader.GetInt64(0));
                    tiles.Add(new TileCoordinate
                    {
                        Zoom = reader.GetInt32(1),
                        X = reader.GetInt32(2),
                        Z = reader.GetInt32(3)
                    });
                }

                reader.Close();

                // Remove from queue
                if (idsToDelete.Count > 0)
                {
                    using var deleteCmd = _db.CreateCommand();
                    deleteCmd.CommandText = $@"
                        DELETE FROM generation_queue
                        WHERE id IN ({string.Join(",", idsToDelete)})
                    ";
                    deleteCmd.ExecuteNonQuery();
                }

                return tiles;
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to get next batch: {ex.Message}");
                return new List<TileCoordinate>();
            }
        }
    }

    /// <summary>
    /// Get statistics about tile generation
    /// </summary>
    public TileStatistics GetStatistics()
    {
        if (_db == null) return new TileStatistics();

        lock (_dbLock)
        {
            try
            {
                using var cmd = _db.CreateCommand();
                cmd.CommandText = @"
                    SELECT 
                        COUNT(*) as total,
                        SUM(CASE WHEN status = 'ready' THEN 1 ELSE 0 END) as ready,
                        SUM(CASE WHEN status = 'error' THEN 1 ELSE 0 END) as errors,
                        SUM(file_size) as total_size,
                        AVG(generation_time_ms) as avg_time,
                        (SELECT COUNT(*) FROM generation_queue) as queued
                    FROM tiles
                ";

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    return new TileStatistics
                    {
                        TotalTiles = reader.GetInt32(0),
                        ReadyTiles = reader.GetInt32(1),
                        ErrorTiles = reader.GetInt32(2),
                        TotalSizeBytes = reader.IsDBNull(3) ? 0 : reader.GetInt64(3),
                        AverageGenerationTimeMs = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                        QueuedTiles = reader.GetInt32(5)
                    };
                }

                return new TileStatistics();
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to get statistics: {ex.Message}");
                return new TileStatistics();
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_dbLock)
            {
                _db?.Close();
                _db?.Dispose();
                _db = null;
            }

            _sapi.Logger.Notification("[VintageAtlas] Tile state database disposed");
        }
    }
}
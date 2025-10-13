using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;
using Vintagestory.Server;
using VintageAtlas.Core;
using Vintagestory.Common.Database;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Config;

namespace VintageAtlas.Export;

/// <summary>
/// Extracts chunk data on main thread following Vintage Story constraints
/// 
/// KEY CONSTRAINTS (from vintagestory-modding-constraints.md):
/// - Chunk access MUST be on main thread
/// - Cannot cache chunk references (prevents unloading)
/// - Extract quickly, minimize main thread time
/// 
/// HYBRID APPROACH:
/// - Try to get chunks from memory first (fast, preferred)
/// - Fall back to database read if not in memory (slower but works)
/// </summary>
public class ChunkDataExtractor
{
    private readonly ICoreServerAPI _sapi;
    private readonly ServerMain _server;
    private readonly ModConfig _config;
    private readonly SavegameDataLoader? _dataLoader;
    private static readonly int ChunkSize = GlobalConstants.ChunkSize;

    public ChunkDataExtractor(ICoreServerAPI sapi, ModConfig config)
    {
        _sapi = sapi;
        _server = (ServerMain)sapi.World;
        _config = config;

        // NOTE: Database fallback disabled due to lock conflicts with live server
        // The game's savegame DB is locked during runtime, causing "database is locked" errors
        // Live map will only show currently loaded chunks (explored areas)
        // For full map export, use /atlas export command instead
        _sapi.Logger.Notification("[VintageAtlas] ChunkDataExtractor initialized (memory-only mode)");
        _sapi.Logger.Debug("[VintageAtlas] Live map shows explored areas only. Use /atlas export for full map.");
        _dataLoader = null;
    }

    /// <summary>
    /// Extract all chunks needed for a tile
    /// MUST be called from main thread or via EnqueueMainThreadTask
    ///
    /// IMPORTANT: Tile coordinates are ALWAYS in absolute chunk space.
    /// The coordinate transformation happens in MapConfigController when serving extent/config to frontend.
    /// This ensures tiles are generated at their actual world positions.
    /// </summary>
    public TileChunkData ExtractTileData(int zoom, int tileX, int tileZ)
    {
        var tileSize = _config.TileSize;
        var chunksPerTile = tileSize / ChunkSize;

        var tileData = new TileChunkData
        {
            TileX = tileX,
            TileZ = tileZ,
            Zoom = zoom,
            TileSize = tileSize,
            ChunksPerTileEdge = chunksPerTile
        };

        // Calculate starting chunk coordinates for this tile
        // Tiles are always in absolute world coordinates - no offset needed here
        // The frontend handles coordinate transformation via worldExtent from MapConfigController
        var startChunkX = tileX * chunksPerTile;
        var startChunkZ = tileZ * chunksPerTile;

        _sapi.Logger.VerboseDebug(
            $"[VintageAtlas] Extracting tile z{zoom} t({tileX},{tileZ}) → chunks({startChunkX},{startChunkZ})-({startChunkX + chunksPerTile - 1},{startChunkZ + chunksPerTile - 1}) [absolute coordinates]");

        // Extract all chunks that make up this tile
        for (var offsetX = 0; offsetX < chunksPerTile; offsetX++)
        {
            for (var offsetZ = 0; offsetZ < chunksPerTile; offsetZ++)
            {
                var chunkX = startChunkX + offsetX;
                var chunkZ = startChunkZ + offsetZ;

                // Extract chunk snapshot
                var snapshot = ExtractChunkSnapshot(chunkX, chunkZ);
                if (snapshot.IsLoaded)
                {
                    tileData.AddChunk(snapshot);
                }
            }
        }

        var loadedCount = tileData.Chunks.Count(c => c.Value.IsLoaded);
        _sapi.Logger.Debug(
            $"[VintageAtlas] Extracted {tileData.Chunks.Count}/{chunksPerTile * chunksPerTile} chunks for tile z{zoom} t({tileX},{tileZ}), {loadedCount} loaded");

        if (loadedCount == 0)
        {
            _sapi.Logger.Warning($"[VintageAtlas] No chunks successfully loaded for tile z{zoom} t({tileX},{tileZ})");
        }

        return tileData;
    }

    /// <summary>
    /// Extract a single chunk snapshot
    /// MUST be called from the main thread
    /// Tries memory first, falls back to the database if available
    /// 
    /// PERFORMANCE: Uses direct chunk.Data[] access instead of 32,768 GetBlockId() calls
    /// This provides ~1000x speedup for block data extraction
    /// </summary>
    private ChunkSnapshot ExtractChunkSnapshot(int chunkX, int chunkZ)
    {
        var snapshot = new ChunkSnapshot
        {
            ChunkX = chunkX,
            ChunkZ = chunkZ,
            IsLoaded = false
        };

        try
        {
            // Try to get map chunk from memory
            var mapChunk = _server.WorldMap.GetMapChunk(chunkX, chunkZ);

            if (!TryGetMapChunkData(mapChunk, chunkX, chunkZ, out var validMapChunk))
            {
                return ExtractChunkFromDatabase(chunkX, chunkZ);
            }

            // Extract height map
            ExtractHeightMapData(snapshot, validMapChunk);

            // Determine surface chunk Y and get world chunk
            var surfaceChunkY = DetermineSurfaceChunkY(snapshot.HeightMap);
            snapshot.ChunkY = surfaceChunkY;

            var worldChunk = _sapi.World.BlockAccessor.GetChunk(chunkX, surfaceChunkY, chunkZ);
            if (worldChunk == null)
            {
                _sapi.Logger.VerboseDebug(
                    $"[VintageAtlas] Could not access block data for chunk ({chunkX},{surfaceChunkY},{chunkZ})");
                return snapshot;
            }

            // Extract block IDs
            ExtractBlockData(snapshot, chunkX, surfaceChunkY, chunkZ);

            // Extract block entities for chiseled blocks, signs, etc.
            ExtractBlockEntities(snapshot, chunkX, surfaceChunkY, chunkZ);

            snapshot.IsLoaded = true;
            snapshot.SnapshotTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _sapi.Logger.Warning(
                $"[VintageAtlas] Failed to extract chunk ({chunkX},{chunkZ}): {ex.Message}");
        }

        return snapshot;
    }

    private bool TryGetMapChunkData(IMapChunk? mapChunk, int chunkX, int chunkZ, out IMapChunk validMapChunk)
    {
        validMapChunk = null!;

        var hasData = mapChunk is { RainHeightMap: not null } &&
                      mapChunk.RainHeightMap.Any(h => h > 0);

        if (mapChunk == null || !hasData)
        {
            _sapi.Logger.VerboseDebug(
                _dataLoader != null
                    ? $"[VintageAtlas] Chunk ({chunkX},{chunkZ}) {(mapChunk is null ? "not in memory" : "empty in memory")}, trying database..."
                    : $"[VintageAtlas] Chunk ({chunkX},{chunkZ}) not available (no memory/database or no data)");
            return false;
        }

        validMapChunk = mapChunk;
        return true;
    }

    private static void ExtractHeightMapData(ChunkSnapshot snapshot, IMapChunk mapChunk)
    {
        snapshot.HeightMap = new int[ChunkSize * ChunkSize];

        if (mapChunk.RainHeightMap == null) return;

        for (var i = 0; i < mapChunk.RainHeightMap.Length && i < snapshot.HeightMap.Length; i++)
        {
            snapshot.HeightMap[i] = mapChunk.RainHeightMap[i];
        }
    }

    private void ExtractBlockData(ChunkSnapshot snapshot, int chunkX, int surfaceChunkY, int chunkZ)
    {
        snapshot.BlockIds = new int[ChunkSize * ChunkSize * ChunkSize];

        // Direct chunk data access - much faster than 32,768 GetBlockId() calls
        var chunk = _sapi.World.BlockAccessor.GetChunk(chunkX, surfaceChunkY, chunkZ);
        if (chunk?.Data == null)
        {
            _sapi.Logger.VerboseDebug(
                $"[VintageAtlas] Chunk ({chunkX},{surfaceChunkY},{chunkZ}) has no data array");
            return;
        }

        // Copy block data directly from chunk's internal array  
        // chunk.Data is IChunkBlocks - need to copy block by block
        for (int i = 0; i < snapshot.BlockIds.Length; i++)
        {
            snapshot.BlockIds[i] = chunk.Data[i];
        }

        _sapi.Logger.VerboseDebug(
            $"[VintageAtlas] Copied {snapshot.BlockIds.Length} block IDs from chunk ({chunkX},{surfaceChunkY},{chunkZ})");
    }

    private void ExtractBlockEntities(ChunkSnapshot snapshot, int chunkX, int surfaceChunkY, int chunkZ)
    {
        snapshot.BlockEntities = new Dictionary<BlockPos, BlockEntity>();

        try
        {
            var chunk = _sapi.World.BlockAccessor.GetChunk(chunkX, surfaceChunkY, chunkZ);
            if (chunk?.BlockEntities == null)
            {
                return;
            }

            // Copy all block entities from this chunk
            foreach (var kvp in chunk.BlockEntities)
            {
                // BlockPos is mutable and may be reused by the engine; copy to avoid later mutation side-effects
                var posCopy = kvp.Key?.Copy();
                if (posCopy != null)
                {
                    snapshot.BlockEntities[posCopy] = kvp.Value;
                }
            }

            _sapi.Logger.VerboseDebug(
                $"[VintageAtlas] Extracted {snapshot.BlockEntities.Count} block entities from chunk ({chunkX},{surfaceChunkY},{chunkZ})");
        }
        catch (Exception ex)
        {
            _sapi.Logger.Warning(
                $"[VintageAtlas] Failed to extract block entities from chunk ({chunkX},{surfaceChunkY},{chunkZ}): {ex.Message}");
        }
    }

    /// <summary>
    /// Determine which Y chunk level contains the surface for this area
    /// Most efficient to extract the chunk with most surface blocks
    /// </summary>
    private static int DetermineSurfaceChunkY(int[] heightMap)
    {
        if (heightMap.Length == 0) return 4; // Default to Y chunk 4 (Y=128-159)

        // Find average height (only count non-zero values)
        var validHeights = heightMap.Where(h => h > 0).ToArray();
        if (validHeights.Length == 0) return 4;

        var avgHeight = (int)validHeights.Average();
        var chunkY = avgHeight / ChunkSize;

        // Clamp to reasonable values
        // Most worlds have surface between Y=64 (chunk 2) and Y=192 (chunk 6)
        return Math.Clamp(chunkY, 2, 8);
    }

    /// <summary>
    /// Extract multiple tiles at once (more efficient for main thread usage)
    /// MUST be called from main thread
    /// </summary>
    public List<TileChunkData> ExtractMultipleTiles(int zoom, List<(int tileX, int tileZ)> tiles)
    {
        var results = new List<TileChunkData>();

        foreach (var (tileX, tileZ) in tiles)
        {
            results.Add(ExtractTileData(zoom, tileX, tileZ));
        }

        return results;
    }

    /// <summary>
    /// Extract chunk data directly from database when not in memory
    /// This is slower than memory access but allows access to all chunks
    /// </summary>
    private ChunkSnapshot ExtractChunkFromDatabase(int chunkX, int chunkZ)
    {
        var snapshot = new ChunkSnapshot
        {
            ChunkX = chunkX,
            ChunkZ = chunkZ,
            IsLoaded = false
        };

        if (_dataLoader == null)
        {
            return snapshot;
        }

        try
        {
            // Get database connection
            var sqliteConn = _dataLoader.SqliteThreadConn;

            try
            {
                // Load map chunk from database for height data
                var pos = new ChunkPos(chunkX, 0, chunkZ);
                var mapChunkData = _dataLoader.GetServerMapChunk(sqliteConn, pos);

                if (mapChunkData != null)
                {
                    // Extract height map
                    snapshot.HeightMap = new int[ChunkSize * ChunkSize];
                    if (mapChunkData.RainHeightMap != null)
                    {
                        for (var i = 0; i < mapChunkData.RainHeightMap.Length && i < snapshot.HeightMap.Length; i++)
                        {
                            snapshot.HeightMap[i] = mapChunkData.RainHeightMap[i];
                        }
                    }

                    // Determine surface chunk Y
                    var surfaceChunkY = DetermineSurfaceChunkY(snapshot.HeightMap);
                    snapshot.ChunkY = surfaceChunkY;

                    // Load actual chunk data from database
                    pos = new ChunkPos(chunkX, surfaceChunkY, chunkZ);
                    var chunkIndex = pos.ToChunkIndex();
                    var serverChunk = _dataLoader.GetServerChunk(sqliteConn, chunkIndex);

                    if (serverChunk is { Data: not null })
                    {
                        // Extract block IDs from chunk data
                        snapshot.BlockIds = new int[ChunkSize * ChunkSize * ChunkSize];

                        // ServerChunk.Data is the raw block data array
                        for (var i = 0; i < serverChunk.Data.Length && i < snapshot.BlockIds.Length; i++)
                        {
                            snapshot.BlockIds[i] = serverChunk.Data[i];
                        }

                        // Extract block entities from server chunk
                        snapshot.BlockEntities = new Dictionary<BlockPos, BlockEntity>();
                        if (serverChunk.BlockEntities != null)
                        {
                            foreach (var kvp in serverChunk.BlockEntities)
                            {
                                snapshot.BlockEntities[kvp.Key] = kvp.Value;
                            }
                        }

                        snapshot.IsLoaded = true;
                        snapshot.SnapshotTime = DateTime.UtcNow;

                        _sapi.Logger.VerboseDebug(
                            $"[VintageAtlas] Loaded chunk ({chunkX},{surfaceChunkY},{chunkZ}) from database with {snapshot.BlockEntities.Count} block entities");
                    }
                }
            }
            finally
            {
                // Always free the connection
                sqliteConn.Free();
            }
        }
        catch (Exception ex)
        {
            _sapi.Logger.Warning(
                $"[VintageAtlas] Failed to load chunk ({chunkX},{chunkZ}) from database: {ex.Message}");
        }

        return snapshot;
    }
}


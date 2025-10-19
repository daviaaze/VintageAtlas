using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Server;
using VintageAtlas.Core;
using Vintagestory.Common.Database;
using Vintagestory.GameContent;
using Vintagestory.API.Config;
using VintageAtlas.Models.Domain;

namespace VintageAtlas.Export;

/// <summary>
/// Chunk data source that reads directly from the savegame database.
/// Used for full map exports where we need to access all chunks, even unloaded ones.
/// Does NOT require main thread access (reads from the database directly).
/// </summary>
public sealed class SavegameDataSource(ServerMain server, ModConfig config, ILogger logger)
    : IChunkDataSource, IDisposable
{
    private readonly SavegameDataLoader _loader = new(server, config.MaxDegreeOfParallelism, logger);
    private readonly int _chunkSize = MagicNum.ServerChunkSize;

    public string SourceName => "SavegameDatabase";
    public bool RequiresMainThread => false;

    /// <summary>
    /// Get all map chunk positions that exist in the savegame database.
    /// Used to calculate the actual world extent for tile generation.
    /// </summary>
    public List<ChunkPos> GetAllMapChunkPositions()
    {
        var sqliteConn = _loader.SqliteThreadConn;
        List<ChunkPos> positions;

        lock (sqliteConn.Con)
        {
            positions = _loader.GetAllMapChunkPositions(sqliteConn).ToList();
        }

        sqliteConn.Free();
        logger.Notification($"[VintageAtlas] Found {positions.Count} map chunks in savegame database");
        return positions;
    }

    public List<ChunkPos> GetAllMapRegionPositions()
    {
        List<ChunkPos> positions;
        var sqliteConn = _loader.SqliteThreadConn;
        lock (sqliteConn)
        {
            positions = _loader.GetAllMapRegions(sqliteConn).ToList();
        }
        
        sqliteConn.Free();
        logger.Notification($"[VintageAtlas] Found {positions.Count} map regions in savegame database");
        return positions;
    }

    public ServerMapRegion? GetServerMapRegion(ulong position)
    {
        var sqliteConn = _loader.SqliteThreadConn;
        try
        {
            lock (sqliteConn.Con)
            {
                return _loader.GetServerMapRegion(sqliteConn, position);
            }
        }
        finally
        {
            sqliteConn.Free();
        }
    }

    /// <summary>
    /// Get chunks for a tile from the savegame database.
    /// This can be called from any thread (does not require the main thread).
    /// </summary>
    public async Task<TileChunkData?> GetTileChunksAsync(int zoom, int tileX, int tileZ)
    {
        return await Task.Run(() =>
        {
            try
            {
                var chunksPerTile = config.TileSize / _chunkSize;
                var startChunkX = tileX * chunksPerTile;
                var startChunkZ = tileZ * chunksPerTile;

                var tileData = new TileChunkData
                {
                    Zoom = zoom,
                    TileX = tileX,
                    TileZ = tileZ,
                    TileSize = config.TileSize,
                    ChunksPerTileEdge = chunksPerTile
                };

                var sqliteConn = _loader.SqliteThreadConn;

                try
                {
                    lock (sqliteConn)
                    {
                        // Load all chunks for this tile
                        for (var offsetX = 0; offsetX < chunksPerTile; offsetX++)
                        {
                            for (var offsetZ = 0; offsetZ < chunksPerTile; offsetZ++)
                            {
                                var chunkX = startChunkX + offsetX;
                                var chunkZ = startChunkZ + offsetZ;

                                // Map chunks always use Y=0 (they represent the 2D surface view)
                                // The actual 3D world chunks are loaded dynamically based on height
                                const int chunkY = 0;

                                var chunkPos = new ChunkPos(chunkX, chunkY, chunkZ);

                                // Get ServerMapChunk (contains height map and top block IDs)
                                var mapChunk = _loader.GetServerMapChunk(sqliteConn, chunkPos);

                                if (mapChunk is null)
                                    continue;

                                // Create a snapshot from ServerMapChunk
                                var snapshot = CreateSnapshotFromMapChunk(mapChunk, chunkX, chunkY, chunkZ, sqliteConn);
                                if (snapshot != null)
                                {
                                    tileData.AddChunk(snapshot);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    sqliteConn.Free();
                }

                return tileData.Chunks.Count != 0 ? tileData : null;
            }
            catch (Exception ex)
            {
                logger.Error($"[VintageAtlas] SavegameDataSource: Failed to load tile data: {ex.Message}");
                return null;
            }
        });
    }

    /// <summary>
    /// Create a ChunkSnapshot from ServerMapChunk data.
    /// ServerMapChunk contains a RainHeightMap with surface heights.
    /// </summary>
    private ChunkSnapshot? CreateSnapshotFromMapChunk(ServerMapChunk mapChunk, int chunkX, int chunkY, int chunkZ, SqliteThreadCon sqliteConn)
    {
        try
        {
            // ServerMapChunk contains RainHeightMap: array of heights (one per X,Z position)
            if (mapChunk.RainHeightMap == null)
            {
                return null;
            }

            var heightMap = new int[_chunkSize * _chunkSize];

            // For full rendering, we'd need to load ServerChunk data for block IDs
            // For now, we'll just populate the height map
            // The rendering code will need access to actual ServerChunks for block colors

            for (var x = 0; x < _chunkSize; x++)
            {
                for (var z = 0; z < _chunkSize; z++)
                {
                    var index = z * _chunkSize + x;
                    if (index < mapChunk.RainHeightMap.Length)
                    {
                        heightMap[index] = mapChunk.RainHeightMap[index];
                    }
                }
            }

            // ═══════════════════════════════════════════════════════════════
            // LOAD SURFACE BLOCK IDS FROM SERVER CHUNKS
            // For each X,Z position, we need to:
            // 1. Get the height from RainHeightMap
            // 2. Calculate which vertical chunk (chunkY) contains that height
            // 3. Load that ServerChunk from the database
            // 4. Extract the block ID at that position
            // This matches how Extractor.cs does it in ExtractWorldMap()
            // ═══════════════════════════════════════════════════════════════

            var blockIds = new int[_chunkSize * _chunkSize * _chunkSize];

            // Determine which vertical chunks we need to load
            var chunksToLoad = new HashSet<int>();
            for (var x = 0; x < _chunkSize; x++)
            {
                for (var z = 0; z < _chunkSize; z++)
                {
                    var mapIndex = z * _chunkSize + x;
                    if (mapIndex >= heightMap.Length || heightMap[mapIndex] <= 0)
                        continue;

                    var height = heightMap[mapIndex];
                    var chunkYForHeight = height / _chunkSize;
                    chunksToLoad.Add(chunkYForHeight);
                }
            }

            // Load the required ServerChunks
            var loadedChunks = new Dictionary<int, ServerChunk>();
            var allBlockEntities = new Dictionary<BlockPos, BlockEntity>();
            var allTraders = new Dictionary<long, Trader>();
            foreach (var y in chunksToLoad)
            {
                var chunkPos = new ChunkPos(chunkX, y, chunkZ);
                var serverChunk = _loader.GetServerChunk(sqliteConn, chunkPos.ToChunkIndex());
                if (serverChunk == null)
                    continue;

                serverChunk.Unpack_ReadOnly();
                loadedChunks[y] = serverChunk;

                if(serverChunk.EntitiesCount > 0) {
                    foreach(var entity in serverChunk.Entities.Where(e => e is EntityTrader).Cast<EntityTrader>()) {
                        var entityBehaviorName =
                            entity.WatchedAttributes.GetTreeAttribute("nametag").GetString("name");
                        // item-creature-humanoid-trader-commodities
                        var type = Lang.Get("item-creature-" + entity.Code.Path);
                        allTraders[entity.EntityId] = new Trader
                            {
                                Name = entityBehaviorName,
                                Type = type,
                                Pos = entity.Pos.AsBlockPos
                        };
                    }
                }

                // Collect block entities from this chunk
                if (serverChunk.BlockEntities != null)
                {
                    foreach (var kvp in serverChunk.BlockEntities)
                    {
                        allBlockEntities[kvp.Key] = kvp.Value;
                    }
                }
            }

            // Extract block IDs at surface positions (and handle snow-underlay)
            for (var x = 0; x < _chunkSize; x++)
            {
                for (var z = 0; z < _chunkSize; z++)
                {
                    var mapIndex = z * _chunkSize + x;
                    if (mapIndex >= heightMap.Length || heightMap[mapIndex] <= 0)
                        continue;

                    var height = heightMap[mapIndex];
                    var chunkYForHeight = height / _chunkSize;

                    if (!loadedChunks.TryGetValue(chunkYForHeight, out var serverChunk))
                        continue;

                    var localY = height % _chunkSize; // Y within the chunk
                    var chunkDataIndex = (localY * _chunkSize + z) * _chunkSize + x;
                    var blockId = serverChunk.Data[chunkDataIndex];

                    // Store in our BlockIds array at the same local Y position
                    var blockIndex = localY * _chunkSize * _chunkSize + z * _chunkSize + x;
                    if (blockIndex >= 0 && blockIndex < blockIds.Length)
                    {
                        blockIds[blockIndex] = blockId;
                    }

                    // If surface block is snow, also fetch the block underneath (height-1)
                    // This mirrors Extractor.cs behavior for snow-covered terrain
                    if (server.World.Blocks[blockId].BlockMaterial == EnumBlockMaterial.Snow)
                    {
                        var adjustedHeight = height - 1;
                        if (adjustedHeight >= 0)
                        {
                            var adjustedChunkY = adjustedHeight / _chunkSize;
                            var adjustedLocalY = adjustedHeight % _chunkSize;

                            // Ensure adjusted chunk is loaded
                            if (!loadedChunks.TryGetValue(adjustedChunkY, out var adjustedChunk))
                            {
                                var adjPos = new ChunkPos(chunkX, adjustedChunkY, chunkZ);
                                adjustedChunk = _loader.GetServerChunk(sqliteConn, adjPos.ToChunkIndex());
                                if (adjustedChunk != null)
                                {
                                    adjustedChunk.Unpack_ReadOnly();
                                    loadedChunks[adjustedChunkY] = adjustedChunk;

                                    // Merge its block entities as well
                                    if (adjustedChunk.BlockEntities != null)
                                    {
                                        foreach (var kvp in adjustedChunk.BlockEntities)
                                        {
                                            allBlockEntities[kvp.Key] = kvp.Value;
                                        }
                                    }
                                }
                            }

                            if (adjustedChunk != null)
                            {
                                var adjustedIndex = (adjustedLocalY * _chunkSize + z) * _chunkSize + x;
                                var underBlockId = adjustedChunk.Data[adjustedIndex];

                                var adjustedBlockIndex = adjustedLocalY * _chunkSize * _chunkSize + z * _chunkSize + x;
                                if (adjustedBlockIndex >= 0 && adjustedBlockIndex < blockIds.Length)
                                {
                                    blockIds[adjustedBlockIndex] = underBlockId;
                                }
                            }
                        }
                    }
                }
            }

            return new ChunkSnapshot
            {
                ChunkX = chunkX,
                ChunkY = chunkY,
                ChunkZ = chunkZ,
                HeightMap = heightMap,
                BlockIds = blockIds,
                BlockEntities = allBlockEntities,
                Traders = allTraders,
                IsLoaded = true
            };
        }
        catch (Exception ex)
        {
            logger.Error($"[VintageAtlas] Failed to create snapshot from map chunk: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _loader.Dispose();
        }
    }
}


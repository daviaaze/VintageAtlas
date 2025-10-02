using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Server;
using Vintagestory.ServerMods;
using VintageAtlas.Core;

namespace VintageAtlas.Export;

/// <summary>
/// Extracts chunk data on main thread following Vintage Story constraints
/// 
/// KEY CONSTRAINTS (from vintagestory-modding-constraints.md):
/// - Chunk access MUST be on main thread
/// - Cannot cache chunk references (prevents unloading)
/// - Extract quickly, minimize main thread time
/// </summary>
public class ChunkDataExtractor
{
    private readonly ICoreServerAPI _sapi;
    private readonly ServerMain _server;
    private readonly ModConfig _config;
    private const int CHUNK_SIZE = 32;
    
    public ChunkDataExtractor(ICoreServerAPI sapi, ModConfig config)
    {
        _sapi = sapi;
        _server = (ServerMain)sapi.World;
        _config = config;
    }
    
    /// <summary>
    /// Extract all chunks needed for a tile
    /// MUST be called from main thread or via EnqueueMainThreadTask
    /// </summary>
    public TileChunkData ExtractTileData(int zoom, int tileX, int tileZ)
    {
        var tileSize = _config.TileSize;
        var chunksPerTile = tileSize / CHUNK_SIZE;
        
        var tileData = new TileChunkData
        {
            TileX = tileX,
            TileZ = tileZ,
            Zoom = zoom,
            TileSize = tileSize,
            ChunksPerTileEdge = chunksPerTile
        };
        
        // Calculate starting chunk coordinates for this tile
        var startChunkX = tileX * chunksPerTile;
        var startChunkZ = tileZ * chunksPerTile;
        
        _sapi.Logger.VerboseDebug(
            $"[VintageAtlas] Extracting tile data: z{zoom} t({tileX},{tileZ}) chunks({startChunkX},{startChunkZ})-({startChunkX + chunksPerTile - 1},{startChunkZ + chunksPerTile - 1})");
        
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
        
        _sapi.Logger.VerboseDebug(
            $"[VintageAtlas] Extracted {tileData.Chunks.Count}/{chunksPerTile * chunksPerTile} chunks for tile z{zoom} t({tileX},{tileZ})");
        
        return tileData;
    }
    
    /// <summary>
    /// Extract a single chunk snapshot
    /// MUST be called from main thread
    /// </summary>
    public ChunkSnapshot ExtractChunkSnapshot(int chunkX, int chunkZ)
    {
        var snapshot = new ChunkSnapshot
        {
            ChunkX = chunkX,
            ChunkZ = chunkZ,
            IsLoaded = false
        };
        
        try
        {
            // Try to get map chunk first (lighter weight)
            var mapChunk = _server.WorldMap.GetMapChunk(chunkX, chunkZ);
            if (mapChunk == null)
            {
                _sapi.Logger.VerboseDebug($"[VintageAtlas] Chunk ({chunkX},{chunkZ}) not loaded");
                return snapshot;
            }
            
            // Extract height map
            snapshot.HeightMap = new int[CHUNK_SIZE * CHUNK_SIZE];
            if (mapChunk.RainHeightMap != null)
            {
                for (int i = 0; i < mapChunk.RainHeightMap.Length && i < snapshot.HeightMap.Length; i++)
                {
                    snapshot.HeightMap[i] = mapChunk.RainHeightMap[i];
                }
            }
            
            // Now extract block data - need full chunk access
            // We need to find which Y chunk contains the surface
            // Most surface blocks are between Y=64-192 (chunks 2-6)
            var surfaceChunkY = DetermineSurfaceChunkY(snapshot.HeightMap);
            snapshot.ChunkY = surfaceChunkY;
            
            // Get the actual chunk with block data
            var worldChunk = _sapi.World.BlockAccessor.GetChunk(chunkX, surfaceChunkY, chunkZ);
            if (worldChunk == null)
            {
                _sapi.Logger.VerboseDebug(
                    $"[VintageAtlas] Could not access block data for chunk ({chunkX},{surfaceChunkY},{chunkZ})");
                return snapshot;
            }
            
            // Extract block IDs
            snapshot.BlockIds = new int[CHUNK_SIZE * CHUNK_SIZE * CHUNK_SIZE];
            
            // Copy block data from chunk
            // ServerChunk has .Data array, but IWorldChunk doesn't expose it directly
            // We need to use BlockAccessor for safety
            var baseX = chunkX * CHUNK_SIZE;
            var baseY = surfaceChunkY * CHUNK_SIZE;
            var baseZ = chunkZ * CHUNK_SIZE;
            var pos = new BlockPos();
            
            for (int y = 0; y < CHUNK_SIZE; y++)
            {
                for (int z = 0; z < CHUNK_SIZE; z++)
                {
                    for (int x = 0; x < CHUNK_SIZE; x++)
                    {
                        pos.Set(baseX + x, baseY + y, baseZ + z);
                        int index = y * CHUNK_SIZE * CHUNK_SIZE + z * CHUNK_SIZE + x;
                        snapshot.BlockIds[index] = _sapi.World.BlockAccessor.GetBlockId(pos);
                    }
                }
            }
            
            snapshot.IsLoaded = true;
            snapshot.SnapshotTime = DateTime.UtcNow;
            
            _sapi.Logger.VerboseDebug(
                $"[VintageAtlas] Successfully extracted chunk ({chunkX},{surfaceChunkY},{chunkZ})");
        }
        catch (Exception ex)
        {
            _sapi.Logger.Warning(
                $"[VintageAtlas] Failed to extract chunk ({chunkX},{chunkZ}): {ex.Message}");
        }
        
        return snapshot;
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
        
        int avgHeight = (int)validHeights.Average();
        int chunkY = avgHeight / CHUNK_SIZE;
        
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
}


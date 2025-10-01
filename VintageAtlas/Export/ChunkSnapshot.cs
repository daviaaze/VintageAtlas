using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageAtlas.Export;

/// <summary>
/// Thread-safe snapshot of chunk data for tile rendering
/// Follows Vintage Story constraint: Extract on main thread, process on background thread
/// </summary>
public class ChunkSnapshot
{
    /// <summary>
    /// Chunk X coordinate in world space
    /// </summary>
    public int ChunkX { get; set; }
    
    /// <summary>
    /// Chunk Z coordinate in world space
    /// </summary>
    public int ChunkZ { get; set; }
    
    /// <summary>
    /// Chunk Y coordinate (height slice)
    /// </summary>
    public int ChunkY { get; set; }
    
    /// <summary>
    /// Block IDs in this chunk (32x32x32 = 32,768 blocks)
    /// Indexed as: [y * 32 * 32 + z * 32 + x]
    /// </summary>
    public int[] BlockIds { get; set; }
    
    /// <summary>
    /// Height map for this chunk (32x32 = 1,024 values)
    /// Indexed as: [z * 32 + x]
    /// Represents the Y coordinate of the topmost non-air block
    /// </summary>
    public int[] HeightMap { get; set; }
    
    /// <summary>
    /// Whether this chunk was successfully loaded
    /// False indicates chunk is not loaded or inaccessible
    /// </summary>
    public bool IsLoaded { get; set; }
    
    /// <summary>
    /// Timestamp when snapshot was taken
    /// </summary>
    public DateTime SnapshotTime { get; set; }
    
    /// <summary>
    /// Block entities in this chunk (for chiseled blocks, signs, etc.)
    /// Key: BlockPos, Value: BlockEntity
    /// </summary>
    public Dictionary<BlockPos, BlockEntity> BlockEntities { get; set; }
    
    public ChunkSnapshot()
    {
        BlockIds = [];
        HeightMap = [];
        BlockEntities = new Dictionary<BlockPos, BlockEntity>();
        IsLoaded = false;
        SnapshotTime = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Get block ID at local chunk coordinates
    /// </summary>
    public int GetBlockId(int localX, int localY, int localZ)
    {
        if (localX < 0 || localX >= 32 || 
            localY < 0 || localY >= 32 || 
            localZ < 0 || localZ >= 32)
        {
            throw new ArgumentOutOfRangeException(nameof(localX), "Coordinates must be 0-31");
        }
        
        var index = localY * 32 * 32 + localZ * 32 + localX;
        return BlockIds[index];
    }
    
    /// <summary>
    /// Get height at local X,Z coordinates
    /// </summary>
    public int GetHeight(int localX, int localZ)
    {
        if (localX < 0 || localX >= 32 || localZ < 0 || localZ >= 32)
        {
            throw new ArgumentOutOfRangeException(nameof(localX), "Coordinates must be 0-31");
        }
        
        var index = localZ * 32 + localX;
        return HeightMap[index];
    }
}

/// <summary>
/// Collection of chunk snapshots for a tile
/// A tile typically spans multiple chunks (e.g., 256px tile = 8x8 chunks)
/// </summary>
public class TileChunkData
{
    /// <summary>
    /// Tile X coordinate
    /// </summary>
    public int TileX { get; set; }
    
    /// <summary>
    /// Tile Z coordinate
    /// </summary>
    public int TileZ { get; set; }
    
    /// <summary>
    /// Zoom level
    /// </summary>
    public int Zoom { get; set; }
    
    /// <summary>
    /// All chunk snapshots needed for this tile
    /// Key: "chunkX_chunkZ_chunkY"
    /// </summary>
    public Dictionary<string, ChunkSnapshot> Chunks { get; set; }
    
    /// <summary>
    /// Number of chunks per tile edge (e.g., 8 for 256px tiles)
    /// </summary>
    public int ChunksPerTileEdge { get; set; }
    
    /// <summary>
    /// Tile size in pixels
    /// </summary>
    public int TileSize { get; set; }
    
    public TileChunkData()
    {
        Chunks = new Dictionary<string, ChunkSnapshot>();
    }
    
    /// <summary>
    /// Get chunk snapshot at world chunk coordinates
    /// </summary>
    public ChunkSnapshot? GetChunk(int chunkX, int chunkZ, int chunkY = 0)
    {
        var key = $"{chunkX}_{chunkZ}_{chunkY}";
        return Chunks.GetValueOrDefault(key);
    }
    
    /// <summary>
    /// Add chunk snapshot
    /// </summary>
    public void AddChunk(ChunkSnapshot snapshot)
    {
        var key = $"{snapshot.ChunkX}_{snapshot.ChunkZ}_{snapshot.ChunkY}";
        Chunks[key] = snapshot;
    }
}


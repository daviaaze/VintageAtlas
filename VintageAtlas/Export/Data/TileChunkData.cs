using System.Collections.Generic;

namespace VintageAtlas.Export.Data;

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
        Chunks = [];
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
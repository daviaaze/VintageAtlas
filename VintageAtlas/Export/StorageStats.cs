using System.Collections.Generic;

namespace VintageAtlas.Export;

/// <summary>
/// Storage statistics for tile generation
/// </summary>
public class StorageStats
{
    public long DatabaseSizeBytes { get; set; }
    public int MemoryCachedTiles { get; set; }
    public long TotalTiles { get; set; }
    public Dictionary<int, long> TilesPerZoom { get; set; } = [];
}
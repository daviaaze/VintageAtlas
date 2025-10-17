using System.Collections.Generic;

namespace VintageAtlas.Models.API;

public class TileStatistics
{
    public int TotalTiles { get; set; }
    public long TotalSizeBytes { get; set; }
    public Dictionary<int, ZoomLevelStats> ZoomLevels { get; set; } = new();
}
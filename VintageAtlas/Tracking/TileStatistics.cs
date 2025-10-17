namespace VintageAtlas.Tracking;

public class TileStatistics
{
    public int TotalTiles { get; set; }
    public int ReadyTiles { get; set; }
    public int ErrorTiles { get; set; }
    public long TotalSizeBytes { get; set; }
    public double AverageGenerationTimeMs { get; set; }
    public int QueuedTiles { get; set; }
}
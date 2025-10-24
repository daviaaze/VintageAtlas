namespace VintageAtlas.Export.Data;

/// <summary>
/// Progress information for export operations
/// </summary>
public class ExportProgress
{
    public int TilesCompleted { get; set; }
    public int TotalTiles { get; set; }
    public int CurrentZoomLevel { get; set; }
    public double PercentComplete => TotalTiles > 0 ? TilesCompleted * 100.0 / TotalTiles : 0;
}
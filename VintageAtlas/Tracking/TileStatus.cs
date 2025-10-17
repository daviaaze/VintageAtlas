namespace VintageAtlas.Tracking;

public class TileStatus
{
    public string Status { get; set; } = "missing";
    public long GeneratedAt { get; set; }
    public long LastUpdated { get; set; }
    public long FileSize { get; set; }
    public int ErrorCount { get; set; }
    public string? LastError { get; set; }
}
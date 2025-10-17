namespace VintageAtlas.Storage;

/// <summary>
/// Tile extent data (min/max tile coordinates)
/// </summary>
public class TileExtent
{
    public int MinX { get; set; }
    public int MaxX { get; set; }
    public int MinY { get; set; }
    public int MaxY { get; set; }
}
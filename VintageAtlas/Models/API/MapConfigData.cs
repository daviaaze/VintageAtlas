namespace VintageAtlas.Models.API;

public class MapConfigData
{
    /// <summary>
    /// Extent in world BLOCK coordinates: [minX, minZ, maxX, maxZ]
    /// These are game world coordinates that define the map bounds.
    /// OpenLayers uses these to create the TileGrid extent.
    /// Frontend's getTileUrl() maps grid coords to storage tile numbers.
    /// </summary>
    public int[] WorldExtent { get; set; } = [];

    /// <summary>
    /// Origin (top-left) in world BLOCK coordinates: [x, z]
    /// This is where tile (0,0) would be located in the tile grid.
    /// OpenLayers uses this to create the TileGrid origin.
    /// </summary>
    public int[] WorldOrigin { get; set; } = [];

    /// <summary>
    /// Default center in world BLOCK coordinates: [x, z]
    /// Usually the spawn point or middle of explored area.
    /// OpenLayers centers the view here initially.
    /// </summary>
    public int[] DefaultCenter { get; set; } = [];

    public int DefaultZoom { get; set; }
    public int MinZoom { get; set; }
    public int MaxZoom { get; set; }
    public int BaseZoomLevel { get; set; }
    public int TileSize { get; set; }

    /// <summary>
    /// Tile resolutions for each zoom level (blocks per pixel)
    /// </summary>
    public double[] TileResolutions { get; set; } = [];

    /// <summary>
    /// View resolutions for smooth zooming (includes extra zoom levels)
    /// </summary>
    public double[] ViewResolutions { get; set; } = [];

    /// <summary>
    /// Absolute tile origin per zoom level (X,Y) in storage coordinates.
    /// Allows the frontend to offset tile grid indices to direct XYZ requests.
    /// </summary>
    public int[][] OriginTilesPerZoom { get; set; } = [];

    /// <summary>
    /// Spawn position in world block coordinates: [x, z]
    /// </summary>
    public int[] SpawnPosition { get; set; } = [];

    public int MapSizeX { get; set; }
    public int MapSizeZ { get; set; }
    public int MapSizeY { get; set; }
    public TileStatistics? TileStats { get; set; }
    public string? ServerName { get; set; }
    public string? WorldName { get; set; }
}
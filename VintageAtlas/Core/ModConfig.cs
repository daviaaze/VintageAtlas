using System.IO;
using Newtonsoft.Json;
using VintageAtlas.Export.Colors;
using Vintagestory.API.Config;

namespace VintageAtlas.Core;

/// <summary>
/// Main configuration for VintageAtlas mod
/// </summary>
public class ModConfig
{
    #region Map Export Settings

    /// <summary>
    /// The color and height information the resulting images will use
    /// Default: MedievalStyleWithHillShading
    /// </summary>
    public ImageMode Mode { get; set; } = ImageMode.MedievalStyleWithHillShading;

    /// <summary>
    /// Path to a directory where all output files will be placed into
    /// </summary>
    public string OutputDirectory { get; set; } = Path.Combine(GamePaths.DataPath, "ModData", "VintageAtlas");

    [JsonIgnore]
    public string OutputDirectoryWorld { get; set; } = "";

    [JsonIgnore]
    public string OutputDirectoryGeojson { get; set; } = "";

    /// <summary>
    /// Extract the world's topmost blocklayer as images
    /// Default: true
    /// </summary>
    public bool ExtractWorldMap { get; set; } = true;

    /// <summary>
    /// Fix white lines on the in-game map by rebuilding the rain map
    /// Default: false
    /// </summary>
    public bool FixWhiteLines { get; set; } = false;

    /// <summary>
    /// Extract Traders and Translocators (only repaired ones)
    /// Default: true
    /// </summary>
    public bool ExtractStructures { get; set; } = true;

    /// <summary>
    /// Export the heightmap along with the world map
    /// Default: false
    /// </summary>
    public bool ExportHeightmap { get; set; } = false;

    /// <summary>
    /// Export signs that use the Automap sign prefixes
    /// Tags: &lt;AM:BASE&gt;, &lt;AM:MISC&gt;, &lt;AM:SERVER&gt;, &lt;AM:TL&gt;
    /// Default: true
    /// </summary>
    public bool ExportSigns { get; set; } = true;

    /// <summary>
    /// Export signs that don't use special tags
    /// Default: false
    /// </summary>
    public bool ExportUntaggedSigns { get; set; } = false;

    /// <summary>
    /// Export signs that use custom Automap sign prefixes
    /// Tags: &lt;AM:CONTINENT&gt;, &lt;AM:PORT&gt;, etc.
    /// Default: false
    /// </summary>
    public bool ExportCustomTaggedSigns { get; set; } = false;

    /// <summary>
    /// Set the size of the individual image tiles (must be evenly divisible by 32)
    /// Default: 256
    /// </summary>
    public int TileSize { get; set; } = 256;

    /// <summary>
    /// The number of zoom levels for the webmap to generate
    /// Default: 9
    /// </summary>
    public int BaseZoomLevel { get; set; } = 9;

    /// <summary>
    /// Create the zoom levels required for the web map
    /// Default: true
    /// </summary>
    public bool CreateZoomLevels { get; set; } = true;

    /// <summary>
    /// Number of concurrent tasks to use to generate tile images
    /// -1 uses all available processors
    /// Default: -1
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = -1;

    /// <summary>
    /// Start the exporter when the server is ready
    /// Default: false
    /// </summary>
    public bool ExportOnStart { get; set; } = false;

    /// <summary>
    /// Export geojson to show the version a chunk was generated in
    /// Default: false
    /// </summary>
    public bool ExportChunkVersionMap { get; set; } = false;

    /// <summary>
    /// Kicks everyone from the server and pauses for export
    /// Default: true
    /// </summary>
    public bool SaveMode { get; set; } = true;

    /// <summary>
    /// Stops the server when done exporting
    /// Default: false
    /// </summary>
    public bool StopOnDone { get; set; } = false;

    #endregion

    #region Web Server Settings

    /// <summary>
    /// Enable live web server for real-time player and animal data
    /// Default: true
    /// </summary>
    public bool EnableLiveServer { get; set; } = true;

    /// <summary>
    /// Port for the live server HTTP listener (defaults to game port + 1)
    /// </summary>
    public int? LiveServerPort { get; set; }

    /// <summary>
    /// Host for the live server
    /// Default: localhost
    /// </summary>
    public string LiveServerHost { get; set; } = "localhost";

    /// <summary>
    /// API endpoint path for status data
    /// Default: status
    /// </summary>
    public string LiveServerEndpoint { get; set; } = "status";

    /// <summary>
    /// Enable CORS for API endpoints
    /// Default: true
    /// </summary>
    public bool EnableCors { get; set; } = true;

    /// <summary>
    /// Auto-export map data periodically when a live server is running
    /// Default: true
    /// </summary>
    public bool AutoExportMap { get; set; } = true;

    /// <summary>
    /// Interval in milliseconds for auto map export
    /// Default: 300000 (5 minutes)
    /// </summary>
    public int MapExportIntervalMs { get; set; } = 300000;

    /// <summary>
    /// Maximum concurrent API requests allowed (prevents DoS attacks)
    /// Default: 50
    /// Recommended: 20 for small servers, 50 for medium, 100 for large
    /// </summary>
    public int? MaxConcurrentRequests { get; set; } = 50;

    /// <summary>
    /// Maximum concurrent tile requests allowed (separate from API limit)
    /// Default: 500
    /// High value needed for map tile loading (a single map view can request 20-50+ tiles)
    /// </summary>
    public int? MaxConcurrentTileRequests { get; set; } = 500;

    /// <summary>
    /// Maximum concurrent static file requests allowed (separate from API limit)
    /// Default: 200
    /// Used for HTML, CSS, JS, fonts, etc.
    /// </summary>
    public int? MaxConcurrentStaticRequests { get; set; } = 200;

    /// <summary>
    /// Base path for the web application (e.g., "/" or "/vintagestory/")
    /// Useful when serving behind nginx at a sub-path
    /// Default: "/"
    /// </summary>
    public string BasePath { get; set; } = "/";

    #endregion

    #region Historical Tracking Settings

    /// <summary>
    /// Enable historical tracking (player positions, entity census, server stats)
    /// Default: true
    /// </summary>
    public bool EnableHistoricalTracking { get; set; } = true;

    /// <summary>
    /// Interval in milliseconds for recording historical data
    /// Default: 5000 (5 seconds)
    /// </summary>
    public int HistoricalTickIntervalMs { get; set; } = 5000;

    #endregion
}
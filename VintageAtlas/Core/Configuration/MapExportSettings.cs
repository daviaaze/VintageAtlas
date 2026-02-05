using System.IO;
using Newtonsoft.Json;
using VintageAtlas.Export.Colors;
using Vintagestory.API.Config;

namespace VintageAtlas.Core.Configuration;

/// <summary>
/// Configuration settings specific to map export operations
/// </summary>
public class MapExportSettings
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
}


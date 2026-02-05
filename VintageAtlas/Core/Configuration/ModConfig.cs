using Newtonsoft.Json;
using VintageAtlas.Export.Colors;

namespace VintageAtlas.Core.Configuration;

/// <summary>
/// Main configuration for VintageAtlas mod.
/// Composed of focused setting classes following Single Responsibility Principle.
/// </summary>
public class ModConfig
{
    /// <summary>
    /// Settings related to map export operations
    /// </summary>
    [JsonProperty("export")]
    public MapExportSettings Export { get; set; } = new();

    /// <summary>
    /// Settings related to the live web server
    /// </summary>
    [JsonProperty("webServer")]
    public WebServerSettings WebServer { get; set; } = new();

    /// <summary>
    /// Settings related to historical data tracking
    /// </summary>
    [JsonProperty("tracking")]
    public TrackingSettings Tracking { get; set; } = new();

}


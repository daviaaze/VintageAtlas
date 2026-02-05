namespace VintageAtlas.Core.Configuration;

/// <summary>
/// Configuration settings specific to the live web server
/// </summary>
public class WebServerSettings
{
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
    /// Maximum concurrent requests allowed (prevents DoS attacks)
    /// Default: 100
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 100;

    /// <summary>
    /// Base path for the web application (e.g., "/" or "/vintagestory/")
    /// Useful when serving behind nginx at a sub-path
    /// Default: "/"
    /// </summary>
    public string BasePath { get; set; } = "/";

    #endregion
}


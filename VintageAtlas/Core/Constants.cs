namespace VintageAtlas.Core;

/// <summary>
/// Application-wide constants for VintageAtlas.
/// Centralizes magic numbers following KISS and maintainability principles.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Size of a single chunk in blocks (Vintage Story standard)
    /// </summary>
    public const int ChunkSize = 32;

    /// <summary>
    /// Delay in milliseconds to wait for player disconnections during save mode
    /// </summary>
    public const int DisconnectionDelayMs = 500;

    /// <summary>
    /// Interval for reporting export progress (every N tiles)
    /// </summary>
    public const int ProgressReportInterval = 100;

    /// <summary>
    /// Minimum recommended map export interval in milliseconds (10 seconds)
    /// </summary>
    public const int MinMapExportIntervalMs = 10000;

    /// <summary>
    /// Minimum recommended historical tracking interval in milliseconds (1 second)
    /// </summary>
    public const int MinHistoricalTickIntervalMs = 1000;

    /// <summary>
    /// Minimum valid tile size in pixels
    /// </summary>
    public const int MinTileSize = 32;

    /// <summary>
    /// Maximum valid tile size in pixels
    /// </summary>
    public const int MaxTileSize = 1024;

    /// <summary>
    /// Minimum valid zoom level
    /// </summary>
    public const int MinZoomLevel = 1;

    /// <summary>
    /// Maximum valid zoom level
    /// </summary>
    public const int MaxZoomLevel = 15;

    /// <summary>
    /// Minimum valid port number
    /// </summary>
    public const int MinPortNumber = 1;

    /// <summary>
    /// Maximum valid port number
    /// </summary>
    public const int MaxPortNumber = 65535;

    /// <summary>
    /// Minimum concurrent API requests
    /// </summary>
    public const int MinConcurrentRequests = 1;

    /// <summary>
    /// Maximum concurrent API requests
    /// </summary>
    public const int MaxConcurrentRequests = 1000;

}


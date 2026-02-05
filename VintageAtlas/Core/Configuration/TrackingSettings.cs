namespace VintageAtlas.Core.Configuration;

/// <summary>
/// Configuration settings specific to historical data tracking
/// </summary>
public class TrackingSettings
{
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


using System.Collections.Generic;
using VintageAtlas.Models;

namespace VintageAtlas.Core;

/// <summary>
/// Interface for collecting live game data
/// Thread-safe: UpdateCache() called from game tick, CollectData() called from HTTP threads
/// </summary>
public interface IDataCollector
{
    /// <summary>
    /// Returns pre-computed server status from cache
    /// Safe to call from any thread (HTTP threads)
    /// </summary>
    ServerStatusData CollectData();
}

/// <summary>
/// Interface for historical data tracking
/// </summary>
public interface IHistoricalTracker
{
    /// <summary>
    /// Get heatmap data for visualization
    /// </summary>
    List<HeatmapPoint> GetHeatmap(HistoricalQueryParams queryParams);
    
    /// <summary>
    /// Get player path history
    /// </summary>
    List<PlayerPathPoint> GetPlayerPath(HistoricalQueryParams queryParams);
    
    /// <summary>
    /// Get entity census data
    /// </summary>
    List<EntityCensusSnapshot> GetEntityCensus(HistoricalQueryParams queryParams);
    
    /// <summary>
    /// Get server statistics
    /// </summary>
    ServerStatistics GetServerStatistics();
}

/// <summary>
/// Interface for map export functionality
/// </summary>
public interface IMapExporter
{
    /// <summary>
    /// Start the map export process
    /// </summary>
    void StartExport();
    
    /// <summary>
    /// Check if export is currently running
    /// </summary>
    bool IsRunning { get; }
}


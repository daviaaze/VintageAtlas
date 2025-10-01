using System.Collections.Generic;
using VintageAtlas.Models;

namespace VintageAtlas.Core;

/// <summary>
/// Interface for collecting live game data
/// </summary>
public interface IDataCollector
{
    /// <summary>
    /// Collects current server status including players, animals, weather
    /// </summary>
    ServerStatusData CollectData();
}

/// <summary>
/// Interface for historical data tracking
/// </summary>
public interface IHistoricalTracker
{
    /// <summary>
    /// Initialize the historical tracker
    /// </summary>
    void Initialize();
    
    /// <summary>
    /// Called on game tick to update tracking data
    /// </summary>
    void OnGameTick(float deltaTime);
    
    /// <summary>
    /// Get heatmap data for visualization
    /// </summary>
    List<Models.HeatmapPoint> GetHeatmap(HistoricalQueryParams queryParams);
    
    /// <summary>
    /// Get player path history
    /// </summary>
    List<Models.PlayerPathPoint> GetPlayerPath(HistoricalQueryParams queryParams);
    
    /// <summary>
    /// Get entity census data
    /// </summary>
    List<Models.EntityCensusSnapshot> GetEntityCensus(HistoricalQueryParams queryParams);
    
    /// <summary>
    /// Get server statistics
    /// </summary>
    ServerStatistics GetServerStatistics();
    
    /// <summary>
    /// Record a player death event
    /// </summary>
    void RecordPlayerDeath(Vintagestory.API.Server.IServerPlayer player, string? deathSource);
    
    /// <summary>
    /// Dispose resources
    /// </summary>
    void Dispose();
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


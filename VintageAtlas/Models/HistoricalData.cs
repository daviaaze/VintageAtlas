using System.Collections.Generic;

namespace VintageAtlas.Models;

/// <summary>
/// Player position snapshot for historical tracking
/// </summary>
public class PlayerPositionSnapshot
{
    public long Id { get; set; }
    public long Timestamp { get; set; }
    public string PlayerUid { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double? Health { get; set; }
    public double? MaxHealth { get; set; }
    public double? Hunger { get; set; }
    public double? MaxHunger { get; set; }
    public double? Temperature { get; set; }
    public double? BodyTemp { get; set; }
}

/// <summary>
/// Entity census data for a specific time period
/// </summary>
public class EntityCensusSnapshot
{
    public long Id { get; set; }
    public long Timestamp { get; set; }
    public string EntityType { get; set; } = "";
    public int Count { get; set; }
    public double? AvgHealth { get; set; }
    public double? MinX { get; set; }
    public double? MaxX { get; set; }
    public double? MinZ { get; set; }
    public double? MaxZ { get; set; }
}

/// <summary>
/// Server performance statistics
/// </summary>
public class ServerStatsSnapshot
{
    public long Id { get; set; }
    public long Timestamp { get; set; }
    public int PlayersOnline { get; set; }
    public int EntitiesLoaded { get; set; }
    public int ChunksLoaded { get; set; }
    public double? MemoryMb { get; set; }
    public double ServerUptimeSeconds { get; set; }
}

/// <summary>
/// Player death event
/// </summary>
public class PlayerDeathEvent
{
    public long Id { get; set; }
    public long Timestamp { get; set; }
    public string PlayerUid { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public string? DamageSource { get; set; }
}

/// <summary>
/// Heatmap data point for visualization
/// </summary>
public class HeatmapPoint
{
    public double X { get; set; }
    public double Z { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// Player path point for visualization
/// </summary>
public class PlayerPathPoint
{
    public long Timestamp { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public double? Health { get; set; }
}

/// <summary>
/// Historical data query parameters
/// </summary>
public class HistoricalQueryParams
{
    public string? PlayerUid { get; set; }
    public string? EntityType { get; set; }
    public long? FromTimestamp { get; set; }
    public long? ToTimestamp { get; set; }
    public int? Hours { get; set; }
    public int? GridSize { get; set; } = 32; // For heatmap grid resolution
}

/// <summary>
/// Server statistics response
/// </summary>
public class ServerStatistics
{
    public long CurrentTimestamp { get; set; }
    public int TotalPlayersTracked { get; set; }
    public int TotalPositionsRecorded { get; set; }
    public int TotalDeaths { get; set; }
    public long OldestDataTimestamp { get; set; }
    public double DatabaseSizeMb { get; set; }
    public List<EntityTypeCount> EntityTypeCounts { get; set; } = new();
    public List<PlayerActivitySummary> TopPlayers { get; set; } = new();
}

public class EntityTypeCount
{
    public string EntityType { get; set; } = "";
    public int TotalSightings { get; set; }
    public int CurrentCount { get; set; }
}

public class PlayerActivitySummary
{
    public string PlayerUid { get; set; } = "";
    public string PlayerName { get; set; } = "";
    public int TotalSnapshots { get; set; }
    public long FirstSeenTimestamp { get; set; }
    public long LastSeenTimestamp { get; set; }
    public double TotalDistanceTraveled { get; set; }
    public int Deaths { get; set; }
}


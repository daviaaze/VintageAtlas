using System.Collections.Generic;
using VintageAtlas.Models.API;

namespace VintageAtlas.Models.Domain;

/// <summary>
/// Current server status data for API responses
/// </summary>
public class ServerStatusData
{
    public SpawnPoint SpawnPoint { get; set; } = null!;
    public DateInfo Date { get; set; } = null!;
    public double? SpawnTemperature { get; set; }
    public double? SpawnRainfall { get; set; }
    public WeatherInfo Weather { get; set; } = null!;
    public List<PlayerData> Players { get; set; } = new();
    public List<AnimalData> Animals { get; set; } = new();
}
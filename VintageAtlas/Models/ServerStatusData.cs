using System.Collections.Generic;

namespace VintageAtlas.Models;

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

public class SpawnPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public class DateInfo
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public int Hour { get; set; }
    public int Minute { get; set; }
}

public class WeatherInfo
{
    public double Temperature { get; set; }
    public double Rainfall { get; set; }
    public double WindSpeed { get; set; }
}

public class PlayerData
{
    public string Name { get; set; } = "";
    public string Uid { get; set; } = "";
    public CoordinateData Coordinates { get; set; } = null!;
    public HealthData Health { get; set; } = null!;
    public HealthData Hunger { get; set; } = null!;
    public double? Temperature { get; set; }
    public double? BodyTemp { get; set; }
}

public class AnimalData
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public CoordinateData Coordinates { get; set; } = null!;
    public HealthData Health { get; set; } = null!;
    public double? Temperature { get; set; }
    public double? Rainfall { get; set; }
    public WindData Wind { get; set; } = null!;
}

public class CoordinateData
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public class HealthData
{
    public double Current { get; set; }
    public double Max { get; set; }
}

public class WindData
{
    public double Percent { get; set; }
}


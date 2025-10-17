namespace VintageAtlas.Models.API;

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
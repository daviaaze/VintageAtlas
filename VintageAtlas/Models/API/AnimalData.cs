namespace VintageAtlas.Models.API;

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
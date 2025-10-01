using Newtonsoft.Json;

namespace VintageAtlas.GeoJson.Sign;

public record SignFeature(SignProperties Properties, PointGeometry Geometry, string Type = "Feature")
{
    [JsonProperty("type")] public string Type { get; set; } = Type;

    [JsonProperty("properties")] public SignProperties Properties { get; set; } = Properties;

    [JsonProperty("geometry")] public PointGeometry Geometry { get; set; } = Geometry;

}
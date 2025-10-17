using Newtonsoft.Json;

namespace VintageAtlas.GeoJson;

public record TraderFeature(TraderProperties Properties, PointGeometry Geometry, string Type = "Feature")
{
    [JsonProperty("type")]
    public string Type { get; set; } = Type;

    [JsonProperty("properties")]
    public TraderProperties Properties { get; set; } = Properties;

    [JsonProperty("geometry")]
    public PointGeometry Geometry { get; set; } = Geometry;
}
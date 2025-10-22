using Newtonsoft.Json;

namespace VintageAtlas.GeoJson;

/// <summary>
/// GeoJSON Feature representing a single climate data point
/// </summary>
public record ClimateFeature(ClimateProperties Properties, PointGeometry Geometry, string Type = "Feature")
{
    [JsonProperty("type")]
    public string Type { get; set; } = Type;

    [JsonProperty("properties")]
    public ClimateProperties Properties { get; set; } = Properties;

    [JsonProperty("geometry")]
    public PointGeometry Geometry { get; set; } = Geometry;
}


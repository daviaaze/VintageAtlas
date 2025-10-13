using Newtonsoft.Json;

namespace VintageAtlas.GeoJson.SignPost;

public record SignPostFeature(SignPostProperties Properties, PointGeometry Geometry, string Type = "Feature")
{

    [JsonProperty("type")]
    public string Type { get; set; } = Type;

    [JsonProperty("properties")]
    public SignPostProperties Properties { get; set; } = Properties;

    [JsonProperty("geometry")]
    public PointGeometry Geometry { get; set; } = Geometry;
}
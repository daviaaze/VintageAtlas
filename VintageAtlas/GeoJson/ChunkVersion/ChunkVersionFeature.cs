using Newtonsoft.Json;

namespace VintageAtlas.GeoJson;

public record ChunkVersionFeature(ChunkVersionProperties Properties, PolygonGeometry Geometry, string Type = "Feature")
{
    [JsonProperty("type")] public string Type { get; set; } = Type;

    [JsonProperty("properties")] public ChunkVersionProperties Properties { get; set; } = Properties;

    [JsonProperty("geometry")] public PolygonGeometry Geometry { get; set; } = Geometry;

}
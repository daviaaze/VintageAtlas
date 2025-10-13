using Newtonsoft.Json;

namespace VintageAtlas.GeoJson;

public record ChunkVersionProperties(string Color, string Version)
{
    [JsonProperty("color")]
    public string Color { get; set; } = Color;

    [JsonProperty("version")]
    public string Version { get; set; } = Version;
}
using Newtonsoft.Json;

namespace VintageAtlas.GeoJson;

public record Crs(string Name, string Type = "name")
{

    [JsonProperty("type")]
    public string Type { get; set; } = Type;

    [JsonProperty("properties")]
    public Properties Properties { get; set; } = new(Name);
}
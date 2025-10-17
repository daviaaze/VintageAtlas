using Newtonsoft.Json;

namespace VintageAtlas.GeoJson;

public record Properties(string Name)
{
    [JsonProperty("name")]
    public string Name { get; set; } = Name;
}
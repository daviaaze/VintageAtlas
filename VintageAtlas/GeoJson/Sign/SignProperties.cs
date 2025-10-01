using Newtonsoft.Json;

namespace VintageAtlas.GeoJson.Sign;

public record SignProperties(string Label, int Z,string Type = "")
{
    [JsonProperty("type")]
    public string Type { get; set; } = Type;

    [JsonProperty("label")]
    public string Label { get; set; } = Label;
    
    [JsonProperty("z")]
    public int Z { get; set; } = Z;
}
using Newtonsoft.Json;

namespace VintageAtlas.GeoJson.SignPost;

public record SignPostProperties(string Type, string Label, int Z, int Direction)
{

    [JsonProperty("type")]
    public string Type { get; set; } = Type;

    [JsonProperty("label")]
    public string Label { get; set; } = Label;

    [JsonProperty("z")]
    public int Z { get; set; } = Z;

    [JsonProperty("direction")]
    public int Direction { get; set; } = Direction;
}
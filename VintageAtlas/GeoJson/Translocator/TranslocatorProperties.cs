using Newtonsoft.Json;

namespace VintageAtlas.GeoJson.Translocator;

public record TranslocatorProperties(int Depth1, int Depth2, string Label = "", string Tag = "")
{
    [JsonProperty("depth1")]
    public int Depth1 { get; set; } = Depth1;


    [JsonProperty("depth2")]
    public int Depth2 { get; set; } = Depth2;


    [JsonProperty("label")]
    public string Label { get; set; } = Label;


    [JsonProperty("tag")]
    public string Tag { get; set; } = Tag;
}
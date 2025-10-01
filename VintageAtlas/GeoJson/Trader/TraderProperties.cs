using Newtonsoft.Json;

namespace VintageAtlas.GeoJson.Trader;

public record TraderProperties(string Name, string Wares, int Z)
{
    [JsonProperty("name")]
    public string Name { get; set; } = Name;

    [JsonProperty("wares")]
    public string Wares { get; set; } = Wares;


    [JsonProperty("z")]
    public int Z { get; set; } = Z;
}
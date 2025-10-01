using System.Collections.Generic;
using Newtonsoft.Json;

namespace VintageAtlas.GeoJson;

public record PointGeometry(List<int> Coordinates, string Type = "Point")
{
    [JsonProperty("type")]
    public string Type { get; set; } = Type;

    [JsonProperty("coordinates")]
    public List<int> Coordinates { get; set; } = Coordinates;

}
using System.Collections.Generic;
using Newtonsoft.Json;

namespace VintageAtlas.GeoJson;

public record LineGeometry(List<List<int>> Coordinates)
{
    [JsonProperty("type")]
    public string Type { get; set; } = "LineString";

    [JsonProperty("coordinates")]
    public List<List<int>> Coordinates { get; set; } = Coordinates;
}
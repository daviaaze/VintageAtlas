using System.Collections.Generic;
using Newtonsoft.Json;

namespace VintageAtlas.GeoJson;

public record PolygonGeometry(List<List<List<int>>> Coordinates)
{
    [JsonProperty("type")]
    public string Type { get; set; } = "Polygon";

    [JsonProperty("coordinates")]
    public List<List<List<int>>> Coordinates { get; set; } = Coordinates;
}
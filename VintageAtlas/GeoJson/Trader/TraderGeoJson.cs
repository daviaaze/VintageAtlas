using System.Collections.Generic;
using Newtonsoft.Json;

namespace VintageAtlas.GeoJson.Trader;

public record TraderGeoJson
{
    [JsonProperty("type")]
    public string Type { get; set; } = "FeatureCollection";

    // No CRS specified - OpenLayers will use the projection we provide
    // This allows our custom VINTAGESTORY projection to work correctly

    [JsonProperty("name")]
    public string Name { get; set; } = "traders";

    [JsonProperty("features")]
    public List<TraderFeature> Features { get; set; } = new();
}
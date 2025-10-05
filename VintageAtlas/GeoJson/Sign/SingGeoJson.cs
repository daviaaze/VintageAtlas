using System.Collections.Generic;
using Newtonsoft.Json;

namespace VintageAtlas.GeoJson.Sign;

public record SingGeoJson
{
    [JsonProperty("type")]
    public string Type { get; set; } = "FeatureCollection";

    // No CRS specified - OpenLayers will use the projection we provide

    [JsonProperty("name")]
    public string Name { get; set; } = "landmarks";

    [JsonProperty("features")]
    public List<SignFeature> Features { get; set; } = new();
}
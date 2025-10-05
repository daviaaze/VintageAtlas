using System.Collections.Generic;
using Newtonsoft.Json;

namespace VintageAtlas.GeoJson.Translocator;

public record TranslocatorGeoJson
{
    [JsonProperty("type")]
    public string Type { get; set; } = "FeatureCollection";

    // No CRS specified - OpenLayers will use the projection we provide

    [JsonProperty("name")]
    public string Name { get; set; } = "translocators";

    [JsonProperty("features")]
    public List<TranslocatorFeature> Features { get; set; } = new();
}
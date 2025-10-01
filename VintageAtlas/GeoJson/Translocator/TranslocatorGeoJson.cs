using System.Collections.Generic;
using Newtonsoft.Json;

namespace VintageAtlas.GeoJson.Translocator;

public record TranslocatorGeoJson
{
    [JsonProperty("type")]
    public string Type { get; set; } = "FeatureCollection";

    [JsonProperty("crs")]
    public Crs Crs { get; set; } = new("urn:ogc:def:crs:EPSG::3857");

    [JsonProperty("name")]
    public string Name { get; set; } = "translocators";

    [JsonProperty("features")]
    public List<TranslocatorFeature> Features { get; set; } = new();
}
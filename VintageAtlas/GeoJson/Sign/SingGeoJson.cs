using System.Collections.Generic;
using Newtonsoft.Json;

namespace VintageAtlas.GeoJson.Sign;

public record SingGeoJson
{
    [JsonProperty("type")]
    public string Type { get; set; } = "FeatureCollection";

    [JsonProperty("crs")]
    public Crs Crs { get; set; } = new("urn:ogc:def:crs:EPSG::3857");

    [JsonProperty("name")]
    public string Name { get; set; } = "landmarks";

    [JsonProperty("features")]
    public List<SignFeature> Features { get; set; } = new();
}
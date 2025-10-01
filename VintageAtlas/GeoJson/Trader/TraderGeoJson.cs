using System.Collections.Generic;
using Newtonsoft.Json;

namespace VintageAtlas.GeoJson.Trader;

public record TraderGeoJson
{
    [JsonProperty("type")]
    public string Type { get; set; } = "FeatureCollection";

    [JsonProperty("crs")]
    public Crs Crs { get; set; } = new("urn:ogc:def:crs:EPSG::3857");

    [JsonProperty("name")]
    public string Name { get; set; } = "traders";

    [JsonProperty("features")]
    public List<TraderFeature> Features { get; set; } = new();
}
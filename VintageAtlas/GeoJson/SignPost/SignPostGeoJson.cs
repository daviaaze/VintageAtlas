using System.Collections.Generic;
using Newtonsoft.Json;

namespace VintageAtlas.GeoJson.SignPost;

public record SignPostGeoJson
{
    [JsonProperty("type")]
    public string Type { get; set; } = "FeatureCollection";

    [JsonProperty("crs")]
    public Crs Crs { get; set; } = new("urn:ogc:def:crs:EPSG::3857");

    [JsonProperty("name")]
    public string Name { get; set; } = "signposts";

    [JsonProperty("features")]
    public List<SignPostFeature> Features { get; set; } = new();
}
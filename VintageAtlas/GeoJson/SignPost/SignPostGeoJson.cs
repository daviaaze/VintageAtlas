using System.Collections.Generic;
using Newtonsoft.Json;

namespace VintageAtlas.GeoJson.SignPost;

public record SignPostGeoJson
{
    [JsonProperty("type")]
    public string Type { get; set; } = "FeatureCollection";

    // No CRS specified - OpenLayers will use the projection we provide

    [JsonProperty("name")]
    public string Name { get; set; } = "signposts";

    [JsonProperty("features")]
    public List<SignPostFeature> Features { get; set; } = new();
}
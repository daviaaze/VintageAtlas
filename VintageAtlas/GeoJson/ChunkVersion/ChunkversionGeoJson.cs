using System.Collections.Generic;
using Newtonsoft.Json;

namespace VintageAtlas.GeoJson;

public class ChunkversionGeoJson
{
    [JsonProperty("type")]
    public string Type { get; set; } = "FeatureCollection";

    [JsonProperty("crs")]
    public Crs Crs { get; set; } = new("urn:ogc:def:crs:EPSG::3857");

    [JsonProperty("name")]
    public string Name { get; set; } = "chunk";

    [JsonProperty("features")]
    public List<ChunkVersionFeature> Features { get; set; } = new();
}
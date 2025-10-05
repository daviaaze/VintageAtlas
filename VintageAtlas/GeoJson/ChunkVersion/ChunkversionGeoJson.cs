using System.Collections.Generic;
using Newtonsoft.Json;

namespace VintageAtlas.GeoJson;

public class ChunkversionGeoJson
{
    [JsonProperty("type")]
    public string Type { get; set; } = "FeatureCollection";

    // No CRS specified - OpenLayers will use the projection we provide

    [JsonProperty("name")]
    public string Name { get; set; } = "chunk";

    [JsonProperty("features")]
    public List<ChunkVersionFeature> Features { get; set; } = new();
}
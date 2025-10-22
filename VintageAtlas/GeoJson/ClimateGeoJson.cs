using System.Collections.Generic;
using Newtonsoft.Json;

namespace VintageAtlas.GeoJson;

/// <summary>
/// GeoJSON FeatureCollection for climate data points
/// Used for heatmap visualization in OpenLayers
/// </summary>
public record ClimateGeoJson
{
    [JsonProperty("type")]
    public string Type { get; set; } = "FeatureCollection";

    [JsonProperty("name")]
    public string Name { get; set; } = "climate";

    [JsonProperty("features")]
    public List<ClimateFeature> Features { get; set; } = new();
}


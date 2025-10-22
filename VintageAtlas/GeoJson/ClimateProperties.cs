using Newtonsoft.Json;

namespace VintageAtlas.GeoJson;

/// <summary>
/// Properties for a climate data point
/// Contains both scaled (0-255) and real-world values for flexibility
/// </summary>
public record ClimateProperties
{
    /// <summary>
    /// Scaled value 0-255 (used for heatmap weight in OpenLayers)
    /// </summary>
    [JsonProperty("value")]
    public float Value { get; set; }

    /// <summary>
    /// Real-world value (temperature in °C or rainfall as 0-1 normalized)
    /// </summary>
    [JsonProperty("realValue")]
    public float RealValue { get; set; }

    public ClimateProperties(float value, float realValue)
    {
        Value = value;
        RealValue = realValue;
    }
}


using Newtonsoft.Json;

namespace VintageAtlas.GeoJson.Translocator;

public record TranslocatorFeature(TranslocatorProperties Properties, LineGeometry Geometry, string Type = "LineString")
{
    [JsonProperty("type")]
    public string Type { get; set; } = Type;

    [JsonProperty("properties")]
    public TranslocatorProperties Properties { get; set; } = Properties;

    [JsonProperty("geometry")]
    public LineGeometry Geometry { get; set; } = Geometry;

    public void SetTexts(string text)
    {
        var lines = text.Split('\n');
        if (lines.Length > 1)
        {
            Properties.Tag = lines[1];
        }

        if (lines.Length > 2)
        {
            Properties.Label = lines[2];
        }

        if (lines.Length > 3)
        {
            Properties.Label += (lines[2].EndsWith("-") ? "" : " ") + lines[3];
        }
    }
}
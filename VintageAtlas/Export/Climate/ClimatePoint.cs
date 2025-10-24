namespace VintageAtlas.Export.Climate;

/// <summary>
/// Represents a single climate data point with location and values
/// </summary>
public class ClimatePoint
{
    public int X { get; set; }
    public int Z { get; set; }
    public float Value { get; set; }
    public float RealValue { get; set; }
}


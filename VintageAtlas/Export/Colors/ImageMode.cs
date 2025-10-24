namespace VintageAtlas.Export.Colors;

/// <summary>
/// Image rendering modes for map export
/// </summary>
public enum ImageMode
{
    /// <summary>Color variations only</summary>
    ColorVariations = 0,

    /// <summary>Color variations with height information</summary>
    ColorVariationsWithHeight = 1,

    /// <summary>Single color rendering</summary>
    OnlyOneColor = 2,

    /// <summary>Color variations with hill shading</summary>
    ColorVariationsWithHillShading = 3,

    /// <summary>Medieval style with hill shading (recommended)</summary>
    MedievalStyleWithHillShading = 4
}
using SkiaSharp;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace VintageAtlas.Export;

public static class MapColors
{
    public static string GetDefaultMapColorCode(EnumBlockMaterial material)
    {
        return material switch
        {
            EnumBlockMaterial.Soil => "land",
            EnumBlockMaterial.Sand => "desert",
            EnumBlockMaterial.Ore => "land",
            EnumBlockMaterial.Gravel => "desert",
            EnumBlockMaterial.Stone => "land",
            EnumBlockMaterial.Leaves => "forest",
            EnumBlockMaterial.Plant => "plant",
            EnumBlockMaterial.Wood => "forest",
            EnumBlockMaterial.Snow => "glacier",
            EnumBlockMaterial.Liquid => "lake",
            EnumBlockMaterial.Ice => "glacier",
            EnumBlockMaterial.Lava => "lava",
            _ => "land"
        };
    }

    private static readonly OrderedDictionary<string, string> HexColorsByCode = new()
    {
        { "ink", "#483018" },
        { "settlement", "#856844" },
        { "water-edge", "#483018" },
        { "land", "#AC8858" },
        { "desert", "#C4A468" },
        { "forest", "#98844C" },
        { "road", "#805030" },
        { "plant", "#808650" },
        { "lake", "#CCC890" },
        { "ocean", "#CCC890" },
        { "glacier", "#E0E0C0" },
        { "devastation", "#755c3c" }
    };

    public static readonly OrderedDictionary<string, uint> ColorsByCode = new();

    static MapColors()
    {
        foreach (var val in HexColorsByCode)
        {
            ColorsByCode[val.Key] = (uint)SKColor.Parse(val.Value);
        }
    }
}
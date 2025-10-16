using System.Globalization;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using System;

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

    public static readonly OrderedDictionary<string, uint> ColorsByCode = [];

    static MapColors()
    {
        foreach (var val in HexColorsByCode)
        {
            ColorsByCode[val.Key] = ParseHexToColor(val.Value);
        }
    }

    private static uint ParseHexToColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return 0u;
        if (hex[0] == '#') hex = hex[1..];

        byte a = 0xFF, r = 0, g = 0, b = 0;

        switch (hex.Length)
        {
            case 6:
                r = byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                g = byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                b = byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                break;
            case 8:
                // Assume AARRGGBB if 8 digits provided
                a = byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                r = byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                g = byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                b = byte.Parse(hex.AsSpan(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                break;
            default:
                // Unsupported format
                return 0u;
        }

        return ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
    }
}
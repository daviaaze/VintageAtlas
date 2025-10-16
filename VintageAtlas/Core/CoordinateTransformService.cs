using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VintageAtlas.Core;

/// <summary>
/// Centralized coordinate transformation service for VintageAtlas.
/// Handles all conversions between different coordinate systems:
/// - Game world coordinates (Vintage Story native)
/// - Map display coordinates (GeoJSON with Z-flip)
/// - OpenLayers grid coordinates (tile requests)
/// - Storage tile coordinates (MBTiles database)
/// </summary>
public class CoordinateTransformService(ICoreServerAPI api)
{
    private readonly int _offsetX = api.World.BlockAccessor.MapSizeX / 2;
    private readonly int _offsetY = api.World.BlockAccessor.MapSizeZ / 2;

    /// <summary>
    /// Convert game world coordinates to map display coordinates (GeoJSON).
    /// Applies Z-axis flip for north-up display.
    /// 
    /// Game: Z+ = south, Z- = north
    /// Display: Y+ = north (top), Y- = south (bottom)
    /// Transformation: displayY = -gameZ
    /// </summary>
    public (int x, int y) GameToDisplay(BlockPos gamePos)
    {
        return (gamePos.X - _offsetX, gamePos.Z - _offsetY);
    }
}

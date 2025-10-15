using System;
using Vintagestory.API.MathTools;
using VintageAtlas.Web.API;
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
public class CoordinateTransformService
{
    private readonly MapConfigController _mapConfigController;
    private readonly ModConfig _config;

    private readonly ICoreServerAPI _api;

    private readonly int _offsetX;
    private readonly int _offsetY;
    

    public CoordinateTransformService(MapConfigController mapConfigController, ModConfig config, ICoreServerAPI api)
    {
        _mapConfigController = mapConfigController;
        _config = config;
        _api = api;

        _offsetX = _api.World.BlockAccessor.MapSizeX / 2;
        _offsetY = _api.World.BlockAccessor.MapSizeZ / 2;
    }

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

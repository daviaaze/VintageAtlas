using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Storage;
using VintageAtlas.Web.API.Base;

namespace VintageAtlas.Web.API.Controllers;

/// <summary>
/// Generic controller for serving climate layer tiles (rain, temperature, etc.)
/// Replaces the duplicated RainTileController and TempTileController
/// </summary>
public partial class ClimateLayerTileController : TileControllerBase
{
    private readonly MbTilesStorage _storage;
    private readonly ClimateLayerType _layerType;
    private readonly Regex _pathRegex;

    public ClimateLayerTileController(
        ICoreServerAPI sapi,
        MbTilesStorage storage,
        ClimateLayerType layerType) : base(sapi)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _layerType = layerType;
        _pathRegex = CreateRegexForLayerType(layerType);
    }

    /// <summary>
    /// Serve a climate layer tile
    /// </summary>
    public async Task ServeTile(HttpListenerContext context, string path)
    {
        if (!TryParseCoordinates(path, _pathRegex, out var coordinates))
        {
            LogWarning($"Invalid {_layerType} tile path format: {path}");
            await ServeError(context, $"Invalid {_layerType} tile path format", 400);
            return;
        }

        var gridX = coordinates[0];
        var gridY = coordinates[1];

        // Get tile data from storage based on layer type
        var tileData = _layerType switch
        {
            ClimateLayerType.Rain => await _storage.GetRainTileAsync(gridX, gridY),
            ClimateLayerType.Temperature => await _storage.GetTempTileAsync(gridX, gridY),
            _ => throw new NotSupportedException($"Layer type {_layerType} not supported")
        };

        await ServeTileData(context, tileData, [gridX, gridY], path);
    }

    /// <summary>
    /// Check if a path matches this layer's tile pattern
    /// </summary>
    public bool IsMatchingPath(string path)
    {
        return _pathRegex.IsMatch(path);
    }

    /// <summary>
    /// Create regex pattern for the specified layer type
    /// </summary>
    private static Regex CreateRegexForLayerType(ClimateLayerType layerType)
    {
        var pattern = layerType switch
        {
            ClimateLayerType.Rain => @"^/rain-tiles/(-?\d+)_(-?\d+)\.png$",
            ClimateLayerType.Temperature => @"^/temperature-tiles/(-?\d+)_(-?\d+)\.png$",
            _ => throw new NotSupportedException($"Layer type {layerType} not supported")
        };

        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }
}

/// <summary>
/// Climate layer type enumeration
/// </summary>
public enum ClimateLayerType
{
    Rain,
    Temperature
}


using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Web.API.Base;
using VintageAtlas.Export;

namespace VintageAtlas.Web.API;

/// <summary>
/// Serves map tiles with caching and ETag support.
/// Accepts OpenLayers grid coordinates and transforms them to storage coordinates internally.
/// </summary>
public partial class TileController : TileControllerBase
{
    private readonly ModConfig _config;
    private readonly ITileGenerator _tileGenerator;
    private readonly MapConfigController _mapConfigController;
    private static readonly Regex TilePathRegex = MyRegex();

    public TileController(
        ICoreServerAPI sapi,
        ModConfig config,
        ITileGenerator tileGenerator,
        MapConfigController mapConfigController) : base(sapi)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _tileGenerator = tileGenerator ?? throw new ArgumentNullException(nameof(tileGenerator));
        _mapConfigController = mapConfigController ?? throw new ArgumentNullException(nameof(mapConfigController));
    }

    /// <summary>
    /// Serve a tile with proper caching headers and ETag support.
    /// Accepts OpenLayers grid coordinates (0-based from origin) and transforms to storage coordinates.
    /// </summary>
    public async Task ServeTile(HttpListenerContext context, string path)
    {
        // Parse tile coordinates
        if (!TryParseCoordinates(path, TilePathRegex, out var coordinates))
        {
            LogWarning($"Invalid tile path format: {path}");
            await ServeError(context, "Invalid tile path format", 400);
            return;
        }

        var zoom = coordinates[0];
        var gridX = coordinates[1];
        var gridY = coordinates[2];

        // Validate zoom level
        var mapConfig = _mapConfigController.GetCurrentConfig();
        var maxZoom = mapConfig?.TileResolutions is { Length: > 0 }
            ? mapConfig.TileResolutions.Length - 1
            : _config.BaseZoomLevel;

        if (zoom < 0 || zoom > maxZoom)
        {
            LogWarning($"Invalid zoom level {zoom}, must be between 0 and {maxZoom}");
            await ServeError(context, $"Invalid zoom level. Must be between 0 and {maxZoom}", 400);
            return;
        }

        // Get tile data
        var tileData = await _tileGenerator.GetTileDataAsync(zoom, gridX, gridY);

        // Serve tile using base controller
        await ServeTileData(context, tileData, coordinates, path);
    }

    /// <summary>
    /// Check if a path matches the tile pattern
    /// </summary>
    public static bool IsTilePath(string path)
    {
        return TilePathRegex.IsMatch(path);
    }

    [GeneratedRegex(@"^/tiles/(\d+)/(-?\d+)_(-?\d+)\.png$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();
}


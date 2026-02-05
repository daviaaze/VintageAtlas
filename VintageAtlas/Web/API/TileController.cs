using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Core.Configuration;
using VintageAtlas.Web.API.Base;
using VintageAtlas.Export;
using VintageAtlas.Web.API.Helpers;

namespace VintageAtlas.Web.API;

/// <summary>
/// Serves map tiles with caching and ETag support.
/// Accepts OpenLayers grid coordinates and transforms them to storage coordinates internally.
/// </summary>
public partial class TileController(
    ICoreServerAPI sapi,
    ModConfig config,
    ITileGenerator tileGenerator,
    IMapConfigController mapConfigController) : BaseController(sapi)
{
    private readonly ModConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly ITileGenerator _tileGenerator = tileGenerator ?? throw new ArgumentNullException(nameof(tileGenerator));
    private readonly IMapConfigController _mapConfigController = mapConfigController ?? throw new ArgumentNullException(nameof(mapConfigController));
    
    // Optional climate generators (can be null if not initialized)
    public Export.Generation.ClimateTileGenerator? TempGenerator { get; set; }
    public Export.Generation.ClimateTileGenerator? RainGenerator { get; set; }

    private static readonly Regex TilePathRegex = MyRegex();

    /// <summary>
    /// Serve a tile with proper caching headers and ETag support.
    /// Accepts OpenLayers grid coordinates (0-based from origin) and transforms to storage coordinates.
    /// </summary>
    public async Task ServeTile(HttpListenerContext context, string path)
    {
        // Parse tile coordinates
        if (!TilePathRegex.IsMatch(path))
        {
            LogWarning($"Invalid tile path format: {path}");
            await ServeError(context, "Invalid tile path format", 400);
            return;
        }

        var match = TilePathRegex.Match(path);
        var layer = match.Groups[1].Value;
        var zoom = int.Parse(match.Groups[2].Value);
        var gridX = int.Parse(match.Groups[3].Value);
        var gridY = int.Parse(match.Groups[4].Value);

        var coordinates = new int[] { zoom, gridX, gridY };

        // Validate zoom level
        var mapConfig = _mapConfigController.GetCurrentConfig();
        var maxZoom = mapConfig?.TileResolutions is { Length: > 0 }
            ? mapConfig.TileResolutions.Length - 1
            : _config.Export.BaseZoomLevel;

        if (zoom < 0 || zoom > maxZoom)
        {
            LogWarning($"Invalid zoom level {zoom}, must be between 0 and {maxZoom}");
            await ServeError(context, $"Invalid zoom level. Must be between 0 and {maxZoom}", 400);
            return;
        }

        // Get tile data based on layer
        byte[]? tileData = null;
        
        if (string.IsNullOrEmpty(layer))
        {
            tileData = await _tileGenerator.GetTileDataAsync(zoom, gridX, gridY);
        }
        else if (layer.Equals("temperature", StringComparison.OrdinalIgnoreCase) && TempGenerator != null)
        {
            tileData = await TempGenerator.GetTileDataAsync(zoom, gridX, gridY);
        }
        else if (layer.Equals("rainfall", StringComparison.OrdinalIgnoreCase) && RainGenerator != null)
        {
            tileData = await RainGenerator.GetTileDataAsync(zoom, gridX, gridY);
        }
        else
        {
             await ServeError(context, $"Layer '{layer}' not found or not initialized", 404);
             return;
        }

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

    /// <summary>
    /// Serve a tile with proper caching headers and ETag support
    /// </summary>
    private async Task ServeTileData(
        HttpListenerContext context,
        byte[]? tileData,
        int[] coordinates,
        string requestPath)
    {
        try
        {
            // Handle missing tile - return 404
            if (tileData == null)
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            // Generate ETag based on tile content hash
            var etag = ETagHelper.GenerateFromTileData(tileData, coordinates[0], coordinates[1], coordinates[2]);

            // Check If-None-Match header for conditional requests (ETag-based caching)
            if (CheckETagMatch(context, etag))
            {
                return; // 304 Not Modified already sent
            }

            // Serve the tile with proper caching headers
            context.Response.StatusCode = 200;
            context.Response.ContentType = "image/png";
            context.Response.ContentLength64 = tileData.Length;
            context.Response.Headers.Add("ETag", etag);
            context.Response.Headers.Add("Cache-Control", CacheHelper.ForTiles());
            context.Response.Headers.Add("Last-Modified", DateTime.UtcNow.ToString("R"));
            context.Response.Headers.Add("Vary", "If-None-Match");
            context.Response.Headers.Add("X-Tile-Cache", "static");

            await context.Response.OutputStream.WriteAsync(tileData);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            LogError($"Error serving tile {requestPath}: {ex.Message}", ex);
            await ServeError(context, "Internal server error");
        }
    }

    [GeneratedRegex(@"^/tiles/(?:(temperature|rainfall)/)?(\d+)/(-?\d+)_(-?\d+)\.png$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();
}


using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Export;

namespace VintageAtlas.Web.API;

/// <summary>
/// Serves map tiles with caching and ETag support.
/// Accepts OpenLayers grid coordinates and transforms them to storage coordinates internally.
/// </summary>
public partial class TileController(
    ICoreServerAPI sapi,
    ModConfig config,
    ITileGenerator tileGenerator,
    MapConfigController mapConfigController)
{
    private static readonly Regex TilePathRegex = MyRegex();

    /// <summary>
    /// Serve a tile with proper caching headers and ETag support.
    /// Accepts OpenLayers grid coordinates (0-based from origin) and transforms to storage coordinates.
    /// </summary>
    public async Task ServeTile(HttpListenerContext context, string path)
    {
        try
        {
            // Parse OpenLayers grid coordinates from path: /tiles/{zoom}/{gridX}_{gridY}.png
            var match = TilePathRegex.Match(path);
            if (!match.Success)
            {
                sapi.Logger.Warning($"[VintageAtlas] Invalid tile path format: {path}");
                await ServeError(context, "Invalid tile path format", 400);
                return;
            }

            var zoom = int.Parse(match.Groups[1].Value);
            var gridX = int.Parse(match.Groups[2].Value);
            var gridY = int.Parse(match.Groups[3].Value);

            // Validate zoom level using mapConfig.TileResolutions when available
            var mapConfig = mapConfigController.GetCurrentConfig();
            int maxZoom = (mapConfig?.TileResolutions != null && mapConfig.TileResolutions.Length > 0)
                ? mapConfig.TileResolutions.Length - 1
                : config.BaseZoomLevel;

            if (zoom < 0 || zoom > maxZoom)
            {
                sapi.Logger.Warning($"[VintageAtlas] Invalid zoom level {zoom}, must be between 0 and {maxZoom} (max based on {(mapConfig != null ? "TileResolutions" : "BaseZoomLevel")})");
                await ServeError(context, $"Invalid zoom level. Must be between 0 and {maxZoom}", 400);
                return;
            }

            var extent = await tileGenerator.GetTileExtentAsync(zoom);

            // Adjust grid coordinates using backend origin offsets for absolute storage tiles
            var originTiles = mapConfig?.OriginTilesPerZoom;
            int offsetX = 0;
            int offsetZ = 0;

            var hasOrigin = originTiles != null && zoom < originTiles.Length && originTiles[zoom] != null;

            if (hasOrigin)
            {
                offsetX = originTiles![zoom][0];
                offsetZ = originTiles![zoom][1];
            }

            if (extent != null)
            {
                if (!hasOrigin || offsetX != extent.MinX || offsetZ != extent.MinY)
                {
                    offsetX = extent.MinX;
                    offsetZ = extent.MinY;

                    // Cached config is outdated; refresh so future requests use latest offsets
                    mapConfigController.InvalidateCache();
                }
            }

            var storageTileX = gridX;
            var storageTileZ = gridY;

            sapi.Logger.Notification($"[TileController] 🎯 Tile Request: zoom={zoom}, grid=({gridX},{gridY}) → storage=({storageTileX},{storageTileZ}) with offset ({offsetX},{offsetZ})");

            // if (extent != null && (storageTileX < extent.MinX || storageTileX > extent.MaxX ||
            //         storageTileZ < extent.MinY || storageTileZ > extent.MaxY))
            // {
            //     // Serve a transparent PNG placeholder rather than 404 to prevent visual cuts
            //     var transparentPng = CreateTransparentPng(config.TileSize);
            //     context.Response.StatusCode = 200;
            //     context.Response.ContentType = "image/png";
            //     context.Response.ContentLength64 = transparentPng.Length;
            //     context.Response.Headers.Add("Cache-Control", "public, max-age=60");
            //     await context.Response.OutputStream.WriteAsync(transparentPng);
            //     context.Response.Close();
            //     return;
            // }

            var result = await tileGenerator.GetTileDataAsync(zoom, storageTileX, storageTileZ);

            // Handle missing tile - return 404
            if (result == null)
            {
                // Debug: Show what tiles are actually available at this zoom level
                var extent404 = await tileGenerator.GetTileExtentAsync(zoom);
                if (extent404 != null)
                {
                    sapi.Logger.Warning($"[TileController] ❌ Tile not found: zoom={zoom}, storage=({storageTileX},{storageTileZ}). Available extent: X[{extent404.MinX}-{extent404.MaxX}], Y[{extent404.MinY}-{extent404.MaxY}]");
                }
                else
                {
                    sapi.Logger.Warning($"[TileController] ❌ Tile not found: zoom={zoom}, storage=({storageTileX},{storageTileZ}). No tiles found at zoom {zoom} - may need to run /atlas export");
                }

                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }
            else
            {
                sapi.Logger.Debug($"[TileController] Tile found: zoom={zoom}, storage=({storageTileX},{storageTileZ}), size={result.Length} bytes");
            }

            // Generate ETag based on tile content hash (first 16 bytes for performance)
            var etag = GenerateETag(result, zoom, storageTileX, storageTileZ);

            // Check If-None-Match header for conditional requests (ETag-based caching)
            var clientETag = context.Request.Headers["If-None-Match"];
            if (!string.IsNullOrEmpty(clientETag) && clientETag == etag)
            {
                // Client has current version - return 304 Not Modified
                context.Response.StatusCode = 304;
                context.Response.Headers.Add("ETag", etag);
                context.Response.Headers.Add("Cache-Control", "public, max-age=3600, immutable");
                context.Response.Headers.Add("Vary", "If-None-Match");
                context.Response.Close();
                return;
            }

            // Serve the tile with proper caching headers
            context.Response.StatusCode = 200;
            context.Response.ContentType = "image/png";
            context.Response.ContentLength64 = result.Length;
            context.Response.Headers.Add("ETag", etag);
            context.Response.Headers.Add("Cache-Control", "public, max-age=3600, immutable");
            context.Response.Headers.Add("Last-Modified", DateTime.UtcNow.ToString("R"));
            context.Response.Headers.Add("Vary", "If-None-Match");

            // Add performance hint: this tile won't change unless map is regenerated
            context.Response.Headers.Add("X-Tile-Cache", "static");

            await context.Response.OutputStream.WriteAsync(result);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error serving tile: {ex.Message}");
            sapi.Logger.Error(ex.StackTrace ?? "");
            await ServeError(context, "Internal server error");
        }
    }

    /// <summary>
    /// Generate ETag for tile (based on content hash for cache validation)
    /// </summary>
    private static string GenerateETag(byte[] tileData, int zoom, int tileX, int tileZ)
    {
        // For performance, use the first 16 bytes + size as fingerprint
        // (full hash would be expensive for high-volume tile serving)
        var fingerprint = tileData.Length;
        if (tileData.Length >= 16)
        {
            for (var i = 0; i < 16; i++)
            {
                fingerprint = fingerprint * 31 + tileData[i];
            }
        }

        return $"\"{zoom}-{tileX}-{tileZ}-{fingerprint:X}\"";
    }

    /// <summary>
    /// Check if a path matches the tile pattern
    /// </summary>
    public static bool IsTilePath(string path)
    {
        return TilePathRegex.IsMatch(path);
    }

    private static async Task ServeError(HttpListenerContext context, string message, int statusCode = 500)
    {
        try
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var errorJson = $"{{\"error\":\"{message}\"}}";
            var errorBytes = System.Text.Encoding.UTF8.GetBytes(errorJson);

            context.Response.ContentLength64 = errorBytes.Length;
            await context.Response.OutputStream.WriteAsync(errorBytes);
            context.Response.Close();
        }
        catch
        {
            // Silently fail
        }
    }

    // No grid-to-storage transformation needed in direct XYZ alignment mode

    /// <summary>
    /// Create a transparent PNG of the given size
    /// </summary>
    private static byte[] CreateTransparentPng(int size)
    {
        using var bmp = new SkiaSharp.SKBitmap(size, size);
        using var canvas = new SkiaSharp.SKCanvas(bmp);
        canvas.Clear(SkiaSharp.SKColor.Empty);
        using var img = SkiaSharp.SKImage.FromBitmap(bmp);
        using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    [GeneratedRegex(@"^/tiles/(\d+)/(-?\d+)_(-?\d+)\.png$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();
}


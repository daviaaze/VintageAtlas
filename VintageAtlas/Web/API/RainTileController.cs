using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VintageAtlas.Storage;
using Vintagestory.API.Server;
namespace VintageAtlas.Web.API;

/// <summary>
/// Serves map tiles with caching and ETag support.
/// Accepts OpenLayers grid coordinates and transforms them to storage coordinates internally.
/// </summary>
public partial class RainTileController(
    ICoreServerAPI sapi,
    MbTilesStorage mbTilesStorage)
{
    private static readonly Regex RainTilePathRegex = MyRegex();

    /// <summary>
    /// Serve a tile with proper caching headers and ETag support.
    /// Accepts OpenLayers grid coordinates (0-based from origin) and transforms to storage coordinates.
    /// </summary>
    public async Task ServeRainTile(HttpListenerContext context, string path)
    {
        try
        {
            // Parse OpenLayers grid coordinates from path: /tiles/{zoom}/{gridX}_{gridY}.png
            var match = RainTilePathRegex.Match(path);
            if (!match.Success)
            {
                sapi.Logger.Warning($"[VintageAtlas] Invalid rain tile path format: {path}");
                await ServeError(context, "Invalid rain tile path format", 400);
                return;
            }

            var gridX = int.Parse(match.Groups[1].Value);
            var gridY = int.Parse(match.Groups[2].Value);

            // Adjust grid coordinates using backend origin offsets for absolute storage tiles

            var result = await mbTilesStorage.GetRainTileAsync(gridX, gridY);

            // Handle missing tile - return 404
            if (result == null)
            {
                // Debug: Show what tiles are actually available at this zoom level
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            // Generate ETag based on tile content hash (first 16 bytes for performance)
            var etag = GenerateETag(result, gridX, gridY);

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
    private static string GenerateETag(byte[] tileData, int tileX, int tileZ)
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

        return $"\"{tileX}-{tileZ}-{fingerprint:X}\"";
    }

    /// <summary>
    /// Check if a path matches the tile pattern
    /// </summary>
    public static bool IsRainTilePath(string path)
    {
        return RainTilePathRegex.IsMatch(path);
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

    [GeneratedRegex(@"^/rain-tiles/(-?\d+)_(-?\d+)\.png$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();
}


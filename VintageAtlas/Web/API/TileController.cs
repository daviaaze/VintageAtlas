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
public partial class TileController(ICoreServerAPI sapi, ModConfig config, ITileGenerator tileGenerator, MapConfigController mapConfigController)
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
            
            // Validate zoom level (0 = fully zoomed out, BaseZoomLevel = fully zoomed in)
            if (zoom < 0 || zoom > config.BaseZoomLevel)
            {
                sapi.Logger.Warning($"[VintageAtlas] Invalid zoom level {zoom}, must be between 0 and {config.BaseZoomLevel}");
                await ServeError(context, $"Invalid zoom level. Must be between 0 and {config.BaseZoomLevel}", 400);
                return;
            }

            // Transform OpenLayers grid coordinates to storage tile coordinates
            var (storageTileX, storageTileZ) = TransformGridToStorage(zoom, gridX, gridY);

            // Generate or retrieve tile using storage coordinates
            var result = await tileGenerator.GetTileDataAsync(zoom, storageTileX, storageTileZ);
            
            // Handle missing tile
            if (result == null)
            {
                await ServeError(context, "Tile not found", 404);
                return;
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
    /// Transform OpenLayers grid coordinates (0-based from origin) to storage tile coordinates (absolute world position).
    /// OpenLayers uses: (0,0) at origin, positive X = east, negative Y = north
    /// Storage uses: absolute tile numbers based on world block position
    /// </summary>
    private (int storageTileX, int storageTileZ) TransformGridToStorage(int zoom, int gridX, int gridY)
    {
        // Get map configuration to access world origin and resolutions
        var mapConfig = mapConfigController.GetCurrentConfig();
        
        if (mapConfig == null)
        {
            sapi.Logger.Warning("[VintageAtlas] Map config not available, using fallback tile coordinates");
            // Fallback: assume grid coordinates ARE storage coordinates (legacy behavior)
            return (gridX, gridY);
        }
        
        // Calculate blocks per tile at this zoom level
        var resolution = mapConfig.TileResolutions[zoom];
        var blocksPerTile = config.TileSize * resolution;
        
        // Calculate the origin tile numbers (where OpenLayers grid 0,0 maps to)
        // worldOrigin is in world block coordinates, we convert to tile numbers
        var originTileX = (int)Math.Floor(mapConfig.WorldOrigin[0] / blocksPerTile);
        var originTileY = (int)Math.Floor(mapConfig.WorldOrigin[1] / blocksPerTile);
        
        // Transform grid coordinates to storage coordinates
        // Simple addition: OpenLayers uses bottom-left origin by default
        // Grid (0,0) = origin tile, positive X = east, positive Y = south
        var storageTileX = originTileX + gridX;
        var storageTileZ = originTileY + gridY;
        
        return (storageTileX, storageTileZ);
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
            for (int i = 0; i < 16; i++)
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

    [GeneratedRegex(@"^/tiles/(\d+)/(-?\d+)_(-?\d+)\.png$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();
}


using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Export;

namespace VintageAtlas.Web.API;

/// <summary>
/// Serves map tiles with caching and ETag support
/// </summary>
public class TileController
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly ITileGenerator _tileGenerator;
    
    private static readonly Regex TilePathRegex = new Regex(
        @"^/tiles/(\d+)/(-?\d+)_(-?\d+)\.png$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public TileController(ICoreServerAPI sapi, ModConfig config, ITileGenerator tileGenerator)
    {
        _sapi = sapi;
        _config = config;
        _tileGenerator = tileGenerator;
    }

    /// <summary>
    /// Serve a tile with proper caching headers and ETag support
    /// </summary>
    public async Task ServeTile(HttpListenerContext context, string path)
    {
        try
        {
            // Parse tile coordinates from path: /tiles/{zoom}/{x}_{z}.png
            var match = TilePathRegex.Match(path);
            if (!match.Success)
            {
                _sapi.Logger.Warning($"[VintageAtlas] Invalid tile path format: {path}");
                await ServeError(context, "Invalid tile path format", 400);
                return;
            }

            var zoom = int.Parse(match.Groups[1].Value);
            var tileX = int.Parse(match.Groups[2].Value);
            var tileZ = int.Parse(match.Groups[3].Value);
            
            // Validate zoom level (0 = fully zoomed out, BaseZoomLevel = fully zoomed in)
            if (zoom < 0 || zoom > _config.BaseZoomLevel)
            {
                _sapi.Logger.Warning($"[VintageAtlas] Invalid zoom level {zoom}, must be between 0 and {_config.BaseZoomLevel}");
                await ServeError(context, $"Invalid zoom level. Must be between 0 and {_config.BaseZoomLevel}", 400);
                return;
            }

            // Generate or retrieve tile using ITileGenerator interface
            var result = await _tileGenerator.GetTileDataAsync(zoom, tileX, tileZ);
            
            // Handle missing tile
            if (result == null)
            {
                _sapi.Logger.Debug($"[VintageAtlas] Tile not found: zoom={zoom}, x={tileX}, z={tileZ}");
                await ServeError(context, "Tile not found", 404);
                return;
            }

            // Generate ETag based on tile content hash (first 16 bytes for performance)
            var etag = GenerateETag(result, zoom, tileX, tileZ);
            
            // Check If-None-Match header for conditional requests (ETag-based caching)
            var clientETag = context.Request.Headers["If-None-Match"];
            if (!string.IsNullOrEmpty(clientETag) && clientETag == etag)
            {
                // Client has current version - return 304 Not Modified
                _sapi.Logger.Debug($"[VintageAtlas] Tile not modified (304): zoom={zoom}, x={tileX}, z={tileZ}");
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
            
            await context.Response.OutputStream.WriteAsync(result, 0, result.Length);
            context.Response.Close();
            
            _sapi.Logger.Debug($"[VintageAtlas] Served tile: zoom={zoom}, x={tileX}, z={tileZ}, size={result.Length} bytes");
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Error serving tile: {ex.Message}");
            _sapi.Logger.Error(ex.StackTrace ?? "");
            await ServeError(context, "Internal server error", 500);
        }
    }
    
    /// <summary>
    /// Generate ETag for tile (based on content hash for cache validation)
    /// </summary>
    private string GenerateETag(byte[] tileData, int zoom, int tileX, int tileZ)
    {
        // For performance, use first 16 bytes + size as fingerprint
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
            await context.Response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length);
            context.Response.Close();
        }
        catch
        {
            // Silently fail
        }
    }
}


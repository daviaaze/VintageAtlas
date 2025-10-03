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
    private readonly DynamicTileGenerator _tileGenerator;
    
    private static readonly Regex TilePathRegex = new Regex(
        @"^/tiles/(\d+)/(-?\d+)_(-?\d+)\.png$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public TileController(ICoreServerAPI sapi, ModConfig config, DynamicTileGenerator tileGenerator)
    {
        _sapi = sapi;
        _config = config;
        _tileGenerator = tileGenerator;
    }

    /// <summary>
    /// Serve a tile with proper caching headers
    /// </summary>
    public async Task ServeTile(HttpListenerContext context, string path)
    {
        try
        {
            // Log the incoming request
            _sapi.Logger.Debug($"[VintageAtlas] Tile request: {path}");
            
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
            
            _sapi.Logger.Debug($"[VintageAtlas] Parsed tile request: zoom={zoom}, x={tileX}, z={tileZ}");
            
            // Validate zoom level
            if (zoom < 1 || zoom > _config.BaseZoomLevel)
            {
                _sapi.Logger.Warning($"[VintageAtlas] Invalid zoom level {zoom}, must be between 1 and {_config.BaseZoomLevel}");
                await ServeError(context, $"Invalid zoom level. Must be between 1 and {_config.BaseZoomLevel}", 400);
                return;
            }

            // Get If-None-Match header for conditional requests
            var ifNoneMatch = context.Request.Headers["If-None-Match"];
            
            // Generate or retrieve tile
            var result = await _tileGenerator.GenerateTileAsync(zoom, tileX, tileZ, ifNoneMatch);
            
            // Handle different result types
            if (result.NotModified)
            {
                _sapi.Logger.Debug($"[VintageAtlas] Tile not modified (304): zoom={zoom}, x={tileX}, z={tileZ}");
                context.Response.StatusCode = 304; // Not Modified
                context.Response.Headers.Add("ETag", result.ETag ?? "");
                context.Response.Headers.Add("Cache-Control", "public, max-age=3600");
                context.Response.Close();
                return;
            }

            if (result.NotFound)
            {
                _sapi.Logger.Warning($"[VintageAtlas] Tile not found (404): zoom={zoom}, x={tileX}, z={tileZ}");
                await ServeError(context, "Tile not found", 404);
                return;
            }

            if (result.Data == null)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to generate tile: zoom={zoom}, x={tileX}, z={tileZ}");
                await ServeError(context, "Failed to generate tile", 500);
                return;
            }

            // Serve the tile with caching headers
            context.Response.StatusCode = 200;
            context.Response.ContentType = result.ContentType ?? "image/png";
            context.Response.ContentLength64 = result.Data.Length;
            context.Response.Headers.Add("ETag", result.ETag ?? "");
            context.Response.Headers.Add("Cache-Control", "public, max-age=3600"); // Cache for 1 hour
            context.Response.Headers.Add("Last-Modified", result.LastModified.ToString("R"));
            
            await context.Response.OutputStream.WriteAsync(result.Data, 0, result.Data.Length);
            context.Response.Close();
            
            _sapi.Logger.Debug($"[VintageAtlas] Served tile: zoom={zoom}, x={tileX}, z={tileZ}, size={result.Data.Length} bytes");
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Error serving tile: {ex.Message}");
            _sapi.Logger.Error(ex.StackTrace ?? "");
            await ServeError(context, "Internal server error", 500);
        }
    }

    /// <summary>
    /// Check if a path matches the tile pattern
    /// </summary>
    public static bool IsTilePath(string path)
    {
        return TilePathRegex.IsMatch(path);
    }

    private async Task ServeError(HttpListenerContext context, string message, int statusCode = 500)
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


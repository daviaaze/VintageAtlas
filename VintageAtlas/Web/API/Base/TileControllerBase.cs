using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Web.API.Helpers;

namespace VintageAtlas.Web.API.Base;

/// <summary>
/// Base controller for serving map tiles with common functionality
/// </summary>
public abstract class TileControllerBase : BaseController
{
    protected TileControllerBase(ICoreServerAPI sapi) : base(sapi)
    {
    }

    /// <summary>
    /// Serve a tile with proper caching headers and ETag support
    /// </summary>
    protected async Task ServeTileData(
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
            var etag = ETagHelper.GenerateFromTileData(tileData, coordinates);

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

    /// <summary>
    /// Parse tile coordinates from path using the provided regex
    /// </summary>
    protected bool TryParseCoordinates(string path, Regex regex, out int[] coordinates)
    {
        coordinates = Array.Empty<int>();

        var match = regex.Match(path);
        if (!match.Success)
        {
            return false;
        }

        // Extract coordinate values from regex groups (starting at group 1)
        var coordList = new int[match.Groups.Count - 1];
        for (var i = 1; i < match.Groups.Count; i++)
        {
            if (!int.TryParse(match.Groups[i].Value, out coordList[i - 1]))
            {
                return false;
            }
        }

        coordinates = coordList;
        return true;
    }
}


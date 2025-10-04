using System.Net;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Web.API;

namespace VintageAtlas.Web.Server;

/// <summary>
/// Routes HTTP requests to appropriate handlers
/// </summary>
public class RequestRouter
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly StatusController _statusController;
    private readonly ConfigController _configController;
    private readonly HistoricalController _historicalController;
    private readonly GeoJsonController _geoJsonController;
    private readonly MapConfigController _mapConfigController;
    private readonly TileController _tileController;
    private readonly StaticFileServer _staticFileServer;

    public RequestRouter(
        ICoreServerAPI sapi,
        ModConfig config,
        StatusController statusController,
        ConfigController configController,
        HistoricalController historicalController,
        GeoJsonController geoJsonController,
        MapConfigController mapConfigController,
        TileController tileController,
        StaticFileServer staticFileServer)
    {
        _sapi = sapi;
        _config = config;
        _statusController = statusController;
        _configController = configController;
        _historicalController = historicalController;
        _geoJsonController = geoJsonController;
        _mapConfigController = mapConfigController;
        _tileController = tileController;
        _staticFileServer = staticFileServer;
    }

    public async Task RouteRequest(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        
        // Route tile requests (with caching)
        if (TileController.IsTilePath(path))
        {
            await _tileController.ServeTile(context, path);
            return;
        }
        
        // Route API requests
        if (path.StartsWith("/api/"))
        {
            await RouteApiRequest(context, path.Substring(5).TrimStart('/'));
            return;
        }

        // Try to serve static files
        if (_staticFileServer.TryServeFile(context, path))
        {
            context.Response.Close();
            return;
        }

        // Not found
        _staticFileServer.ServeNotFound(context);
        context.Response.Close();
    }

    private async Task RouteApiRequest(HttpListenerContext context, string apiPath)
    {
        // Main status endpoint
        if (apiPath == _config.LiveServerEndpoint || apiPath == _config.LiveServerEndpoint + "/")
        {
            await _statusController.ServeStatus(context);
            return;
        }

        // Health check endpoint
        if (apiPath == "health" || apiPath == "health/")
        {
            await _statusController.ServeHealth(context);
            return;
        }

        // Configuration endpoints
        if (apiPath.StartsWith("config"))
        {
            if (context.Request.HttpMethod == "GET")
            {
                await _configController.GetConfig(context);
            }
            else if (context.Request.HttpMethod == "POST")
            {
                await _configController.UpdateConfig(context);
            }
            else
            {
                await ServeError(context, "Method not allowed. Use GET or POST", 405);
            }
            return;
        }

        // Export trigger endpoint
        if (apiPath.StartsWith("export"))
        {
            if (context.Request.HttpMethod == "POST")
            {
                await _configController.TriggerExport(context);
            }
            else
            {
                await ServeError(context, "Method not allowed. Use POST", 405);
            }
            return;
        }

        // Historical data endpoints
        if (apiPath.StartsWith("heatmap"))
        {
            await _historicalController.ServeHeatmap(context);
            return;
        }

        if (apiPath.StartsWith("player-path"))
        {
            await _historicalController.ServePlayerPath(context);
            return;
        }

        if (apiPath.StartsWith("census"))
        {
            await _historicalController.ServeCensus(context);
            return;
        }

        if (apiPath == "stats" || apiPath == "stats/")
        {
            await _historicalController.ServeStats(context);
            return;
        }

        // Map configuration endpoints
        if (apiPath.StartsWith("map/config") || apiPath == "map-config")
        {
            await _mapConfigController.ServeMapConfig(context);
            return;
        }

        if (apiPath.StartsWith("map/extent") || apiPath == "map-extent")
        {
            await _mapConfigController.ServeWorldExtent(context);
            return;
        }

        // GeoJSON endpoints
        if (apiPath.StartsWith("geojson/signs") || apiPath == "signs.geojson")
        {
            await _geoJsonController.ServeSigns(context);
            return;
        }

        if (apiPath.StartsWith("geojson/signposts") || apiPath == "signposts.geojson")
        {
            await _geoJsonController.ServeSignPosts(context);
            return;
        }

        if (apiPath.StartsWith("geojson/traders") || apiPath == "traders.geojson")
        {
            await _geoJsonController.ServeTraders(context);
            return;
        }

        if (apiPath.StartsWith("geojson/translocators") || apiPath == "translocators.geojson")
        {
            await _geoJsonController.ServeTranslocators(context);
            return;
        }

        if (apiPath.StartsWith("geojson/chunks") || apiPath == "chunks.geojson")
        {
            await _geoJsonController.ServeChunks(context);
            return;
        }

        // API endpoint not found
        await ServeError(context, "API endpoint not found", 404);
    }

    private async Task ServeError(HttpListenerContext context, string message, int statusCode = 500)
    {
        try
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            
            var errorBytes = Encoding.UTF8.GetBytes($"{{\"error\":\"{message}\"}}");
            context.Response.ContentLength64 = errorBytes.Length;
            await context.Response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length);
            context.Response.Close();
        }
        catch
        {
            // Silently fail if we can't write error response
        }
    }
}


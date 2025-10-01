using System.Net;
using System.Text;
using System.Threading.Tasks;
using VintageAtlas.Core;
using VintageAtlas.Web.API;

namespace VintageAtlas.Web.Server;

/// <summary>
/// Routes HTTP requests to appropriate handlers
/// </summary>
public class RequestRouter(
    ModConfig config,
    StatusController statusController,
    ConfigController configController,
    HistoricalController historicalController,
    GeoJsonController geoJsonController,
    MapConfigController mapConfigController,
    TileController tileController,
    StaticFileServer staticFileServer)
{
    public async Task RouteRequest(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        
        // Route tile requests (with caching)
        if (TileController.IsTilePath(path))
        {
            await tileController.ServeTile(context, path);
            return;
        }
        
        // Route API requests
        if (path.StartsWith("/api/"))
        {
            await RouteApiRequest(context, path[5..].TrimStart('/'));
            return;
        }

        // Try to serve static files
        if (staticFileServer.TryServeFile(context, path))
        {
            context.Response.Close();
            return;
        }

        // Not found
        staticFileServer.ServeNotFound(context);
        context.Response.Close();
    }

    private async Task RouteApiRequest(HttpListenerContext context, string apiPath)
    {
        // Main status endpoint
        if (apiPath == config.LiveServerEndpoint || apiPath == config.LiveServerEndpoint + "/")
        {
            await statusController.ServeStatus(context);
            return;
        }

        // Health check endpoint
        if (apiPath is "health" or "health/")
        {
            await statusController.ServeHealth(context);
            return;
        }

        // Split live endpoints
        if (apiPath is "live" or "live/")
        {
            await statusController.ServeLiveSummary(context);
            return;
        }

        if (apiPath.StartsWith("live/players") || apiPath == "players")
        {
            await statusController.ServePlayers(context);
            return;
        }
        if (apiPath.StartsWith("live/animals") || apiPath == "animals")
        {
            await statusController.ServeAnimals(context);
            return;
        }
        if (apiPath.StartsWith("live/weather") || apiPath == "weather")
        {
            await statusController.ServeWeather(context);
            return;
        }
        if (apiPath.StartsWith("live/date") || apiPath == "date")
        {
            await statusController.ServeDate(context);
            return;
        }
        if (apiPath.StartsWith("live/spawn") || apiPath == "spawn")
        {
            await statusController.ServeSpawn(context);
            return;
        }

        // Configuration endpoints
        if (apiPath.StartsWith("config"))
        {
            switch (context.Request.HttpMethod)
            {
                case "GET":
                    await configController.GetConfig(context);
                    break;
                case "POST":
                    await configController.UpdateConfig(context);
                    break;
                default:
                    await ServeError(context, "Method not allowed. Use GET or POST", 405);
                    break;
            }

            return;
        }

        // Export trigger endpoint
        if (apiPath.StartsWith("export"))
        {
            if (context.Request.HttpMethod == "POST")
            {
                await configController.TriggerExport(context);
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
            await historicalController.ServeHeatmap(context);
            return;
        }

        if (apiPath.StartsWith("player-path"))
        {
            await historicalController.ServePlayerPath(context);
            return;
        }

        if (apiPath.StartsWith("census"))
        {
            await historicalController.ServeCensus(context);
            return;
        }

        if (apiPath is "stats" or "stats/")
        {
            await historicalController.ServeStats(context);
            return;
        }

        // Map configuration endpoints
        if (apiPath.StartsWith("map/config") || apiPath == "map-config")
        {
            await mapConfigController.ServeMapConfig(context);
            return;
        }

        // GeoJSON endpoints
        if (apiPath.StartsWith("geojson/signs") || apiPath == "signs.geojson")
        {
            await geoJsonController.ServeSigns(context);
            return;
        }

        if (apiPath.StartsWith("geojson/signposts") || apiPath == "signposts.geojson")
        {
            await geoJsonController.ServeSignPosts(context);
            return;
        }

        if (apiPath.StartsWith("geojson/traders") || apiPath == "traders.geojson")
        {
            await geoJsonController.ServeTraders(context);
            return;
        }

        if (apiPath.StartsWith("geojson/translocators") || apiPath == "translocators.geojson")
        {
            await geoJsonController.ServeTranslocators(context);
            return;
        }

        if (apiPath.StartsWith("geojson/chunks") || apiPath == "chunks.geojson")
        {
            await geoJsonController.ServeChunks(context);
            return;
        }

        if (apiPath.StartsWith("geojson/chunk-versions") || apiPath == "chunk-versions.geojson")
        {
            await geoJsonController.ServeChunkVersions(context);
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
            await context.Response.OutputStream.WriteAsync(errorBytes);
            context.Response.Close();
        }
        catch
        {
            // Silently fail if we can't write an error response
        }
    }
}


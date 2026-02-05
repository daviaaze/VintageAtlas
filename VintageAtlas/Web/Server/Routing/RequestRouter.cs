using System.Net;
using System.Threading.Tasks;
using VintageAtlas.Web.API;
using VintageAtlas.Web.API.Controllers;
using VintageAtlas.Web.API.Responses;

namespace VintageAtlas.Web.Server.Routing;

/// <summary>
/// Routes HTTP requests to appropriate handlers
/// </summary>
public class RequestRouter(
    ConfigController configController,
    StatusController statusController,
    GeoJsonController geoJsonController,
    IMapConfigController mapConfigController,
    TileController tileController,
    WaypointController waypointController,
    StaticFileServer staticFileServer)
{
    private readonly ApiRouter _apiRouter = BuildApiRouter(
        configController,
        statusController,
        geoJsonController,
        mapConfigController,
        waypointController);

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
        StaticFileServer.ServeNotFound(context);
        context.Response.Close();
    }

    /// <summary>
    /// Build the API routing table with all endpoint configurations
    /// </summary>
    private static ApiRouter BuildApiRouter(
        ConfigController configController,
        StatusController statusController,
        GeoJsonController geoJsonController,

        IMapConfigController mapConfigController,
        WaypointController waypointController)
    {
        var router = new ApiRouter();

        // Status endpoint (game state: calendar, season, time)
        router.AddRoute("status", statusController.GetStatus, "GET");
        
        // Players endpoint (player positions and info)
        router.AddRoute("players", statusController.GetPlayers, "GET");

        // Configuration endpoints (method-specific)
        router.AddRoute("config", configController.GetConfig, "GET");
        router.AddRoute("config", configController.UpdateConfig, "POST");

        // Export trigger endpoint
        router.AddRoute("export", configController.TriggerExport, "POST");

        // Map configuration endpoints
        router.AddRoute(["map/config", "map-config"], mapConfigController.ServeMapConfig);

        // GeoJSON endpoints
        router.AddRoute(["geojson/traders", "traders.geojson"], geoJsonController.ServeTraders);

        // Waypoints endpoint
        router.AddRoute("waypoints", waypointController.GetWaypoints, "GET");

        return router;
    }

    private async Task RouteApiRequest(HttpListenerContext context, string apiPath)
    {
        var route = _apiRouter.FindRoute(apiPath, context.Request.HttpMethod);

        if (route != null)
        {
            await route.Handler(context);
            return;
        }

        // Handle method not allowed for known paths
        if (_apiRouter.FindRoute(apiPath, "*") != null)
        {
            await ErrorResponse.ServeMethodNotAllowedAsync(context);
            return;
        }

        // API endpoint not found
        await ErrorResponse.ServeNotFoundAsync(context, "API endpoint not found");
    }
}


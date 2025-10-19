using System.Net;
using System.Text;
using System.Threading.Tasks;
using VintageAtlas.Web.API;

namespace VintageAtlas.Web.Server.Routing;

/// <summary>
/// Routes HTTP requests to appropriate handlers
/// </summary>
public class RequestRouter(
    ConfigController configController,
    GeoJsonController geoJsonController,
    MapConfigController mapConfigController,
    TileController tileController,
    RainTileController rainTileController,
    TempTileController tempTileController,
    StaticFileServer staticFileServer)
{
    private readonly ApiRouter _apiRouter = BuildApiRouter(
        configController, 
        geoJsonController, 
        mapConfigController);
    public async Task RouteRequest(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";

        if (RainTileController.IsRainTilePath(path))
        {
            await rainTileController.ServeRainTile(context, path);
            return;
        }

        if (TempTileController.IsTempTilePath(path))
        {
            await tempTileController.ServeTempTile(context, path);
            return;
        }

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
        GeoJsonController geoJsonController,
        MapConfigController mapConfigController)
    {
        var router = new ApiRouter();

        // Configuration endpoints (method-specific)
        router.AddRoute("config", configController.GetConfig, "GET");
        router.AddRoute("config", configController.UpdateConfig, "POST");

        // Export trigger endpoint
        router.AddRoute("export", configController.TriggerExport, "POST");

        // Map configuration endpoints
        router.AddRoute(["map/config", "map-config"], mapConfigController.ServeMapConfig);

        // GeoJSON endpoints
        router.AddRoute(["geojson/traders", "traders.geojson"], geoJsonController.ServeTraders);

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
            await ServeError(context, "Method not allowed", 405);
            return;
        }

        // API endpoint not found
        await ServeError(context, "API endpoint not found", 404);
    }

    private static async Task ServeError(HttpListenerContext context, string message, int statusCode = 500)
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


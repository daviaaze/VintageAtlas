using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Models;

namespace VintageAtlas.Web.API;

/// <summary>
/// Handles historical data API endpoints (heatmap, paths, census, stats)
/// </summary>
public class HistoricalController(ICoreServerAPI sapi, IHistoricalTracker? historicalTracker)
{
    private readonly JsonSerializerSettings _jsonSettings = new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        },
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore
    };

    /// <summary>
    /// Serve heatmap data
    /// </summary>
    public async Task ServeHeatmap(HttpListenerContext context)
    {
        if (historicalTracker == null)
        {
            await ServeError(context, "Historical tracking not enabled", 501);
            return;
        }

        try
        {
            var queryParams = ParseHistoricalQuery(context);
            var heatmapData = historicalTracker.GetHeatmap(queryParams);

            var json = JsonConvert.SerializeObject(new
            {
                heatmap = heatmapData,
                gridSize = queryParams.GridSize,
                hours = queryParams.Hours,
                playerUid = queryParams.PlayerUid
            }, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;

            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error serving heatmap: {ex.Message}");
            await ServeError(context, "Failed to generate heatmap");
        }
    }

    /// <summary>
    /// Serve player path data
    /// </summary>
    public async Task ServePlayerPath(HttpListenerContext context)
    {
        if (historicalTracker == null)
        {
            await ServeError(context, "Historical tracking not enabled", 501);
            return;
        }

        try
        {
            var queryParams = ParseHistoricalQuery(context);

            if (string.IsNullOrEmpty(queryParams.PlayerUid))
            {
                await ServeError(context, "Player UID required (use ?player=UUID)", 400);
                return;
            }

            var pathData = historicalTracker.GetPlayerPath(queryParams);

            var json = JsonConvert.SerializeObject(new
            {
                playerUid = queryParams.PlayerUid,
                path = pathData,
                fromTimestamp = queryParams.FromTimestamp,
                toTimestamp = queryParams.ToTimestamp
            }, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;

            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error serving player path: {ex.Message}");
            await ServeError(context, "Failed to get player path");
        }
    }

    /// <summary>
    /// Serve entity census data
    /// </summary>
    public async Task ServeCensus(HttpListenerContext context)
    {
        if (historicalTracker == null)
        {
            await ServeError(context, "Historical tracking not enabled", 501);
            return;
        }

        try
        {
            var queryParams = ParseHistoricalQuery(context);
            var censusData = historicalTracker.GetEntityCensus(queryParams);

            var json = JsonConvert.SerializeObject(new
            {
                census = censusData,
                entityType = queryParams.EntityType,
                hours = queryParams.Hours
            }, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;

            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error serving census: {ex.Message}");
            await ServeError(context, "Failed to get census data");
        }
    }

    /// <summary>
    /// Serve server statistics
    /// </summary>
    public async Task ServeStats(HttpListenerContext context)
    {
        if (historicalTracker == null)
        {
            await ServeError(context, "Historical tracking not enabled", 501);
            return;
        }

        try
        {
            var stats = historicalTracker.GetServerStatistics();

            var json = JsonConvert.SerializeObject(stats, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;

            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error serving stats: {ex.Message}");
            await ServeError(context, "Failed to get server statistics");
        }
    }

    private HistoricalQueryParams ParseHistoricalQuery(HttpListenerContext context)
    {
        var query = context.Request.Url?.Query;
        var queryParams = new HistoricalQueryParams();

        if (string.IsNullOrEmpty(query)) return queryParams;

        // Remove leading '?' and split by '&'
        query = query.TrimStart('?');
        var pairs = query.Split('&');

        foreach (var pair in pairs)
        {
            var parts = pair.Split('=');
            if (parts.Length != 2) continue;

            var key = Uri.UnescapeDataString(parts[0]);
            var value = Uri.UnescapeDataString(parts[1]);

            switch (key.ToLowerInvariant())
            {
                case "player":
                    queryParams.PlayerUid = value;
                    break;
                case "entity":
                    queryParams.EntityType = value;
                    break;
                case "hours":
                    if (int.TryParse(value, out var hours))
                        queryParams.Hours = hours;
                    break;
                case "from":
                    if (long.TryParse(value, out var from))
                        queryParams.FromTimestamp = from;
                    break;
                case "to":
                    if (long.TryParse(value, out var to))
                        queryParams.ToTimestamp = to;
                    break;
                case "gridsize":
                    if (int.TryParse(value, out var gridSize))
                        queryParams.GridSize = gridSize;
                    break;
            }
        }

        return queryParams;
    }

    private async Task ServeError(HttpListenerContext context, string message, int statusCode = 500)
    {
        try
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var errorJson = JsonConvert.SerializeObject(new { error = message }, _jsonSettings);
            var errorBytes = Encoding.UTF8.GetBytes(errorJson);

            context.Response.ContentLength64 = errorBytes.Length;
            await context.Response.OutputStream.WriteAsync(errorBytes);
            context.Response.Close();
        }
        catch
        {
            // Silently fail if we can't write error response
        }
    }
}


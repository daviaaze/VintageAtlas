using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Vintagestory.API.Server;
using VintageAtlas.Core;

namespace VintageAtlas.Web.API;

/// <summary>
/// Handles status and health check API endpoints
/// </summary>
public class StatusController(ICoreServerAPI sapi, IDataCollector dataCollector)
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
    /// Serve current server status data (players, animals, weather, etc.)
    /// </summary>
    public async Task ServeStatus(HttpListenerContext context)
    {
        try
        {
            // Check if world is ready (like ServerstatusQuery does)
            if (sapi.World == null || sapi.World.BlockAccessor == null)
            {
                context.Response.StatusCode = 503; // Service Unavailable
                context.Response.ContentType = "application/json";
                var errorJson = JsonConvert.SerializeObject(new { error = "World not ready" }, _jsonSettings);
                var errorBytes = Encoding.UTF8.GetBytes(errorJson);
                await context.Response.OutputStream.WriteAsync(errorBytes);
                context.Response.Close();
                return;
            }

            var data = dataCollector.CollectData();
            var json = JsonConvert.SerializeObject(data, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            
            await context.Response.OutputStream.WriteAsync(bytes);
            await context.Response.OutputStream.FlushAsync();
            context.Response.Close();
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error serving status data: {ex.Message}");
            await ServeError(context, "Internal server error");
        }
    }

    /// <summary>
    /// Serve health check endpoint (lightweight status check)
    /// </summary>
    public async Task ServeHealth(HttpListenerContext context)
    {
        try
        {
            var health = new
            {
                status = "ok",
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                players = sapi.World?.AllOnlinePlayers?.Length ?? 0,
                uptime = DateTime.UtcNow.ToString("o")
            };

            var json = JsonConvert.SerializeObject(health, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error serving health check: {ex.Message}");
            await ServeError(context, "Internal server error");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Split live endpoints (lighter payloads than /status)
    // ─────────────────────────────────────────────────────────────────────────────

    public async Task ServePlayers(HttpListenerContext context)
    {
        try
        {
            if (sapi.World == null || sapi.World.BlockAccessor == null)
            {
                await ServeError(context, "World not ready", 503);
                return;
            }

            var data = dataCollector.CollectData();
            var payload = new { players = data.Players };
            var json = JsonConvert.SerializeObject(payload, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error serving players: {ex.Message}");
            await ServeError(context, "Internal server error");
        }
    }

    public async Task ServeAnimals(HttpListenerContext context)
    {
        try
        {
            if (sapi.World == null || sapi.World.BlockAccessor == null)
            {
                await ServeError(context, "World not ready", 503);
                return;
            }

            var data = dataCollector.CollectData();
            var payload = new { animals = data.Animals };
            var json = JsonConvert.SerializeObject(payload, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error serving animals: {ex.Message}");
            await ServeError(context, "Internal server error");
        }
    }

    public async Task ServeWeather(HttpListenerContext context)
    {
        try
        {
            if (sapi.World == null || sapi.World.BlockAccessor == null)
            {
                await ServeError(context, "World not ready", 503);
                return;
            }

            var data = dataCollector.CollectData();
            var payload = new { weather = data.Weather };
            var json = JsonConvert.SerializeObject(payload, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error serving weather: {ex.Message}");
            await ServeError(context, "Internal server error");
        }
    }

    public async Task ServeDate(HttpListenerContext context)
    {
        try
        {
            if (sapi.World == null || sapi.World.BlockAccessor == null)
            {
                await ServeError(context, "World not ready", 503);
                return;
            }

            var data = dataCollector.CollectData();
            var payload = new { date = data.Date };
            var json = JsonConvert.SerializeObject(payload, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error serving date: {ex.Message}");
            await ServeError(context, "Internal server error");
        }
    }

    public async Task ServeSpawn(HttpListenerContext context)
    {
        try
        {
            if (sapi.World == null || sapi.World.BlockAccessor == null)
            {
                await ServeError(context, "World not ready", 503);
                return;
            }

            var data = dataCollector.CollectData();
            var payload = new { spawnPoint = data.SpawnPoint, temperature = data.SpawnTemperature, rainfall = data.SpawnRainfall };
            var json = JsonConvert.SerializeObject(payload, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error serving spawn: {ex.Message}");
            await ServeError(context, "Internal server error");
        }
    }

    public async Task ServeLiveSummary(HttpListenerContext context)
    {
        try
        {
            if (sapi.World == null || sapi.World.BlockAccessor == null)
            {
                await ServeError(context, "World not ready", 503);
                return;
            }

            var data = dataCollector.CollectData();
            var payload = new
            {
                playersOnline = data.Players?.Count ?? 0,
                animalsTracked = data.Animals?.Count ?? 0,
                date = data.Date,
                weather = data.Weather
            };
            var json = JsonConvert.SerializeObject(payload, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error serving live summary: {ex.Message}");
            await ServeError(context, "Internal server error");
        }
    }

    private async Task ServeError(HttpListenerContext context, string message, int statusCode = 500)
    {
        try
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            
            var errorJson = JsonConvert.SerializeObject(new { error = message }, _jsonSettings);
            var errorBytes = Encoding.UTF8.GetBytes(errorJson);
            
            await context.Response.OutputStream.WriteAsync(errorBytes);
            context.Response.Close();
        }
        catch
        {
            // Silently fail if we can't write error response
        }
    }
}


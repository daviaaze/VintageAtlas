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
public class StatusController
{
    private readonly ICoreServerAPI _sapi;
    private readonly IDataCollector _dataCollector;
    private readonly JsonSerializerSettings _jsonSettings;

    public StatusController(ICoreServerAPI sapi, IDataCollector dataCollector)
    {
        _sapi = sapi;
        _dataCollector = dataCollector;
        
        _jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };
    }

    /// <summary>
    /// Serve current server status data (players, animals, weather, etc.)
    /// </summary>
    public async Task ServeStatus(HttpListenerContext context)
    {
        try
        {
            // Check if world is ready (like ServerstatusQuery does)
            if (_sapi.World == null || _sapi.World.BlockAccessor == null)
            {
                context.Response.StatusCode = 503; // Service Unavailable
                context.Response.ContentType = "application/json";
                var errorJson = JsonConvert.SerializeObject(new { error = "World not ready" }, _jsonSettings);
                var errorBytes = Encoding.UTF8.GetBytes(errorJson);
                await context.Response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length);
                context.Response.Close();
                return;
            }

            var data = _dataCollector.CollectData();
            var json = JsonConvert.SerializeObject(data, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.OutputStream.Flush();
            context.Response.Close();
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Error serving status data: {ex.Message}");
            await ServeError(context, "Internal server error", 500);
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
                players = _sapi.World?.AllOnlinePlayers?.Length ?? 0,
                uptime = DateTime.UtcNow.ToString("o")
            };

            var json = JsonConvert.SerializeObject(health, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Error serving health check: {ex.Message}");
            await ServeError(context, "Internal server error", 500);
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
            
            await context.Response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length);
            context.Response.Close();
        }
        catch
        {
            // Silently fail if we can't write error response
        }
    }
}


using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Vintagestory.API.Server;
using VintageAtlas.Core;

namespace VintageAtlas.Web.API;

/// <summary>
/// Handles configuration and export trigger API endpoints
/// </summary>
public class ConfigController
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly IMapExporter _mapExporter;
    private readonly JsonSerializerSettings _jsonSettings;
    private bool _autoExportEnabled;
    private bool _historicalTrackingEnabled;

    public ConfigController(
        ICoreServerAPI sapi,
        ModConfig config,
        IMapExporter mapExporter)
    {
        _sapi = sapi;
        _config = config;
        _mapExporter = mapExporter;
        
        // Initialize runtime state from config
        _autoExportEnabled = config.AutoExportMap;
        _historicalTrackingEnabled = config.EnableHistoricalTracking;
        
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
    /// Get current runtime configuration
    /// </summary>
    public async Task GetConfig(HttpListenerContext context)
    {
        try
        {
            var configData = new
            {
                autoExportMap = _autoExportEnabled,
                historicalTracking = _historicalTrackingEnabled,
                exportIntervalMs = _config.MapExportIntervalMs,
                isExporting = _mapExporter.IsRunning,
                enableLiveServer = _config.EnableLiveServer,
                maxConcurrentRequests = _config.MaxConcurrentRequests ?? 50
            };
            
            var json = JsonConvert.SerializeObject(configData, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.Close();
            
            _sapi.Logger.Debug("[VintageAtlas] Config requested via API");
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Error serving config: {ex.Message}");
            await ServeError(context, "Failed to get configuration");
        }
    }

    /// <summary>
    /// Update runtime configuration
    /// </summary>
    public async Task UpdateConfig(HttpListenerContext context)
    {
        try
        {
            // Read request body
            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            var body = await reader.ReadToEndAsync();
            var update = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
            
            if (update == null)
            {
                await ServeError(context, "Invalid JSON body", 400);
                return;
            }
            
            // Update runtime config
            if (update.ContainsKey("autoExportMap"))
            {
                var oldValue = _autoExportEnabled;
                _autoExportEnabled = Convert.ToBoolean(update["autoExportMap"]);
                _sapi.Logger.Notification($"[VintageAtlas] Auto-export toggled: {oldValue} → {_autoExportEnabled} (via web UI)");
            }
            
            if (update.ContainsKey("historicalTracking"))
            {
                var oldValue = _historicalTrackingEnabled;
                _historicalTrackingEnabled = Convert.ToBoolean(update["historicalTracking"]);
                _sapi.Logger.Notification($"[VintageAtlas] Historical tracking toggled: {oldValue} → {_historicalTrackingEnabled} (via web UI)");
            }
            
            // Optionally save to persistent config
            if (update.ContainsKey("saveToDisk") && Convert.ToBoolean(update["saveToDisk"]))
            {
                _config.AutoExportMap = _autoExportEnabled;
                _config.EnableHistoricalTracking = _historicalTrackingEnabled;
                SaveConfig(_config);
                _sapi.Logger.Notification("[VintageAtlas] Runtime config saved to disk");
            }
            
            // Return updated config
            await GetConfig(context);
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Error updating config: {ex.Message}");
            await ServeError(context, "Failed to update configuration");
        }
    }

    /// <summary>
    /// Trigger a manual map export
    /// </summary>
    public async Task TriggerExport(HttpListenerContext context)
    {
        try
        {
            if (_mapExporter.IsRunning)
            {
                var json = JsonConvert.SerializeObject(new { 
                    success = false, 
                    message = "Export already running" 
                }, _jsonSettings);
                var bytes = Encoding.UTF8.GetBytes(json);
                
                context.Response.StatusCode = 409; // Conflict
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                context.Response.Close();
                return;
            }
            
            // Trigger export
            _mapExporter.StartExport();
            _sapi.Logger.Notification("[VintageAtlas] Manual export triggered via web UI");
            
            var successJson = JsonConvert.SerializeObject(new { 
                success = true, 
                message = "Export started" 
            }, _jsonSettings);
            var successBytes = Encoding.UTF8.GetBytes(successJson);
            
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = successBytes.Length;
            
            await context.Response.OutputStream.WriteAsync(successBytes, 0, successBytes.Length);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Error triggering export: {ex.Message}");
            await ServeError(context, "Failed to trigger export");
        }
    }

    /// <summary>
    /// Get current auto-export enabled state
    /// </summary>
    public bool AutoExportEnabled => _autoExportEnabled;
    
    /// <summary>
    /// Get current historical tracking enabled state
    /// </summary>
    public bool HistoricalTrackingEnabled => _historicalTrackingEnabled;

    private void SaveConfig(ModConfig config)
    {
        try
        {
            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            var configPath = _sapi.GetOrCreateDataPath("ModConfig") + "/vintageatlas.json";
            File.WriteAllText(configPath, json);
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to save config: {ex.Message}");
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


using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Web.API.Base;
using VintageAtlas.Web.API.Helpers;

namespace VintageAtlas.Web.API;

/// <summary>
/// Handles configuration and export trigger API endpoints
/// </summary>
public class ConfigController : JsonController
{
    private readonly ModConfig _config;
    private readonly IMapExporter _mapExporter;
    private bool _autoExportEnabled;
    private bool _historicalTrackingEnabled;

    public ConfigController(
        ICoreServerAPI sapi,
        ModConfig config,
        IMapExporter mapExporter) : base(sapi)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _mapExporter = mapExporter ?? throw new ArgumentNullException(nameof(mapExporter));
        _autoExportEnabled = config.AutoExportMap;
        _historicalTrackingEnabled = config.EnableHistoricalTracking;
    }

    // Initialize runtime state from config

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

            await ServeJson(context, configData, cacheControl: CacheHelper.NoCache);
        }
        catch (Exception ex)
        {
            LogError($"Error serving config: {ex.Message}", ex);
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
            if (update.TryGetValue("autoExportMap", out var autoExportMap))
            {
                var oldValue = _autoExportEnabled;
                _autoExportEnabled = Convert.ToBoolean(autoExportMap);
                Sapi.Logger.Notification($"[VintageAtlas] Auto-export toggled: {oldValue} → {_autoExportEnabled} (via web UI)");
            }

            if (update.TryGetValue("historicalTracking", out var historicalTracking))
            {
                var oldValue = _historicalTrackingEnabled;
                _historicalTrackingEnabled = Convert.ToBoolean(historicalTracking);
                Sapi.Logger.Notification($"[VintageAtlas] Historical tracking toggled: {oldValue} → {_historicalTrackingEnabled} (via web UI)");
            }

            // Optionally save to persistent config
            if (update.ContainsKey("saveToDisk") && Convert.ToBoolean(update["saveToDisk"]))
            {
                _config.AutoExportMap = _autoExportEnabled;
                _config.EnableHistoricalTracking = _historicalTrackingEnabled;
                SaveConfig(_config);
                Sapi.Logger.Notification("[VintageAtlas] Runtime config saved to disk");
            }

            // Return updated config
            await GetConfig(context);
        }
        catch (Exception ex)
        {
            LogError($"Error updating config: {ex.Message}", ex);
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
                await ServeJson(context, new
                {
                    success = false,
                    message = "Export already running"
                }, statusCode: 409);
                return;
            }

            // Trigger export
            _mapExporter.StartExport();
            Sapi.Logger.Notification("[VintageAtlas] Manual export triggered via web UI");

            await ServeJson(context, new
            {
                success = true,
                message = "Export started"
            });
        }
        catch (Exception ex)
        {
            LogError($"Error triggering export: {ex.Message}", ex);
            await ServeError(context, "Failed to trigger export");
        }
    }

    /// <summary>
    /// Get current auto-export enabled state
    /// </summary>
    public bool AutoExportEnabled => _autoExportEnabled;

    /// <summary>
    /// Get the current historical tracking enabled state
    /// </summary>
    public bool HistoricalTrackingEnabled => _historicalTrackingEnabled;

    private void SaveConfig(ModConfig modConfig)
    {
        try
        {
            var json = JsonConvert.SerializeObject(modConfig, Formatting.Indented);
            var configPath = Sapi.GetOrCreateDataPath("ModConfig") + "/vintageatlas.json";
            File.WriteAllText(configPath, json);
        }
        catch (Exception ex)
        {
            LogError($"Failed to save config: {ex.Message}", ex);
        }
    }
}


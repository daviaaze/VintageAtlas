using System;
using System.IO;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Server;
using VintageAtlas.Commands;
using VintageAtlas.Core;
using VintageAtlas.Export;
using VintageAtlas.Tracking;
using VintageAtlas.Web.API;
using VintageAtlas.Web.Server;

namespace VintageAtlas;

/// <summary>
/// Main mod system for VintageAtlas - a comprehensive mapping and tracking solution for Vintage Story
/// Provides map export, live web server, and historical data tracking
/// </summary>
public class VintageAtlasModSystem : ModSystem
{
    private ICoreServerAPI? _sapi;
    private ModConfig? _config;
    
    // Core components
    private MapExporter? _mapExporter;
    private DataCollector? _dataCollector;
    private HistoricalTracker? _historicalTracker;
    
    // Web components
    private WebServer? _webServer;
    private long _lastMapExport;
    
    // Network channel for color data from clients
    private IServerNetworkChannel? _serverNetworkChannel;

    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide == EnumAppSide.Server; // Server-side only
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _sapi = api;
        _config = LoadConfig();
        
        // Validate configuration
        var validationErrors = ConfigValidator.Validate(_config);
        if (validationErrors.Count > 0)
        {
            _sapi.Logger.Error("[VintageAtlas] Configuration errors:");
            foreach (var error in validationErrors)
            {
                _sapi.Logger.Error($"  - {error}");
            }
            _sapi.Logger.Error("[VintageAtlas] Please fix configuration and restart server");
            return;
        }
        
        // Apply auto-fixes
        ConfigValidator.ApplyAutoFixes(_config);
        
        _sapi.Logger.Notification("[VintageAtlas] Initializing...");
        
        // Initialize map exporter
        _mapExporter = new MapExporter(_sapi, _config);
        
        // Register commands
        ExportCommand.Register(_sapi, _mapExporter);
        
        // Register network channel for receiving color data from client
        _serverNetworkChannel = _sapi.Network.RegisterChannel("vintageatlas");
        _serverNetworkChannel.RegisterMessageType(typeof(ExportData));
        _serverNetworkChannel.SetMessageHandler<ExportData>(OnClientData);
        
        // Setup export on start if configured
        if (_config.ExportOnStart)
        {
            _sapi.Event.ServerRunPhase(EnumServerRunPhase.RunGame, () => _mapExporter.StartExport());
        }
        
        // Setup live server if enabled
        if (_config.EnableLiveServer)
        {
            _sapi.Event.ServerRunPhase(EnumServerRunPhase.GameReady, SetupLiveServer);
        }
        
        _sapi.Logger.Notification("[VintageAtlas] Initialization complete");
    }

    private void SetupLiveServer()
    {
        if (_sapi == null || _config == null || _mapExporter == null) return;
        
        try
        {
            _sapi.Logger.Notification("[VintageAtlas] Setting up live web server...");
            
            // Initialize data collector
            _dataCollector = new DataCollector(_sapi);
            
            // Initialize historical tracker if enabled
            if (_config.EnableHistoricalTracking)
            {
                _historicalTracker = new HistoricalTracker(_sapi);
                _historicalTracker.Initialize();
                _sapi.Event.PlayerDeath += OnPlayerDeath;
            }
            
            // Setup web root directory
            var webRoot = FindWebRoot();
            if (webRoot == null)
            {
                _sapi.Logger.Error("[VintageAtlas] Could not find web files! Live server disabled.");
                _sapi.Logger.Error("[VintageAtlas] Please ensure the mod was built correctly with embedded HTML files.");
                return;
            }
            
            // Create web server components
            var staticFileServer = new StaticFileServer(_sapi, webRoot, _config);
            var statusController = new StatusController(_sapi, _dataCollector);
            var configController = new ConfigController(_sapi, _config, _mapExporter);
            var historicalController = new HistoricalController(_sapi, _historicalTracker);
            
            var router = new RequestRouter(
                _sapi,
                _config,
                statusController,
                configController,
                historicalController,
                staticFileServer
            );
            
            _webServer = new WebServer(_sapi, _config, router);
            _webServer.Start();
            
            // Register game tick for historical tracking and auto-export
            _sapi.Event.RegisterGameTickListener(OnGameTick, 1000);
            
            // Register shutdown handler
            _sapi.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, OnShutdown);
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to setup live server: {ex.Message}");
            _sapi.Logger.Error(ex.StackTrace ?? "");
        }
    }

    private string? FindWebRoot()
    {
        if (_config == null || _sapi == null) return null;
        
        // Try multiple locations in order:
        // 1. OutputDirectory/html (where exports go)
        // 2. Mod directory/html (bundled with mod)
        // 3. ModData directory (fallback)
        var possibleRoots = new[]
        {
            Path.Combine(_config.OutputDirectory, "html"),
            Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location) ?? "", "html"),
            Path.Combine(_sapi.DataBasePath, "ModData", "VintageAtlas", "html")
        };
        
        foreach (var path in possibleRoots)
        {
            _sapi.Logger.Debug($"[VintageAtlas] Checking web root: {path}");
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "index.html")))
            {
                _sapi.Logger.Notification($"[VintageAtlas] Using web root: {path}");
                return path;
            }
        }
        
        // Create fallback directory
        var fallback = possibleRoots[2];
        Directory.CreateDirectory(fallback);
        _sapi.Logger.Warning($"[VintageAtlas] Created empty web root: {fallback}");
        _sapi.Logger.Warning("[VintageAtlas] Please copy html/ folder contents or rebuild the mod.");
        
        return fallback;
    }

    private void OnGameTick(float dt)
    {
        if (_config == null || _sapi == null) return;
        
        // Update historical tracker if enabled
        if (_config.EnableHistoricalTracking && _historicalTracker != null)
        {
            _historicalTracker.OnGameTick(dt);
        }
        
        // Auto-export map data if enabled and interval has passed
        if (_config.AutoExportMap && _config.EnableLiveServer && _mapExporter != null)
        {
            var currentTime = _sapi.World.ElapsedMilliseconds;
            if ((currentTime - _lastMapExport) >= _config.MapExportIntervalMs)
            {
                _lastMapExport = currentTime;
                _mapExporter.StartExport();
            }
        }
    }

    private void OnPlayerDeath(IServerPlayer byPlayer, DamageSource damageSource)
    {
        if (_historicalTracker == null) return;
        
        string? source = damageSource?.Type.ToString();
        if (damageSource?.SourceEntity != null)
        {
            source = damageSource.SourceEntity.Code?.ToString() ?? source;
        }
        
        _historicalTracker.RecordPlayerDeath(byPlayer, source);
    }

    private void OnShutdown()
    {
        _sapi?.Logger.Notification("[VintageAtlas] Shutting down...");
        
        _webServer?.Dispose();
        _historicalTracker?.Dispose();
        
        _sapi?.Logger.Notification("[VintageAtlas] Shutdown complete");
    }

    private void OnClientData(IServerPlayer fromplayer, ExportData exportData)
    {
        if (fromplayer.HasPrivilege("root") && _sapi != null)
        {
            _sapi.StoreModConfig(exportData, "blockColorMapping.json");
            _sapi.Logger.Notification($"[VintageAtlas] Received block color data from {fromplayer.PlayerName}");
            _mapExporter?.StartExport();
        }
    }

    private ModConfig LoadConfig()
    {
        if (_sapi == null) throw new InvalidOperationException("API not initialized");
        
        var config = _sapi.LoadModConfig<ModConfig>("VintageAtlasConfig.json");

        if (config is null)
        {
            _sapi.Logger.Warning("[VintageAtlas] No configuration found, creating default config");

            config = new ModConfig
            {
                Mode = ImageMode.MedievalStyleWithHillShading,
                OutputDirectory = Path.Combine(GamePaths.DataPath, "vintageatlas")
            };
            
            _sapi.StoreModConfig(config, "VintageAtlasConfig.json");
            _sapi.Logger.Notification($"[VintageAtlas] Created default config at: {Path.Combine(GamePaths.ModConfig, "VintageAtlasConfig.json")}");
        }

        return config;
    }

    public override void Dispose()
    {
        _webServer?.Dispose();
        _historicalTracker?.Dispose();
        base.Dispose();
    }
}


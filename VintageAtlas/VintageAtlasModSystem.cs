using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
    private ChunkChangeTracker? _chunkChangeTracker;
    private DynamicTileGenerator? _tileGenerator;
    private TileGenerationState? _tileState;
    private BackgroundTileService? _backgroundTileService;
    
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
            
            // Initialize output directories for proper path resolution
            _config.OutputDirectoryWorld = Path.Combine(_config.OutputDirectory, "data", "world");
            _config.OutputDirectoryGeojson = Path.Combine(_config.OutputDirectory, "data", "geojson");
            
            // Initialize data collector
            _dataCollector = new DataCollector(_sapi);
            
            // Initialize chunk change tracker for dynamic updates
            _chunkChangeTracker = new ChunkChangeTracker(_sapi);
            
            // Initialize block color cache for tile generation
            var colorCache = new BlockColorCache(_sapi, _config);
            colorCache.Initialize(); // Load block color mappings
            
            // Initialize dynamic tile generator
            _tileGenerator = new DynamicTileGenerator(_sapi, _config, colorCache);
            
            // Initialize tile generation state database
            _tileState = new TileGenerationState(_sapi, _config.OutputDirectory);
            
            // Initialize background tile generation service
            _backgroundTileService = new BackgroundTileService(
                _sapi, 
                _config, 
                _tileState, 
                _tileGenerator, 
                _chunkChangeTracker
            );
            
            // IMPROVED: Register as async server system (proper Vintage Story API integration)
            _sapi.Server.AddServerThread("tile_service", _backgroundTileService);
            _sapi.Logger.Debug("[VintageAtlas] Background tile service registered as async system");
            
            _backgroundTileService.Start();
            
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
            var geoJsonController = new GeoJsonController(_sapi, _config);
            var mapConfigController = new MapConfigController(_sapi, _config, _tileGenerator);
            var tileController = new TileController(_sapi, _config, _tileGenerator);
            
            var router = new RequestRouter(
                _sapi,
                _config,
                statusController,
                configController,
                historicalController,
                geoJsonController,
                mapConfigController,
                tileController,
                staticFileServer
            );
            
            _webServer = new WebServer(_sapi, _config, router);
            _webServer.Start();
            
            // Register game tick for historical tracking and auto-export
            _sapi.Event.RegisterGameTickListener(OnGameTick, 1000);
            
            // Register shutdown handler
            _sapi.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, OnShutdown);
            
            _sapi.Logger.Notification("[VintageAtlas] Live server ready with background tile generation");
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
        
        // Serve HTML directly from the mod's bundled html directory
        // No need to copy - static files are served from the mod
        var modHtml = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location) ?? "", "html");
        
        if (Directory.Exists(modHtml) && File.Exists(Path.Combine(modHtml, "index.html")))
        {
            _sapi.Logger.Notification($"[VintageAtlas] Serving static files from mod directory: {modHtml}");
            _sapi.Logger.Notification($"[VintageAtlas] Generated data will be stored in: {_config.OutputDirectory}");
            return modHtml;
        }
        
        _sapi.Logger.Error($"[VintageAtlas] Could not find HTML files in mod directory: {modHtml}");
        _sapi.Logger.Error("[VintageAtlas] Please ensure the mod was built correctly with embedded HTML files.");
        return null;
    }

    private void OnGameTick(float dt)
    {
        if (_config == null || _sapi == null) return;
        
        // Update historical tracker if enabled
        if (_config.EnableHistoricalTracking && _historicalTracker != null)
        {
            _historicalTracker.OnGameTick(dt);
        }
        
        // Note: Tile regeneration is now handled by BackgroundTileService
        // It runs in a separate thread and doesn't block the game thread
        
        // Auto-export full map data if enabled and interval has passed (optional full regeneration)
        if (!_config.AutoExportMap || !_config.EnableLiveServer || _mapExporter == null) return;
        
        var currentTime = _sapi.World.ElapsedMilliseconds;
        
        if (currentTime - _lastMapExport < _config.MapExportIntervalMs) return;
        
        _lastMapExport = currentTime;
        _mapExporter.StartExport();
    }

    private void OnPlayerDeath(IServerPlayer byPlayer, DamageSource? damageSource)
    {
        if (_historicalTracker == null) return;
        
        var source = damageSource?.Type.ToString();
        if (damageSource?.SourceEntity != null)
        {
            source = damageSource.SourceEntity.Code?.ToString() ?? source;
        }
        
        _historicalTracker.RecordPlayerDeath(byPlayer, source);
    }

    private void OnShutdown()
    {
        _sapi?.Logger.Notification("[VintageAtlas] Shutting down...");
        
        _backgroundTileService?.Stop();
        _tileState?.Dispose();
        _webServer?.Dispose();
        _historicalTracker?.Dispose();
        _chunkChangeTracker?.Dispose();
        
        _sapi?.Logger.Notification("[VintageAtlas] Shutdown complete");
    }

    private void OnClientData(IServerPlayer sourcePlayer, ExportData exportData)
    {
        if (!sourcePlayer.HasPrivilege("root") || _sapi == null) return;
        _sapi.StoreModConfig(exportData, "blockColorMapping.json");
        _sapi.Logger.Notification($"[VintageAtlas] Received block color data from {sourcePlayer.PlayerName}");
        _mapExporter?.StartExport();
    }

    private ModConfig LoadConfig()
    {
        if (_sapi == null) throw new InvalidOperationException("API not initialized");
        
        var config = _sapi.LoadModConfig<ModConfig>("VintageAtlasConfig.json");

        if (config is null)
        {
            _sapi.Logger.Warning("[VintageAtlas] No configuration found, creating default config");

            // Use ModData directory for all VintageAtlas data
            var modDataPath = Path.Combine(GamePaths.DataPath, "ModData", "VintageAtlas");
            config = new ModConfig
            {
                Mode = ImageMode.MedievalStyleWithHillShading,
                OutputDirectory = modDataPath
            };
            
            _sapi.StoreModConfig(config, "VintageAtlasConfig.json");
            _sapi.Logger.Notification($"[VintageAtlas] Created default config at: {Path.Combine(GamePaths.ModConfig, "VintageAtlasConfig.json")}");
            _sapi.Logger.Notification($"[VintageAtlas] Data will be stored in: {modDataPath}");
        }

        return config ?? throw new InvalidOperationException("Config initialization failed");
    }

    public override void Dispose()
    {
        _backgroundTileService?.Dispose();
        _tileState?.Dispose();
        _webServer?.Dispose();
        _historicalTracker?.Dispose();
        _chunkChangeTracker?.Dispose();
        base.Dispose();
    }
}


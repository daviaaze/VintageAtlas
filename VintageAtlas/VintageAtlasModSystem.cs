using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using VintageAtlas.Commands;
using VintageAtlas.Core;
using VintageAtlas.Export;
using VintageAtlas.Storage;
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
    private MapConfigController? _mapConfigController;
    // private ChunkChangeTracker? _chunkChangeTracker; // DISABLED for testing
    private ITileGenerator? _tileGenerator;
    private TileGenerationState? _tileState;
    // private BackgroundTileService? _backgroundTileService; // DISABLED for testing
    private MbTilesStorage? _storage;
    private BlockColorCache? _colorCache;
    
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
        
        _sapi.Logger.Notification("[VintageAtlas] Initializing...");
        
        // Initialize tile storage (shared by all tile generators)
        var dbPath = Path.Combine(_config.OutputDirectory, "data", "tiles.mbtiles");
        _storage = new MbTilesStorage(dbPath);
        _sapi.Logger.Debug($"[VintageAtlas] Tile storage initialized: {dbPath}");
        
        // Initialize the block color cache (needed for tile rendering)
        _colorCache = new BlockColorCache(_sapi, _config);
        _colorCache.Initialize();
        _sapi.Logger.Debug("[VintageAtlas] Block color cache initialized");
        
        // Initialize unified tile generator for full exports
        var unifiedGenerator = new UnifiedTileGenerator(_sapi, _config, _colorCache, _storage);
        _sapi.Logger.Debug("[VintageAtlas] Unified tile generator initialized");
        
        // Initialize map config controller (needed by exporter for cache invalidation)
        _mapConfigController = new MapConfigController(_sapi);
        _sapi.Logger.Debug("[VintageAtlas] Map config controller initialized");
        
        // Initialize map exporter with unified generator and map config controller
        _mapExporter = new MapExporter(_sapi, _config, unifiedGenerator, _mapConfigController);
        
        // Register commands
        ExportCommand.Register(_sapi, _mapExporter);
        
        // Register a network channel for receiving color data from the client
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
            
            // Initialize the chunk change tracker for dynamic updates (DISABLED for testing)
            // _chunkChangeTracker = new ChunkChangeTracker(_sapi);
            
            // Initialize the historical tracker if enabled (before game tick listener)
            if (_config.EnableHistoricalTracking)
            {
                _historicalTracker = new HistoricalTracker(_sapi);
                _historicalTracker.Initialize();
                _sapi.Event.PlayerDeath += OnPlayerDeath;
            }
            
            // CRITICAL: Register game tick listener to update caches ON MAIN THREAD
            // This prevents HTTP threads from accessing game state directly
            _sapi.Event.RegisterGameTickListener(dt => 
            {
                // Update data cache (called on main thread - THREAD SAFE)
                _dataCollector.UpdateCache(dt);
                
                // Update historical tracker if enabled
                if (_config.EnableHistoricalTracking && _historicalTracker != null)
                {
                    _historicalTracker.OnGameTick(dt);
                }
            }, 1000); // Call every second (1000ms)
            
            _sapi.Logger.Notification("[VintageAtlas] Main thread cache updates registered (HTTP threads isolated from game state)");
            
            // Reuse color cache initialized in StartServerSide
            if (_colorCache == null)
            {
                _colorCache = new BlockColorCache(_sapi, _config);
                _colorCache.Initialize();
            }
            
            // Initialize unified tile generator for live serving (shares storage with exporter)
            // This is the SAME generator used for full exports - no more code duplication!
            if (_storage == null)
            {
                var dbPath = Path.Combine(_config.OutputDirectory, "data", "tiles.mbtiles");
                _storage = new MbTilesStorage(dbPath);
            }
            
            // IMPORTANT: Use UnifiedTileGenerator for both export AND live generation
            // This ensures hill shading and all rendering modes work for on-demand tiles too!
            _tileGenerator = new UnifiedTileGenerator(_sapi, _config, _colorCache, _storage);
            
            // Initialize tile generation state database
            _tileState = new TileGenerationState(_sapi, _config.OutputDirectory);
            
            // ═══════════════════════════════════════════════════════════════
            // BACKGROUND TILE SERVICE DISABLED FOR TESTING
            // This prevents automatic tile generation on chunk updates
            // Tiles are ONLY generated during /atlas export
            // ═══════════════════════════════════════════════════════════════
            
            // // Initialize background tile generation service
            // _backgroundTileService = new BackgroundTileService(
            //     _sapi, 
            //     _config, 
            //     _tileState, 
            //     _tileGenerator, 
            //     _chunkChangeTracker
            // );
            
            // // IMPROVED: Register as async server system (proper Vintage Story API integration)
            // _sapi.Server.AddServerThread("tile_service", _backgroundTileService);
            // _sapi.Logger.Debug("[VintageAtlas] Background tile service registered as async system");
            
            // _backgroundTileService.Start();
            
            _sapi.Logger.Notification("[VintageAtlas] ⚠️  Background tile generation DISABLED - tiles only generated via /atlas export");
            
            // Create web server components
            var staticFileServer = new StaticFileServer(_sapi, _config);
            var statusController = new StatusController(_sapi, _dataCollector);
            var configController = new ConfigController(_sapi, _config, _mapExporter);
            var historicalController = new HistoricalController(_sapi, _historicalTracker);
            // Use the existing mapConfigController instance (created earlier)
            var mapConfigController = _mapConfigController ?? new MapConfigController(_sapi);
            
            // Create coordinate transformation service (centralized coordinate logic)
            var coordinateService = new CoordinateTransformService(mapConfigController, _config);
            
            // Inject coordinate service into controllers
            var geoJsonController = new GeoJsonController(_sapi, _config, coordinateService);
            var tileController = new TileController(_sapi, _config, _tileGenerator, mapConfigController);
            
            var router = new RequestRouter(
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

    private void OnGameTick(float dt)
    {
        if (_config == null || _sapi == null) return;
        
        // Note: Data collection is now handled by RegisterGameTickListener in SetupLiveServer()
        // Note: Historical tracking is also handled by RegisterGameTickListener in SetupLiveServer()
        // Note: Tile regeneration is handled by BackgroundTileService in a separate thread
        
        // Auto-export full map data if enabled, and the interval has passed (optional full regeneration)
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
        
        // _backgroundTileService?.Stop();
        _tileState?.Dispose();
        _webServer?.Dispose();
        _historicalTracker?.Dispose();
        // _chunkChangeTracker?.Dispose();
        
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

        if (config is not null) 
            return config ?? throw new InvalidOperationException("Config initialization failed");
        
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
        
        // DEBUG: Log the actual Mode value loaded
        _sapi.Logger.Notification($"[VintageAtlas] ════════ CONFIG LOADED ════════");
        _sapi.Logger.Notification($"[VintageAtlas] Mode = {config.Mode} ({(int)config.Mode})");
        _sapi.Logger.Notification($"[VintageAtlas] ════════════════════════════════");
        
        // Validate configuration
        var validationErrors = ConfigValidator.Validate(config);
        if (validationErrors.Count > 0)
        {
            _sapi.Logger.Error("[VintageAtlas] Configuration errors:");
            foreach (var error in validationErrors)
            {
                _sapi.Logger.Error($"  - {error}");
            }
            _sapi.Logger.Error("[VintageAtlas] Please fix configuration and restart server");
        }
        
        // Apply auto-fixes
        ConfigValidator.ApplyAutoFixes(config);
        return config ?? throw new InvalidOperationException("Config initialization failed");
    }

    public override void Dispose()
    {
        // _backgroundTileService?.Dispose();
        _tileState?.Dispose();
        _tileGenerator?.Dispose();
        _storage?.Dispose();
        _webServer?.Dispose();
        _historicalTracker?.Dispose();
        // _chunkChangeTracker?.Dispose();
        base.Dispose();
    }
}


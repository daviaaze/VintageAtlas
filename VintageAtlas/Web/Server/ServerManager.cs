using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Export;
using VintageAtlas.Storage;
using VintageAtlas.Tracking;
using VintageAtlas.Web.API;

namespace VintageAtlas.Web.Server;

/// <summary>
/// Manages the setup and lifecycle of the live web server and all its dependencies
/// </summary>
public sealed class ServerManager : IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly MapExporter _mapExporter;
    private readonly BlockColorCache _colorCache;
    private readonly MbTilesStorage _storage;
    private readonly MapConfigController? _mapConfigController;

    // Server components
    private DataCollector? _dataCollector;
    private HistoricalTracker? _historicalTracker;
    private UnifiedTileGenerator? _tileGenerator;
    private TileGenerationState? _tileState;
    private WebServer? _webServer;
    
    // Event callbacks for cleanup
    private long _gameTickListenerId;
    private PlayerDeathDelegate? _playerDeathCallback;

    public ServerManager(
        ICoreServerAPI sapi, 
        ModConfig config, 
        MapExporter mapExporter, 
        BlockColorCache colorCache, 
        MbTilesStorage storage, 
        MapConfigController? mapConfigController = null)
    {
        _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _mapExporter = mapExporter ?? throw new ArgumentNullException(nameof(mapExporter));
        _colorCache = colorCache ?? throw new ArgumentNullException(nameof(colorCache));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _mapConfigController = mapConfigController;
    }

    /// <summary>
    /// Initialize and start the live web server with all dependencies
    /// </summary>
    public void Initialize()
    {
        try
        {
            _sapi.Logger.Notification("[VintageAtlas] Setting up live web server...");
            
            InitializeDirectories();
            InitializeServices();
            RegisterEventHandlers();
            CreateWebServerComponents();
            StartWebServer();
            
            _sapi.Logger.Notification("[VintageAtlas] Live server ready");
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to setup live server: {ex.Message}");
            _sapi.Logger.Error(ex.StackTrace ?? "");
            throw;
        }
    }

    private void InitializeDirectories()
    {
        // Initialize output directories for proper path resolution
        _config.OutputDirectoryWorld = Path.Combine(_config.OutputDirectory, "data", "world");
        _config.OutputDirectoryGeojson = Path.Combine(_config.OutputDirectory, "data", "geojson");
    }

    private void InitializeServices()
    {
        // Initialize data collector
        _dataCollector = new DataCollector(_sapi);
        
        // Initialize the historical tracker if enabled
        if (_config.EnableHistoricalTracking)
        {
            _historicalTracker = new HistoricalTracker(_sapi);
            _historicalTracker.Initialize();
        }
        
        // Initialize unified tile generator for live serving (shares storage with exporter)
        // This is the SAME generator used for full exports - no more code duplication!
        _tileGenerator = new UnifiedTileGenerator(_sapi, _config, _colorCache, _storage);
        
        // Initialize tile generation state database
        _tileState = new TileGenerationState(_sapi, _config.OutputDirectory);
        
        _sapi.Logger.Notification("[VintageAtlas] ⚠️  Background tile generation DISABLED - tiles only generated via /atlas export");
    }

    private void RegisterEventHandlers()
    {
        // CRITICAL: Register game tick listener to update caches ON MAIN THREAD
        // This prevents HTTP threads from accessing game state directly
        _gameTickListenerId = _sapi.Event.RegisterGameTickListener(dt =>
        {
            // Update data cache (called on main thread - THREAD SAFE)
            _dataCollector?.UpdateCache(dt);
            
            // Update historical tracker if enabled
            if (_config.EnableHistoricalTracking && _historicalTracker != null)
            {
                _historicalTracker.OnGameTick(dt);
            }
        }, 1000); // Call every second (1000ms)
        _sapi.Logger.Notification("[VintageAtlas] Main thread cache updates registered (HTTP threads isolated from game state)");
        
        // Register player death handler for historical tracking
        if (_config.EnableHistoricalTracking)
        {
            _playerDeathCallback = (player, damageSource) => {
                var source = damageSource?.Type.ToString();
                if (damageSource?.SourceEntity != null)
                {
                    source = damageSource.SourceEntity.Code?.ToString() ?? source;
                }
                _historicalTracker?.RecordPlayerDeath(player, source);
            };
            _sapi.Event.PlayerDeath += _playerDeathCallback;
        }
        
        // Register shutdown handler
        _sapi.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, OnShutdown);
    }

    private void CreateWebServerComponents()
    {
        if (_tileGenerator == null || _dataCollector == null)
        {
            throw new InvalidOperationException("Services must be initialized before creating web server components");
        }

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
    }

    private void StartWebServer()
    {
        _webServer?.Start();
    }

    private void OnShutdown()
    {
        Dispose();
    }

    public void Dispose()
    {
        // Cleanup event handlers
        if (_gameTickListenerId != 0)
        {
            _sapi.Event.UnregisterGameTickListener(_gameTickListenerId);
        }
        
        if (_playerDeathCallback != null)
        {
            _sapi.Event.PlayerDeath -= _playerDeathCallback;
        }
        
        // Dispose web server
        _webServer?.Dispose();
        
        // Dispose other services
        _historicalTracker?.Dispose();
        _tileState?.Dispose();
    }
}

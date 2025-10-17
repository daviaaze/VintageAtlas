using System;
using System.IO;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Export;
using VintageAtlas.Storage;
using VintageAtlas.Tracking;
using VintageAtlas.Web.API;
using VintageAtlas.Web.Server.Routing;

namespace VintageAtlas.Web.Server;

/// <summary>
/// Manages the setup and lifecycle of the live web server and all its dependencies
/// </summary>
public sealed class ServerManager(
    ICoreServerAPI sapi,
    ModConfig config,
    MapExporter mapExporter,
    BlockColorCache colorCache,
    MbTilesStorage storage,
    MapConfigController? controller = null)
    : IDisposable
{
    private readonly ICoreServerAPI _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
    private readonly ModConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly MapExporter _mapExporter = mapExporter ?? throw new ArgumentNullException(nameof(mapExporter));
    private readonly BlockColorCache _colorCache = colorCache ?? throw new ArgumentNullException(nameof(colorCache));
    private readonly MbTilesStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));

    private UnifiedTileGenerator? _tileGenerator;
    private WebServer? _webServer;

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
        // Initialize unified tile generator for live serving (shares storage with exporter)
        // This is the SAME generator used for full exports - no more code duplication!
        _tileGenerator = new UnifiedTileGenerator(_sapi, _config, _colorCache, _storage);

        _sapi.Logger.Notification("[VintageAtlas] ⚠️  Background tile generation DISABLED - tiles only generated via /atlas export");
    }

    private void RegisterEventHandlers()
    {
        _sapi.Logger.Notification("[VintageAtlas] Main thread cache updates registered (HTTP threads isolated from game state)");

        // Register shutdown handler
        _sapi.Event.ServerRunPhase(EnumServerRunPhase.Shutdown, OnShutdown);
    }

    private void CreateWebServerComponents()
    {
        if (_tileGenerator == null)
        {
            throw new InvalidOperationException("Services must be initialized before creating web server components");
        }

        // Create web server components
        var staticFileServer = new StaticFileServer(_sapi, _config);
        var configController = new ConfigController(_sapi, _config, _mapExporter);

        // Use the existing mapConfigController instance (created earlier)
        var mapConfigController = controller ?? new MapConfigController(_sapi, _tileGenerator);

        // Create coordinate transformation service (centralized coordinate logic)
        var coordinateService = new CoordinateTransformService(_sapi);

        // Inject coordinate service into controllers
        var geoJsonController = new GeoJsonController(_sapi, coordinateService);
        var tileController = new TileController(_sapi, _config, _tileGenerator, mapConfigController);

        var router = new RequestRouter(
            configController,
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
        _webServer?.Dispose();
    }
}

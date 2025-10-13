using System;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VintageAtlas.Commands;
using VintageAtlas.Core;
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
    private CoreComponents? _coreComponents;

    // Web components
    private ServerManager? _serverManager;
    private long _lastMapExport;


    public override bool ShouldLoad(EnumAppSide forSide)
    {
        return forSide == EnumAppSide.Server; // Server-side only
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        _sapi = api;
        _config = ConfigValidator.LoadAndValidateConfig(_sapi);

        // Initialize core components
        _coreComponents = ComponentInitializer.Initialize(_sapi, _config);

        // Register commands
        ExportCommand.Register(_sapi, _coreComponents.MapExporter);

        // Setup export on start if configured
        if (_config.ExportOnStart)
        {
            _sapi.Event.ServerRunPhase(EnumServerRunPhase.RunGame, () => _coreComponents.MapExporter.StartExport());
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
        if (_sapi == null || _config == null || _coreComponents == null) return;

        try
        {
            // Initialize server manager with all dependencies
            _serverManager = new ServerManager(
                _sapi,
                _config,
                _coreComponents.MapExporter,
                _coreComponents.ColorCache,
                _coreComponents.Storage,
                _coreComponents.MapConfigController
            );

            // Let the ServerManager handle all the setup
            _serverManager.Initialize();

            // Register game tick for auto-export (separate from server manager responsibilities)
            _sapi.Event.RegisterGameTickListener(OnGameTick, 1000);
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to setup live server: {ex.Message}");
            _sapi.Logger.Error(ex.StackTrace ?? "");
        }
    }

    private void OnGameTick(float dt)
    {
        if (_config == null || _sapi == null || _coreComponents == null) return;

        // Auto-export full map data if enabled and the interval has passed
        if (!_config.AutoExportMap || !_config.EnableLiveServer) return;

        var currentTime = _sapi.World.ElapsedMilliseconds;

        if (currentTime - _lastMapExport < _config.MapExportIntervalMs) return;

        _lastMapExport = currentTime;
        _coreComponents.MapExporter.StartExport();
    }

    public override void Dispose()
    {
        // Dispose server manager (handles web server, historical tracker, and other server components)
        _serverManager?.Dispose();

        // Dispose core components
        _coreComponents?.Storage.Dispose();

        base.Dispose();
    }
}


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

    private ChunkChangeTracker? _chunkChangeTracker;
    private DynamicTileGenerator? _tileGenerator;

    private void SetupLiveServer()
    {
        if (_sapi == null || _config == null || _mapExporter == null) return;
        
        try
        {
            _sapi.Logger.Notification("[VintageAtlas] Setting up live web server...");
            
            // Initialize data collector
            _dataCollector = new DataCollector(_sapi);
            
            // Initialize chunk change tracker for dynamic updates
            _chunkChangeTracker = new ChunkChangeTracker(_sapi);
            
            // Initialize dynamic tile generator
            _tileGenerator = new DynamicTileGenerator(_sapi, _config);
            
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
            var mapConfigController = new MapConfigController(_sapi, _config);
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
            
            _sapi.Logger.Notification("[VintageAtlas] Live server ready with dynamic tile generation");
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
        
        // Primary location: OutputDirectory/html (where exports and data go)
        var outputHtml = Path.Combine(_config.OutputDirectory, "html");
        
        // If OutputDirectory/html doesn't exist, copy HTML files from mod
        if (!Directory.Exists(outputHtml) || !File.Exists(Path.Combine(outputHtml, "index.html")))
        {
            var modHtml = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location) ?? "", "html");
            if (Directory.Exists(modHtml))
            {
                _sapi.Logger.Notification($"[VintageAtlas] Copying HTML files from mod to output directory...");
                _sapi.Logger.Debug($"[VintageAtlas] Source: {modHtml}");
                _sapi.Logger.Debug($"[VintageAtlas] Target: {outputHtml}");
                
                try
                {
                    CopyDirectory(modHtml, outputHtml);
                    _sapi.Logger.Notification("[VintageAtlas] HTML files copied successfully");
                }
                catch (Exception ex)
                {
                    _sapi.Logger.Error($"[VintageAtlas] Failed to copy HTML files: {ex.Message}");
                }
            }
        }
        
        // Always use OutputDirectory/html for web root
        if (Directory.Exists(outputHtml) && File.Exists(Path.Combine(outputHtml, "index.html")))
        {
            _sapi.Logger.Notification($"[VintageAtlas] Using web root: {outputHtml}");
            return outputHtml;
        }
        
        _sapi.Logger.Error($"[VintageAtlas] Could not setup web root at: {outputHtml}");
        return null;
    }
    
    private void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        
        // Copy all files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var targetFile = Path.Combine(targetDir, fileName);
            File.Copy(file, targetFile, overwrite: true);
        }
        
        // Copy all subdirectories
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);
            
            // Skip the data directory to avoid overwriting exports
            if (dirName == "data") continue;
            
            var targetSubDir = Path.Combine(targetDir, dirName);
            CopyDirectory(subDir, targetSubDir);
        }
    }

    private void OnGameTick(float dt)
    {
        if (_config == null || _sapi == null) return;
        
        // Update historical tracker if enabled
        if (_config.EnableHistoricalTracking && _historicalTracker != null)
        {
            _historicalTracker.OnGameTick(dt);
        }
        
        // Check for modified chunks and regenerate tiles dynamically
        if (_chunkChangeTracker != null && _tileGenerator != null && _chunkChangeTracker.ModifiedChunkCount > 0)
        {
            // Regenerate tiles for modified chunks every 30 seconds
            var currentTime = _sapi.World.ElapsedMilliseconds;
            if ((currentTime - _lastMapExport) >= 30000) // 30 seconds
            {
                _lastMapExport = currentTime;
                
                var modifiedChunks = _chunkChangeTracker.GetAllModifiedChunks().Keys.ToList();
                if (modifiedChunks.Count > 0)
                {
                    _sapi.Logger.Notification($"[VintageAtlas] Regenerating {modifiedChunks.Count} modified chunk tiles");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _tileGenerator.RegenerateTilesForChunksAsync(modifiedChunks);
                            _chunkChangeTracker.ClearAllChanges();
                        }
                        catch (Exception ex)
                        {
                            _sapi.Logger.Error($"[VintageAtlas] Failed to regenerate tiles: {ex.Message}");
                        }
                    });
                }
            }
        }
        
        // Auto-export full map data if enabled and interval has passed (fallback/full regeneration)
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
        _chunkChangeTracker?.Dispose();
        
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
        _chunkChangeTracker?.Dispose();
        base.Dispose();
    }
}


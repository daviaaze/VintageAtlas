using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using VintageAtlas.Application.UseCases;
using VintageAtlas.Commands;
using VintageAtlas.Core;
using VintageAtlas.Core.Configuration;
using VintageAtlas.Export;
using VintageAtlas.Export.Rendering;
using VintageAtlas.Export.Colors;
using VintageAtlas.Export.Extraction;
using VintageAtlas.Export.Generation;
using VintageAtlas.Infrastructure.VintageStory;
using VintageAtlas.Storage;
using VintageAtlas.Web.API;
using VintageAtlas.Web.Server;
using System.Threading.Tasks;

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
        _coreComponents = InitializeCoreComponents(_sapi, _config);

        // Register commands
        ExportCommand.Register(_sapi, _coreComponents.MapExporter);

        // Setup export on start if configured
        if (_config.Export.ExportOnStart)
        {
            _sapi.Event.ServerRunPhase(EnumServerRunPhase.RunGame, () => _coreComponents.MapExporter.StartExport());
        }

        // Setup live server if enabled
        if (_config.WebServer.EnableLiveServer)
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
                _coreComponents.MapConfigController,
                _coreComponents.MetadataStorage
            );

            // Let the ServerManager handle all the setup
            _serverManager.Initialize();

            // Register game tick for auto-export (separate from server manager responsibilities)
            _sapi.Event.RegisterGameTickListener(OnGameTick, 1000);

            // Register high-frequency tick for WebSocket updates (100ms)
            _sapi.Event.RegisterGameTickListener(BroadcastPlayerUpdates, 100);
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
        if (!_config.WebServer.AutoExportMap || !_config.WebServer.EnableLiveServer) return;

        var currentTime = _sapi.World.ElapsedMilliseconds;

        if (currentTime - _lastMapExport < _config.WebServer.MapExportIntervalMs) return;

        _lastMapExport = currentTime;
        _coreComponents.MapExporter.StartExport();
    }

    private void BroadcastPlayerUpdates(float dt)
    {
        if (_serverManager?.WebSocketManager == null || _serverManager.CoordinateService == null || _sapi == null) return;

        try
        {
            var players = _sapi.World.AllOnlinePlayers;
            if (players == null || players.Length == 0) return;

            // Optimization: Pre-allocate list with capacity
            var playerData = new System.Collections.Generic.List<object>(players.Length);

            foreach (var player in players)
            {
                var pos = player.Entity?.Pos;
                if (pos != null)
                {
                    // Convert game coordinates to display coordinates
                    var (x, y) = _serverManager.CoordinateService.GameToDisplay(new Vintagestory.API.MathTools.BlockPos((int)pos.X, (int)pos.Y, (int)pos.Z));
                    
                    playerData.Add(new
                    {
                        name = player.PlayerName,
                        uid = player.PlayerUID,
                        x,
                        y,
                        yaw = pos.Yaw,
                        pitch = pos.Pitch
                    });
                }
            }

            if (playerData.Count == 0) return;

            var update = new
            {
                type = "players",
                data = playerData
            };

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(update, new Newtonsoft.Json.JsonSerializerSettings
            {
                ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver
                {
                    NamingStrategy = new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy()
                },
                Formatting = Newtonsoft.Json.Formatting.None,
                NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
            });

            _ = _serverManager.WebSocketManager.BroadcastAsync(json);
        }
        catch (Exception ex)
        {
            // Log sparingly to avoid spam
            if (_sapi.World.ElapsedMilliseconds % 10000 < 100)
            {
                _sapi.Logger.Warning($"[VintageAtlas] Error broadcasting updates: {ex.Message}");
            }
        }
    }

    public override void Dispose()
    {
        // Dispose server manager (handles web server, historical tracker, and other server components)
        _serverManager?.Dispose();

        // Dispose core components
        _coreComponents?.Storage.Dispose();

        base.Dispose();
    }

    /// <summary>
    /// Export climate layers (Temperature, Rainfall)
    /// </summary>
    private static async Task ExportClimateLayersAsync(
        ICoreServerAPI sapi,
        ModConfig config,
        IProgress<Application.UseCases.ExportProgress>? progress)
    {
        try
        {
            var server = (Vintagestory.Server.ServerMain)sapi.World;
            using var dataSource = new Export.DataSources.SavegameDataSource(server, config, sapi.Logger);

            // Create adapter for progress reporting
            var climateProgress = progress == null ? null : new Progress<VintageAtlas.Export.Data.ExportProgress>(p => 
            {
                progress.Report(new Application.UseCases.ExportProgress 
                { 
                    TilesCompleted = p.TilesCompleted, 
                    TotalTiles = p.TotalTiles, 
                    CurrentPhase = "Exporting Climate Layers" 
                });
            });

            // Export Temperature
            var tempDbPath = Path.Combine(config.Export.OutputDirectory, "data", "temperature.mbtiles");
            using var tempStorage = new MbTilesStorage(tempDbPath);
            var tempGen = new ClimateTileGenerator(sapi, config, tempStorage, ClimateType.Temperature);
            await tempGen.ExportClimateMapAsync(dataSource, climateProgress);

            // Export Rainfall
            var rainDbPath = Path.Combine(config.Export.OutputDirectory, "data", "rainfall.mbtiles");
            using var rainStorage = new MbTilesStorage(rainDbPath);
            var rainGen = new ClimateTileGenerator(sapi, config, rainStorage, ClimateType.Rainfall);
            await rainGen.ExportClimateMapAsync(dataSource, climateProgress);
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Failed to export climate layers: {ex.Message}");
        }
    }

    /// <summary>
    /// Process chunks with multiple extractors
    /// </summary>
    private static async Task ProcessWithExtractorsAsync(
        Application.DTOs.ExportOptions options,
        IProgress<Application.UseCases.ExportProgress>? progress,
        TileExtractor tileExtractor,
        TraderExtractor traderExtractor,
        ICoreServerAPI sapi,
        ModConfig config)
    {
        // Get chunk positions from savegame
        var server = (Vintagestory.Server.ServerMain)sapi.World;
        var dataSource = new Export.DataSources.SavegameDataSource(server, config, sapi.Logger);

        var chunkPositions = dataSource.GetAllMapChunkPositions();
        sapi.Logger.Notification($"[VintageAtlas] Processing {chunkPositions.Count} chunks");

        // Calculate tiles
        var chunksPerTile = config.Export.TileSize / Constants.ChunkSize;
        var tiles = CalculateTileCoverage(chunkPositions, chunksPerTile);

        var tilesProcessed = 0;
        var chunksProcessed = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = config.Export.MaxDegreeOfParallelism == -1
                ? Environment.ProcessorCount
                : config.Export.MaxDegreeOfParallelism
        };

        await Parallel.ForEachAsync(tiles, parallelOptions, async (tile, token) =>
        {
            try
            {
                // Load tile chunks
                var tileData = await dataSource.GetTileChunksAsync(config.Export.BaseZoomLevel, tile.X, tile.Y);

                if (tileData?.Chunks != null)
                {
                    // Process each chunk sequentially with both extractors
                    // (Extractors are thread-safe for concurrent calls from different tiles)
                    foreach (var chunkSnapshot in tileData.Chunks.Values)
                    {
                        await tileExtractor.ProcessChunkAsync(chunkSnapshot);
                        await traderExtractor.ProcessChunkAsync(chunkSnapshot);
                        System.Threading.Interlocked.Increment(ref chunksProcessed);
                    }
                    
                    // Optimization: Render and clear tile immediately to free memory
                    await tileExtractor.RenderTileAsync(tile.X, tile.Y);
                }

                var completed = System.Threading.Interlocked.Increment(ref tilesProcessed);
                if (completed % 50 == 0) // Report more frequently
                {
                    progress?.Report(new ExportProgress
                    {
                        TilesCompleted = completed,
                        TotalTiles = tiles.Count,
                        ChunksProcessed = chunksProcessed,
                        CurrentPhase = "Exporting"
                    });
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[VintageAtlas] Failed to process tile {tile.X},{tile.Y}: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Calculate which tiles cover the given chunk positions
    /// </summary>
    private static System.Collections.Generic.List<Vec2i> CalculateTileCoverage(
        System.Collections.Generic.List<Vec2i> chunkPositions,
        int chunksPerTile)
    {
        var tiles = new System.Collections.Generic.HashSet<Vec2i>();

        foreach (var chunkPos in chunkPositions)
        {
            var tileX = chunkPos.X / chunksPerTile;
            var tileY = chunkPos.Y / chunksPerTile;
            tiles.Add(new Vec2i(tileX, tileY));
        }

        return new System.Collections.Generic.List<Vec2i>(tiles);
    }

    /// <summary>
    /// Initialize core components needed for both export and live server functionality
    /// </summary>
    private static CoreComponents InitializeCoreComponents(ICoreServerAPI sapi, ModConfig config)
    {
        sapi.Logger.Notification("[VintageAtlas] Initializing core components...");

        // Initialize tile storage (shared by all tile generators)
        var dbPath = Path.Combine(config.Export.OutputDirectory, "data", "tiles.mbtiles");
        var storage = new MbTilesStorage(dbPath);
        var metadataStorage = new MetadataStorage(Path.Combine(config.Export.OutputDirectory, "data", "metadata.db"));

        // Initialize the block color cache (needed for tile rendering)
        var colorCache = new BlockColorCache(sapi, config);
        colorCache.Initialize();

        // Initialize unified tile generator for tile rendering operations
        var unifiedGenerator = new UnifiedTileGenerator(sapi, config, colorCache, storage);

        // Initialize map config controller (needed by exporter for cache invalidation)
        var mapConfigController = new MapConfigController(sapi);

        // ═══════════════════════════════════════════════════════════════
        // EXTRACTION SETUP
        // ═══════════════════════════════════════════════════════════════

        // Create extractors directly
        var tileExtractor = new TileExtractor(unifiedGenerator, config, storage, sapi, mapConfigController);
        var traderExtractor = new TraderExtractor(sapi, metadataStorage);

        sapi.Logger.Notification("[VintageAtlas] Created 2 extractors");

        // ═══════════════════════════════════════════════════════════════
        // APPLICATION LAYER SETUP (Clean Architecture)
        // ═══════════════════════════════════════════════════════════════

        // Initialize server state manager (infrastructure service)
        ServerStateManager serverStateManager = new ServerStateManager(sapi);

        // Create simplified export action that processes directly
        async Task<Application.DTOs.ExportResult> ExportAction(
            Application.DTOs.ExportOptions options,
            IProgress<Application.UseCases.ExportProgress>? progress)
        {
            try
            {
                // Initialize extractors
                await tileExtractor.InitializeAsync();
                await traderExtractor.InitializeAsync();

                // Process with both extractors
                await ProcessWithExtractorsAsync(options, progress, tileExtractor, traderExtractor, sapi, config);

                // Export climate layers
                await ExportClimateLayersAsync(sapi, config, progress);

                // Finalize extractors
                await tileExtractor.FinalizeAsync(progress);
                await traderExtractor.FinalizeAsync(progress);

                return Application.DTOs.ExportResult.Successful(
                    TimeSpan.Zero, 0, 0); // Stats will be tracked elsewhere
            }
            catch (Exception ex)
            {
                return Application.DTOs.ExportResult.Failed($"Export failed: {ex.Message}", ex);
            }
        }

        // Initialize export use case (application layer business logic)
        IExportMapUseCase exportUseCase = new ExportMapUseCase(sapi, serverStateManager, ExportAction);

        // Initialize map exporter (infrastructure adapter)
        var mapExporter = new MapExporter(sapi, config, exportUseCase);

        sapi.Logger.Notification("[VintageAtlas] Core components initialized successfully");

        return new CoreComponents(
            storage,
            metadataStorage,
            colorCache,
            mapConfigController,
            mapExporter
        );
    }
}


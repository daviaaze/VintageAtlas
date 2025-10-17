using System.IO;
using Vintagestory.API.Server;
using VintageAtlas.Export;
using VintageAtlas.Storage;
using VintageAtlas.Web.API;

namespace VintageAtlas.Core;

/// <summary>
/// Handles initialization of core VintageAtlas components
/// Extracted from VintageAtlasModSystem to improve separation of concerns
/// </summary>
public static class ComponentInitializer
{
    /// <summary>
    /// Initialize core components needed for both export and live server functionality
    /// </summary>
    public static CoreComponents Initialize(ICoreServerAPI sapi, ModConfig config)
    {
        sapi.Logger.Notification("[VintageAtlas] Initializing core components...");

        // Initialize tile storage (shared by all tile generators)
        var dbPath = Path.Combine(config.OutputDirectory, "data", "tiles.mbtiles");
        var storage = new MbTilesStorage(dbPath);

        // Initialize the block color cache (needed for tile rendering)
        var colorCache = new BlockColorCache(sapi, config);
        colorCache.Initialize();

        // Initialize unified tile generator for full exports
        var unifiedGenerator = new UnifiedTileGenerator(sapi, config, colorCache, storage);

        // Initialize map config controller (needed by exporter for cache invalidation)
        var mapConfigController = new MapConfigController(sapi);

        // Initialize map exporter with unified generator and map config controller
        var mapExporter = new MapExporter(sapi, config, unifiedGenerator, mapConfigController);

        sapi.Logger.Notification("[VintageAtlas] Core components initialized successfully");

        return new CoreComponents(
            storage,
            colorCache,
            mapConfigController,
            mapExporter
        );
    }
}
using System.IO;
using Vintagestory.API.Server;
using VintageAtlas.Export;
using VintageAtlas.Export.Colors;
using VintageAtlas.Export.Extraction;
using VintageAtlas.Export.Generation;
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
        var metadataStorage = new MetadataStorage(Path.Combine(config.OutputDirectory, "data", "metadata.db"));

        // Initialize the block color cache (needed for tile rendering)
        var colorCache = new BlockColorCache(sapi, config);
        colorCache.Initialize();

        // Initialize unified tile generator for tile rendering operations
        var unifiedGenerator = new UnifiedTileGenerator(sapi, config, colorCache, storage);

        // Initialize map config controller (needed by exporter for cache invalidation)
        var mapConfigController = new MapConfigController(sapi);

        // ═══════════════════════════════════════════════════════════════
        // EXTRACTION ORCHESTRATION SETUP
        // ═══════════════════════════════════════════════════════════════

        // Create orchestrator for managing the extraction pipeline
        var orchestrator = new ExportOrchestrator(sapi, config);

        // Register all extractors in the desired execution order
        orchestrator.RegisterExtractor(new TileExtractor(unifiedGenerator, config, storage, sapi, mapConfigController));
        orchestrator.RegisterExtractor(new TraderExtractor(sapi, config, metadataStorage));
        orchestrator.RegisterExtractor(new ClimateExtractor(sapi, config, metadataStorage, samplesPerChunk: 2));

        sapi.Logger.Notification($"[VintageAtlas] Registered {orchestrator.GetExtractors().Count} extractors");

        // Initialize map exporter with orchestrator
        var mapExporter = new MapExporter(sapi, config, orchestrator);

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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Server;
using VintageAtlas.Core;
using VintageAtlas.Export.Data;
using VintageAtlas.Export.DataSources;

namespace VintageAtlas.Export.Extraction;

/// <summary>
/// Orchestrates the extraction pipeline by iterating through chunks ONCE
/// and allowing multiple extractors to process each chunk.
/// This avoids redundant iteration and multiple database loads.
/// </summary>
public class ExportOrchestrator : IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly ServerMain _server;
    private readonly ModConfig _config;
    private readonly List<IDataExtractor> _extractors = new();
    
    private const int ChunkSize = 32;

    public ExportOrchestrator(ICoreServerAPI sapi, ModConfig config)
    {
        _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _server = (ServerMain)sapi.World;
    }

    /// <summary>
    /// Register an extractor to be included in the pipeline.
    /// </summary>
    public void RegisterExtractor(IDataExtractor extractor)
    {
        if (!_extractors.Contains(extractor))
        {
            _extractors.Add(extractor);
            _sapi.Logger.Debug($"[VintageAtlas] Registered extractor: {extractor.Name}");
        }
    }

    /// <summary>
    /// Execute full map export from savegame database.
    /// Iterates through chunks ONCE and calls all extractors for each chunk.
    /// </summary>
    public async Task ExecuteFullExportAsync(IProgress<ExportProgress>? progress = null)
    {
        _sapi.Logger.Notification("[VintageAtlas] Starting full export from savegame database...");

        // Create savegame data source for reading all chunks from database
        using var savegameDataSource = new SavegameDataSource(_server, _config, _sapi.Logger);

        // Get all chunk positions to process
        var chunkPositions = savegameDataSource.GetAllMapChunkPositions();
        _sapi.Logger.Notification($"[VintageAtlas] Found {chunkPositions.Count} chunks to process");

        if (chunkPositions.Count == 0)
        {
            _sapi.Logger.Warning("[VintageAtlas] No chunks found!");
            return;
        }

        // Initialize all extractors
        _sapi.Logger.Notification("[VintageAtlas] Initializing extractors...");
        foreach (var extractor in _extractors)
        {
            try
            {
                await extractor.InitializeAsync();
                _sapi.Logger.Debug($"[VintageAtlas] Initialized: {extractor.Name}");
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to initialize '{extractor.Name}': {ex.Message}");
                _sapi.Logger.Error(ex.StackTrace ?? "");
            }
        }

        // Calculate tiles to process (for progress reporting and batching)
        var chunksPerTile = _config.TileSize / ChunkSize;
        var tiles = CalculateTileCoverage(chunkPositions, chunksPerTile);
        _sapi.Logger.Notification($"[VintageAtlas] Processing {tiles.Count} tiles with {_extractors.Count} extractors");

        var tilesProcessed = 0;
        var totalChunks = 0;

        // Process each tile (which contains multiple chunks)
        foreach (var tile in tiles)
        {
            try
            {
                // Load all chunks for this tile
                var tileData = await savegameDataSource.GetTileChunksAsync(_config.BaseZoomLevel, tile.X, tile.Y);

                if (tileData?.Chunks != null)
                {
                    // Process each chunk in this tile with all extractors
                    foreach (var chunkSnapshot in tileData.Chunks.Values)
                    {
                        // Call all extractors for this chunk
                        foreach (var extractor in _extractors)
                        {
                            try
                            {
                                await extractor.ProcessChunkAsync(chunkSnapshot);
                            }
                            catch (Exception ex)
                            {
                                _sapi.Logger.Error(
                                    $"[VintageAtlas] Extractor '{extractor.Name}' failed on chunk " +
                                    $"({chunkSnapshot.ChunkX},{chunkSnapshot.ChunkZ}): {ex.Message}");
                            }
                        }
                        
                        totalChunks++;
                    }
                }

                tilesProcessed++;
                if (tilesProcessed % 100 == 0)
                {
                    _sapi.Logger.Notification(
                        $"[VintageAtlas] Processed {tilesProcessed}/{tiles.Count} tiles, {totalChunks} chunks");

                    progress?.Report(new ExportProgress
                    {
                        TilesCompleted = tilesProcessed,
                        TotalTiles = tiles.Count,
                        CurrentZoomLevel = _config.BaseZoomLevel
                    });
                }
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to process tile {tile.X},{tile.Y}: {ex.Message}");
            }
        }

        _sapi.Logger.Notification($"[VintageAtlas] Chunk iteration complete: processed {totalChunks} chunks");

        // Finalize all extractors
        _sapi.Logger.Notification("[VintageAtlas] Finalizing extractors...");
        foreach (var extractor in _extractors)
        {
            try
            {
                _sapi.Logger.Notification($"[VintageAtlas] Finalizing: {extractor.Name}");
                await extractor.FinalizeAsync(progress);
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to finalize '{extractor.Name}': {ex.Message}");
                _sapi.Logger.Error(ex.StackTrace ?? "");
            }
        }

        _sapi.Logger.Notification("[VintageAtlas] Full export completed");
    }

    /// <summary>
    /// Execute live extraction from loaded chunks only.
    /// This context only accesses chunks that are currently loaded in memory.
    /// </summary>
    public async Task ExecuteLiveExtractionAsync(IProgress<ExportProgress>? progress = null)
    {
        _sapi.Logger.Notification("[VintageAtlas] Starting live extraction from loaded chunks...");

        var loadedChunksDataSource = new LoadedChunksDataSource(_sapi, _config);

        // Initialize extractors
        foreach (var extractor in _extractors)
        {
            try
            {
                await extractor.InitializeAsync();
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to initialize '{extractor.Name}': {ex.Message}");
            }
        }

        // For live extraction, we'd need to get loaded chunks differently
        // This is a placeholder - actual implementation would enumerate loaded chunks from the game
        _sapi.Logger.Warning("[VintageAtlas] Live extraction not fully implemented yet");

        // Finalize extractors
        foreach (var extractor in _extractors)
        {
            try
            {
                await extractor.FinalizeAsync(progress);
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to finalize '{extractor.Name}': {ex.Message}");
            }
        }

        _sapi.Logger.Notification("[VintageAtlas] Live extraction completed");
    }

    /// <summary>
    /// Calculate which tiles cover the given chunk positions.
    /// </summary>
    private static List<Vec2i> CalculateTileCoverage(List<Vec2i> chunkPositions, int chunksPerTile)
    {
        var tiles = new HashSet<Vec2i>();

        foreach (var chunkPos in chunkPositions)
        {
            var tileX = chunkPos.X / chunksPerTile;
            var tileY = chunkPos.Y / chunksPerTile;
            tiles.Add(new Vec2i(tileX, tileY));
        }

        return tiles.ToList();
    }

    /// <summary>
    /// Get all registered extractors.
    /// </summary>
    public IReadOnlyList<IDataExtractor> GetExtractors() => _extractors.AsReadOnly();

    public void Dispose()
    {
        foreach (var extractor in _extractors.OfType<IDisposable>())
        {
            try
            {
                extractor.Dispose();
            }
            catch (Exception ex)
            {
                _sapi.Logger.Warning($"[VintageAtlas] Error disposing extractor: {ex.Message}");
            }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VintageAtlas.Core.Configuration;
using VintageAtlas.Export.Data;
using VintageAtlas.Export.Generation;
using VintageAtlas.Storage;
using VintageAtlas.Web.API;
using Vintagestory.API.Server;

namespace VintageAtlas.Export.Extraction;

/// <summary>
/// Extractor for generating map tiles.
/// Accumulates chunks organized by tile, then renders all tiles during finalization.
/// </summary>
public class TileExtractor(
    UnifiedTileGenerator tileGenerator,
    ModConfig config,
    MbTilesStorage storage,
    ICoreServerAPI sapi,
    MapConfigController? mapConfigController = null) : IDataExtractor
{
    private readonly UnifiedTileGenerator _tileGenerator = tileGenerator ?? throw new ArgumentNullException(nameof(tileGenerator));
    private readonly ModConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly MbTilesStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    private readonly MapConfigController? _mapConfigController = mapConfigController;
    private readonly ICoreServerAPI _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
    
    // Organize chunks by tile coordinates (tileX, tileZ) -> TileChunkData
    // Thread-safe dictionary for parallel processing
    private readonly ConcurrentDictionary<(int, int), TileChunkData> _tileChunks = new();
    
    private IncrementalZoomTracker? _zoomTracker;
    private int _tilesCompleted;

    private const int ChunkSize = 32;

    public string Name => "Map Tiles";
    public bool RequiresLoadedChunks => false; // Can work with savegame DB

    public Task InitializeAsync()
    {
        _tileChunks.Clear();
        _tilesCompleted = 0;

        // Initialize zoom tracker if enabled
        if (_config.Export.CreateZoomLevels)
        {
            // Calculate parallelism settings
            var effectiveParallelism = _config.Export.MaxDegreeOfParallelism == -1
                ? Environment.ProcessorCount
                : _config.Export.MaxDegreeOfParallelism;

            // Calculate optimal concurrency for zoom generation
            var maxConcurrentZoomTiles = effectiveParallelism switch
            {
                <= 4 => 2,                                    // Low-end systems: minimal zoom concurrency
                <= 8 => effectiveParallelism / 2,             // Mid-range: 50% for zoom
                _ => Math.Max(4, effectiveParallelism / 2)    // High-end: at least 4, up to 50%
            };

            _zoomTracker = new IncrementalZoomTracker(
                _sapi,
                _tileGenerator,
                _config.Export.BaseZoomLevel,
                minZoom: 0,
                maxConcurrentZoomTiles: maxConcurrentZoomTiles
            );

            _tileGenerator.Logger.Notification(
                $"[VintageAtlas] ⚡ Incremental zoom generation enabled with {maxConcurrentZoomTiles} concurrent zoom tiles");
        }

        return Task.CompletedTask;
    }

    public Task ProcessChunkAsync(ChunkSnapshot chunk)
    {
        // Calculate which tile this chunk belongs to
        var chunksPerTile = _config.Export.TileSize / ChunkSize;
        var tileX = chunk.ChunkX / chunksPerTile;
        var tileZ = chunk.ChunkZ / chunksPerTile;
        var tileKey = (tileX, tileZ);

        // Get or create TileChunkData for this tile (thread-safe)
        var tileData = _tileChunks.GetOrAdd(tileKey, _ => new TileChunkData
        {
            Zoom = _config.Export.BaseZoomLevel,
            TileX = tileX,
            TileZ = tileZ,
            TileSize = _config.Export.TileSize,
            ChunksPerTileEdge = chunksPerTile
        });

        // Add this chunk to the tile
        tileData.AddChunk(chunk);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Render a specific tile immediately and remove it from memory.
    /// Called by the orchestrator when it knows a tile is complete.
    /// </summary>
    public async Task RenderTileAsync(int tileX, int tileZ)
    {
        var key = (tileX, tileZ);
        if (_tileChunks.TryRemove(key, out var tileData))
        {
            await RenderAndSaveTileAsync(tileData);
        }
    }

    public async Task FinalizeAsync(IProgress<Application.UseCases.ExportProgress>? progress = null)
    {
        // Process any remaining tiles (that weren't explicitly rendered)
        if (!_tileChunks.IsEmpty)
        {
            _tileGenerator.Logger.Notification($"[VintageAtlas] Rendering remaining {_tileChunks.Count} tiles...");

            var tiles = _tileChunks.Values.ToList();

            // Sort tiles by parent zoom tile to maximize early zoom generation
            if (_zoomTracker != null)
            {
                tiles = SortTilesByZoomParent(tiles);
            }

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _config.Export.MaxDegreeOfParallelism == -1
                    ? Environment.ProcessorCount
                    : _config.Export.MaxDegreeOfParallelism
            };

            await Parallel.ForEachAsync(tiles, parallelOptions, async (tileData, _) =>
            {
                await RenderAndSaveTileAsync(tileData, progress);
            });
        }

        _tileGenerator.Logger.Notification($"[VintageAtlas] ✅ Base tile rendering complete: {_tilesCompleted} tiles generated");

        // Wait for incremental zoom generation to complete
        if (_zoomTracker != null)
        {
            _tileGenerator.Logger.Notification("[VintageAtlas] 🔄 Waiting for remaining zoom tile cascade to complete...");
            await _zoomTracker.WaitForCompletionAsync();

            var (total, perZoom) = _zoomTracker.GetStatistics();
            if (total > 0)
            {
                var totalTiles = _tilesCompleted + total;
                var zoomPercentage = total * 100.0 / totalTiles;
                _tileGenerator.Logger.Notification(
                    $"[VintageAtlas] 🎯 Total tiles generated: {totalTiles} " +
                    $"({_tilesCompleted} base + {total} zoom = {zoomPercentage:F1}% were zoom tiles)");
            }
        }

        // Checkpoint WAL to commit all tiles
        _tileGenerator.Logger.Notification("[VintageAtlas] Committing tiles to database...");
        _storage.CheckpointWal();

        // Invalidate map config cache
        _mapConfigController?.InvalidateCache();

        _tileGenerator.Logger.Notification("[VintageAtlas] Tile extraction complete");
    }

    private async Task RenderAndSaveTileAsync(TileChunkData tileData, IProgress<Application.UseCases.ExportProgress>? progress = null)
    {
        try
        {
            // Render the tile
            var tileImage = await _tileGenerator.RenderTileFromChunkDataAsync(tileData);

            if (tileImage != null)
            {
                // Write tile to storage
                await _storage.PutTileAsync(_config.Export.BaseZoomLevel, tileData.TileX, tileData.TileZ, tileImage);

                // Notify zoom tracker about completion (triggers cascade)
                _zoomTracker?.NotifyTileComplete(_config.Export.BaseZoomLevel, tileData.TileX, tileData.TileZ);

                var completed = System.Threading.Interlocked.Increment(ref _tilesCompleted);

                if (completed % 100 == 0)
                {
                    _tileGenerator.Logger.Notification(
                        $"[VintageAtlas] Rendered {completed} tiles");

                    progress?.Report(new Application.UseCases.ExportProgress
                    {
                        TilesCompleted = completed,
                        // Note: TotalTiles might be unknown if we are streaming, or we can pass it if known
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _tileGenerator.Logger.Error(
                $"[VintageAtlas] Failed to render tile {tileData.TileX},{tileData.TileZ}: {ex.Message}");
        }
    }

    /// <summary>
    /// Sort tiles by their parent zoom tile coordinates to optimize zoom generation.
    /// Groups tiles into 2x2 blocks that share the same parent zoom tile.
    /// This ensures zoom tiles can be generated as early as possible.
    /// </summary>
    private List<TileChunkData> SortTilesByZoomParent(List<TileChunkData> tiles)
    {
        return tiles
            .OrderBy(t => t.TileX / 2)  // Parent X coordinate
            .ThenBy(t => t.TileZ / 2)   // Parent Z coordinate
            .ThenBy(t => t.TileX % 2)   // Position within parent (0=left, 1=right)
            .ThenBy(t => t.TileZ % 2)   // Position within parent (0=top, 1=bottom)
            .ToList();
    }
}

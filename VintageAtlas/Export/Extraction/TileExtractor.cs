using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VintageAtlas.Core;
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
public class TileExtractor : IDataExtractor
{
    private readonly UnifiedTileGenerator _tileGenerator;
    private readonly ModConfig _config;
    private readonly MbTilesStorage _storage;
    private readonly MapConfigController? _mapConfigController;
    private readonly ICoreServerAPI _sapi;
    // Organize chunks by tile coordinates (tileX, tileZ) -> TileChunkData
    private readonly Dictionary<(int, int), TileChunkData> _tileChunks = new();

    private const int ChunkSize = 32;

    public string Name => "Map Tiles";
    public bool RequiresLoadedChunks => false; // Can work with savegame DB

    public TileExtractor(
        UnifiedTileGenerator tileGenerator,
        ModConfig config,
        MbTilesStorage storage,
        ICoreServerAPI sapi,
        MapConfigController? mapConfigController = null)
    {
        _tileGenerator = tileGenerator ?? throw new ArgumentNullException(nameof(tileGenerator));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _mapConfigController = mapConfigController;
        _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
    }

    public Task InitializeAsync()
    {
        _tileChunks.Clear();
        return Task.CompletedTask;
    }

    public Task ProcessChunkAsync(ChunkSnapshot chunk)
    {
        // Calculate which tile this chunk belongs to
        var chunksPerTile = _config.TileSize / ChunkSize;
        var tileX = chunk.ChunkX / chunksPerTile;
        var tileZ = chunk.ChunkZ / chunksPerTile;
        var tileKey = (tileX, tileZ);

        // Get or create TileChunkData for this tile
        if (!_tileChunks.TryGetValue(tileKey, out var tileData))
        {
            tileData = new TileChunkData
            {
                Zoom = _config.BaseZoomLevel,
                TileX = tileX,
                TileZ = tileZ,
                TileSize = _config.TileSize,
                ChunksPerTileEdge = chunksPerTile
            };
            _tileChunks[tileKey] = tileData;
        }

        // Add this chunk to the tile
        tileData.AddChunk(chunk);

        return Task.CompletedTask;
    }

    public async Task FinalizeAsync(IProgress<ExportProgress>? progress = null)
    {
        if (_tileChunks.Count == 0)
        {
            return;
        }

        _tileGenerator.Logger.Notification($"[VintageAtlas] Rendering {_tileChunks.Count} tiles...");

        var tiles = _tileChunks.Values.ToList();
        var tilesCompleted = 0;

        // Create incremental zoom tracker (if enabled)
        IncrementalZoomTracker? zoomTracker = null;
        if (_config.CreateZoomLevels)
        {
            zoomTracker = new IncrementalZoomTracker(
                _sapi,
                _tileGenerator,
                _config.BaseZoomLevel,
                minZoom: 0,
                maxConcurrentZoomTiles: Math.Max(2, _config.MaxDegreeOfParallelism / 4)
            );
            _tileGenerator.Logger.Notification("[VintageAtlas] ⚡ Incremental zoom generation enabled - tiles will render in zoom-optimized order");

            // Sort tiles by parent zoom tile to maximize early zoom generation
            tiles = SortTilesByZoomParent(tiles);
            _tileGenerator.Logger.Notification("[VintageAtlas] ✨ Tiles sorted by parent zoom coordinates for optimal generation order");
        }

        // Render all accumulated tiles
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _config.MaxDegreeOfParallelism == -1
                ? Environment.ProcessorCount
                : _config.MaxDegreeOfParallelism
        };

        await Parallel.ForEachAsync(tiles, parallelOptions, async (tileData, _) =>
        {
            try
            {
                // Render the tile
                var tileImage = await _tileGenerator.RenderTileFromChunkDataAsync(tileData);

                if (tileImage != null)
                {
                    // Write tile to storage
                    await _storage.PutTileAsync(_config.BaseZoomLevel, tileData.TileX, tileData.TileZ, tileImage);

                    // Notify zoom tracker about completion (triggers cascade)
                    zoomTracker?.NotifyTileComplete(_config.BaseZoomLevel, tileData.TileX, tileData.TileZ);

                    var completed = System.Threading.Interlocked.Increment(ref tilesCompleted);

                    if (completed % 100 == 0)
                    {
                        _tileGenerator.Logger.Notification(
                            $"[VintageAtlas] Rendered {completed}/{tiles.Count} tiles ({completed * 100.0 / tiles.Count:F1}%)");

                        progress?.Report(new ExportProgress
                        {
                            TilesCompleted = completed,
                            TotalTiles = tiles.Count,
                            CurrentZoomLevel = _config.BaseZoomLevel
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _tileGenerator.Logger.Error(
                    $"[VintageAtlas] Failed to render tile {tileData.TileX},{tileData.TileZ}: {ex.Message}");
            }
        });

        _tileGenerator.Logger.Notification($"[VintageAtlas] Tile rendering complete: {tilesCompleted} tiles generated");

        // Wait for incremental zoom generation to complete
        if (zoomTracker != null)
        {
            await zoomTracker.WaitForCompletionAsync();

            var (total, perZoom) = zoomTracker.GetStatistics();
            if (total > 0)
            {
                _tileGenerator.Logger.Notification($"[VintageAtlas] ⚡ Incremental zoom optimization: {total} zoom tiles generated concurrently with base tiles");
            }
        }

        // Checkpoint WAL to commit all tiles
        _tileGenerator.Logger.Notification("[VintageAtlas] Committing tiles to database...");
        _storage.CheckpointWal();

        // Invalidate map config cache
        _mapConfigController?.InvalidateCache();

        _tileGenerator.Logger.Notification("[VintageAtlas] Tile extraction complete");
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

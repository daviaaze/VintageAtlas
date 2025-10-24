using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VintageAtlas.Core;
using VintageAtlas.Export.Data;
using VintageAtlas.Export.Generation;
using VintageAtlas.Storage;
using VintageAtlas.Web.API;

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
    
    // Organize chunks by tile coordinates (tileX, tileZ) -> TileChunkData
    private readonly Dictionary<(int, int), TileChunkData> _tileChunks = new();
    
    private const int ChunkSize = 32;

    public string Name => "Map Tiles";
    public bool RequiresLoadedChunks => false; // Can work with savegame DB

    public TileExtractor(
        UnifiedTileGenerator tileGenerator,
        ModConfig config,
        MbTilesStorage storage,
        MapConfigController? mapConfigController = null)
    {
        _tileGenerator = tileGenerator ?? throw new ArgumentNullException(nameof(tileGenerator));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _mapConfigController = mapConfigController;
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

        // Generate zoom levels by downsampling
        if (_config.CreateZoomLevels)
        {
            _tileGenerator.Logger.Notification("[VintageAtlas] Generating zoom levels...");
            await _tileGenerator.GenerateZoomLevelsAsync(progress);
        }

        // Checkpoint WAL to commit all tiles
        _tileGenerator.Logger.Notification("[VintageAtlas] Committing tiles to database...");
        _storage.CheckpointWal();

        // Invalidate map config cache
        _mapConfigController?.InvalidateCache();
        
        _tileGenerator.Logger.Notification("[VintageAtlas] Tile extraction complete");
    }
}

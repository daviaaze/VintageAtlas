using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Core.Configuration;
using VintageAtlas.Export.Colors;
using VintageAtlas.Export.Rendering;
using VintageAtlas.Storage;
using Vintagestory.API.MathTools;
using VintageAtlas.Export.Data;
using VintageAtlas.Export.DataSources;
using VintageAtlas.Models.Domain;
using Vintagestory.API.Common;

namespace VintageAtlas.Export.Generation;

/// <summary>
/// Unified tile generation system that replaces both Extractor and DynamicTileGenerator.
/// Uses a single rendering implementation with pluggable data sources (IChunkDataSource)
/// for both full exports and on-demand generation.
/// </summary>
public sealed partial class UnifiedTileGenerator : ITileGenerator
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly ITileStorage _storage;
    private readonly PyramidTileDownsampler _downsampler;
    private readonly FastBitmapRenderer _renderer;

    // In-memory cache for frequently accessed tiles
    private readonly ConcurrentDictionary<string, CachedTile> _memoryCache = new();

    private const int ChunkSize = 32;
    private const int MaxCacheSize = 100;

    /// <summary>
    /// Expose properties for extractors and incremental zoom tracker.
    /// </summary>
    public ILogger Logger => _sapi.Logger;
    public ICoreServerAPI Sapi => _sapi;
    public ModConfig Config => _config;
    public ITileStorage Storage => _storage;

    public UnifiedTileGenerator(
        ICoreServerAPI sapi,
        ModConfig config,
        IBlockColorCache colorCache,
        ITileStorage storage)
    {
        _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));

        // Initialize downsampler for lower zoom levels
        _downsampler = new PyramidTileDownsampler(sapi, config, this);

        // Initialize the microblocks collection (chiseled blocks)
        var microBlocks = sapi.World.Blocks
            .Where(b => b.Code?.Path.StartsWith("chiseledblock") is true ||
                        b.Code?.Path.StartsWith("microblock") is true)
            .Select(x => x.Id)
            .ToHashSet();

        // Initialize optimized renderer
        _renderer = new FastBitmapRenderer(sapi, config, colorCache, microBlocks);

        _sapi.Logger.Notification("[VintageAtlas] UnifiedTileGenerator initialized with FastBitmapRenderer");
    }

    /// <summary>
    /// Export the full map from the savegame database.
    /// This is the replacement for Extractor.ExtractWorldMap().
    /// </summary>
    public async Task ExportFullMapAsync(
        SavegameDataSource dataSource,
        IProgress<ExportProgress>? progress = null)
    {
        _sapi.Logger.Notification("[VintageAtlas] Starting full map export (unified generator)...");

        var startTime = DateTime.UtcNow;
        var totalTiles = 0;

        try
        {
            // Query actual chunk positions from the data source
            List<Vec2i> tiles;

            tiles = CalculateTileCoverageFromChunks(dataSource.GetAllMapChunkPositions());

            _sapi.Logger.Notification($"[VintageAtlas] Exporting {tiles.Count} tiles at zoom {_config.Export.BaseZoomLevel}");

            // Generate base zoom tiles in parallel
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _config.Export.MaxDegreeOfParallelism == -1
                    ? Environment.ProcessorCount
                    : _config.Export.MaxDegreeOfParallelism
            };

            await Parallel.ForEachAsync(tiles, parallelOptions, async (tile, _) =>
            {
                try
                {
                    var tileData = await RenderTileAsync(
                        _config.Export.BaseZoomLevel,
                        tile.X,
                        tile.Y,
                        dataSource
                    );

                    if (tileData != null)
                    {
                        // Write tile using absolute world coordinates (matching legacy extractor)
                        await _storage.PutTileAsync(_config.Export.BaseZoomLevel, tile.X, tile.Y, tileData);

                        var completed = System.Threading.Interlocked.Increment(ref totalTiles);

                        if (completed % 100 == 0)
                        {
                            _sapi.Logger.Notification(
                                $"[VintageAtlas] Exported {completed}/{tiles.Count} tiles ({completed * 100.0 / tiles.Count:F1}%)");

                            progress?.Report(new ExportProgress
                            {
                                TilesCompleted = completed,
                                TotalTiles = tiles.Count,
                                CurrentZoomLevel = _config.Export.BaseZoomLevel
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _sapi.Logger.Error($"[VintageAtlas] Failed to export tile {tile.X}_{tile.Y}: {ex.Message}");
                }
            });

            var duration = DateTime.UtcNow - startTime;
            _sapi.Logger.Notification(
                $"[VintageAtlas] Base zoom export complete: {totalTiles} tiles in {duration.TotalSeconds:F1}s " +
                $"({totalTiles / duration.TotalSeconds:F1} tiles/sec)");

            // Generate zoom levels by downsampling from database
            if (_config.Export.CreateZoomLevels)
            {
                _sapi.Logger.Notification("[VintageAtlas] Generating zoom levels...");
                await GenerateZoomLevelsAsync(progress);
            }

            // CRITICAL: Checkpoint WAL to commit all tiles to the main database
            _sapi.Logger.Notification("[VintageAtlas] Committing tiles to database...");
            _storage.CheckpointWal();

            _sapi.Logger.Notification("[VintageAtlas] Full map export completed successfully!");
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Full map export failed: {ex.Message}");
            _sapi.Logger.Error(ex.StackTrace ?? "");
            throw;
        }
    }

    /// <summary>
    /// Calculate which tiles need to be generated based on actual chunks in the savegame.
    /// This is the accurate method that avoids generating empty tiles.
    /// </summary>
    private List<Vec2i> CalculateTileCoverageFromChunks(List<Vec2i> chunkPositions)
    {
        if (chunkPositions.Count == 0)
        {
            _sapi.Logger.Warning("[VintageAtlas] No chunks found in savegame database!");
            return [];
        }

        var tiles = new HashSet<Vec2i>();
        var chunksPerTile = _config.Export.TileSize / ChunkSize;

        _sapi.Logger.Notification($"[VintageAtlas] Calculating tile coverage from {chunkPositions.Count} chunks...");

        // Convert each chunk position to a tile position
        foreach (var chunkPos in chunkPositions)
        {
            // Calculate which tile this chunk belongs to
            var tileX = chunkPos.X / chunksPerTile;
            var tileY = chunkPos.Y / chunksPerTile;

            tiles.Add(new Vec2i(tileX, tileY));
        }

        return tiles.ToList();
    }

    /// <summary>
    /// Generate lower zoom levels by downsampling from the base zoom.
    /// Reads from and writes to MBTiles database only.
    /// Exposed publicly for use by TileExtractor.
    /// </summary>
    public async Task GenerateZoomLevelsAsync(IProgress<ExportProgress>? progress)
    {
        for (var zoom = _config.Export.BaseZoomLevel - 1; zoom >= 0; zoom--)
        {
            await GenerateSingleZoomLevelAsync(zoom, progress);
        }
    }

    /// <summary>
    /// Generate tiles for a single zoom level by downsampling from the level above.
    /// </summary>
    private async Task GenerateSingleZoomLevelAsync(int zoom, IProgress<ExportProgress>? progress)
    {
        _sapi.Logger.Notification($"[VintageAtlas] Generating zoom level {zoom}...");

        var sourceZoom = zoom + 1;
        var extent = await _storage.GetTileExtentAsync(sourceZoom);

        if (extent == null)
        {
            _sapi.Logger.Warning($"[VintageAtlas] No tiles found at zoom {sourceZoom}, skipping {zoom}");
            return;
        }

        var targetTiles = CalculateTargetTilesForZoom(extent);
        _sapi.Logger.Notification($"[VintageAtlas] Generating {targetTiles.Count} tiles for zoom {zoom}");

        var generated = await GenerateDownsampledTilesAsync(zoom, targetTiles, progress);
        _sapi.Logger.Notification($"[VintageAtlas] Generated {generated} tiles for zoom {zoom}");
    }

    /// <summary>
    /// Calculate which tiles need to be generated at a zoom level based on the source extent.
    /// </summary>
    private static List<Vec2i> CalculateTargetTilesForZoom(TileExtent extent)
    {
        // FORGIVING APPROACH: Generate tiles even if not all 4 source tiles exist
        // This matches old Extractor.cs behavior where edge tiles with partial coverage
        // are still created (with transparent areas for missing source tiles)
        var targetTiles = new List<Vec2i>();

        // Simple division by 2 (matches old Extractor.cs)
        // Edge tiles will have some null source tiles, which is OK
        for (var tileX = extent.MinX / 2; tileX <= extent.MaxX / 2; tileX++)
        {
            for (var tileY = extent.MinY / 2; tileY <= extent.MaxY / 2; tileY++)
            {
                targetTiles.Add(new Vec2i(tileX, tileY));
            }
        }

        return targetTiles;
    }

    /// <summary>
    /// Generate downsampled tiles in parallel.
    /// </summary>
    private async Task<int> GenerateDownsampledTilesAsync(int zoom, List<Vec2i> targetTiles,
        IProgress<ExportProgress>? progress)
    {
        var generated = 0;

        await Parallel.ForEachAsync(targetTiles, async (tile, _) =>
        {
            try
            {
                var downsampled = await _downsampler.GenerateTileByDownsamplingAsync(zoom, tile.X, tile.Y);

                if (downsampled != null)
                {
                    await _storage.PutTileAsync(zoom, tile.X, tile.Y, downsampled);
                    var count = System.Threading.Interlocked.Increment(ref generated);

                    if (count % 100 == 0)
                    {
                        progress?.Report(new ExportProgress
                        {
                            TilesCompleted = count,
                            TotalTiles = targetTiles.Count,
                            CurrentZoomLevel = zoom
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to generate zoom tile {zoom}/{tile.X}_{tile.Y}: {ex.Message}");
            }
        });

        return generated;
    }

    /// <summary>
    /// Get tile data for ITileGenerator interface (used by PyramidTileDownsampler).
    /// Returns raw tile bytes or null if not found.
    /// </summary>
    public async Task<byte[]?> GetTileDataAsync(int zoom, int tileX, int tileZ)
    {
        var result = await GetTileAsync(zoom, tileX, tileZ);
        return result.NotFound ? null : result.Data;
    }

    /// <summary>
    /// Get or generate a single tile (for web requests).
    /// This is the replacement for DynamicTileGenerator.GenerateTileAsync().
    /// </summary>
    private async Task<TileResult> GetTileAsync(int zoom, int tileX, int tileZ, string? ifNoneMatch = null)
    {
        var tileKey = $"{zoom}_{tileX}_{tileZ}";

        // Check memory cache first
        if (_memoryCache.TryGetValue(tileKey, out var cached))
        {
            if (ifNoneMatch == cached.ETag)
            {
                return new TileResult
                {
                    NotModified = true,
                    ETag = cached.ETag
                };
            }

            return new TileResult
            {
                Data = cached.Data,
                ETag = cached.ETag,
                LastModified = cached.LastModified,
                ContentType = "image/png"
            };
        }

        // Check database
        var tileData = await _storage.GetTileAsync(zoom, tileX, tileZ);

        if (tileData != null)
        {
            var lastModified = DateTime.UtcNow;
            var etag = GenerateETag(tileData, lastModified);

            CacheInMemory(tileKey, tileData, etag, lastModified);

            return new TileResult
            {
                Data = tileData,
                ETag = etag,
                LastModified = lastModified,
                ContentType = "image/png"
            };
        }

        // Return 404 - tile must be generated via /atlas export
        return new TileResult { NotFound = true };
    }

    /// <summary>
    /// Render a tile from pre-loaded chunk data.
    /// Used by TileExtractor to render tiles from accumulated chunks.
    /// </summary>
    public async Task<byte[]?> RenderTileFromChunkDataAsync(TileChunkData tileData)
    {
        try
        {
            if (tileData == null)
            {
                _sapi.Logger.Error("[VintageAtlas] ⚠️  Tile data is null");
                return null;
            }

            if (tileData.Chunks.Count > 0)
            {
                var result = await Task.Run(() => _renderer.RenderTileImage(tileData));
                return result;
            }

            _sapi.Logger.Warning($"[VintageAtlas] ⚠️  No chunks found for tile {tileData.TileX},{tileData.TileZ}");
            return null;
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to render tile: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// CORE RENDERING METHOD - Used by both full export and on-demand generation.
    /// This is the single source of truth for tile rendering logic.
    /// Now delegates to FastBitmapRenderer for optimized rendering.
    /// </summary>
    private async Task<byte[]?> RenderTileAsync(
        int zoom,
        int tileX,
        int tileZ,
        IChunkDataSource dataSource)
    {
        try
        {
            // Get chunk data from the source
            var tileData = await dataSource.GetTileChunksAsync(zoom, tileX, tileZ);

            if (tileData == null)
            {
                _sapi.Logger.Error($"[VintageAtlas] ⚠️  Data source returned null for tile {zoom}/{tileX}_{tileZ}");
                return null;
            }

            return await RenderTileFromChunkDataAsync(tileData);
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to render tile: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Get tile extent (min/max coordinates) for a zoom level.
    /// Required by ITileGenerator interface.
    /// </summary>
    public async Task<TileExtent?> GetTileExtentAsync(int zoom)
    {
        return await _storage.GetTileExtentAsync(zoom);
    }

    /// <summary>
    /// Invalidate a tile (forces regeneration on next request)
    /// </summary>
    public async Task InvalidateTileAsync(int zoom, int tileX, int tileZ)
    {
        var tileKey = $"{zoom}_{tileX}_{tileZ}";
        _memoryCache.TryRemove(tileKey, out _);
        await _storage.DeleteTileAsync(zoom, tileX, tileZ);
    }

    private void CacheInMemory(string key, byte[] data, string etag, DateTime lastModified)
    {
        if (_memoryCache.Count >= MaxCacheSize)
        {
            var oldest = DateTime.MaxValue;
            string? oldestKey = null;

            foreach (var kvp in _memoryCache)
            {
                if (kvp.Value.LastModified >= oldest) continue;
                oldest = kvp.Value.LastModified;
                oldestKey = kvp.Key;
            }

            if (oldestKey != null)
            {
                _memoryCache.TryRemove(oldestKey, out _);
            }
        }

        _memoryCache[key] = new CachedTile
        {
            Data = data,
            ETag = etag,
            LastModified = lastModified
        };
    }

    private static string GenerateETag(byte[] data, DateTime lastModified)
    {
        return $"\"{data.Length}-{lastModified.Ticks}\"";
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _storage.Dispose();
        }
    }
}

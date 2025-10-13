using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VintageAtlas.Core;
using VintageAtlas.Models;
using VintageAtlas.Storage;
using Vintagestory.Common.Database;

namespace VintageAtlas.Export;

/// <summary>
/// Unified tile generation system that replaces both Extractor and DynamicTileGenerator.
/// Uses a single rendering implementation with pluggable data sources (IChunkDataSource)
/// for both full exports and on-demand generation.
/// </summary>
public sealed class UnifiedTileGenerator : ITileGenerator
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly MbTilesStorage _storage;
    private readonly BlockColorCache _colorCache;
    private readonly PyramidTileDownsampler _downsampler;

    // In-memory cache for frequently accessed tiles
    private readonly ConcurrentDictionary<string, CachedTile> _memoryCache = new();

    // Cache for microblock detection (chiseled blocks)
    private readonly HashSet<int> _microBlocks;

    private const int ChunkSize = 32;
    private const int MaxCacheSize = 100;

    public UnifiedTileGenerator(
        ICoreServerAPI sapi,
        ModConfig config,
        BlockColorCache colorCache,
        MbTilesStorage storage)
    {
        _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _colorCache = colorCache ?? throw new ArgumentNullException(nameof(colorCache));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));

        // Initialize downsampler for lower zoom levels
        _downsampler = new PyramidTileDownsampler(sapi, config, this);

        // Initialize the microblocks collection (chiseled blocks)
        _microBlocks = sapi.World.Blocks
            .Where(b => b.Code?.Path.StartsWith("chiseledblock") is true ||
                        b.Code?.Path.StartsWith("microblock") is true)
            .Select(x => x.Id)
            .ToHashSet();

        _sapi.Logger.Notification("[VintageAtlas] UnifiedTileGenerator initialized");
    }

    #region Full Export

    /// <summary>
    /// Export the full map from the savegame database.
    /// This is the replacement for Extractor.ExtractWorldMap().
    /// </summary>
    public async Task ExportFullMapAsync(
        IChunkDataSource dataSource,
        IProgress<ExportProgress>? progress = null)
    {
        _sapi.Logger.Notification("[VintageAtlas] Starting full map export (unified generator)...");

        var startTime = DateTime.UtcNow;
        var totalTiles = 0;

        try
        {
            // Query actual chunk positions from the data source
            List<TilePos> tiles;

            // TODO: Check if this is really needed.
            if (dataSource is SavegameDataSource savegameSource)
            {
                var chunkPositions = savegameSource.GetAllMapChunkPositions();
                tiles = CalculateTileCoverageFromChunks(chunkPositions);
            }
            else
            {
                // Fallback to estimated coverage for other data sources
                tiles = CalculateTileCoverage();
            }

            _sapi.Logger.Notification($"[VintageAtlas] Exporting {tiles.Count} tiles at zoom {_config.BaseZoomLevel}");

            // Generate base zoom tiles in parallel
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _config.MaxDegreeOfParallelism == -1
                    ? Environment.ProcessorCount
                    : _config.MaxDegreeOfParallelism
            };

            await Parallel.ForEachAsync(tiles, parallelOptions, async (tile, _) =>
            {
                try
                {
                    var tileData = await RenderTileAsync(
                        _config.BaseZoomLevel,
                        tile.X,
                        tile.Z,
                        dataSource
                    );

                    if (tileData != null)
                    {
                        // Write tile using absolute world coordinates (matching legacy extractor)
                        await _storage.PutTileAsync(_config.BaseZoomLevel, tile.X, tile.Z, tileData);

                        var completed = System.Threading.Interlocked.Increment(ref totalTiles);

                        if (completed % 100 == 0)
                        {
                            _sapi.Logger.Notification(
                                $"[VintageAtlas] Exported {completed}/{tiles.Count} tiles ({completed * 100.0 / tiles.Count:F1}%)");

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
                    _sapi.Logger.Error($"[VintageAtlas] Failed to export tile {tile.X}_{tile.Z}: {ex.Message}");
                }
            });

            var duration = DateTime.UtcNow - startTime;
            _sapi.Logger.Notification(
                $"[VintageAtlas] Base zoom export complete: {totalTiles} tiles in {duration.TotalSeconds:F1}s " +
                $"({totalTiles / duration.TotalSeconds:F1} tiles/sec)");

            // Generate zoom levels by downsampling from database
            if (_config.CreateZoomLevels)
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
    private List<TilePos> CalculateTileCoverageFromChunks(List<ChunkPos> chunkPositions)
    {
        if (chunkPositions.Count == 0)
        {
            _sapi.Logger.Warning("[VintageAtlas] No chunks found in savegame database!");
            return [];
        }

        var tiles = new HashSet<TilePos>();
        var chunksPerTile = _config.TileSize / ChunkSize;

        _sapi.Logger.Notification($"[VintageAtlas] Calculating tile coverage from {chunkPositions.Count} chunks...");

        // Convert each chunk position to a tile position
        foreach (var chunkPos in chunkPositions)
        {
            // Calculate which tile this chunk belongs to
            var tileX = chunkPos.X / chunksPerTile;
            var tileZ = chunkPos.Z / chunksPerTile;

            tiles.Add(new TilePos(tileX, tileZ));
        }

        return tiles.ToList();
    }

    /// <summary>
    /// Calculate which tiles need to be generated based on estimated world size.
    /// Fallback for when actual chunk positions are not available.
    /// </summary>
    private List<TilePos> CalculateTileCoverage()
    {
        // Remove estimation: require actual chunk-driven coverage
        _sapi.Logger.Warning("[VintageAtlas] CalculateTileCoverage() called without chunk positions; returning empty set");
        return new List<TilePos>();
    }

    /// <summary>
    /// Generate lower zoom levels by downsampling from the base zoom.
    /// Reads from and writes to MBTiles database only.
    /// </summary>
    private async Task GenerateZoomLevelsAsync(IProgress<ExportProgress>? progress)
    {
        for (var zoom = _config.BaseZoomLevel - 1; zoom >= 0; zoom--)
        {
            _sapi.Logger.Notification($"[VintageAtlas] Generating zoom level {zoom}...");

            // Get the extent of tiles at a higher zoom level
            var sourceZoom = zoom + 1;
            var extent = await _storage.GetTileExtentAsync(sourceZoom);

            if (extent == null)
            {
                _sapi.Logger.Warning($"[VintageAtlas] No tiles found at zoom {sourceZoom}, skipping {zoom}");
                continue;
            }

            // Calculate target tiles at current zoom
            // FORGIVING APPROACH: Generate tiles even if not all 4 source tiles exist
            // This matches old Extractor.cs behavior where edge tiles with partial coverage
            // are still created (with transparent areas for missing source tiles)
            var targetTiles = new List<TilePos>();

            // Simple division by 2 (matches old Extractor.cs)
            // Edge tiles will have some null source tiles, which is OK
            for (var tileX = extent.MinX / 2; tileX <= extent.MaxX / 2; tileX++)
            {
                for (var tileZ = extent.MinY / 2; tileZ <= extent.MaxY / 2; tileZ++)
                {
                    targetTiles.Add(new TilePos(tileX, tileZ));
                }
            }

            _sapi.Logger.Notification($"[VintageAtlas] Generating {targetTiles.Count} tiles for zoom {zoom}");

            var generated = 0;
            await Parallel.ForEachAsync(targetTiles, async (tile, _) =>
            {
                try
                {
                    var downsampled = await _downsampler.GenerateTileByDownsamplingAsync(zoom, tile.X, tile.Z);

                    if (downsampled != null)
                    {
                        await _storage.PutTileAsync(zoom, tile.X, tile.Z, downsampled);
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
                    _sapi.Logger.Error($"[VintageAtlas] Failed to generate zoom tile {zoom}/{tile.X}_{tile.Z}: {ex.Message}");
                }
            });

            _sapi.Logger.Notification($"[VintageAtlas] Generated {generated} tiles for zoom {zoom}");
        }
    }

    #endregion

    #region On-Demand Generation

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

        // // Generate on-demand from loaded chunks
        // _sapi.Logger.Debug($"[VintageAtlas] Generating tile on-demand: {zoom}/{tileX}_{tileZ}");

        // try
        // {
        //     byte[]? newTileData = null;

        //     if (zoom == _config.BaseZoomLevel)
        //     {
        //         // Base zoom: generate from loaded chunks
        //         var loadedChunksSource = new LoadedChunksDataSource(_sapi, _config);
        //         newTileData = await RenderTileAsync(zoom, tileX, tileZ, loadedChunksSource);
        //     }
        //     else if (zoom < _config.BaseZoomLevel)
        //     {
        //         // Lower zoom: downsample from higher zoom
        //         newTileData = await _downsampler.GenerateTileByDownsamplingAsync(zoom, tileX, tileZ);
        //     }

        //     // Fallback to placeholder
        //     if (newTileData == null)
        //     {
        //         return new TileResult { NotFound = true };
        //     }

        //     // Store in database
        //     await _storage.PutTileAsync(zoom, tileX, tileZ, newTileData);

        //     var lastModified = DateTime.UtcNow;
        //     var etag = GenerateETag(newTileData, lastModified);

        //     // Cache in memory
        //     CacheInMemory(tileKey, newTileData, etag, lastModified);

        //     _sapi.Logger.Debug($"[VintageAtlas] Generated and stored tile: {zoom}/{tileX}_{tileZ}");

        //     return new TileResult
        //     {
        //         Data = newTileData,
        //         ETag = etag,
        //         LastModified = lastModified,
        //         ContentType = "image/png"
        //     };
        // }
        // catch (Exception ex)
        // {
        //     _sapi.Logger.Error($"[VintageAtlas] Failed to generate tile {zoom}/{tileX}_{tileZ}: {ex.Message}");
        // }

        // ═══════════════════════════════════════════════════════════════
        // ON-DEMAND GENERATION DISABLED
        // Tiles are ONLY generated during /atlas export
        // This ensures we're testing the full export system properly
        // ═══════════════════════════════════════════════════════════════

        // Return 404 - tile must be generated via /atlas export
        return new TileResult { NotFound = true };
    }

    #endregion

    #region Core Rendering (Single Implementation!)

    /// <summary>
    /// CORE RENDERING METHOD - Used by both full export and on-demand generation.
    /// This is the single source of truth for tile rendering logic.
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

            if (tileData.Chunks.Count != 0)
                return await Task.Run(() => RenderTileImage(tileData));

            _sapi.Logger.Warning($"[VintageAtlas] ⚠️  No chunks found for tile {zoom}/{tileX}_{tileZ}");
            return null;
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to render tile: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Render a tile image from chunk data using SkiaSharp.
    /// This runs on a background thread.
    /// </summary>
    private byte[]? RenderTileImage(TileChunkData tileData)
    {
        try
        {
            var tileSize = tileData.TileSize;
            var chunksPerTile = tileData.ChunksPerTileEdge;

            using var bitmap = new SKBitmap(tileSize, tileSize);
            using var canvas = new SKCanvas(bitmap);

            canvas.Clear(); // TODO: can use to create background | new SKColor(41, 128, 185) Ocean blue

            // Per-tile randomness (parity with Extractor's per-image Random)
            // Non-deterministic across runs; stable within a tile during a single render
            var randomForTile = new Random(unchecked(Environment.TickCount ^ (tileData.TileX * 73856093) ^ (tileData.TileZ * 19349663)));

            // Create a shadow map for hill shading modes
            Span<byte> shadowMap = null;
            if (_config.Mode is ImageMode.ColorVariationsWithHillShading or ImageMode.MedievalStyleWithHillShading)
            {
                shadowMap = new byte[tileSize * tileSize];
                for (var i = 0; i < shadowMap.Length; i++)
                    shadowMap[i] = 128; // Initialize to neutral (no shadow/highlight)
            }

            var chunksRendered = 0;
            var startChunkX = tileData.TileX * chunksPerTile;
            var startChunkZ = tileData.TileZ * chunksPerTile;

            for (var offsetX = 0; offsetX < chunksPerTile; offsetX++)
            {
                for (var offsetZ = 0; offsetZ < chunksPerTile; offsetZ++)
                {
                    var chunkX = startChunkX + offsetX;
                    var chunkZ = startChunkZ + offsetZ;

                    var snapshot = tileData.GetChunk(chunkX, chunkZ, 0);
                    if (snapshot == null)
                    {
                        continue;
                    }

                    if (!snapshot.IsLoaded)
                    {
                        _sapi.Logger.Warning($"[VintageAtlas] ⚠️  Chunk ({chunkX},{chunkZ}) exists but IsLoaded=false!");
                        continue;
                    }

                    RenderChunkToCanvas(canvas, snapshot, offsetX * ChunkSize, offsetZ * ChunkSize,
                        shadowMap, tileSize, tileData, randomForTile);
                    chunksRendered++;
                }
            }

            if (chunksRendered == 0)
            {
                _sapi.Logger.Warning("[VintageAtlas] ⚠️  NO chunks were rendered!");
                return null;
            }

            // Apply shadow map blur and shading for hill shading modes
            if (_config.Mode is ImageMode.ColorVariationsWithHillShading or ImageMode.MedievalStyleWithHillShading)
            {
                ApplyShadowMapToBitmap(bitmap, shadowMap, tileSize);
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to render tile image: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Render a single chunk snapshot onto the canvas.
    /// Uses BlockColorCache for proper terrain coloring with full rendering mode support.
    /// </summary>
    private void RenderChunkToCanvas(SKCanvas canvas, ChunkSnapshot snapshot, int offsetX, int offsetZ,
        Span<byte> shadowMap, int tileSize, TileChunkData tileData, Random randomForTile)
    {
        try
        {
            var heightMap = snapshot.HeightMap;
            var blockIds = snapshot.BlockIds;

            if (heightMap.Length == 0 || blockIds.Length == 0)
            {
                _sapi.Logger.Warning($"[VintageAtlas] ⚠️  Empty data! heightMap={heightMap.Length}, blockIds={blockIds.Length}");
                return;
            }

            using var paint = new SKPaint();
            // Use per-tile randomness (parity with Extractor) instead of per-chunk deterministic seed
            var random = randomForTile;

            var mapYHalf = _sapi.WorldManager.MapSizeY / 2;
            var mapMaxY = _sapi.WorldManager.MapSizeY - 1;

            for (var x = 0; x < ChunkSize; x++)
            {
                for (var z = 0; z < ChunkSize; z++)
                {
                    var heightIndex = z * ChunkSize + x;
                    if (heightIndex >= heightMap.Length)
                        continue;

                    var height = heightMap[heightIndex];
                    // Clamp height to valid map range (Extractor behavior)
                    height = GameMath.Clamp(height, 0, mapMaxY);

                    // ═══════════════════════════════════════════════════════════════
                    // SURFACE BLOCK RENDERING (from SavegameDataSource)
                    // BlockIds are stored at their actual height's local Y position
                    // e.g., height=114 → localY=114%32=18 → blockIndex at Y=18
                    // ═══════════════════════════════════════════════════════════════
                    var localY = height % ChunkSize; // Local Y within a 32-block range

                    var blockIndex = localY * ChunkSize * ChunkSize + z * ChunkSize + x;

                    if (blockIndex < 0 || blockIndex >= blockIds.Length)
                    {
                        // TODO: Check why we are setting this color
                        // paint.Color = new SKColor(172, 136, 88);
                        // canvas.DrawPoint(offsetX + x, offsetZ + z, paint);
                        continue;
                    }

                    var blockId = blockIds[blockIndex];

                    // Handle snow blocks: look at the block underneath
                    var isHeightOffset = 0;
                    if (_sapi.World.Blocks[blockId].BlockMaterial == EnumBlockMaterial.Snow)
                    {
                        isHeightOffset = 1;
                        var adjustedHeight = height - 1;
                        if (adjustedHeight >= 0)
                        {
                            var adjustedLocalY = adjustedHeight % ChunkSize;
                            // Use the current snapshot's BlockIds at the adjusted local Y.
                            // SavegameDataSource populates BlockIds at the surface localY positions.
                            var adjustedBlockIndex = adjustedLocalY * ChunkSize * ChunkSize + z * ChunkSize + x;
                            if (adjustedBlockIndex >= 0 && adjustedBlockIndex < blockIds.Length)
                            {
                                blockId = blockIds[adjustedBlockIndex];
                            }
                        }
                    }

                    // Get color based on render mode
                    uint color;
                    var imgX = offsetX + x;
                    var imgZ = offsetZ + z;

                    // Handle chiseled/micro blocks: use the actual block from the chisel
                    uint? overrideColor = null;
                    if (_microBlocks.Contains(blockId))
                    {
                        var worldX = snapshot.ChunkX * ChunkSize + x;
                        var worldZ = snapshot.ChunkZ * ChunkSize + z;
                        var blockPos = new BlockPos(worldX, height + isHeightOffset, worldZ, 0);

                        if (snapshot.BlockEntities.TryGetValue(blockPos, out var blockEntity) &&
                            blockEntity is BlockEntityMicroBlock { BlockIds.Length: > 0 } blockEntityChisel)
                        // Use the first block ID from the chiseled block
                        {
                            blockId = blockEntityChisel.BlockIds[0];
                        }
                        else
                        {
                            // Fallback parity with Extractor
                            // Non-medieval: flat green. Medieval: land color.
                            if (_config.Mode == ImageMode.MedievalStyleWithHillShading)
                            {
                                overrideColor = MapColors.ColorsByCode["land"];
                            }
                            else
                            {
                                var green = (uint)SKColors.Green;
                                overrideColor = green;
                            }
                        }
                    }

                    switch (_config.Mode)
                    {
                        case ImageMode.OnlyOneColor:
                            color = overrideColor ?? _colorCache.GetBaseColor(blockId);
                            break;

                        case ImageMode.ColorVariations:
                            color = overrideColor ?? _colorCache.GetRandomColorVariation(blockId, random);
                            break;

                        case ImageMode.ColorVariationsWithHeight:
                            color = overrideColor ?? _colorCache.GetRandomColorVariation(blockId, random);
                            // Apply height-based darkening/lightening (add height offset for snow)
                            var adjustedHeight = height + isHeightOffset;
                            color = (uint)ColorUtil.ColorMultiply3Clamped((int)color, adjustedHeight / (float)mapYHalf);
                            break;

                        case ImageMode.ColorVariationsWithHillShading:
                            color = overrideColor ?? _colorCache.GetRandomColorVariation(blockId, random);

                            // Calculate slope and populate shadow map
                            if (shadowMap != null)
                            {
                                var (nwDelta, nDelta, wDelta) = CalculateAltitudeDiff(x, height, z, snapshot.HeightMap);
                                var boostMultiplier = CalculateSlopeBoost(nwDelta, nDelta, wDelta);
                                var shadowIndex = imgZ * tileSize + imgX;
                                if (shadowIndex >= 0 && shadowIndex < shadowMap.Length)
                                {
                                    shadowMap[shadowIndex] = (byte)(shadowMap[shadowIndex] * boostMultiplier);
                                }
                            }
                            break;

                        case ImageMode.MedievalStyleWithHillShading:
                            // Check if this is a water edge
                            var isWaterEdge = DetectWaterEdge(blockId, x, z, snapshot);
                            color = overrideColor ?? _colorCache.GetMedievalStyleColor(blockId, isWaterEdge);

                            // Apply hill shading for non-water blocks
                            if (shadowMap != null && !_colorCache.IsLake(blockId))
                            {
                                var (nwDelta, nDelta, wDelta) = CalculateAltitudeDiff(x, height, z, snapshot.HeightMap);
                                var boostMultiplier = CalculateSlopeBoost(nwDelta, nDelta, wDelta);
                                var shadowIndex = imgZ * tileSize + imgX;
                                if (shadowIndex >= 0 && shadowIndex < shadowMap.Length)
                                {
                                    shadowMap[shadowIndex] = (byte)(shadowMap[shadowIndex] * boostMultiplier);
                                }
                            }
                            break;

                        default:
                            color = _colorCache.GetBaseColor(blockId);
                            break;
                    }

                    // Convert uint ARGB to SKColor
                    var a = (byte)((color >> 24) & 0xFF);
                    var r = (byte)((color >> 16) & 0xFF);
                    var g = (byte)((color >> 8) & 0xFF);
                    var b = (byte)(color & 0xFF);

                    paint.Color = new SKColor(r, g, b, a);
                    canvas.DrawPoint(imgX, imgZ, paint);
                }
            }
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to render chunk: {ex.Message}");
        }
    }

    #endregion

    #region Utilities

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

    /// <summary>
    /// Get storage statistics
    /// </summary>
    public async Task<StorageStats> GetStatsAsync()
    {
        var stats = new StorageStats
        {
            DatabaseSizeBytes = _storage.GetDatabaseSize(),
            MemoryCachedTiles = _memoryCache.Count,
            TotalTiles = await _storage.GetTileCountAsync()
        };

        for (var z = 0; z <= _config.BaseZoomLevel; z++)
        {
            stats.TilesPerZoom[z] = await _storage.GetTileCountAsync(z);
        }

        return stats;
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

    /// <summary>
    /// Calculate altitude differences with neighboring blocks for hill shading.
    /// Optimized to stay within chunk boundaries.
    /// </summary>
    private static (int northWestDelta, int northDelta, int westDelta) CalculateAltitudeDiff(
        int x, int y, int z, int[] heightMap)
    {
        var westernX = x - 1;
        var northernZ = z - 1;

        if (westernX < 0)
        {
            westernX++;
        }

        if (northernZ < 0)
        {
            northernZ++;
        }

        westernX = GameMath.Mod(westernX, ChunkSize);
        northernZ = GameMath.Mod(northernZ, ChunkSize);

        var northWestIndex = northernZ * ChunkSize + westernX;
        var northWestHeight = northWestIndex < heightMap.Length ? heightMap[northWestIndex] : y;

        var northIndex = northernZ * ChunkSize + x;
        var northHeight = northIndex < heightMap.Length ? heightMap[northIndex] : y;

        var westIndex = z * ChunkSize + westernX;
        var westHeight = westIndex < heightMap.Length ? heightMap[westIndex] : y;

        return (y - northWestHeight, y - northHeight, y - westHeight);
    }

    /// <summary>
    /// Calculate slope boost multiplier for the shadow map based on altitude deltas.
    /// Returns a value to darken or lighten the pixel based on the slope direction.
    /// </summary>
    private static float CalculateSlopeBoost(int northWestDelta, int northDelta, int westDelta)
    {
        var direction = Math.Sign(northWestDelta) + Math.Sign(northDelta) + Math.Sign(westDelta);
        float steepness = Math.Max(Math.Max(Math.Abs(northWestDelta), Math.Abs(northDelta)), Math.Abs(westDelta));
        var slopeFactor = Math.Min(0.5f, steepness / 10f) / 1.25f;

        return direction switch
        {
            > 0 => 1.08f + slopeFactor,
            < 0 => 0.92f - slopeFactor,
            _ => 1
        };
    }

    /// <summary>
    /// Detect if a water block is at the edge (borders non-water blocks).
    /// Used for medieval style rendering to draw darker water edges.
    /// </summary>
    private bool DetectWaterEdge(int blockId, int x, int z, ChunkSnapshot snapshot)
    {
        if (!_colorCache.IsLake(blockId))
            return false;

        // Check boundaries - edges are always rendered as water
        if (x == 0 || x == ChunkSize - 1 || z == 0 || z == ChunkSize - 1)
            return false;

        var heightMap = snapshot.HeightMap;
        var blockIds = snapshot.BlockIds;

        // Check 4 neighbors
        var n = z - 1;
        var s = z + 1;
        var e = x + 1;
        var w = x - 1;

        // Get neighbor block IDs
        var neighborN = GetBlockAtPosition(x, n, heightMap, blockIds);
        var neighborS = GetBlockAtPosition(x, s, heightMap, blockIds);
        var neighborE = GetBlockAtPosition(e, z, heightMap, blockIds);
        var neighborW = GetBlockAtPosition(w, z, heightMap, blockIds);

        // If all neighbors are also water/lake, this is interior water
        return !_colorCache.IsLake(neighborN) || !_colorCache.IsLake(neighborS) ||
               !_colorCache.IsLake(neighborE) || !_colorCache.IsLake(neighborW);
        // At least one neighbor is land - this is a water edge
    }

    /// <summary>
    /// Get block ID at a specific X, Z position within a chunk using the height map.
    /// </summary>
    private static int GetBlockAtPosition(int x, int z, int[] heightMap, int[] blockIds)
    {
        if (x < 0 || x >= ChunkSize || z < 0 || z >= ChunkSize)
            return 0;

        var heightIndex = z * ChunkSize + x;
        if (heightIndex >= heightMap.Length)
            return 0;

        var height = heightMap[heightIndex];
        if (height == 0)
            return 0;

        // Calculate local Y and block index
        var localY = height % ChunkSize;
        var blockIndex = localY * ChunkSize * ChunkSize + z * ChunkSize + x;

        if (blockIndex < 0 || blockIndex >= blockIds.Length)
            return 0;

        return blockIds[blockIndex];
    }

    /// <summary>
    /// Apply shadow map blur and lighting to the final bitmap.
    /// This creates the hill shading effect.
    /// </summary>
    private static unsafe void ApplyShadowMapToBitmap(SKBitmap bitmap, Span<byte> shadowMap, int size)
    {
        // Create a copy for sharpening
        Span<byte> originalShadowMap = new byte[shadowMap.Length];
        shadowMap.CopyTo(originalShadowMap);

        // Blur the shadow map to soften harsh edges
        BlurTool.Blur(shadowMap, size, size, 2);

        const float sharpen = 1.4f;

        var imgPtr = (byte*)bitmap.GetPixels().ToPointer();
        var imgRowBytes = bitmap.RowBytes;

        for (var i = 0; i < shadowMap.Length; i++)
        {
            // Combine blurred and sharp shadows for detail preservation
            var blurredValue = shadowMap[i] / 128f - 1f;
            var originalValue = originalShadowMap[i] / 128f - 1f;

            var shadowEffect = (int)(blurredValue * 5) / 5f;
            shadowEffect += originalValue * 5 % 1 / 5f;

            if (shadowEffect is 0)
                continue;

            var imgX = i % size;
            var imgZ = i / size;

            var row = (uint*)(imgPtr + imgZ * imgRowBytes);
            var pixel = (int)row[imgX];

            // Apply shadow/highlight with sharpening
            var adjusted = ColorUtil.ColorMultiply3Clamped(pixel, shadowEffect * sharpen + 1f);
            row[imgX] = (uint)(adjusted | 255 << 24); // Preserve alpha
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (disposing)
        {
            _storage.Dispose();
        }
    }
}

/// <summary>
/// Storage statistics for tile generation
/// </summary>
public class StorageStats
{
    public long DatabaseSizeBytes { get; set; }
    public int MemoryCachedTiles { get; set; }
    public long TotalTiles { get; set; }
    public Dictionary<int, long> TilesPerZoom { get; set; } = new();
}

/// <summary>
/// Cached tile data for in-memory storage
/// </summary>
public class CachedTile
{
    public byte[] Data { get; set; } = [];
    public DateTime LastModified { get; set; }
    public string ETag { get; set; } = "";
}

#endregion

/// <summary>
/// Simple struct for tile coordinates
/// </summary>
public record struct TilePos(int X, int Z);

/// <summary>
/// Progress information for export operations
/// </summary>
public class ExportProgress
{
    public int TilesCompleted { get; set; }
    public int TotalTiles { get; set; }
    public int CurrentZoomLevel { get; set; }
    public double PercentComplete => TotalTiles > 0 ? TilesCompleted * 100.0 / TotalTiles : 0;
}


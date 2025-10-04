using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
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
public class UnifiedTileGenerator : ITileGenerator, IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly MbTilesStorage _storage;
    private readonly BlockColorCache _colorCache;
    private readonly PyramidTileDownsampler _downsampler;

    // In-memory cache for frequently accessed tiles
    private readonly ConcurrentDictionary<string, CachedTile> _memoryCache = new();

    private const int CHUNK_SIZE = 32;
    private const int MAX_CACHE_SIZE = 100;

    public UnifiedTileGenerator(
        ICoreServerAPI sapi,
        ModConfig config,
        BlockColorCache colorCache,
        MbTilesStorage storage)
    {
        _sapi = sapi;
        _config = config;
        _colorCache = colorCache;
        _storage = storage;

        // Initialize downsampler for lower zoom levels
        _downsampler = new PyramidTileDownsampler(sapi, config, this);

        _sapi.Logger.Notification("[VintageAtlas] UnifiedTileGenerator initialized");
    }

    #region Full Export

    /// <summary>
    /// Export full map from savegame database.
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

            await Parallel.ForEachAsync(tiles, parallelOptions, async (tile, ct) =>
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
                        // Write directly to MBTiles database (NO PNG files!)
                        await _storage.PutTileAsync(_config.BaseZoomLevel, tile.X, tile.Z, tileData);

                        var completed = System.Threading.Interlocked.Increment(ref totalTiles);

                        if (completed % 100 == 0)
                        {
                            _sapi.Logger.Notification(
                                $"[VintageAtlas] Exported {completed}/{tiles.Count} tiles ({(completed * 100.0 / tiles.Count):F1}%)");
                            
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

            // CRITICAL: Checkpoint WAL to commit all tiles to main database
            _sapi.Logger.Notification("[VintageAtlas] Committing tiles to database...");
            _storage.CheckpointWAL();

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
            return new List<TilePos>();
        }

        var tiles = new HashSet<TilePos>();
        var chunksPerTile = _config.TileSize / CHUNK_SIZE;

        _sapi.Logger.Notification($"[VintageAtlas] Calculating tile coverage from {chunkPositions.Count} chunks...");

        // Convert each chunk position to a tile position
        foreach (var chunkPos in chunkPositions)
        {
            // Calculate which tile this chunk belongs to
            var tileX = chunkPos.X / chunksPerTile;
            var tileZ = chunkPos.Z / chunksPerTile;
            
            tiles.Add(new TilePos(tileX, tileZ));
        }

        // Calculate actual world extent for logging
        var minChunkX = chunkPositions.Min(c => c.X);
        var maxChunkX = chunkPositions.Max(c => c.X);
        var minChunkZ = chunkPositions.Min(c => c.Z);
        var maxChunkZ = chunkPositions.Max(c => c.Z);
        
        var minBlockX = minChunkX * CHUNK_SIZE;
        var maxBlockX = maxChunkX * CHUNK_SIZE;
        var minBlockZ = minChunkZ * CHUNK_SIZE;
        var maxBlockZ = maxChunkZ * CHUNK_SIZE;

        _sapi.Logger.Notification($"[VintageAtlas] World extent: X [{minBlockX} to {maxBlockX}], Z [{minBlockZ} to {maxBlockZ}]");
        _sapi.Logger.Notification($"[VintageAtlas] Generated {tiles.Count} unique tiles from {chunkPositions.Count} chunks");

        return tiles.ToList();
    }

    /// <summary>
    /// Calculate which tiles need to be generated based on estimated world size.
    /// Fallback for when actual chunk positions are not available.
    /// </summary>
    private List<TilePos> CalculateTileCoverage()
    {
        var tiles = new HashSet<TilePos>();
        var chunksPerTile = _config.TileSize / CHUNK_SIZE;

        // CRITICAL FIX: Use actual world extent, not theoretical max
        // Vintage Story worlds are typically much smaller than MapSizeX
        // Most worlds are ~10,000 to 100,000 blocks, not 1,000,000+
        
        // Use a reasonable default based on typical world sizes
        // This will be overridden by actual chunk positions if available
        var estimatedWorldRadius = 5000; // 10,000 blocks across (reasonable for most worlds)
        var tilesPerSide = (estimatedWorldRadius * 2) / CHUNK_SIZE / chunksPerTile;
        
        var minTile = -tilesPerSide / 2;
        var maxTile = tilesPerSide / 2;

        _sapi.Logger.Notification($"[VintageAtlas] Estimated tile coverage: {minTile} to {maxTile} ({(maxTile - minTile + 1) * (maxTile - minTile + 1)} tiles)");

        for (var tileX = minTile; tileX <= maxTile; tileX++)
        {
            for (var tileZ = minTile; tileZ <= maxTile; tileZ++)
            {
                tiles.Add(new TilePos(tileX, tileZ));
            }
        }

        return tiles.ToList();
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

            // Get extent of tiles at higher zoom level
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
            await Parallel.ForEachAsync(targetTiles, async (tile, ct) =>
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
        var result = await GetTileAsync(zoom, tileX, tileZ, null);
        return result.NotFound ? null : result.Data;
    }

    /// <summary>
    /// Get or generate a single tile (for web requests).
    /// This is the replacement for DynamicTileGenerator.GenerateTileAsync().
    /// </summary>
    public async Task<TileResult> GetTileAsync(int zoom, int tileX, int tileZ, string? ifNoneMatch = null)
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

            _sapi.Logger.Debug($"[VintageAtlas] ✅ Found tile in DB: {zoom}/{tileX}_{tileZ}");

            return new TileResult
            {
                Data = tileData,
                ETag = etag,
                LastModified = lastModified,
                ContentType = "image/png"
            };
        }

        //  // Generate on-demand from loaded chunks
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
        //         _sapi.Logger.Warning($"[VintageAtlas] ⚠️  Tile generation returned null, creating placeholder: {zoom}/{tileX}_{tileZ}");
        //         newTileData = await GeneratePlaceholderTileAsync(zoom, tileX, tileZ);
        //     }

        //     if (newTileData != null)
        //     {
        //         // Store in database
        //         await _storage.PutTileAsync(zoom, tileX, tileZ, newTileData);

        //         var lastModified = DateTime.UtcNow;
        //         var etag = GenerateETag(newTileData, lastModified);

        //         // Cache in memory
        //         CacheInMemory(tileKey, newTileData, etag, lastModified);

        //         _sapi.Logger.Debug($"[VintageAtlas] Generated and stored tile: {zoom}/{tileX}_{tileZ}");

        //         return new TileResult
        //         {
        //             Data = newTileData,
        //             ETag = etag,
        //             LastModified = lastModified,
        //             ContentType = "image/png"
        //         };
        //     }
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
        
        _sapi.Logger.Debug($"[VintageAtlas] ❌ Tile not in database (on-demand generation disabled): {zoom}/{tileX}_{tileZ}");
        
        // Return 404 - tile must be generated via /atlas export
        return new TileResult { NotFound = true };
    }

    #endregion

    #region Core Rendering (Single Implementation!)

    /// <summary>
    /// CORE RENDERING METHOD - Used by both full export and on-demand generation.
    /// This is the single source of truth for tile rendering logic.
    /// </summary>
    public async Task<byte[]?> RenderTileAsync(
        int zoom,
        int tileX,
        int tileZ,
        IChunkDataSource dataSource)
    {
        try
        {
            _sapi.Logger.Debug($"[VintageAtlas] RenderTile: z{zoom} t({tileX},{tileZ}) from {dataSource.SourceName}");

            // Get chunk data from the source
            TileChunkData? tileData;
            
            if (dataSource.RequiresMainThread)
            {
                // Already handled in LoadedChunksDataSource
                tileData = await dataSource.GetTileChunksAsync(zoom, tileX, tileZ);
            }
            else
            {
                // Can call directly (e.g., SavegameDataSource)
                tileData = await dataSource.GetTileChunksAsync(zoom, tileX, tileZ);
            }

            if (tileData == null)
            {
                _sapi.Logger.Warning($"[VintageAtlas] ⚠️  Data source returned null for tile {zoom}/{tileX}_{tileZ}");
                return null;
            }

            if (tileData.Chunks.Count == 0)
            {
                _sapi.Logger.Warning($"[VintageAtlas] ⚠️  No chunks found for tile {zoom}/{tileX}_{tileZ}");
                return null;
            }

            _sapi.Logger.Debug($"[VintageAtlas] Extracted {tileData.Chunks.Count} chunks for tile {zoom}/{tileX}_{tileZ}");

            // Render tile on background thread
            return await Task.Run(() => RenderTileImage(tileData));
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to render tile: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Render tile image from chunk data using SkiaSharp.
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

            canvas.Clear(new SKColor(41, 128, 185)); // Ocean blue

            // Create shadow map for hill shading modes
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

            _sapi.Logger.Debug(
                $"[VintageAtlas] Rendering tile z{tileData.Zoom} t({tileData.TileX},{tileData.TileZ}), " +
                $"start chunk=({startChunkX},{startChunkZ}), chunks={tileData.Chunks.Count}");

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

                    RenderChunkToCanvas(canvas, snapshot, offsetX * CHUNK_SIZE, offsetZ * CHUNK_SIZE, 
                        shadowMap, tileSize, tileData);
                    chunksRendered++;
                }
            }

            _sapi.Logger.Debug($"[VintageAtlas] Rendered {chunksRendered}/{tileData.Chunks.Count} chunks");

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
        Span<byte> shadowMap, int tileSize, TileChunkData tileData)
    {
        // DEBUG: Log that we're entering this method
        _sapi.Logger.Notification($"[VintageAtlas] 🖌️ RenderChunkToCanvas called! Mode={_config.Mode}");
        
        try
        {
            var heightMap = snapshot.HeightMap;
            var blockIds = snapshot.BlockIds;

            if (heightMap.Length == 0 || blockIds.Length == 0)
            {
                _sapi.Logger.Warning($"[VintageAtlas] ⚠️  Empty data! heightMap={heightMap.Length}, blockIds={blockIds.Length}");
                return;
            }

            _sapi.Logger.Notification($"[VintageAtlas] ✅ Starting pixel loop: heightMap={heightMap.Length}, blockIds={blockIds.Length}, mode={_config.Mode}");

            using var paint = new SKPaint();
            var random = new Random(snapshot.ChunkX * 31 + snapshot.ChunkZ); // Deterministic seed
            
            var mapYHalf = _sapi.WorldManager.MapSizeY / 2;

            for (var x = 0; x < CHUNK_SIZE; x++)
            {
                for (var z = 0; z < CHUNK_SIZE; z++)
                {
                    // DEBUG: Log first pixel
                    if (x == 0 && z == 0)
                    {
                        _sapi.Logger.Notification($"[VintageAtlas] 🔍 First pixel: CHUNK_SIZE={CHUNK_SIZE}, heightMap.Length={heightMap.Length}");
                    }
                    
                    var heightIndex = z * CHUNK_SIZE + x;
                    if (heightIndex >= heightMap.Length) continue;

                    var height = heightMap[heightIndex];
                    
                    // DEBUG: Log first pixel's height
                    if (x == 0 && z == 0)
                    {
                        _sapi.Logger.Notification($"[VintageAtlas] 📏 First pixel height: {height}, chunkY={snapshot.ChunkY}");
                    }
                    
                    if (height == 0)
                    {
                        if (x == 0 && z == 0)
                        {
                            _sapi.Logger.Warning($"[VintageAtlas] ⚠️  First pixel height is ZERO! Skipping.");
                        }
                        continue;
                    }

                    // ═══════════════════════════════════════════════════════════════
                    // SURFACE BLOCK RENDERING (from SavegameDataSource)
                    // BlockIds are stored at their actual height's local Y position
                    // e.g., height=114 → localY=114%32=18 → blockIndex at Y=18
                    // ═══════════════════════════════════════════════════════════════
                    var localY = height % CHUNK_SIZE; // Local Y within 32-block range
                    
                    // DEBUG: Log first pixel's localY
                    if (x == 0 && z == 0)
                    {
                        _sapi.Logger.Notification($"[VintageAtlas] 📐 First pixel localY: {localY} (height%CHUNK_SIZE)");
                    }

                    var blockIndex = localY * CHUNK_SIZE * CHUNK_SIZE + z * CHUNK_SIZE + x;
                    
                    // DEBUG: Log first pixel's blockIndex
                    if (x == 0 && z == 0)
                    {
                        _sapi.Logger.Notification($"[VintageAtlas] 🔢 First pixel blockIndex: {blockIndex} (blockIds.Length={blockIds.Length})");
                    }
                    
                    if (blockIndex < 0 || blockIndex >= blockIds.Length)
                    {
                        if (x == 0 && z == 0)
                        {
                            _sapi.Logger.Warning($"[VintageAtlas] ⚠️  First pixel blockIndex OUT OF RANGE! Drawing fallback.");
                        }
                        paint.Color = new SKColor(172, 136, 88);
                        canvas.DrawPoint(offsetX + x, offsetZ + z, paint);
                        continue;
                    }

                    var blockId = blockIds[blockIndex];
                    
                    // DEBUG: Log first pixel's blockId
                    if (x == 0 && z == 0)
                    {
                        _sapi.Logger.Notification($"[VintageAtlas] 🧱 First pixel blockId: {blockId}");
                    }

                    // Get color based on render mode
                    uint color;
                    var imgX = offsetX + x;
                    var imgZ = offsetZ + z;

                    // DEBUG: Log mode ONCE per tile
                    if (x == 0 && z == 0 && offsetX == 0 && offsetZ == 0)
                    {
                        _sapi.Logger.Notification($"[VintageAtlas] 🎨 RENDERING MODE: {_config.Mode} ({(int)_config.Mode})");
                    }

                    switch (_config.Mode)
                    {
                        case ImageMode.OnlyOneColor:
                            color = _colorCache.GetBaseColor(blockId);
                            break;

                        case ImageMode.ColorVariations:
                            color = _colorCache.GetRandomColorVariation(blockId, random);
                            break;

                        case ImageMode.ColorVariationsWithHeight:
                            color = _colorCache.GetRandomColorVariation(blockId, random);
                            // Apply height-based darkening/lightening
                            color = (uint)ColorUtil.ColorMultiply3Clamped((int)color, height / (float)mapYHalf);
                            break;

                        case ImageMode.ColorVariationsWithHillShading:
                            color = _colorCache.GetRandomColorVariation(blockId, random);
                            
                            // DEBUG: Log first pixel of first tile only
                            if (x == 0 && z == 0 && imgX == 0 && imgZ == 0)
                            {
                                _sapi.Logger.Notification($"[VintageAtlas] MODE 3 RENDERING: blockId={blockId}, color=0x{color:X8}");
                            }
                            
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
                            var isWaterEdge = DetectWaterEdge(blockId, x, z, snapshot, tileData);
                            color = _colorCache.GetMedievalStyleColor(blockId, isWaterEdge);
                            
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
    public async Task<Storage.TileExtent?> GetTileExtentAsync(int zoom)
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
        if (_memoryCache.Count >= MAX_CACHE_SIZE)
        {
            var oldest = DateTime.MaxValue;
            string? oldestKey = null;

            foreach (var kvp in _memoryCache)
            {
                if (kvp.Value.LastModified < oldest)
                {
                    oldest = kvp.Value.LastModified;
                    oldestKey = kvp.Key;
                }
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

    private async Task<byte[]?> GeneratePlaceholderTileAsync(int zoom, int tileX, int tileZ)
    {
        return await Task.Run(() =>
        {
            try
            {
                var tileSize = _config.TileSize;
                using var bitmap = new SKBitmap(tileSize, tileSize);
                using var canvas = new SKCanvas(bitmap);

                canvas.Clear(new SKColor(41, 128, 185));

                using var gridPaint = new SKPaint();
                gridPaint.Color = new SKColor(52, 152, 219);
                gridPaint.StrokeWidth = 1;
                gridPaint.Style = SKPaintStyle.Stroke;

                for (var i = 0; i < tileSize; i += 32)
                {
                    canvas.DrawLine(i, 0, i, tileSize, gridPaint);
                    canvas.DrawLine(0, i, tileSize, i, gridPaint);
                }

                using var textPaint = new SKPaint
                {
                    Color = SKColors.White,
                    IsAntialias = true
                };

                using var font = new SKFont();
                font.Size = 12;
                var text = $"z{zoom} x{tileX} z{tileZ}";
                canvas.DrawText(text, 10, 20, SKTextAlign.Left, font, textPaint);

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                return data.ToArray();
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to generate placeholder: {ex.Message}");
                return null;
            }
        });
    }

    private static string GenerateETag(byte[] data, DateTime lastModified)
    {
        return $"\"{data.Length}-{lastModified.Ticks}\"";
    }

    /// <summary>
    /// Calculate altitude differences with neighboring blocks for hill shading.
    /// Optimized to stay within chunk boundaries.
    /// </summary>
    private (int northWestDelta, int northDelta, int westDelta) CalculateAltitudeDiff(
        int x, int y, int z, int[] heightMap)
    {
        var westernX = x - 1;
        var northernZ = z - 1;

        // Stay within chunk boundaries to avoid loading neighbors
        if (westernX < 0) westernX++;
        if (northernZ < 0) northernZ++;

        westernX = GameMath.Mod(westernX, CHUNK_SIZE);
        northernZ = GameMath.Mod(northernZ, CHUNK_SIZE);

        var northWestIndex = northernZ * CHUNK_SIZE + westernX;
        var northIndex = northernZ * CHUNK_SIZE + x;
        var westIndex = z * CHUNK_SIZE + westernX;

        var northWestHeight = northWestIndex < heightMap.Length ? heightMap[northWestIndex] : y;
        var northHeight = northIndex < heightMap.Length ? heightMap[northIndex] : y;
        var westHeight = westIndex < heightMap.Length ? heightMap[westIndex] : y;

        return (y - northWestHeight, y - northHeight, y - westHeight);
    }

    /// <summary>
    /// Calculate slope boost multiplier for shadow map based on altitude deltas.
    /// Returns a value to darken or lighten the pixel based on slope direction.
    /// </summary>
    private float CalculateSlopeBoost(int northWestDelta, int northDelta, int westDelta)
    {
        var direction = Math.Sign(northWestDelta) + Math.Sign(northDelta) + Math.Sign(westDelta);
        float steepness = Math.Max(Math.Max(Math.Abs(northWestDelta), Math.Abs(northDelta)), Math.Abs(westDelta));
        var slopeFactor = Math.Min(0.5f, steepness / 10f) / 1.25f;

        if (direction > 0)
            return 1.08f + slopeFactor; // Brighten slopes facing light
        if (direction < 0)
            return 0.92f - slopeFactor; // Darken slopes away from light
        return 1; // Flat terrain
    }

    /// <summary>
    /// Detect if a water block is at the edge (borders non-water blocks).
    /// Used for medieval style rendering to draw darker water edges.
    /// </summary>
    private bool DetectWaterEdge(int blockId, int x, int z, ChunkSnapshot snapshot, TileChunkData tileData)
    {
        if (!_colorCache.IsLake(blockId))
            return false;

        // Check boundaries - edges are always rendered as water
        if (x == 0 || x == CHUNK_SIZE - 1 || z == 0 || z == CHUNK_SIZE - 1)
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
        if (_colorCache.IsLake(neighborN) && _colorCache.IsLake(neighborS) &&
            _colorCache.IsLake(neighborE) && _colorCache.IsLake(neighborW))
        {
            return false;
        }

        // At least one neighbor is land - this is a water edge
        return true;
    }

    /// <summary>
    /// Get block ID at a specific X,Z position within a chunk using height map.
    /// </summary>
    private int GetBlockAtPosition(int x, int z, int[] heightMap, int[] blockIds)
    {
        if (x < 0 || x >= CHUNK_SIZE || z < 0 || z >= CHUNK_SIZE)
            return 0;

        var heightIndex = z * CHUNK_SIZE + x;
        if (heightIndex >= heightMap.Length)
            return 0;

        var height = heightMap[heightIndex];
        if (height == 0)
            return 0;

        // Calculate local Y and block index
        var localY = height % CHUNK_SIZE;
        var blockIndex = localY * CHUNK_SIZE * CHUNK_SIZE + z * CHUNK_SIZE + x;

        if (blockIndex < 0 || blockIndex >= blockIds.Length)
            return 0;

        return blockIds[blockIndex];
    }

    /// <summary>
    /// Apply shadow map blur and lighting to the final bitmap.
    /// This creates the hill shading effect.
    /// </summary>
    private unsafe void ApplyShadowMapToBitmap(SKBitmap bitmap, Span<byte> shadowMap, int size)
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

            if (shadowEffect == 0) continue;

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
        _storage?.Dispose();
    }

    #endregion
}

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
    public double PercentComplete => TotalTiles > 0 ? (TilesCompleted * 100.0 / TotalTiles) : 0;
}


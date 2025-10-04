using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using SkiaSharp;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Models;
using VintageAtlas.Storage;

namespace VintageAtlas.Export;

/// <summary>
/// Alternative implementation using MBTiles database storage instead of file system
/// This demonstrates how to use SQLite-based tile storage
/// </summary>
public class DynamicTileGenerator : ITileGenerator, IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly MbTilesStorage _storage;
    private readonly ChunkDataExtractor _extractor;
    private readonly BlockColorCache _colorCache;
    private readonly PyramidTileDownsampler _downsampler;

    // In-memory cache for frequently accessed tiles
    private readonly ConcurrentDictionary<string, CachedTile> _memoryCache = new();

    private const int CHUNK_SIZE = 32;
    private const int MAX_CACHE_SIZE = 100; // Keep 100 most recent tiles in memory

    public DynamicTileGenerator(ICoreServerAPI sapi, ModConfig config, BlockColorCache colorCache, MbTilesStorage storage)
    {
        _sapi = sapi;
        _config = config;
        _extractor = new ChunkDataExtractor(sapi, config);
        _colorCache = colorCache;
        _storage = storage; // Use shared storage

        // Initialize downsampler for lower zoom levels
        _downsampler = new PyramidTileDownsampler(sapi, config, this);

        _sapi.Logger.Notification("[VintageAtlas] DynamicTileGenerator initialized with shared storage");
    }

    /// <summary>
    /// Get tile data for ITileGenerator interface (used by PyramidTileDownsampler).
    /// Returns raw tile bytes or null if not found.
    /// </summary>
    public async Task<byte[]?> GetTileDataAsync(int zoom, int tileX, int tileZ)
    {
        var result = await GenerateTileAsync(zoom, tileX, tileZ, null);
        return result.NotFound ? null : result.Data;
    }

    /// <summary>
    /// Generate or retrieve a tile
    /// </summary>
    public async Task<TileResult> GenerateTileAsync(int zoom, int tileX, int tileZ, string? ifNoneMatch = null)
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
            // Found in database, cache in memory
            var lastModified = DateTime.UtcNow; // Could be stored separately
            var etag = GenerateETag(tileData, lastModified);
            
            CacheInMemory(tileKey, tileData, etag, lastModified);
            
            _sapi.Logger.Debug($"[VintageAtlas] ✅ Found tile in DB: {zoom}/{tileX}_{tileZ}, size={tileData.Length} bytes");
            
            return new TileResult
            {
                Data = tileData,
                ETag = etag,
                LastModified = lastModified,
                ContentType = "image/png"
            };
        }
        
        _sapi.Logger.Debug($"[VintageAtlas] ❌ Tile NOT in DB: {zoom}/{tileX}_{tileZ}, will generate new tile");

        // Generate new tile
        _sapi.Logger.Debug($"[VintageAtlas] Generating new tile: {zoom}/{tileX}_{tileZ}");
        
        // Log coordinate calculation details
        var chunksPerTile = _config.TileSize / CHUNK_SIZE;
        var startChunkX = tileX * chunksPerTile;
        var startChunkZ = tileZ * chunksPerTile;
        var startWorldX = startChunkX * CHUNK_SIZE;
        var startWorldZ = startChunkZ * CHUNK_SIZE;
        _sapi.Logger.Debug($"[VintageAtlas] Tile coords: ({tileX},{tileZ}) -> Chunk coords: ({startChunkX},{startChunkZ}) -> World coords: ({startWorldX},{startWorldZ})");
        
        try
        {
            byte[]? newTileData = null;
            
            if (zoom == _config.BaseZoomLevel)
            {
                // Base zoom: generate from world data (chunks)
                newTileData = await GenerateTileFromWorldDataAsync(zoom, tileX, tileZ);
            }
            else if (zoom < _config.BaseZoomLevel)
            {
                // Lower zoom levels: generate by downsampling from higher zoom
                newTileData = await _downsampler.GenerateTileByDownsamplingAsync(zoom, tileX, tileZ);
            }
            
            // Fallback to placeholder
            if (newTileData == null)
            {
                _sapi.Logger.Warning($"[VintageAtlas] ⚠️  Tile generation returned null, creating placeholder tile: {zoom}/{tileX}_{tileZ}");
                newTileData = await GeneratePlaceholderTileAsync(zoom, tileX, tileZ);
            }
            
            if (newTileData != null)
            {
                // Store in database
                await _storage.PutTileAsync(zoom, tileX, tileZ, newTileData);
                
                var lastModified = DateTime.UtcNow;
                var etag = GenerateETag(newTileData, lastModified);
                
                // Cache in memory
                CacheInMemory(tileKey, newTileData, etag, lastModified);
                
                _sapi.Logger.Debug($"[VintageAtlas] Generated and stored tile: {zoom}/{tileX}_{tileZ}");
                
                return new TileResult
                {
                    Data = newTileData,
                    ETag = etag,
                    LastModified = lastModified,
                    ContentType = "image/png"
                };
            }
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to generate tile {zoom}/{tileX}_{tileZ}: {ex.Message}");
        }

        return new TileResult { NotFound = true };
    }

    /// <summary>
    /// Invalidate a specific tile (forces regeneration on next request)
    /// </summary>
    public async Task InvalidateTileAsync(int zoom, int tileX, int tileZ)
    {
        var tileKey = $"{zoom}_{tileX}_{tileZ}";
        
        // Remove from memory cache
        _memoryCache.TryRemove(tileKey, out _);
        
        // Remove from database
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

        // Count per zoom level
        for (var z = 1; z <= _config.BaseZoomLevel; z++)
        {
            stats.TilesPerZoom[z] = await _storage.GetTileCountAsync(z);
        }

        return stats;
    }

    /// <summary>
    /// Get tile extent (min/max coordinates) for a specific zoom level from the database
    /// </summary>
    public async Task<Storage.TileExtent?> GetTileExtentAsync(int zoom)
    {
        return await _storage.GetTileExtentAsync(zoom);
    }

    private void CacheInMemory(string key, byte[] data, string etag, DateTime lastModified)
    {
        // Implement simple LRU cache by removing oldest entries
        if (_memoryCache.Count >= MAX_CACHE_SIZE)
        {
            // Find and remove oldest entry
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

    private async Task<byte[]?> GenerateTileFromWorldDataAsync(int zoom, int tileX, int tileZ)
    {
        try
        {
            // STEP 1: Extract chunk data on MAIN THREAD using TaskCompletionSource
            var tcs = new TaskCompletionSource<TileChunkData?>();
            
            _sapi.Event.EnqueueMainThreadTask(() =>
            {
                try
                {
                    var data = _extractor.ExtractTileData(zoom, tileX, tileZ);
                    tcs.SetResult(data);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, $"extract-tile-mbtiles-{zoom}-{tileX}-{tileZ}");
            
            var tileData = await tcs.Task;
            
            if (tileData == null)
            {
                _sapi.Logger.Warning($"[VintageAtlas] ⚠️  ChunkDataExtractor returned null for tile {zoom}/{tileX}_{tileZ}");
                return null;
            }
            
            if (tileData.Chunks.Count == 0)
            {
                _sapi.Logger.Warning($"[VintageAtlas] ⚠️  No chunks found for tile {zoom}/{tileX}_{tileZ} (area may not be explored yet)");
                return null;
            }
            
            _sapi.Logger.Debug($"[VintageAtlas] Extracted {tileData.Chunks.Count} chunks for tile {zoom}/{tileX}_{tileZ}");
            
            // STEP 2: Render tile on BACKGROUND THREAD
            return await Task.Run(() => RenderTileFromSnapshot(tileData));
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to generate tile from world data: {ex.Message}");
            return null;
        }
    }
    
    private byte[]? RenderTileFromSnapshot(TileChunkData tileData)
    {
        try
        {
            var tileSize = tileData.TileSize;
            var chunksPerTile = tileData.ChunksPerTileEdge;
            
            using var bitmap = new SKBitmap(tileSize, tileSize);
            using var canvas = new SKCanvas(bitmap);
            
            canvas.Clear(new SKColor(41, 128, 185)); // Ocean blue
            
            var chunksRendered = 0;
            var startChunkX = tileData.TileX * chunksPerTile;
            var startChunkZ = tileData.TileZ * chunksPerTile;
            
            _sapi.Logger.Debug($"[VintageAtlas] Rendering tile z{tileData.Zoom} t({tileData.TileX},{tileData.TileZ}), start chunk=({startChunkX},{startChunkZ}), total chunks={tileData.Chunks.Count}");
            
            for (var offsetX = 0; offsetX < chunksPerTile; offsetX++)
            {
                for (var offsetZ = 0; offsetZ < chunksPerTile; offsetZ++)
                {
                    var chunkX = startChunkX + offsetX;
                    var chunkZ = startChunkZ + offsetZ;
                    
                    var snapshot = tileData.GetChunk(chunkX, chunkZ, 0);
                    if (snapshot == null)
                    {
                        _sapi.Logger.VerboseDebug($"[VintageAtlas]   Chunk ({chunkX},{chunkZ}) is NULL");
                        continue;
                    }
                    if (!snapshot.IsLoaded)
                    {
                        _sapi.Logger.Warning($"[VintageAtlas]   ⚠️  Chunk ({chunkX},{chunkZ}) exists but IsLoaded=false!");
                        continue;
                    }
                    
                    RenderChunkSnapshotToTile(canvas, snapshot, 
                        offsetX * CHUNK_SIZE, offsetZ * CHUNK_SIZE);
                    chunksRendered++;
                }
            }
            
            _sapi.Logger.Debug($"[VintageAtlas] Rendered {chunksRendered}/{tileData.Chunks.Count} chunks successfully");
            
            if (chunksRendered == 0)
            {
                _sapi.Logger.Warning($"[VintageAtlas] ⚠️  NO chunks were rendered (all had IsLoaded=false or were null)!");
                return null;
            }
            
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to render tile from snapshot: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Render a chunk snapshot onto the tile canvas using block colors
    /// Uses BlockColorCache for proper terrain rendering
    /// </summary>
    private void RenderChunkSnapshotToTile(SKCanvas canvas, ChunkSnapshot snapshot, int offsetX, int offsetZ)
    {
        try
        {
            var heightMap = snapshot.HeightMap;
            var blockIds = snapshot.BlockIds;

            if (heightMap.Length == 0 || blockIds.Length == 0) return;

            using var paint = new SKPaint();
            var random = new Random(snapshot.ChunkX * 31 + snapshot.ChunkZ); // Deterministic per-chunk seed

            for (var x = 0; x < CHUNK_SIZE; x++)
            {
                for (var z = 0; z < CHUNK_SIZE; z++)
                {
                    var heightIndex = z * CHUNK_SIZE + x;
                    if (heightIndex >= heightMap.Length) continue;

                    var height = heightMap[heightIndex];
                    if (height == 0) continue; // Skip uninitialized areas

                    // Get the surface block at this position
                    // Height is absolute Y coordinate, convert to local Y within chunk
                    var localY = height - (snapshot.ChunkY * CHUNK_SIZE);

                    // Clamp to chunk bounds
                    if (localY < 0 || localY >= CHUNK_SIZE)
                    {
                        // Surface is in different Y chunk, use default color
                        paint.Color = new SKColor(172, 136, 88); // Default land color
                        canvas.DrawPoint(offsetX + x, offsetZ + z, paint);
                        continue;
                    }

                    // Get block ID at surface position
                    var blockIndex = localY * CHUNK_SIZE * CHUNK_SIZE + z * CHUNK_SIZE + x;
                    if (blockIndex < 0 || blockIndex >= blockIds.Length)
                    {
                        paint.Color = new SKColor(172, 136, 88); // Default land color
                        canvas.DrawPoint(offsetX + x, offsetZ + z, paint);
                        continue;
                    }

                    var blockId = blockIds[blockIndex];

                    // Get color based on render mode
                    uint color;
                    switch (_config.Mode)
                    {
                        case ImageMode.ColorVariations:
                        case ImageMode.ColorVariationsWithHeight:
                            color = _colorCache.GetRandomColorVariation(blockId, random);
                            break;

                        case ImageMode.MedievalStyleWithHillShading:
                            // TODO Phase 5: Implement water edge detection
                            color = _colorCache.GetMedievalStyleColor(blockId, false);
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
                    canvas.DrawPoint(offsetX + x, offsetZ + z, paint);
                }
            }
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to render chunk snapshot: {ex.Message}");
        }
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

    public void Dispose()
    {
        _storage?.Dispose();
    }
}

public class CachedTile
{
    public byte[] Data { get; set; } = [];
    public string ETag { get; set; } = "";
    public DateTime LastModified { get; set; }
}

public class StorageStats
{
    public long DatabaseSizeBytes { get; set; }
    public int MemoryCachedTiles { get; set; }
    public long TotalTiles { get; set; }
    public Dictionary<int, long> TilesPerZoom { get; set; } = new();
}


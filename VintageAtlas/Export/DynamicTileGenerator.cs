using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using SkiaSharp;
using Vintagestory.API.Server;
using Vintagestory.Server;
using Vintagestory.ServerMods;
using VintageAtlas.Core;
using VintageAtlas.Models;
using VintageAtlas.Storage;

namespace VintageAtlas.Export;

/// <summary>
/// Alternative implementation using MBTiles database storage instead of file system
/// This demonstrates how to use SQLite-based tile storage
/// </summary>
public class DynamicTileGenerator : IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly ServerMain _server;
    private readonly MBTilesStorage _storage;
    private readonly ChunkDataExtractor _extractor;

    // In-memory cache for frequently accessed tiles
    private readonly ConcurrentDictionary<string, CachedTile> _memoryCache = new();
    
    private const int CHUNK_SIZE = 32;
    private const int MAX_CACHE_SIZE = 100; // Keep 100 most recent tiles in memory

    public DynamicTileGenerator(ICoreServerAPI sapi, ModConfig config)
    {
        _sapi = sapi;
        _config = config;
        _server = (ServerMain)sapi.World;
        _extractor = new ChunkDataExtractor(sapi, config);
        
        // Initialize MBTiles storage
        var dbPath = System.IO.Path.Combine(config.OutputDirectory, "tiles.mbtiles");
        _storage = new MBTilesStorage(dbPath);
        
        _sapi.Logger.Notification($"[VintageAtlas] Using MBTiles storage: {dbPath}");
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
            
            _sapi.Logger.VerboseDebug($"[VintageAtlas] Serving cached tile from DB: {zoom}/{tileX}_{tileZ}");
            
            return new TileResult
            {
                Data = tileData,
                ETag = etag,
                LastModified = lastModified,
                ContentType = "image/png"
            };
        }

        // Generate new tile
        _sapi.Logger.Debug($"[VintageAtlas] Generating new tile: {zoom}/{tileX}_{tileZ}");
        
        try
        {
            byte[]? newTileData = null;
            
            // Only generate base zoom level from world data
            if (zoom == _config.BaseZoomLevel)
            {
                newTileData = await GenerateTileFromWorldDataAsync(zoom, tileX, tileZ);
            }
            
            // Fallback to placeholder
            if (newTileData == null)
            {
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
            
            if (tileData == null || tileData.Chunks.Count == 0)
            {
                return null;
            }
            
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
            
            for (var offsetX = 0; offsetX < chunksPerTile; offsetX++)
            {
                for (var offsetZ = 0; offsetZ < chunksPerTile; offsetZ++)
                {
                    var chunkX = startChunkX + offsetX;
                    var chunkZ = startChunkZ + offsetZ;
                    
                    var snapshot = tileData.GetChunk(chunkX, chunkZ, 0);
                    if (snapshot != null && snapshot.IsLoaded)
                    {
                        RenderChunkSnapshotToTile(canvas, snapshot, 
                            offsetX * CHUNK_SIZE, offsetZ * CHUNK_SIZE);
                        chunksRendered++;
                    }
                }
            }
            
            if (chunksRendered == 0)
            {
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
    /// Render a chunk snapshot onto the tile canvas
    /// TODO: Update this file to use new ChunkSnapshot pattern like main DynamicTileGenerator
    /// </summary>
    private void RenderChunkSnapshotToTile(SKCanvas canvas, ChunkSnapshot snapshot, int offsetX, int offsetZ)
    {
        try
        {
            var heightMap = snapshot.HeightMap;
            if (heightMap.Length == 0) return;
            
            using var paint = new SKPaint();
            
            for (var x = 0; x < CHUNK_SIZE; x++)
            {
                for (var z = 0; z < CHUNK_SIZE; z++)
                {
                    var heightIndex = z * CHUNK_SIZE + x;
                    if (heightIndex >= heightMap.Length) continue;
                    
                    var height = heightMap[heightIndex];
                    if (height == 0) continue; // Skip uninitialized areas
                    
                    // Normalize height to 0-1 based on typical surface range (64-192)
                    var normalizedHeight = Math.Clamp((height - 64.0f) / 128.0f, 0, 1);
                    var gray = (byte)(normalizedHeight * 255);
                    
                    paint.Color = new SKColor(gray, gray, gray);
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
                
                using var gridPaint = new SKPaint
                {
                    Color = new SKColor(52, 152, 219),
                    StrokeWidth = 1,
                    Style = SKPaintStyle.Stroke
                };

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

                using var font = new SKFont { Size = 12 };
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
    public byte[] Data { get; set; } = Array.Empty<byte>();
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


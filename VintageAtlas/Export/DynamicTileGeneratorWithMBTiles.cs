using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using SkiaSharp;
using Vintagestory.API.Server;
using Vintagestory.Server;
using VintageAtlas.Core;
using VintageAtlas.Storage;

namespace VintageAtlas.Export;

/// <summary>
/// Alternative implementation using MBTiles database storage instead of file system
/// This demonstrates how to use SQLite-based tile storage
/// </summary>
public class DynamicTileGeneratorWithMBTiles : IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly ServerMain _server;
    private readonly MBTilesStorage _storage;

    // In-memory cache for frequently accessed tiles
    private readonly ConcurrentDictionary<string, CachedTile> _memoryCache = new();
    
    private const int CHUNK_SIZE = 32;
    private const int MAX_CACHE_SIZE = 100; // Keep 100 most recent tiles in memory

    public DynamicTileGeneratorWithMBTiles(ICoreServerAPI sapi, ModConfig config)
    {
        _sapi = sapi;
        _config = config;
        _server = (ServerMain)sapi.World;
        
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
        for (int z = 1; z <= _config.BaseZoomLevel; z++)
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
        return await Task.Run(() =>
        {
            try
            {
                var tileSize = _config.TileSize;
                var chunksPerTile = tileSize / CHUNK_SIZE;
                
                var startChunkX = tileX * chunksPerTile;
                var startChunkZ = tileZ * chunksPerTile;
                
                using var bitmap = new SKBitmap(tileSize, tileSize);
                using var canvas = new SKCanvas(bitmap);
                
                canvas.Clear(new SKColor(41, 128, 185)); // Ocean blue
                
                var chunksRendered = 0;
                
                for (var chunkOffsetX = 0; chunkOffsetX < chunksPerTile; chunkOffsetX++)
                {
                    for (var chunkOffsetZ = 0; chunkOffsetZ < chunksPerTile; chunkOffsetZ++)
                    {
                        var chunkX = startChunkX + chunkOffsetX;
                        var chunkZ = startChunkZ + chunkOffsetZ;
                        
                        var mapChunk = _server.WorldMap.GetMapChunk(chunkX, chunkZ);
                        
                        if (mapChunk != null)
                        {
                            RenderChunkToTile(canvas, mapChunk, chunkOffsetX * CHUNK_SIZE, chunkOffsetZ * CHUNK_SIZE);
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
                _sapi.Logger.Error($"[VintageAtlas] Failed to generate tile from world data: {ex.Message}");
                return null;
            }
        });
    }

    private void RenderChunkToTile(SKCanvas canvas, IMapChunk mapChunk, int offsetX, int offsetZ)
    {
        try
        {
            var heightMap = mapChunk.RainHeightMap;
            if (heightMap == null) return;
            
            for (var x = 0; x < CHUNK_SIZE; x++)
            {
                for (var z = 0; z < CHUNK_SIZE; z++)
                {
                    var heightIndex = z * CHUNK_SIZE + x;
                    if (heightIndex >= heightMap.Length) continue;
                    
                    var height = heightMap[heightIndex];
                    var normalizedHeight = Math.Clamp(height / 255.0f, 0, 1);
                    var gray = (byte)(normalizedHeight * 255);
                    
                    var color = new SKColor(gray, gray, gray);
                    using var paint = new SKPaint { Color = color };
                    canvas.DrawPoint(offsetX + x, offsetZ + z, paint);
                }
            }
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to render chunk: {ex.Message}");
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


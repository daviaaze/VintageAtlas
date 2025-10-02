using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Server;
using VintageAtlas.Core;

namespace VintageAtlas.Export;

/// <summary>
/// Generates map tiles dynamically on-demand or for specific chunks
/// Enables incremental updates instead of full map regeneration
/// </summary>
public class DynamicTileGenerator
{
    private readonly ICoreServerAPI _sapi;
    private readonly ServerMain _server;
    private readonly ModConfig _config;
    private readonly Extractor _extractor;
    
    // Cache tile metadata for ETags and conditional requests
    private readonly ConcurrentDictionary<string, TileMetadata> _tileCache;
    
    private readonly int _chunkSize = 32;

    public DynamicTileGenerator(ICoreServerAPI sapi, ModConfig config)
    {
        _sapi = sapi;
        _server = (ServerMain)sapi.World;
        _config = config;
        _extractor = new Extractor(_server, _config, _sapi.Logger);
        _tileCache = new ConcurrentDictionary<string, TileMetadata>();
    }

    /// <summary>
    /// Generate or update a specific tile based on zoom level and coordinates
    /// </summary>
    public async Task<TileResult> GenerateTileAsync(int zoom, int tileX, int tileZ, string? ifNoneMatch = null)
    {
        var tileKey = $"{zoom}_{tileX}_{tileZ}";
        
        // Check cache first
        if (_tileCache.TryGetValue(tileKey, out var metadata))
        {
            // Check if client has cached version
            if (ifNoneMatch == metadata.ETag)
            {
                return new TileResult 
                { 
                    NotModified = true, 
                    ETag = metadata.ETag 
                };
            }
            
            // Check if file exists and is current
            var tilePath = GetTilePath(zoom, tileX, tileZ);
            if (File.Exists(tilePath) && File.GetLastWriteTimeUtc(tilePath) == metadata.LastModified)
            {
                return new TileResult
                {
                    Data = await File.ReadAllBytesAsync(tilePath),
                    ETag = metadata.ETag,
                    LastModified = metadata.LastModified,
                    ContentType = "image/png"
                };
            }
        }

        // Generate tile
        var result = await GenerateTileInternalAsync(zoom, tileX, tileZ);
        
        // Update cache
        if (result.Data != null && result.ETag != null)
        {
            _tileCache[tileKey] = new TileMetadata
            {
                ETag = result.ETag,
                LastModified = result.LastModified,
                Size = result.Data.Length
            };
        }

        return result;
    }

    /// <summary>
    /// Regenerate tiles for specific chunks that have been modified
    /// </summary>
    public async Task RegenerateTilesForChunksAsync(List<Vec2i> modifiedChunks)
    {
        _sapi.Logger.Notification($"[VintageAtlas] Regenerating tiles for {modifiedChunks.Count} modified chunks");

        var affectedTiles = CalculateAffectedTiles(modifiedChunks);
        
        var tasks = new List<Task>();
        foreach (var (zoom, tiles) in affectedTiles)
        {
            foreach (var (tileX, tileZ) in tiles)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await GenerateTileInternalAsync(zoom, tileX, tileZ);
                        _sapi.Logger.Debug($"[VintageAtlas] Regenerated tile: zoom={zoom}, x={tileX}, z={tileZ}");
                    }
                    catch (Exception ex)
                    {
                        _sapi.Logger.Error($"[VintageAtlas] Failed to regenerate tile {zoom}/{tileX}_{tileZ}: {ex.Message}");
                    }
                }));
                
                // Limit concurrent operations
                if (tasks.Count >= (_config.MaxDegreeOfParallelism == -1 ? Environment.ProcessorCount : _config.MaxDegreeOfParallelism))
                {
                    await Task.WhenAny(tasks);
                    tasks.RemoveAll(t => t.IsCompleted);
                }
            }
        }

        await Task.WhenAll(tasks);
        
        _sapi.Logger.Notification($"[VintageAtlas] Completed tile regeneration for {modifiedChunks.Count} chunks");
    }

    private async Task<TileResult> GenerateTileInternalAsync(int zoom, int tileX, int tileZ)
    {
        // For now, check if tile already exists (full implementation would regenerate from world data)
        var tilePath = GetTilePath(zoom, tileX, tileZ);
        
        if (File.Exists(tilePath))
        {
            var data = await File.ReadAllBytesAsync(tilePath);
            var lastModified = File.GetLastWriteTimeUtc(tilePath);
            var etag = GenerateETag(data, lastModified);
            
            return new TileResult
            {
                Data = data,
                ETag = etag,
                LastModified = lastModified,
                ContentType = "image/png"
            };
        }

        // Tile doesn't exist - return 404
        return new TileResult { NotFound = true };
    }

    private Dictionary<int, HashSet<(int tileX, int tileZ)>> CalculateAffectedTiles(List<Vec2i> modifiedChunks)
    {
        var affectedTiles = new Dictionary<int, HashSet<(int, int)>>();
        
        var tileSize = _config.TileSize;
        var chunksPerTile = tileSize / _chunkSize; // 256 / 32 = 8 chunks per tile
        
        // Calculate for base zoom level
        for (int zoom = _config.BaseZoomLevel; zoom >= 1; zoom--)
        {
            affectedTiles[zoom] = new HashSet<(int, int)>();
            
            var zoomDivisor = (int)Math.Pow(2, _config.BaseZoomLevel - zoom);
            
            foreach (var chunk in modifiedChunks)
            {
                // Calculate which tile this chunk belongs to at this zoom level
                var tileX = (chunk.X / chunksPerTile) / zoomDivisor;
                var tileZ = (chunk.Y / chunksPerTile) / zoomDivisor;
                
                affectedTiles[zoom].Add((tileX, tileZ));
            }
        }

        return affectedTiles;
    }

    private string GetTilePath(int zoom, int tileX, int tileZ)
    {
        var zoomDir = Path.Combine(_config.OutputDirectoryWorld, zoom.ToString());
        return Path.Combine(zoomDir, $"{tileX}_{tileZ}.png");
    }

    private string GenerateETag(byte[] data, DateTime lastModified)
    {
        // Simple ETag based on size and timestamp
        var hash = $"{data.Length}-{lastModified.Ticks}";
        return $"\"{hash}\"";
    }

    /// <summary>
    /// Get tile metadata without loading the full tile
    /// </summary>
    public TileMetadata? GetTileMetadata(int zoom, int tileX, int tileZ)
    {
        var tileKey = $"{zoom}_{tileX}_{tileZ}";
        
        if (_tileCache.TryGetValue(tileKey, out var metadata))
        {
            return metadata;
        }

        // Load from disk
        var tilePath = GetTilePath(zoom, tileX, tileZ);
        if (File.Exists(tilePath))
        {
            var fileInfo = new FileInfo(tilePath);
            var lastModified = fileInfo.LastWriteTimeUtc;
            var etag = GenerateETag(new byte[] { }, lastModified);
            
            metadata = new TileMetadata
            {
                ETag = etag,
                LastModified = lastModified,
                Size = fileInfo.Length
            };
            
            _tileCache[tileKey] = metadata;
            return metadata;
        }

        return null;
    }

    /// <summary>
    /// Clear tile cache (useful after manual regeneration)
    /// </summary>
    public void ClearCache()
    {
        _tileCache.Clear();
        _sapi.Logger.Debug("[VintageAtlas] Tile cache cleared");
    }
}

/// <summary>
/// Result of tile generation
/// </summary>
public class TileResult
{
    public byte[]? Data { get; set; }
    public string? ETag { get; set; }
    public DateTime LastModified { get; set; }
    public string? ContentType { get; set; }
    public bool NotModified { get; set; }
    public bool NotFound { get; set; }
}

/// <summary>
/// Metadata for caching tiles
/// </summary>
public class TileMetadata
{
    public string ETag { get; set; } = "";
    public DateTime LastModified { get; set; }
    public long Size { get; set; }
}


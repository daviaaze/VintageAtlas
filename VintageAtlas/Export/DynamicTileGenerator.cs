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
using Vintagestory.ServerMods;
using VintageAtlas.Core;
using Vintagestory.API.Common;

namespace VintageAtlas.Export;

/// <summary>
/// Generates map tiles dynamically on-demand or for specific chunks
/// Enables incremental updates instead of full map regeneration
/// 
/// THREADING CONSTRAINTS (vintagestory-modding-constraints.md):
/// - Chunk access MUST be on main thread
/// - Extract data on main thread, render on background thread
/// - Cannot cache chunk references
/// </summary>
public class DynamicTileGenerator(ICoreServerAPI sapi, ModConfig config)
{
    private readonly ChunkDataExtractor _extractor = new(sapi, config);

    // Cache tile metadata for ETags and conditional requests
    private readonly ConcurrentDictionary<string, TileMetadata> _tileCache = new();

    private const int CHUNK_SIZE = 32;

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
        if (result is { Data: not null, ETag: not null })
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
        sapi.Logger.Notification($"[VintageAtlas] Regenerating tiles for {modifiedChunks.Count} modified chunks");

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
                        sapi.Logger.Debug($"[VintageAtlas] Regenerated tile: zoom={zoom}, x={tileX}, z={tileZ}");
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Error($"[VintageAtlas] Failed to regenerate tile {zoom}/{tileX}_{tileZ}: {ex.Message}");
                    }
                }));
                
                // Limit concurrent operations
                if (tasks.Count >= (config.MaxDegreeOfParallelism == -1 ? Environment.ProcessorCount : config.MaxDegreeOfParallelism))
                {
                    await Task.WhenAny(tasks);
                    tasks.RemoveAll(t => t.IsCompleted);
                }
            }
        }

        await Task.WhenAll(tasks);
        
        sapi.Logger.Notification($"[VintageAtlas] Completed tile regeneration for {modifiedChunks.Count} chunks");
    }

    private async Task<TileResult> GenerateTileInternalAsync(int zoom, int tileX, int tileZ)
    {
        var tilePath = GetTilePath(zoom, tileX, tileZ);
        
        // Check if tile already exists
        if (File.Exists(tilePath))
        {
            var data = await File.ReadAllBytesAsync(tilePath);
            var lastModified = File.GetLastWriteTimeUtc(tilePath);
            var etag = GenerateETag(data, lastModified);
            
            sapi.Logger.VerboseDebug($"[VintageAtlas] Serving cached tile: {zoom}/{tileX}_{tileZ}");
            
            return new TileResult
            {
                Data = data,
                ETag = etag,
                LastModified = lastModified,
                ContentType = "image/png"
            };
        }

        // Tile doesn't exist - try to generate it on-demand from world data
        sapi.Logger.Debug($"[VintageAtlas] Generating missing tile on-demand: {zoom}/{tileX}_{tileZ}");
        
        try
        {
            byte[]? tileData = null;
            
            // Only generate base zoom level from world data
            // Other zoom levels require the export command (too complex for on-demand)
            if (zoom == config.BaseZoomLevel)
            {
                tileData = await GenerateTileFromWorldDataAsync(zoom, tileX, tileZ);
            }
            
            // Fallback to placeholder if generation failed or not base zoom
            if (tileData == null)
            {
                sapi.Logger.VerboseDebug($"[VintageAtlas] Using placeholder for zoom {zoom} (only base zoom {config.BaseZoomLevel} generated from world data)");
                tileData = await GeneratePlaceholderTileAsync(zoom, tileX, tileZ);
            }
            
            if (tileData != null)
            {
                // Save the generated tile to disk
                Directory.CreateDirectory(Path.GetDirectoryName(tilePath) ?? "");
                await File.WriteAllBytesAsync(tilePath, tileData);
                
                var lastModified = DateTime.UtcNow;
                var etag = GenerateETag(tileData, lastModified);
                
                sapi.Logger.Debug($"[VintageAtlas] Generated and cached new tile: {zoom}/{tileX}_{tileZ}");
                
                return new TileResult
                {
                    Data = tileData,
                    ETag = etag,
                    LastModified = lastModified,
                    ContentType = "image/png"
                };
            }
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Failed to generate tile {zoom}/{tileX}_{tileZ}: {ex.Message}");
            sapi.Logger.Error(ex.StackTrace ?? "");
        }

        // If generation failed, return not found
        return new TileResult { NotFound = true };
    }
    
    /// <summary>
    /// Generate a tile from actual world chunk data
    /// Only works for base zoom level - other zoom levels need full export
    /// 
    /// FOLLOWS VS CONSTRAINT: Extract on main thread, render on background
    /// </summary>
    private async Task<byte[]?> GenerateTileFromWorldDataAsync(int zoom, int tileX, int tileZ)
    {
        try
        {
            // STEP 1: Extract chunk data on MAIN THREAD
            // This is required by Vintage Story API constraints
            TileChunkData? tileData = null;
            
            await sapi.Event.EnqueueMainThreadTask(() =>
            {
                tileData = _extractor.ExtractTileData(zoom, tileX, tileZ);
                return true;
            }, $"extract-tile-{zoom}-{tileX}-{tileZ}");
            
            if (tileData == null || tileData.Chunks.Count == 0)
            {
                sapi.Logger.VerboseDebug($"[VintageAtlas] No chunks available for tile {zoom}/{tileX}_{tileZ}");
                return null;
            }
            
            // STEP 2: Render tile on BACKGROUND THREAD
            // Heavy computation can now happen off main thread
            return await Task.Run(() => RenderTileFromSnapshot(tileData));
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Failed to generate tile from world data: {ex.Message}");
            sapi.Logger.Error(ex.StackTrace ?? "");
            return null;
        }
    }
    
    /// <summary>
    /// Render a tile from extracted chunk snapshots
    /// Can run on background thread since it uses snapshots, not live chunks
    /// </summary>
    private byte[]? RenderTileFromSnapshot(TileChunkData tileData)
    {
        try
        {
            var tileSize = tileData.TileSize;
            var chunksPerTile = tileData.ChunksPerTileEdge;
            
            // Create bitmap for this tile
            using var bitmap = new SKBitmap(tileSize, tileSize);
            using var canvas = new SKCanvas(bitmap);
            
            // Fill with ocean blue as base (for areas without chunks)
            canvas.Clear(new SKColor(41, 128, 185));
            
            var chunksRendered = 0;
            
            // Render each chunk onto the tile
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
                return null; // No chunks rendered
            }
            
            sapi.Logger.VerboseDebug(
                $"[VintageAtlas] Rendered {chunksRendered}/{chunksPerTile * chunksPerTile} chunks for tile z{tileData.Zoom} t({tileData.TileX},{tileData.TileZ})");
            
            // Encode to PNG
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Failed to render tile from snapshot: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Render a chunk snapshot onto the tile canvas
    /// Uses extracted snapshot data, so can run safely on background thread
    /// 
    /// TODO: This is currently a simplified heightmap renderer
    /// Phase 2-5 will add: color mapping, render modes, hill shading, medieval style
    /// </summary>
    private void RenderChunkSnapshotToTile(SKCanvas canvas, ChunkSnapshot snapshot, int offsetX, int offsetZ)
    {
        try
        {
            // For now, render using heightmap (grayscale)
            // TODO Phase 2: Add block color mapping
            // TODO Phase 3: Add render modes
            // TODO Phase 4: Add hill shading
            // TODO Phase 5: Add medieval style with water edges
            
            var heightMap = snapshot.HeightMap;
            if (heightMap.Length == 0) return;
            
            using var paint = new SKPaint();
            
            for (var x = 0; x < CHUNK_SIZE; x++)
            {
                for (var z = 0; z < CHUNK_SIZE; z++)
                {
                    var heightIndex = z * CHUNK_SIZE + x;
                    if (heightIndex >= heightMap.Length) continue;
                    
                    // Get height and convert to grayscale color
                    var height = heightMap[heightIndex];
                    var normalizedHeight = Math.Clamp(height / 255.0f, 0, 1);
                    var gray = (byte)(normalizedHeight * 255);
                    
                    // Create a grayscale color based on height
                    paint.Color = new SKColor(gray, gray, gray);
                    canvas.DrawPoint(offsetX + x, offsetZ + z, paint);
                }
            }
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Failed to render chunk snapshot: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Generate a placeholder tile for missing areas or unsupported zoom levels
    /// </summary>
    private async Task<byte[]?> GeneratePlaceholderTileAsync(int zoom, int tileX, int tileZ)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Create a simple placeholder tile (ocean blue color)
                var tileSize = config.TileSize;
                using var bitmap = new SKBitmap(tileSize, tileSize);
                using var canvas = new SKCanvas(bitmap);
                
                // Fill with ocean blue
                var oceanBlue = new SKColor(41, 128, 185);
                canvas.Clear(oceanBlue);
                
                // Add grid lines for debugging
                using var gridPaint = new SKPaint();
                gridPaint.Color = new SKColor(52, 152, 219);
                gridPaint.StrokeWidth = 1;
                gridPaint.Style = SKPaintStyle.Stroke;

                // Draw grid
                for (var i = 0; i < tileSize; i += 32)
                {
                    canvas.DrawLine(i, 0, i, tileSize, gridPaint);
                    canvas.DrawLine(0, i, tileSize, i, gridPaint);
                }
                
                // Add coordinates text
                using var textPaint = new SKPaint();
                textPaint.Color = SKColors.White;
                textPaint.IsAntialias = true;

                using var font = new SKFont();
                font.Size = 12;

                var text = $"z{zoom} x{tileX} z{tileZ}";
                canvas.DrawText(text, 10, 20, SKTextAlign.Left, font, textPaint);
                
                // Encode to PNG
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                return data.ToArray();
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[VintageAtlas] Failed to generate placeholder tile: {ex.Message}");
                return null;
            }
        });
    }

    private Dictionary<int, HashSet<(int tileX, int tileZ)>> CalculateAffectedTiles(List<Vec2i> modifiedChunks)
    {
        var affectedTiles = new Dictionary<int, HashSet<(int, int)>>();
        
        var tileSize = config.TileSize;
        var chunksPerTile = tileSize / CHUNK_SIZE; // 256 / 32 = 8 chunks per tile
        
        // Calculate for base zoom level
        for (var zoom = config.BaseZoomLevel; zoom >= 1; zoom--)
        {
            affectedTiles[zoom] = [];
            
            var zoomDivisor = (int)Math.Pow(2, config.BaseZoomLevel - zoom);
            
            foreach (var chunk in modifiedChunks)
            {
                // Calculate which tile this chunk belongs to at this zoom level
                var tileX = chunk.X / chunksPerTile / zoomDivisor;
                var tileZ = chunk.Y / chunksPerTile / zoomDivisor;
                
                affectedTiles[zoom].Add((tileX, tileZ));
            }
        }

        return affectedTiles;
    }

    private string GetTilePath(int zoom, int tileX, int tileZ)
    {
        var zoomDir = Path.Combine(config.OutputDirectoryWorld, zoom.ToString());
        return Path.Combine(zoomDir, $"{tileX}_{tileZ}.png");
    }

    private static string GenerateETag(byte[] data, DateTime lastModified)
    {
        // Simple ETag based on size and timestamp
        var hash = $"{data.Length}-{lastModified.Ticks}";
        return $"\"{hash}\"";
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


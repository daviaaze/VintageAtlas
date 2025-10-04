using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Models;

namespace VintageAtlas.Export;

/// <summary>
/// Generates lower zoom level tiles by downsampling from higher zoom levels
/// This enables dynamic generation of all zoom levels from the base zoom
/// </summary>
public class PyramidTileDownsampler
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly ITileGenerator _generator;

    public PyramidTileDownsampler(
        ICoreServerAPI sapi, 
        ModConfig config, 
        ITileGenerator generator)
    {
        _sapi = sapi;
        _config = config;
        _generator = generator;
    }

    /// <summary>
    /// Generate a tile by downsampling from the next higher zoom level
    /// Each tile at zoom N is created from 4 tiles at zoom N+1
    /// </summary>
    public async Task<byte[]?> GenerateTileByDownsamplingAsync(int zoom, int tileX, int tileZ)
    {
        if (zoom >= _config.BaseZoomLevel)
        {
            // Can't downsample beyond base zoom
            return null;
        }

        try
        {
            _sapi.Logger.VerboseDebug($"[VintageAtlas] Downsampling tile {zoom}/{tileX}_{tileZ} from zoom {zoom + 1}");

            // Calculate coordinates for the 4 source tiles at the next zoom level
            // In a tile pyramid, each tile is made from a 2x2 grid of tiles from the next zoom
            var higherZoom = zoom + 1;
            var sourceX = tileX * 2;
            var sourceZ = tileZ * 2;

            // Fetch all 4 source tiles concurrently
            var sourceTileTasks = new Task<byte[]?>[]
            {
                _generator.GetTileDataAsync(higherZoom, sourceX, sourceZ),         // Top-left
                _generator.GetTileDataAsync(higherZoom, sourceX + 1, sourceZ),     // Top-right
                _generator.GetTileDataAsync(higherZoom, sourceX, sourceZ + 1),     // Bottom-left
                _generator.GetTileDataAsync(higherZoom, sourceX + 1, sourceZ + 1), // Bottom-right
            };

            var sourceTiles = await Task.WhenAll(sourceTileTasks);

            // ═══════════════════════════════════════════════════════════════
            // FORGIVING DOWNSAMPLING (matches old Extractor.cs behavior)
            // Create downsampled tile even if some source tiles are missing
            // Missing tiles will appear as transparent/empty areas
            // ═══════════════════════════════════════════════════════════════
            
            var missingCount = sourceTiles.Count(t => t == null);
            if (missingCount == 4)
            {
                // ALL 4 tiles missing - skip this entirely
                _sapi.Logger.Debug($"[VintageAtlas] All source tiles missing for {zoom}/{tileX}_{tileZ}, skipping");
                return null;
            }
            
            if (missingCount > 0)
            {
                _sapi.Logger.Debug($"[VintageAtlas] Downsampling with {4 - missingCount}/4 tiles available for {zoom}/{tileX}_{tileZ}");
            }

            // Downsample with whatever tiles we have (null tiles will be skipped)
            var downsampled = DownsampleTiles(sourceTiles);

            _sapi.Logger.VerboseDebug($"[VintageAtlas] Successfully downsampled tile {zoom}/{tileX}_{tileZ}");

            return downsampled;
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to downsample tile {zoom}/{tileX}_{tileZ}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Combine up to 4 tiles into 1 by downsampling with high-quality filtering.
    /// Missing tiles (null) are skipped, resulting in transparent areas.
    /// This matches the old Extractor.cs behavior for edge tiles.
    /// Layout: [0] [1]  →  [output]
    ///         [2] [3]
    /// </summary>
    private byte[] DownsampleTiles(byte[]?[] sourceTiles)
    {
        if (sourceTiles.Length != 4)
        {
            throw new ArgumentException("Expected 4 source tile slots for downsampling", nameof(sourceTiles));
        }

        var tileSize = _config.TileSize;
        var halfSize = tileSize / 2;

        // Create output bitmap (starts transparent)
        using var outputBitmap = new SKBitmap(tileSize, tileSize);
        using var canvas = new SKCanvas(outputBitmap);
        
        // Clear to transparent (matches old Extractor.cs: outputImage.Erase(SKColor.Empty))
        canvas.Clear(SKColor.Empty);

        // High-quality downsampling paint
        using var paint = new SKPaint
        {
            // Use modern sampling options instead of deprecated FilterQuality
            IsAntialias = true
        };

        // Process each of the 4 source tile slots
        for (var i = 0; i < 4; i++)
        {
            // Skip null tiles (missing at edges)
            if (sourceTiles[i] == null)
            {
                _sapi.Logger.VerboseDebug($"[VintageAtlas] Source tile {i} is null, leaving area transparent");
                continue;
            }
            
            try
            {
                using var sourceBitmap = SKBitmap.Decode(sourceTiles[i]!);
                
                if (sourceBitmap == null)
                {
                    _sapi.Logger.Warning($"[VintageAtlas] Failed to decode source tile {i} for downsampling");
                    continue;
                }

                // Calculate destination position in the output tile
                // Layout: 0=top-left, 1=top-right, 2=bottom-left, 3=bottom-right
                var destX = (i % 2) * halfSize;
                var destY = (i / 2) * halfSize;

                // Draw the source tile scaled down to half size
                canvas.DrawBitmap(
                    sourceBitmap,
                    SKRect.Create(0, 0, tileSize, tileSize),      // Source rectangle (full tile)
                    SKRect.Create(destX, destY, halfSize, halfSize), // Destination rectangle (quarter size)
                    paint
                );
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Error processing source tile {i}: {ex.Message}");
                // Continue with other tiles even if one fails
            }
        }

        // Encode to PNG with good compression (quality 85 is fine for downsampled tiles)
        using var image = SKImage.FromBitmap(outputBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 85);
        return data.ToArray();
    }

    /// <summary>
    /// Optimized version that downsamples multiple levels at once
    /// Useful for generating multiple zoom levels from base zoom
    /// </summary>
    public async Task<Dictionary<int, byte[]>> GeneratePyramidAsync(
        int baseZoom, 
        int minZoom, 
        int baseTileX, 
        int baseTileZ, 
        byte[] baseTileData)
    {
        var pyramid = new Dictionary<int, byte[]>
        {
            [baseZoom] = baseTileData
        };

        // Generate each zoom level from the previous one
        for (var zoom = baseZoom - 1; zoom >= minZoom; zoom--)
        {
            var tileX = baseTileX / (1 << (baseZoom - zoom));
            var tileZ = baseTileZ / (1 << (baseZoom - zoom));

            var downsampled = await GenerateTileByDownsamplingAsync(zoom, tileX, tileZ);
            
            if (downsampled != null)
            {
                pyramid[zoom] = downsampled;
            }
            else
            {
                _sapi.Logger.Warning($"[VintageAtlas] Failed to generate zoom {zoom} in pyramid");
                break;
            }
        }

        return pyramid;
    }

    /// <summary>
    /// Pre-generate all lower zoom level tiles from existing base zoom tiles
    /// This can be run as a background task after map export
    /// </summary>
    public async Task GenerateAllLowerZoomsAsync(Action<int, int> progressCallback)
    {
        _sapi.Logger.Notification($"[VintageAtlas] Starting pyramid generation from zoom {_config.BaseZoomLevel} to zoom 1");

        var totalGenerated = 0;
        var startTime = DateTime.UtcNow;

        // Process each zoom level from base-1 down to 1
        for (var zoom = _config.BaseZoomLevel - 1; zoom >= 1; zoom--)
        {
            var zoomGenerated = 0;
            
            // Calculate how many tiles exist at the level above
            var sourceZoom = zoom + 1;
            var sourceDir = System.IO.Path.Combine(_config.OutputDirectoryWorld, sourceZoom.ToString());
            
            if (!System.IO.Directory.Exists(sourceDir))
            {
                _sapi.Logger.Warning($"[VintageAtlas] Source directory not found for zoom {sourceZoom}, skipping pyramid generation");
                break;
            }

            // Get all source tiles at the higher zoom level
            var sourceFiles = System.IO.Directory.GetFiles(sourceDir, "*.png");
            var targetTiles = new HashSet<(int x, int z)>();

            // Calculate which tiles we need at the current zoom level
            foreach (var file in sourceFiles)
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(file);
                var parts = name.Split('_');
                
                if (parts.Length == 2 && 
                    int.TryParse(parts[0], out var sourceX) && 
                    int.TryParse(parts[1], out var sourceZ))
                {
                    var targetX = sourceX / 2;
                    var targetZ = sourceZ / 2;
                    targetTiles.Add((targetX, targetZ));
                }
            }

            _sapi.Logger.Notification($"[VintageAtlas] Generating {targetTiles.Count} tiles for zoom {zoom}");

            // Generate each unique target tile
            var tasks = new List<Task>();
            foreach (var (tileX, tileZ) in targetTiles)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var downsampled = await GenerateTileByDownsamplingAsync(zoom, tileX, tileZ);
                        
                        if (downsampled != null)
                        {
                            // Save to disk
                            var tilePath = System.IO.Path.Combine(
                                _config.OutputDirectoryWorld, 
                                zoom.ToString(), 
                                $"{tileX}_{tileZ}.png"
                            );
                            
                            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(tilePath) ?? "");
                            await System.IO.File.WriteAllBytesAsync(tilePath, downsampled);
                            
                            System.Threading.Interlocked.Increment(ref zoomGenerated);
                        }
                    }
                    catch (Exception ex)
                    {
                        _sapi.Logger.Error($"[VintageAtlas] Failed to generate tile {zoom}/{tileX}_{tileZ}: {ex.Message}");
                    }
                }));

                // Limit concurrent operations
                if (tasks.Count >= (_config.MaxDegreeOfParallelism == -1 ? Environment.ProcessorCount : _config.MaxDegreeOfParallelism))
                {
                    await Task.WhenAny(tasks);
                    tasks.RemoveAll(t => t.IsCompleted);
                }
            }

            await Task.WhenAll(tasks);

            totalGenerated += zoomGenerated;
            progressCallback?.Invoke(zoom, zoomGenerated);
            
            _sapi.Logger.Notification($"[VintageAtlas] Generated {zoomGenerated} tiles for zoom {zoom}");
        }

        var duration = DateTime.UtcNow - startTime;
        _sapi.Logger.Notification($"[VintageAtlas] Pyramid generation complete: {totalGenerated} tiles in {duration.TotalSeconds:F1}s");
    }
}


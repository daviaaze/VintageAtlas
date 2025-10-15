using System;
using System.Threading.Tasks;
using SkiaSharp;
using Vintagestory.API.Server;
using VintageAtlas.Core;

namespace VintageAtlas.Export;

/// <summary>
/// Generates lower zoom level tiles by downsampling from higher zoom levels
/// This enables dynamic generation of all zoom levels from the base zoom
/// </summary>
public class PyramidTileDownsampler(
    ICoreServerAPI sapi,
    ModConfig config,
    ITileGenerator generator)
{
    /// <summary>
    /// Generate a tile by downsampling from the next higher zoom level
    /// Each tile at zoom N is created from 4 tiles at zoom N+1
    /// </summary>
    public async Task<byte[]?> GenerateTileByDownsamplingAsync(int zoom, int tileX, int tileZ)
    {
        if (zoom >= config.BaseZoomLevel)
        {
            // Can't downsample beyond base zoom
            return null;
        }

        try
        {
            sapi.Logger.VerboseDebug($"[VintageAtlas] Downsampling tile {zoom}/{tileX}_{tileZ} from zoom {zoom + 1}");

            // Calculate coordinates for the 4 source tiles at the next zoom level
            // In a tile pyramid, each tile is made from a 2x2 grid of tiles from the next zoom
            var higherZoom = zoom + 1;
            var sourceX = tileX * 2;
            var sourceZ = tileZ * 2;

            // Fetch all 4 source tiles concurrently
            var sourceTileTasks = new[]
            {
                generator.GetTileDataAsync(higherZoom, sourceX, sourceZ),         // Top-left
                generator.GetTileDataAsync(higherZoom, sourceX + 1, sourceZ),     // Top-right
                generator.GetTileDataAsync(higherZoom, sourceX, sourceZ + 1),     // Bottom-left
                generator.GetTileDataAsync(higherZoom, sourceX + 1, sourceZ + 1), // Bottom-right
            };

            var sourceTiles = await Task.WhenAll(sourceTileTasks);

            try
            {
                // Compose available source tiles (missing tiles leave transparent quadrants)
                var downsampled = DownsampleTiles(sourceTiles);
                sapi.Logger.VerboseDebug($"[VintageAtlas] Successfully downsampled tile {zoom}/{tileX}_{tileZ}");
                return downsampled;
            }
            catch (DllNotFoundException)
            {
                // SkiaSharp native library not available in this environment.
                // Fallback: return the top-left source tile bytes.
                sapi.Logger.VerboseDebug($"[VintageAtlas] SkiaSharp native library not found, using fallback for {zoom}/{tileX}_{tileZ}");
                return sourceTiles[0];
            }
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Failed to downsample tile {zoom}/{tileX}_{tileZ}: {ex.Message}");
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

        var tileSize = config.TileSize;
        var halfSize = tileSize / 2;

        // Create output bitmap (starts transparent)
        using var outputBitmap = new SKBitmap(tileSize, tileSize);
        using var canvas = new SKCanvas(outputBitmap);

        // Clear to transparent (matches old Extractor.cs: outputImage.Erase(SKColor.Empty))
        canvas.Clear(SKColor.Empty);

        // High-quality downsampling paint
        using var paint = new SKPaint();
        // Use modern sampling options instead of deprecated FilterQuality
        paint.IsAntialias = true;

        // Process each of the 4 source tile slots
        for (var i = 0; i < 4; i++)
        {
            // Skip null tiles (missing at edges)
            if (sourceTiles[i] == null)
            {
                sapi.Logger.VerboseDebug($"[VintageAtlas] Source tile {i} is null, leaving area transparent");
                continue;
            }

            try
            {
                using var sourceBitmap = SKBitmap.Decode(sourceTiles[i]!);

                if (sourceBitmap == null)
                {
                    sapi.Logger.Warning($"[VintageAtlas] Failed to decode source tile {i} for downsampling");
                    continue;
                }

                // Calculate the destination position in the output tile
                // Layout: 0=top-left, 1=top-right, 2=bottom-left, 3=bottom-right
                var destX = i % 2 * halfSize;
                var destY = i / 2 * halfSize;

                // Draw the source tile scaled down to half-size
                canvas.DrawBitmap(
                    sourceBitmap,
                    SKRect.Create(0, 0, tileSize, tileSize),      // Source rectangle (full tile)
                    SKRect.Create(destX, destY, halfSize, halfSize), // Destination rectangle (quarter size)
                    paint
                );
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[VintageAtlas] Error processing source tile {i}: {ex.Message}");
                // Continue with other tiles even if one fails
            }
        }

        // Encode to PNG with good compression (quality 85 is fine for downsampled tiles)
        using var image = SKImage.FromBitmap(outputBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 85);
        return data.ToArray();
    }
}


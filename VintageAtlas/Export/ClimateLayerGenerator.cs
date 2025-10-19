using System;
using System.Threading.Tasks;
using SkiaSharp;
using VintageAtlas.Storage;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VintageAtlas.Export;

public class ClimateLayerGenerator()
{
    internal void GenerateClimateLayerAsync(SavegameDataSource dataSource, MbTilesStorage mbTilesStorage, ICoreServerAPI api)
    {
        var tiles = dataSource.GetAllMapRegionPositions();

        // Reduce parallelism to decrease database contention
        // SQLite has limited concurrent write capability even with WAL mode
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount
        };

        Parallel.ForEach(tiles, parallelOptions, (tile, _) =>
        {
            var tilePos = tile.ToChunkIndex();
            var serverMapRegion = dataSource.GetServerMapRegion(tilePos);
            if (serverMapRegion == null) 
                return;
            
            // Use BGRA8888 for direct pixel manipulation (much faster than DrawPoint)
            using var tempBitmap = new SKBitmap(512, 512, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            using var rainBitmap = new SKBitmap(512, 512, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            
            // Get direct access to pixel data for fast manipulation
            // Each pixel is 4 bytes: BGRA (Blue, Green, Red, Alpha)
            unsafe
            {
                var tempPtr = (byte*)tempBitmap.GetPixels();
                var rainPtr = (byte*)rainBitmap.GetPixels();

                // Process all pixels with direct memory access (no SKPaint allocations)
                for (var z = 0; z < 512; z++)
                {
                    for (var x = 0; x < 512; x++)
                    {
                        // Use normalized position method (values are already 0-1 range after division)
                        // ClimateMap encoding: Red (16-23 bits) = temperature, Green (8-15 bits) = rainfall
                        var normalizedX = (x + 0.5f) / 512f; // Sample from pixel center for better interpolation
                        var normalizedZ = (z + 0.5f) / 512f;
                        var interpolatedColor = serverMapRegion.ClimateMap.GetUnpaddedColorLerpedForNormalizedPos(normalizedX, normalizedZ);
                        
                        // Extract temperature (red channel) and rainfall (green channel)
                        var tempValue = ColorUtil.ColorR(interpolatedColor);    // 0-255 range
                        var rainValue = ColorUtil.ColorG(interpolatedColor);    // 0-255 range
                        
                        var pixelIndex = (z * 512 + x) * 4; // 4 bytes per pixel (BGRA)
                        
                        // Temperature tile: Store as grayscale with full opacity
                        // This makes the data easier to visualize and process
                        tempPtr[pixelIndex] = tempValue;     // Blue
                        tempPtr[pixelIndex + 1] = tempValue; // Green
                        tempPtr[pixelIndex + 2] = tempValue; // Red
                        tempPtr[pixelIndex + 3] = 255;       // Alpha (fully opaque)
                        
                        // Rain tile: Store as grayscale with full opacity
                        rainPtr[pixelIndex] = rainValue;     // Blue
                        rainPtr[pixelIndex + 1] = rainValue; // Green
                        rainPtr[pixelIndex + 2] = rainValue; // Red
                        rainPtr[pixelIndex + 3] = 255;       // Alpha (fully opaque)
                    }
                }
            }
            
            // Properly dispose SKData objects to prevent memory leaks
            using var tempData = tempBitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var rainData = rainBitmap.Encode(SKEncodedImageFormat.Png, 100);
            
            // Note: These still create individual database connections which can cause contention
            // Consider batching if performance is still an issue
            mbTilesStorage.PutTempTile(tile.X, tile.Z, tempData.ToArray());
            mbTilesStorage.PutRainTile(tile.X, tile.Z, rainData.ToArray());
            
            api.Logger.Debug($"[VintageAtlas] Generated climate layer for {tile.X}-{tile.Z}");
        });
    }
}
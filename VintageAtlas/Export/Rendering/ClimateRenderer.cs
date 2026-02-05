using System;
using SkiaSharp;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using VintageAtlas.Export.Data;
using VintageAtlas.Export.DataSources;


namespace VintageAtlas.Export.Rendering;

public class ClimateRenderer
{
    private const int ChunkSize = 32;

    public byte[]? RenderClimateTile(TileChunkData tileData, ClimateType type)
    {
        try
        {
            var tileSize = tileData.TileSize;
            var chunksPerTile = tileData.ChunksPerTileEdge;

            using var bitmap = new SKBitmap(tileSize, tileSize, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            
            // 1. Extract climate grid from chunks
            // We add padding (1 extra row/col) for interpolation if possible, but TileChunkData only has the tile's chunks.
            // So edges might not interpolate perfectly with neighbor tiles.
            // For now, we'll just interpolate within the available data.
            
            var grid = new int[chunksPerTile, chunksPerTile];
            var hasData = false;

            for (var x = 0; x < chunksPerTile; x++)
            {
                for (var z = 0; z < chunksPerTile; z++)
                {
                    var chunk = tileData.GetChunk(tileData.TileX * chunksPerTile + x, tileData.TileZ * chunksPerTile + z, 0);
                    if (chunk != null && chunk.ClimateData != null && chunk.ClimateData.Length > 0)
                    {
                        grid[x, z] = chunk.ClimateData[0];
                        hasData = true;
                    }
                    else
                    {
                        // Fill with neighbor or default?
                        grid[x, z] = 0; 
                    }
                }
            }

            if (!hasData) return null;

            // 2. Render pixels with bilinear interpolation
            unsafe
            {
                var pixelPtr = (uint*)bitmap.GetPixels();
                var rowPixels = bitmap.RowBytes / 4;

                for (var x = 0; x < tileSize; x++)
                {
                    for (var z = 0; z < tileSize; z++)
                    {
                        // Map pixel to grid coordinates
                        // Grid is chunksPerTile x chunksPerTile (e.g. 16x16)
                        // TileSize is 512
                        // Scale = 32 pixels per grid cell
                        
                        float gx = x / (float)ChunkSize;
                        float gz = z / (float)ChunkSize;
                        
                        var c = GetInterpolatedClimate(grid, gx, gz, chunksPerTile);
                        var color = GetColorForClimate(c, type);
                        
                        var pixelIndex = z * rowPixels + x;
                        pixelPtr[pixelIndex] = color;
                    }
                }
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private int GetInterpolatedClimate(int[,] grid, float x, float z, int size)
    {
        // Clamp to grid bounds
        var x0 = (int)x;
        var z0 = (int)z;
        var x1 = Math.Min(x0 + 1, size - 1);
        var z1 = Math.Min(z0 + 1, size - 1);
        
        // Local fraction
        var fx = x - x0;
        var fz = z - z0;
        
        // Ensure indices are valid
        x0 = Math.Max(0, Math.Min(x0, size - 1));
        z0 = Math.Max(0, Math.Min(z0, size - 1));
        
        var c00 = grid[x0, z0];
        var c10 = grid[x1, z0];
        var c01 = grid[x0, z1];
        var c11 = grid[x1, z1];
        
        // Unpack and interpolate components separately

        // Unpack temperature and rainfall components for each corner using helper
        SavegameDataSource.UnpackClimateData(c00, out int t00_val, out int r00_val);
        SavegameDataSource.UnpackClimateData(c10, out int t10_val, out int r10_val);
        SavegameDataSource.UnpackClimateData(c01, out int t01_val, out int r01_val);
        SavegameDataSource.UnpackClimateData(c11, out int t11_val, out int r11_val);

        // Interpolate temperature and rainfall separately
        var t = GameMath.BiLerp(t00_val, t10_val, t01_val, t11_val, fx, fz);
        var r = GameMath.BiLerp(r00_val, r10_val, r01_val, r11_val, fx, fz);

        // Pack back into climate int (Temp << 16 | Rain << 8)
        return ((int)t << 16) | ((int)r << 8);

    }

    private uint GetColorForClimate(int climate, ClimateType type)
    {
        SavegameDataSource.UnpackClimateData(climate, out int tempRaw, out int rainRaw);

        if (type == ClimateType.Temperature)
        {
            // Temperature: 0-255 maps to -20°C to 40°C
            // We map 0-255 to a 0.0-1.0 gradient
            var t = Math.Clamp(tempRaw / 255f, 0f, 1f);
            // Gradient: Cold (Blue) -> Temperate (Green) -> Hot (Red)
            return ColorGradient(t, 0xFF0000FF, 0xFF00FF00, 0xFFFF0000);
        }
        else
        {
            // Rainfall: 0-255
            // We map 0-255 to a 0.0-1.0 gradient
            var t = Math.Clamp(rainRaw / 255f, 0f, 1f);
            // Gradient: Dry (Yellow/White) -> Wet (Blue)
            return ColorGradient(t, 0xFFFFFFE0, 0xFF0000FF);
        }
    }


    
    private uint ColorGradient(float t, uint c1, uint c2)
    {
        t = Math.Max(0, Math.Min(1, t));
        
        var a1 = (c1 >> 24) & 0xFF;
        var r1 = (c1 >> 16) & 0xFF;
        var g1 = (c1 >> 8) & 0xFF;
        var b1 = c1 & 0xFF;
        
        var a2 = (c2 >> 24) & 0xFF;
        var r2 = (c2 >> 16) & 0xFF;
        var g2 = (c2 >> 8) & 0xFF;
        var b2 = c2 & 0xFF;
        
        var a = (uint)(a1 + (a2 - a1) * t);
        var r = (uint)(r1 + (r2 - r1) * t);
        var g = (uint)(g1 + (g2 - g1) * t);
        var b = (uint)(b1 + (b2 - b1) * t);
        
        return b | (g << 8) | (r << 16) | (a << 24);
    }
    
    private uint ColorGradient(float t, uint c1, uint c2, uint c3)
    {
        if (t < 0.5f) return ColorGradient(t * 2, c1, c2);
        return ColorGradient((t - 0.5f) * 2, c2, c3);
    }
}

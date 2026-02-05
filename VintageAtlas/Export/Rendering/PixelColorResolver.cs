using System;
using Vintagestory.API.MathTools;
using VintageAtlas.Core.Configuration;
using VintageAtlas.Export.Colors;
using VintageAtlas.Export.Data;

namespace VintageAtlas.Export.Rendering;

/// <summary>
/// Handles all pixel color calculation logic for different rendering modes.
/// Extracted from UnifiedTileGenerator for better componentization.
/// </summary>
public sealed class PixelColorResolver(IBlockColorCache colorCache, ModConfig config, int mapYHalf)
{
    private readonly IBlockColorCache _colorCache = colorCache ?? throw new ArgumentNullException(nameof(colorCache));
    private readonly ModConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly int _mapYHalf = mapYHalf;

    /// <summary>
    /// Calculate the final pixel color based on the configured render mode.
    /// </summary>
    public uint CalculatePixelColor(int blockId, uint? overrideColor, PixelContext ctx,
        ChunkSnapshot snapshot, Random random)
    {
        if (overrideColor.HasValue)
            return overrideColor.Value;

        return _config.Export.Mode switch
        {
            ImageMode.OnlyOneColor => _colorCache.GetBaseColor(blockId),
            ImageMode.ColorVariations => _colorCache.GetRandomColorVariation(blockId, random),
            ImageMode.ColorVariationsWithHeight => ApplyHeightVariation(blockId, ctx.Height, random),
            ImageMode.ColorVariationsWithHillShading => _colorCache.GetRandomColorVariation(blockId, random),
            ImageMode.MedievalStyleWithHillShading => CalculateMedievalColor(blockId, ctx, snapshot),
            _ => _colorCache.GetBaseColor(blockId)
        };
    }

    /// <summary>
    /// Apply height-based color variation.
    /// </summary>
    private uint ApplyHeightVariation(int blockId, int height, Random random)
    {
        var color = _colorCache.GetRandomColorVariation(blockId, random);
        return (uint)ColorUtil.ColorMultiply3Clamped((int)color, height / (float)_mapYHalf);
    }

    /// <summary>
    /// Calculate medieval style color with water-edge detection.
    /// </summary>
    private uint CalculateMedievalColor(int blockId, PixelContext ctx, ChunkSnapshot snapshot)
    {
        var isWaterEdge = DetectWaterEdge(blockId, ctx.X, ctx.Z, snapshot);
        return _colorCache.GetMedievalStyleColor(blockId, isWaterEdge);
    }

    /// <summary>
    /// Detect if a water block is at the edge (borders non-water blocks).
    /// </summary>
    private bool DetectWaterEdge(int blockId, int x, int z, ChunkSnapshot snapshot)
    {
        if (!_colorCache.IsLake(blockId))
            return false;

        const int chunkSize = 32;

        // Check boundaries - edges are always rendered as water
        if (x == 0 || x == chunkSize - 1 || z == 0 || z == chunkSize - 1)
            return false;

        var heightMap = snapshot.HeightMap;
        var blockIds = snapshot.BlockIds;

        // Check 4 neighbors
        var neighborN = GetBlockAtPosition(x, z - 1, heightMap, blockIds);
        var neighborS = GetBlockAtPosition(x, z + 1, heightMap, blockIds);
        var neighborE = GetBlockAtPosition(x + 1, z, heightMap, blockIds);
        var neighborW = GetBlockAtPosition(x - 1, z, heightMap, blockIds);

        // If all neighbors are also water/lake, this is interior water
        return !_colorCache.IsLake(neighborN) || !_colorCache.IsLake(neighborS) ||
               !_colorCache.IsLake(neighborE) || !_colorCache.IsLake(neighborW);
    }

    /// <summary>
    /// Get block ID at a specific X, Z position within a chunk using the height map.
    /// </summary>
    private static int GetBlockAtPosition(int x, int z, int[] heightMap, int[] blockIds)
    {
        const int chunkSize = 32;

        if (x < 0 || x >= chunkSize || z < 0 || z >= chunkSize)
            return 0;

        var heightIndex = z * chunkSize + x;
        if (heightIndex >= heightMap.Length)
            return 0;

        var height = heightMap[heightIndex];
        if (height == 0)
            return 0;

        // Calculate local Y and block index
        var localY = height % chunkSize;
        var blockIndex = localY * chunkSize * chunkSize + z * chunkSize + x;

        if (blockIndex < 0 || blockIndex >= blockIds.Length)
            return 0;

        return blockIds[blockIndex];
    }
}


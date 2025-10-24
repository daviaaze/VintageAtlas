using System;
using System.Collections.Generic;
using SkiaSharp;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VintageAtlas.Core;
using VintageAtlas.Export.Colors;
using VintageAtlas.Export.Data;
using VintageAtlas.Export.Utils;

namespace VintageAtlas.Export.Rendering;

/// <summary>
/// Fast bitmap renderer using direct memory access.
/// Replaces slow canvas.DrawPoint() approach with optimized pixel manipulation.
/// Based on techniques from ClimateLayerGenerator.
/// </summary>
public sealed class FastBitmapRenderer
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly BlockColorCache _colorCache;
    private readonly PixelColorResolver _colorResolver;
    private readonly HashSet<int> _microBlocks;

    private const int ChunkSize = 32;

    public FastBitmapRenderer(
        ICoreServerAPI sapi,
        ModConfig config,
        BlockColorCache colorCache,
        HashSet<int> microBlocks)
    {
        _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _colorCache = colorCache ?? throw new ArgumentNullException(nameof(colorCache));
        _microBlocks = microBlocks ?? throw new ArgumentNullException(nameof(microBlocks));
        
        var mapYHalf = sapi.WorldManager.MapSizeY / 2;
        _colorResolver = new PixelColorResolver(_colorCache, config, mapYHalf);
    }

    /// <summary>
    /// Render tile image using fast direct memory access.
    /// </summary>
    public byte[]? RenderTileImage(TileChunkData tileData)
    {
        try
        {
            var tileSize = tileData.TileSize;
            var chunksPerTile = tileData.ChunksPerTileEdge;

            // Use BGRA8888 for direct pixel manipulation (much faster than DrawPoint)
            using var bitmap = new SKBitmap(tileSize, tileSize, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            
            var randomForTile = CreateTileRandomizer(tileData);
            var shadowMap = InitializeShadowMap(tileSize);

            var chunksRendered = RenderAllChunksDirectly(bitmap, tileData, chunksPerTile, shadowMap, tileSize, randomForTile);

            if (chunksRendered <= 0)
            {
                _sapi.Logger.Warning("[VintageAtlas] ⚠️  NO chunks were rendered!");
                return null;
            }

            ApplyShadowMapIfNeeded(bitmap, shadowMap, tileSize);

            return EncodeBitmapToPng(bitmap);
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to render tile image: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Render all chunks directly to bitmap memory.
    /// </summary>
    private int RenderAllChunksDirectly(SKBitmap bitmap, TileChunkData tileData, int chunksPerTile,
        Span<byte> shadowMap, int tileSize, Random randomForTile)
    {
        var chunksRendered = 0;
        var startChunkX = tileData.TileX * chunksPerTile;
        var startChunkZ = tileData.TileZ * chunksPerTile;

        var mapYHalf = _sapi.WorldManager.MapSizeY / 2;
        var mapMaxY = _sapi.WorldManager.MapSizeY - 1;

        // Get direct access to pixel data for fast manipulation
        unsafe
        {
            var pixelPtr = (uint*)bitmap.GetPixels();
            var rowBytes = bitmap.RowBytes / 4; // Convert bytes to pixel count

            for (var offsetX = 0; offsetX < chunksPerTile; offsetX++)
            {
                for (var offsetZ = 0; offsetZ < chunksPerTile; offsetZ++)
                {
                    var chunkX = startChunkX + offsetX;
                    var chunkZ = startChunkZ + offsetZ;

                    var snapshot = tileData.GetChunk(chunkX, chunkZ, 0);
                    if (snapshot?.IsLoaded != true)
                        continue;

                    RenderChunkDirect(pixelPtr, rowBytes, snapshot, offsetX * ChunkSize, offsetZ * ChunkSize,
                        shadowMap, tileSize, randomForTile, mapYHalf, mapMaxY);
                    chunksRendered++;
                }
            }
        }

        return chunksRendered;
    }

    /// <summary>
    /// Render a single chunk directly to bitmap memory (FAST).
    /// </summary>
    private unsafe void RenderChunkDirect(uint* pixelPtr, int rowPixels, ChunkSnapshot snapshot,
        int offsetX, int offsetZ, Span<byte> shadowMap, int tileSize, Random randomForTile,
        int mapYHalf, int mapMaxY)
    {
        try
        {
            if (snapshot.HeightMap.Length == 0 || snapshot.BlockIds.Length == 0)
            {
                _sapi.Logger.Warning($"[VintageAtlas] ⚠️  Empty data! heightMap={snapshot.HeightMap.Length}, blockIds={snapshot.BlockIds.Length}");
                return;
            }

            for (var x = 0; x < ChunkSize; x++)
            {
                for (var z = 0; z < ChunkSize; z++)
                {
                    var heightIndex = z * ChunkSize + x;
                    if (heightIndex >= snapshot.HeightMap.Length)
                        continue;

                    var height = GameMath.Clamp(snapshot.HeightMap[heightIndex], 0, mapMaxY);

                    // Resolve block ID (handles snow blocks)
                    var blockResult = ResolveBlockId(x, z, height, snapshot.BlockIds);
                    if (blockResult.BlockId == 0)
                        continue;

                    var pixelCtx = new PixelContext(x, z, height + blockResult.HeightOffset, offsetX, offsetZ);

                    // Handle microblocks/chiseled blocks
                    var (blockId, overrideColor) = ResolveMicroblock(blockResult.BlockId, snapshot, pixelCtx);

                    // Calculate pixel color
                    var color = _colorResolver.CalculatePixelColor(blockId, overrideColor, pixelCtx, snapshot, randomForTile);

                    // Update shadow map for hill shading modes
                    if (_config.Mode is ImageMode.ColorVariationsWithHillShading or ImageMode.MedievalStyleWithHillShading)
                    {
                        UpdateShadowMap(shadowMap, tileSize, blockId, pixelCtx, snapshot);
                    }

                    // Write pixel directly to memory (BGRA format)
                    var pixelIndex = pixelCtx.ImgZ * rowPixels + pixelCtx.ImgX;
                    pixelPtr[pixelIndex] = ConvertToBgraUint(color);
                }
            }
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to render chunk: {ex.Message}");
        }
    }

    /// <summary>
    /// Convert ARGB color to BGRA format for direct memory writing.
    /// </summary>
    private static uint ConvertToBgraUint(uint argbColor)
    {
        var a = (argbColor >> 24) & 0xFF;
        var r = (argbColor >> 16) & 0xFF;
        var g = (argbColor >> 8) & 0xFF;
        var b = argbColor & 0xFF;
        
        // BGRA format: B in lowest byte, then G, R, A
        return b | (g << 8) | (r << 16) | (a << 24);
    }

    /// <summary>
    /// Result of block ID resolution, including snow offset handling.
    /// </summary>
    private readonly struct BlockIdResult(int blockId, int heightOffset)
    {
        public readonly int BlockId = blockId;
        public readonly int HeightOffset = heightOffset;
    }

    /// <summary>
    /// Resolve the actual block ID to render, handling snow and microblocks.
    /// </summary>
    private BlockIdResult ResolveBlockId(int x, int z, int height, int[] blockIds)
    {
        var localY = height % ChunkSize;
        var blockIndex = localY * ChunkSize * ChunkSize + z * ChunkSize + x;

        if (blockIndex < 0 || blockIndex >= blockIds.Length)
            return new BlockIdResult(0, 0);

        var blockId = blockIds[blockIndex];
        var heightOffset = 0;

        // Handle snow blocks: look at the block underneath
        if (_sapi.World.Blocks[blockId].BlockMaterial != EnumBlockMaterial.Snow)
            return new BlockIdResult(blockId, heightOffset);
        
        heightOffset = 1;
        var adjustedHeight = height - 1;
        if (adjustedHeight < 0) 
            return new BlockIdResult(blockId, heightOffset);
        
        var adjustedLocalY = adjustedHeight % ChunkSize;
        var adjustedBlockIndex = adjustedLocalY * ChunkSize * ChunkSize + z * ChunkSize + x;
        if (adjustedBlockIndex >= 0 && adjustedBlockIndex < blockIds.Length)
        {
            blockId = blockIds[adjustedBlockIndex];
        }

        return new BlockIdResult(blockId, heightOffset);
    }

    /// <summary>
    /// Handle microblock/chiseled block rendering.
    /// </summary>
    private (int blockId, uint? overrideColor) ResolveMicroblock(int blockId, ChunkSnapshot snapshot, PixelContext ctx)
    {
        uint? overrideColor = null;

        if (!_microBlocks.Contains(blockId)) 
            return (blockId, overrideColor);
        
        var worldX = snapshot.ChunkX * ChunkSize + ctx.X;
        var worldZ = snapshot.ChunkZ * ChunkSize + ctx.Z;
        var blockPos = new BlockPos(worldX, ctx.Height, worldZ, 0);

        if (snapshot.BlockEntities.TryGetValue(blockPos, out var blockEntity) &&
            blockEntity is BlockEntityMicroBlock { BlockIds.Length: > 0 } blockEntityChisel)
        {
            blockId = blockEntityChisel.BlockIds[0];
        }
        else
        {
            // Fallback parity with Extractor
            overrideColor = _config.Mode == ImageMode.MedievalStyleWithHillShading
                ? MapColors.ColorsByCode["land"]
                : (uint)SKColors.Green;
        }

        return (blockId, overrideColor);
    }

    /// <summary>
    /// Update the shadow map for hill shading modes if applicable.
    /// </summary>
    private void UpdateShadowMap(Span<byte> shadowMap, int tileSize, int blockId,
        PixelContext ctx, ChunkSnapshot snapshot)
    {
        if (shadowMap == null)
            return;

        // Skip shadow calculation for lakes in medieval mode
        if (_config.Mode == ImageMode.MedievalStyleWithHillShading && _colorCache.IsLake(blockId))
            return;

        var (nwDelta, nDelta, wDelta) = CalculateAltitudeDiff(ctx.X, ctx.Height, ctx.Z, snapshot.HeightMap);
        var boostMultiplier = CalculateSlopeBoost(nwDelta, nDelta, wDelta);
        var shadowIndex = ctx.ImgZ * tileSize + ctx.ImgX;

        if (shadowIndex >= 0 && shadowIndex < shadowMap.Length)
        {
            shadowMap[shadowIndex] = (byte)(shadowMap[shadowIndex] * boostMultiplier);
        }
    }

    private static Random CreateTileRandomizer(TileChunkData tileData)
    {
        return new Random(unchecked(Environment.TickCount ^ (tileData.TileX * 73856093) ^ (tileData.TileZ * 19349663)));
    }

    private Span<byte> InitializeShadowMap(int tileSize)
    {
        if (_config.Mode is not (ImageMode.ColorVariationsWithHillShading or ImageMode.MedievalStyleWithHillShading))
            return null;

        var shadowMap = new byte[tileSize * tileSize];
        Array.Fill(shadowMap, (byte)128);
        return shadowMap;
    }

    private void ApplyShadowMapIfNeeded(SKBitmap bitmap, Span<byte> shadowMap, int tileSize)
    {
        if (_config.Mode is ImageMode.ColorVariationsWithHillShading or ImageMode.MedievalStyleWithHillShading)
        {
            ApplyShadowMapToBitmap(bitmap, shadowMap, tileSize);
        }
    }

    private static byte[] EncodeBitmapToPng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static (int northWestDelta, int northDelta, int westDelta) CalculateAltitudeDiff(
        int x, int y, int z, int[] heightMap)
    {
        var westernX = x - 1;
        var northernZ = z - 1;

        if (westernX < 0) westernX++;
        if (northernZ < 0) northernZ++;

        westernX = GameMath.Mod(westernX, ChunkSize);
        northernZ = GameMath.Mod(northernZ, ChunkSize);

        var northWestIndex = northernZ * ChunkSize + westernX;
        var northWestHeight = northWestIndex < heightMap.Length ? heightMap[northWestIndex] : y;

        var northIndex = northernZ * ChunkSize + x;
        var northHeight = northIndex < heightMap.Length ? heightMap[northIndex] : y;

        var westIndex = z * ChunkSize + westernX;
        var westHeight = westIndex < heightMap.Length ? heightMap[westIndex] : y;

        return (y - northWestHeight, y - northHeight, y - westHeight);
    }

    private static float CalculateSlopeBoost(int northWestDelta, int northDelta, int westDelta)
    {
        var direction = Math.Sign(northWestDelta) + Math.Sign(northDelta) + Math.Sign(westDelta);
        float steepness = Math.Max(Math.Max(Math.Abs(northWestDelta), Math.Abs(northDelta)), Math.Abs(westDelta));
        var slopeFactor = Math.Min(0.5f, steepness / 10f) / 1.25f;

        return direction switch
        {
            > 0 => 1.08f + slopeFactor,
            < 0 => 0.92f - slopeFactor,
            _ => 1
        };
    }

    private static unsafe void ApplyShadowMapToBitmap(SKBitmap bitmap, Span<byte> shadowMap, int size)
    {
        // Create a copy for sharpening
        Span<byte> originalShadowMap = new byte[shadowMap.Length];
        shadowMap.CopyTo(originalShadowMap);

        // Blur the shadow map to soften harsh edges
        BlurTool.Blur(shadowMap, size, size, 2);

        const float sharpen = 1.4f;

        var imgPtr = (byte*)bitmap.GetPixels().ToPointer();
        var imgRowBytes = bitmap.RowBytes;

        for (var i = 0; i < shadowMap.Length; i++)
        {
            // Combine blurred and sharp shadows for detail preservation
            var blurredValue = shadowMap[i] / 128f - 1f;
            var originalValue = originalShadowMap[i] / 128f - 1f;

            var shadowEffect = (int)(blurredValue * 5) / 5f;
            shadowEffect += originalValue * 5 % 1 / 5f;

            if (shadowEffect is 0)
                continue;

            var imgX = i % size;
            var imgZ = i / size;

            var row = (uint*)(imgPtr + imgZ * imgRowBytes);
            var pixel = (int)row[imgX];

            // Apply shadow/highlight with sharpening
            var adjusted = ColorUtil.ColorMultiply3Clamped(pixel, shadowEffect * sharpen + 1f);
            row[imgX] = (uint)(adjusted | 255 << 24); // Preserve alpha
        }
    }
}


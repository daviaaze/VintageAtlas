using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Export.Data;

namespace VintageAtlas.Export.Colors;

/// <summary>
/// Caches block colors for fast terrain rendering
///
/// This class loads and caches block color mappings from multiple sources:
/// 1. blockColorMapping.json (if available) - Custom color mappings
/// 2. MapColors defaults - Fallback based on block material
/// 3. Block material detection - For water/lake identification
/// </summary>
public class BlockColorCache
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;

    // Block ID -> List of color variations (for ColorVariations modes)
    private readonly Dictionary<int, List<uint>> _blockColorVariations = new();

    // Block ID -> Base color index for Medieval style
    private readonly byte[] _blockToColorIndex;

    // Block ID -> Is this a water/lake block?
    private readonly bool[] _blockIsLake;

    // Color palette (indexed by _blockToColorIndex)
    private readonly uint[] _colorPalette;

    // Cache whether the system is initialized
    private bool _isInitialized;

    public BlockColorCache(ICoreServerAPI sapi, ModConfig config)
    {
        _sapi = sapi;
        _config = config;

        var maxBlockId = sapi.World.Blocks.Count + 1;
        _blockToColorIndex = new byte[maxBlockId];
        _blockIsLake = new bool[maxBlockId];
        _colorPalette = new uint[MapColors.ColorsByCode.Count];
    }

    /// <summary>
    /// Initialize the color cache
    /// Call this once after the server startup, on the main thread
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized)
        {
            _sapi.Logger.Warning("[VintageAtlas] BlockColorCache already initialized");
            return;
        }

        _sapi.Logger.Notification("[VintageAtlas] Initializing block color cache...");

        // Load color palette from MapColors
        InitializeColorPalette();

        // Try to load custom color mappings
        var customMappings = LoadCustomColorMappings();

        // Build color cache for all blocks
        BuildBlockColorCache(customMappings);

        _isInitialized = true;

        var variationCount = _blockColorVariations.Count;
        var lakeCount = _blockIsLake.Count(b => b);

        _sapi.Logger.Notification($"[VintageAtlas] Block color cache initialized: " +
            $"{variationCount} blocks with color variations, " +
            $"{lakeCount} water/lake blocks identified");
    }

    /// <summary>
    /// Get color variations for a block (for ColorVariations modes)
    /// Returns null if the block has no color variations
    /// </summary>
    private List<uint>? GetColorVariations(int blockId)
    {
        if (blockId < 0 || blockId >= _blockToColorIndex.Length)
            return null;

        return _blockColorVariations.GetValueOrDefault(blockId);
    }

    /// <summary>
    /// Get base color for a block (for Medieval style and simple modes)
    /// </summary>
    public uint GetBaseColor(int blockId)
    {
        if (blockId < 0 || blockId >= _blockToColorIndex.Length)
            return MapColors.ColorsByCode["land"]; // Default fallback

        // Try to use the first color from variations (most detailed)
        if (_blockColorVariations.TryGetValue(blockId, out var variations) && variations.Count > 0)
        {
            return variations[0];
        }

        // Fallback to palette
        var colorIndex = _blockToColorIndex[blockId];
        return _colorPalette[colorIndex];
    }

    /// <summary>
    /// Check if a block is water/lake
    /// </summary>
    public bool IsLake(int blockId)
    {
        if (blockId < 0 || blockId >= _blockIsLake.Length)
            return false;

        return _blockIsLake[blockId];
    }

    /// <summary>
    /// Get color by material (fallback when block not in cache)
    /// </summary>
    public static uint GetColorByMaterial(EnumBlockMaterial material)
    {
        var colorCode = MapColors.GetDefaultMapColorCode(material);
        return MapColors.ColorsByCode.TryGetValue(colorCode, out var color) ? color : MapColors.ColorsByCode["land"];
    }

    private void InitializeColorPalette()
    {
        for (var i = 0; i < _colorPalette.Length; i++)
        {
            _colorPalette[i] = MapColors.ColorsByCode.GetValueAtIndex(i);
        }
    }

    private ExportData? LoadCustomColorMappings()
    {
        var customData = _sapi.LoadModConfig<ExportData>("blockColorMapping.json");

        if (customData == null && _config.Mode != ImageMode.MedievalStyleWithHillShading)
        {
            _sapi.Logger.Warning("[VintageAtlas] blockColorMapping.json not found - using material-based colors only");
        }
        else
        {
            _sapi.Logger.Notification("[VintageAtlas] Loaded custom block color mappings from blockColorMapping.json");
        }

        return customData;
    }

    private void BuildBlockColorCache(ExportData? customMappings)
    {
        var blocks = _sapi.World.Blocks;
        var processedCount = 0;

        foreach (var block in blocks)
        {
            if (block == null || block.Id == 0) continue;

            try
            {
                // Get color code from block attributes (much more detailed than material type)
                // This matches the old Extractor.cs behavior for rich color variation
                string colorCode; // Default fallback
                if (block.Attributes != null)
                {
                    colorCode = block.Attributes["mapColorCode"].AsString() ??
                                MapColors.GetDefaultMapColorCode(block.BlockMaterial);
                }
                else
                {
                    colorCode = MapColors.GetDefaultMapColorCode(block.BlockMaterial);
                }

                var colorIndex = (byte)MapColors.ColorsByCode.IndexOfKey(colorCode);
                _blockToColorIndex[block.Id] = colorIndex;

                // Identify water/lake blocks (Extractor parity: liquids and ice excluding glacier ice)
                _blockIsLake[block.Id] = block.BlockMaterial == EnumBlockMaterial.Liquid ||
                                          (block.BlockMaterial == EnumBlockMaterial.Ice && block.Code.Path != "glacierice");

                processedCount++;
            }
            catch (Exception ex)
            {
                _sapi.Logger.Warning($"[VintageAtlas] Failed to process block {block.Code}: {ex.Message}");
            }
        }

        // Apply custom color mappings if available
        if (customMappings?.Blocks != null)
        {
            ApplyCustomMappings(customMappings);
        }

        _sapi.Logger.Debug($"[VintageAtlas] Processed {processedCount} blocks for color cache");
    }

    private void ApplyCustomMappings(ExportData customMappings)
    {
        var appliedCount = 0;

        foreach (var (blockCode, value) in customMappings.Blocks)
        {
            try
            {
                // Ensure colors have alpha channel set
                var colors = EnsureAlphaChannel(value);

                if (colors.Count == 0) continue;

                // Find block by code (handle wildcards)

                if (blockCode.Contains('*'))
                {
                    // Wildcard matching
                    var pattern = blockCode.Replace("*", ".*");
                    var regex = new System.Text.RegularExpressions.Regex(pattern);

                    foreach (var block in _sapi.World.Blocks)
                    {
                        if (block is { Id: 0 })
                            continue;

                        if (!regex.IsMatch(block.Code.ToString()))
                            continue;

                        _blockColorVariations[block.Id] = colors;
                        appliedCount++;
                    }
                }
                else
                {
                    // Exact match
                    var block = _sapi.World.GetBlock(new AssetLocation(blockCode));
                    if (block == null)
                        continue;

                    _blockColorVariations[block.Id] = colors;
                    appliedCount++;
                }
            }
            catch (Exception ex)
            {
                _sapi.Logger.Warning($"[VintageAtlas] Failed to apply custom mapping for {blockCode}: {ex.Message}");
            }
        }

        _sapi.Logger.Notification($"[VintageAtlas] Applied {appliedCount} custom color mappings");
    }

    private static List<uint> EnsureAlphaChannel(uint[] value)
    {
        return value.Select(color => (color & 0xff000000) == 0 ? color | 0xff000000 : color).ToList();
    }

    /// <summary>
    /// Get color for Medieval style rendering with water-edge detection
    /// This is the most complex color selection mode
    /// </summary>
    public uint GetMedievalStyleColor(int blockId, bool isWaterEdge = false)
    {
        if (blockId < 0 || blockId >= _blockToColorIndex.Length)
            return MapColors.ColorsByCode["land"];

        // Mode 4 ALWAYS uses the basic palette (like old Extractor.cs)
        // This gives ~12 distinct colors based on block material/type
        var colorIndex = _blockToColorIndex[blockId];
        var baseColor = _colorPalette[colorIndex];

        // If this is a water edge, use darker water-edge color
        return isWaterEdge ? MapColors.ColorsByCode["water-edge"] : baseColor;
    }

    /// <summary>
    /// Get a random color variation for a block
    /// Used for ColorVariations and ColorVariationsWithHeight modes
    /// </summary>
    public uint GetRandomColorVariation(int blockId, Random random)
    {
        var variations = GetColorVariations(blockId);

        if (variations == null || variations.Count == 0)
        {
            // Fallback to palette-based color
            return GetBaseColor(blockId);
        }

        var selectedColor = variations[random.Next(variations.Count)];

        return selectedColor;
    }
}

using System;

namespace VintageAtlas.Export.Colors;

/// <summary>
/// Interface for block color caching and retrieval.
/// Abstracts color lookup for terrain rendering.
/// </summary>
public interface IBlockColorCache
{
    /// <summary>
    /// Initialize the color cache.
    /// Must be called once on startup, on the main thread.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Get the base color for a block ID
    /// </summary>
    uint GetBaseColor(int blockId);

    /// <summary>
    /// Get a random color variation for a block ID
    /// </summary>
    uint GetRandomColorVariation(int blockId, Random random);

    /// <summary>
    /// Get the medieval style color for a block ID
    /// </summary>
    uint GetMedievalStyleColor(int blockId, bool isWaterEdge);

    /// <summary>
    /// Check if a block is a water/lake block
    /// </summary>
    bool IsLake(int blockId);
}


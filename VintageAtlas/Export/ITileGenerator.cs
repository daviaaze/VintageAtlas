using System;
using System.Threading.Tasks;

namespace VintageAtlas.Export;

/// <summary>
/// Interface for tile generators.
/// Allows PyramidTileDownsampler and other components to work with
/// both DynamicTileGenerator (old) and UnifiedTileGenerator (new).
/// </summary>
public interface ITileGenerator : IDisposable
{
    /// <summary>
    /// Get or generate a tile asynchronously.
    /// Returns null if tile cannot be generated.
    /// </summary>
    Task<byte[]?> GetTileDataAsync(int zoom, int tileX, int tileZ);

    /// <summary>
    /// Get tile extent (min/max coordinates) for a zoom level from storage.
    /// Used by pyramid downsampler to determine which tiles exist.
    /// </summary>
    Task<Storage.TileExtent?> GetTileExtentAsync(int zoom);
}


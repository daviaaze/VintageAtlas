using System;

namespace VintageAtlas.Models.Domain;

/// <summary>
/// Result of a tile generation request
/// </summary>
public class TileResult
{
    /// <summary>
    /// Tile image data (PNG bytes)
    /// </summary>
    public byte[]? Data { get; set; }

    /// <summary>
    /// Whether the tile was not found (404 response)
    /// </summary>
    public bool NotFound { get; set; }
}


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

    public bool NotModified { get; set; }
    public string ETag { get; set; } = "";
    public DateTime LastModified { get; set; }
    public string ContentType { get; set; } = "";
}


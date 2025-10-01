using System;

namespace VintageAtlas.Models;

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
    /// ETag for caching
    /// </summary>
    public string? ETag { get; set; }
    
    /// <summary>
    /// Content type (usually "image/png")
    /// </summary>
    public string ContentType { get; set; } = "image/png";

    public DateTime LastModified { get; set; }
    
    /// <summary>
    /// Whether the tile was not modified (304 response)
    /// </summary>
    public bool NotModified { get; set; }
    
    /// <summary>
    /// Whether the tile was not found (404 response)
    /// </summary>
    public bool NotFound { get; set; }
}


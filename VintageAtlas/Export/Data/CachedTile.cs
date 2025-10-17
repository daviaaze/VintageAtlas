using System;

namespace VintageAtlas.Export;

/// <summary>
/// Cached tile data for in-memory storage
/// </summary>
public class CachedTile
{
    public byte[] Data { get; set; } = [];
    public DateTime LastModified { get; set; }
    public string ETag { get; set; } = "";
}
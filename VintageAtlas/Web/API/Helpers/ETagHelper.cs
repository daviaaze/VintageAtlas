namespace VintageAtlas.Web.API.Helpers;

/// <summary>
/// Helper utilities for ETag generation
/// </summary>
public static class ETagHelper
{
    /// <summary>
    /// Generate ETag from content hash
    /// </summary>
    public static string GenerateFromString(string content)
    {
        var hash = content.GetHashCode();
        return $"\"{hash}\"";
    }

    /// <summary>
    /// Generate ETag for tile data using first 16 bytes + size as fingerprint
    /// (full hash would be expensive for high-volume tile serving)
    /// </summary>
    public static string GenerateFromTileData(byte[] tileData, params int[] coordinates)
    {
        var fingerprint = tileData.Length;
        
        if (tileData.Length >= 16)
        {
            for (var i = 0; i < 16; i++)
            {
                fingerprint = fingerprint * 31 + tileData[i];
            }
        }

        var coordString = string.Join("-", coordinates);
        return $"\"{coordString}-{fingerprint:X}\"";
    }

    /// <summary>
    /// Generate ETag from file metadata
    /// </summary>
    public static string GenerateFromFileInfo(long lastWriteTicks, long fileSize)
    {
        return $"\"{lastWriteTicks:X}-{fileSize:X}\"";
    }

    /// <summary>
    /// Generate ETag from timestamp (for cached data)
    /// </summary>
    public static string GenerateFromTimestamp(long timestamp)
    {
        return $"\"{timestamp:X}\"";
    }
}


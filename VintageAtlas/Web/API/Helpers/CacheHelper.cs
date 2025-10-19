namespace VintageAtlas.Web.API.Helpers;

/// <summary>
/// Cache control header constants and utilities
/// </summary>
public static class CacheHelper
{
    // Common cache control strategies
    public const string NoCache = "no-cache, must-revalidate";
    public const string ShortCache = "public, max-age=30";
    public const string MediumCache = "public, max-age=300";
    public const string LongCache = "public, max-age=3600";
    public const string ImmutableCache = "public, max-age=3600, immutable";
    public const string VeryLongCache = "public, max-age=86400, immutable"; // 24 hours

    /// <summary>
    /// Get cache control header for API responses
    /// </summary>
    public static string ForApi() => ShortCache;

    /// <summary>
    /// Get cache control header for map data
    /// </summary>
    public static string ForMapData() => MediumCache;

    /// <summary>
    /// Get cache control header for static tiles
    /// </summary>
    public static string ForTiles() => ImmutableCache;

    /// <summary>
    /// Get cache control header for GeoJSON data
    /// </summary>
    public static string ForGeoJson() => ShortCache;

    /// <summary>
    /// Get cache control header with custom max-age
    /// </summary>
    public static string Custom(int maxAgeSeconds, bool immutable = false)
    {
        var cache = $"public, max-age={maxAgeSeconds}";
        return immutable ? $"{cache}, immutable" : cache;
    }
}


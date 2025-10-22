using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.GeoJson;
using VintageAtlas.Storage;
using VintageAtlas.Web.API.Base;
using VintageAtlas.Web.API.Helpers;

namespace VintageAtlas.Web.API.Controllers;

/// <summary>
/// Serves climate data as GeoJSON for heatmap visualization
/// </summary>
public class ClimateGeoJsonController : JsonController
{
    private readonly MetadataStorage _metadataStorage;
    
    // Cache for climate GeoJSON with timestamps
    private ClimateGeoJson? _cachedTemperature;
    private ClimateGeoJson? _cachedRainfall;
    private long _lastTemperatureUpdate;
    private long _lastRainfallUpdate;
    private readonly object _cacheLock = new();
    
    // Climate data is static, cache for 1 hour
    private const int ClimateCacheMs = 3600000;

    public ClimateGeoJsonController(
        ICoreServerAPI sapi,
        MetadataStorage metadataStorage) : base(sapi)
    {
        _metadataStorage = metadataStorage ?? throw new ArgumentNullException(nameof(metadataStorage));
    }

    /// <summary>
    /// Serve temperature data as GeoJSON
    /// </summary>
    public async Task ServeTemperature(HttpListenerContext context)
    {
        try
        {
            var (geoJson, timestamp) = await GetTemperatureGeoJsonAsync();
            var etag = ETagHelper.GenerateFromTimestamp(timestamp);

            // Check ETag and return 304 if match
            if (CheckETagMatch(context, etag))
            {
                return;
            }

            await ServeGeoJson(context, geoJson, etag, CacheHelper.ForGeoJson());
        }
        catch (Exception ex)
        {
            LogError($"Error serving temperature GeoJSON: {ex.Message}", ex);
            await ServeError(context, "Failed to generate temperature data");
        }
    }

    /// <summary>
    /// Serve rainfall data as GeoJSON
    /// </summary>
    public async Task ServeRainfall(HttpListenerContext context)
    {
        try
        {
            var (geoJson, timestamp) = await GetRainfallGeoJsonAsync();
            var etag = ETagHelper.GenerateFromTimestamp(timestamp);

            // Check ETag and return 304 if match
            if (CheckETagMatch(context, etag))
            {
                return;
            }

            await ServeGeoJson(context, geoJson, etag, CacheHelper.ForGeoJson());
        }
        catch (Exception ex)
        {
            LogError($"Error serving rainfall GeoJSON: {ex.Message}", ex);
            await ServeError(context, "Failed to generate rainfall data");
        }
    }

    /// <summary>
    /// Get temperature GeoJSON with caching
    /// </summary>
    private async Task<(ClimateGeoJson, long)> GetTemperatureGeoJsonAsync()
    {
        var now = Sapi.World.ElapsedMilliseconds;

        lock (_cacheLock)
        {
            if (_cachedTemperature != null && now - _lastTemperatureUpdate < ClimateCacheMs)
            {
                return (_cachedTemperature, _lastTemperatureUpdate);
            }
        }

        var geoJson = await GenerateClimateGeoJsonAsync("temperature");

        lock (_cacheLock)
        {
            _cachedTemperature = geoJson;
            _lastTemperatureUpdate = now;
        }

        return (geoJson, now);
    }

    /// <summary>
    /// Get rainfall GeoJSON with caching
    /// </summary>
    private async Task<(ClimateGeoJson, long)> GetRainfallGeoJsonAsync()
    {
        var now = Sapi.World.ElapsedMilliseconds;

        lock (_cacheLock)
        {
            if (_cachedRainfall != null && now - _lastRainfallUpdate < ClimateCacheMs)
            {
                return (_cachedRainfall, _lastRainfallUpdate);
            }
        }

        var geoJson = await GenerateClimateGeoJsonAsync("rainfall");

        lock (_cacheLock)
        {
            _cachedRainfall = geoJson;
            _lastRainfallUpdate = now;
        }

        return (geoJson, now);
    }

    /// <summary>
    /// Generate climate GeoJSON from database
    /// Note: Coordinates are already stored as display coordinates in the database
    /// </summary>
    private async Task<ClimateGeoJson> GenerateClimateGeoJsonAsync(string layerType)
    {
        // Fetch from database (coordinates already in display format)
        var points = await _metadataStorage.GetClimateDataAsync(layerType);
        
        var features = new List<ClimateFeature>();
        foreach (var point in points)
        {
            features.Add(new ClimateFeature(
                new ClimateProperties(point.Value, point.RealValue),
                new PointGeometry([point.X, point.Z]) // Already in display coordinates
            ));
        }

        return new ClimateGeoJson
        {
            Name = layerType,
            Features = features
        };
    }

    /// <summary>
    /// Invalidate cache to force regeneration
    /// Called after climate data is regenerated
    /// </summary>
    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cachedTemperature = null;
            _cachedRainfall = null;
        }
    }
}



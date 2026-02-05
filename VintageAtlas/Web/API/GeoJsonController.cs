using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.GeoJson;
using VintageAtlas.Storage;
using VintageAtlas.Web.API.Base;
using VintageAtlas.Web.API.Helpers;

namespace VintageAtlas.Web.API;

/// <summary>
/// Provides GeoJSON data dynamically via API with efficient caching
/// Scans loaded chunks in memory to find signs, signposts, traders, and translocators
/// </summary>
public class GeoJsonController(
    ICoreServerAPI sapi,
    CoordinateTransformService coordinateService,
    IMetadataStorage metadataStorage) : JsonController(sapi)
{
    private readonly CoordinateTransformService _coordinateService = coordinateService ?? throw new ArgumentNullException(nameof(coordinateService));
    private readonly IMetadataStorage _metadataStorage = metadataStorage ?? throw new ArgumentNullException(nameof(metadataStorage));

    // Cache GeoJSON data with timestamps (in milliseconds)
    private TraderGeoJson? _cachedTraders;
    private long _lastTraderUpdate;
    private readonly object _cacheLock = new();

    private const int TraderCacheMs = 600000; // 10 minutes - traders can move/spawn

    /// <summary>
    /// Get all traders as GeoJSON
    /// </summary>
    public async Task ServeTraders(HttpListenerContext context)
    {
        try
        {
            var geoJson = await GetTradersGeoJsonAsync();
            var etag = ETagHelper.GenerateFromTimestamp(_lastTraderUpdate);

            // Check ETag and return 304 if match
            if (CheckETagMatch(context, etag))
            {
                return;
            }

            await ServeGeoJson(context, geoJson, etag, CacheHelper.ForGeoJson());
        }
        catch (Exception ex)
        {
            LogError($"Error serving traders GeoJSON: {ex.Message}", ex);
            await ServeError(context, "Failed to generate traders data");
        }
    }

    private async Task<TraderGeoJson> GetTradersGeoJsonAsync()
    {
        var now = Sapi.World.ElapsedMilliseconds;

        lock (_cacheLock)
        {
            if (_cachedTraders != null && now - _lastTraderUpdate < TraderCacheMs)
            {
                return _cachedTraders;
            }
        }

        var storageTraders = await _metadataStorage.GetTraders();

        var traders = new TraderGeoJson
        {
            Features = storageTraders.ConvertAll(trader =>
                new TraderFeature(new TraderProperties(trader.Name, trader.Type, 0),
                    new PointGeometry(GetGeoJsonCoordinates(trader.Pos))))
        };

        lock (_cacheLock)
        {
            _cachedTraders = traders;
            _lastTraderUpdate = now;
        }

        return traders;
    }

    private List<int> GetGeoJsonCoordinates(BlockPos pos)
    {
        // Use centralized coordinate transformation service
        // Converts game world coordinates to map display coordinates (Z-flip for north-up)
        var (x, y) = _coordinateService.GameToDisplay(pos);
        return [x, -y];
    }

    /// <summary>
    /// Invalidate cache to force regeneration on the next request
    /// </summary>
    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cachedTraders = null;
        }

        LogDebug("GeoJSON cache invalidated");
    }
}

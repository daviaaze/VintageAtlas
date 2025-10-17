using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VintageAtlas.Core;
using VintageAtlas.GeoJson;

namespace VintageAtlas.Web.API;

/// <summary>
/// Provides GeoJSON data dynamically via API with efficient caching
/// Scans loaded chunks in memory to find signs, signposts, traders, and translocators
/// </summary>
public class GeoJsonController(ICoreServerAPI sapi, CoordinateTransformService coordinateService)
{
    private readonly JsonSerializerSettings _jsonSettings = new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        },
        Formatting = Formatting.None, // Compact JSON for network transfer
        NullValueHandling = NullValueHandling.Ignore
    };

    // Cache GeoJSON data with timestamps (in milliseconds)
    private TraderGeoJson? _cachedTraders;
    private long _lastTraderUpdate;
    private readonly object _cacheLock = new();

    private const int TraderCacheMs = 600000; // 1 minute - traders can move/spawn

    /// <summary>
    /// Get all traders as GeoJSON
    /// </summary>
    public async Task ServeTraders(HttpListenerContext context)
    {
        try
        {
            var ifNoneMatch = context.Request.Headers["If-None-Match"];
            var geoJson = await GetTradersGeoJsonAsync();

            var json = JsonConvert.SerializeObject(geoJson, _jsonSettings);
            var etag = GenerateETag(json);

            if (ifNoneMatch == etag)
            {
                context.Response.StatusCode = 304;
                context.Response.Headers.Add("ETag", etag);
                context.Response.Close();
                return;
            }

            await ServeGeoJson(context, json, etag);
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error serving traders GeoJSON: {ex.Message}");
            await ServeError(context, "Failed to generate traders data");
        }
    }

    private async Task<TraderGeoJson> GetTradersGeoJsonAsync()
    {
        var now = sapi.World.ElapsedMilliseconds;

        lock (_cacheLock)
        {
            if (_cachedTraders != null && now - _lastTraderUpdate < TraderCacheMs)
            {
                return _cachedTraders;
            }
        }

        var traders = new TraderGeoJson();

        await Task.Run(() =>
        {
            try
            {
                // Get all loaded entities and filter for traders
                foreach (var entity in sapi.World.LoadedEntities.AsReadOnly().Values)
                {
                    if (entity is not EntityTrader trader)
                        continue;

                    var feature = CreateTraderFeature(trader);
                    traders.Features.Add(feature);
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning($"[VintageAtlas] Error scanning for traders: {ex.Message}");
            }
        });

        lock (_cacheLock)
        {
            _cachedTraders = traders;
            _lastTraderUpdate = now;
        }

        return traders;
    }

    private TraderFeature CreateTraderFeature(EntityTrader trader)
    {
        var name = trader.WatchedAttributes.GetTreeAttribute("nametag")?.GetString("name") ?? "Trader";
        var wares = Vintagestory.API.Config.Lang.Get("item-creature-" + trader.Code.Path);

        return new TraderFeature(
            new TraderProperties(name, wares, trader.Pos.AsBlockPos.Y),
            new PointGeometry(GetGeoJsonCoordinates(trader.Pos.AsBlockPos))
        );
    }

    private List<int> GetGeoJsonCoordinates(BlockPos pos)
    {
        // Use centralized coordinate transformation service
        // Converts game world coordinates to map display coordinates (Z-flip for north-up)
        var (x, y) = coordinateService.GameToDisplay(pos);
        return [x, y];
    }

    private static async Task ServeGeoJson(HttpListenerContext context, string json, string etag)
    {
        var bytes = Encoding.UTF8.GetBytes(json);

        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/geo+json";
        context.Response.ContentLength64 = bytes.Length;
        context.Response.Headers.Add("ETag", etag);
        context.Response.Headers.Add("Cache-Control", "public, max-age=30");

        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private static string GenerateETag(string content)
    {
        var hash = content.GetHashCode();
        return $"\"{hash}\"";
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

        sapi.Logger.Debug("[VintageAtlas] GeoJSON cache invalidated");
    }

    private async Task ServeError(HttpListenerContext context, string message, int statusCode = 500)
    {
        try
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var errorJson = JsonConvert.SerializeObject(new { error = message }, _jsonSettings);
            var errorBytes = Encoding.UTF8.GetBytes(errorJson);

            context.Response.ContentLength64 = errorBytes.Length;
            await context.Response.OutputStream.WriteAsync(errorBytes);
            context.Response.Close();
        }
        catch
        {
            // Silently fail
        }
    }
}

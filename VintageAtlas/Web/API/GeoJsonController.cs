using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using VintageAtlas.Core;
using VintageAtlas.GeoJson;
using VintageAtlas.GeoJson.Sign;
using VintageAtlas.GeoJson.SignPost;
using VintageAtlas.GeoJson.Trader;
using VintageAtlas.GeoJson.Translocator;

namespace VintageAtlas.Web.API;

/// <summary>
/// Provides GeoJSON data dynamically via API instead of static files
/// </summary>
public class GeoJsonController
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly JsonSerializerSettings _jsonSettings;
    
    // Cache GeoJSON data with timestamps
    private SingGeoJson? _cachedSigns;
    private SignPostGeoJson? _cachedSignPosts;
    private TraderGeoJson? _cachedTraders;
    private TranslocatorGeoJson? _cachedTranslocators;
    private long _lastSignUpdate;
    private long _lastTraderUpdate;
    private long _lastTranslocatorUpdate;
    
    private readonly object _cacheLock = new();

    public GeoJsonController(ICoreServerAPI sapi, ModConfig config)
    {
        _sapi = sapi;
        _config = config;
        
        _jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            Formatting = Formatting.None, // Compact JSON for network transfer
            NullValueHandling = NullValueHandling.Ignore
        };
    }

    /// <summary>
    /// Get all signs (landmarks) as GeoJSON
    /// </summary>
    public async Task ServeSigns(HttpListenerContext context)
    {
        try
        {
            var ifNoneMatch = context.Request.Headers["If-None-Match"];
            var geoJson = await GetSignsGeoJsonAsync();
            
            var json = JsonConvert.SerializeObject(geoJson, _jsonSettings);
            var etag = GenerateETag(json);
            
            // Check if client has cached version
            if (ifNoneMatch == etag)
            {
                context.Response.StatusCode = 304; // Not Modified
                context.Response.Headers.Add("ETag", etag);
                context.Response.Close();
                return;
            }
            
            await ServeGeoJson(context, json, etag);
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Error serving signs GeoJSON: {ex.Message}");
            await ServeError(context, "Failed to generate signs data", 500);
        }
    }

    /// <summary>
    /// Get all signposts as GeoJSON
    /// </summary>
    public async Task ServeSignPosts(HttpListenerContext context)
    {
        try
        {
            var ifNoneMatch = context.Request.Headers["If-None-Match"];
            var geoJson = await GetSignPostsGeoJsonAsync();
            
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
            _sapi.Logger.Error($"[VintageAtlas] Error serving signposts GeoJSON: {ex.Message}");
            await ServeError(context, "Failed to generate signposts data", 500);
        }
    }

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
            _sapi.Logger.Error($"[VintageAtlas] Error serving traders GeoJSON: {ex.Message}");
            await ServeError(context, "Failed to generate traders data", 500);
        }
    }

    /// <summary>
    /// Get all translocators as GeoJSON
    /// </summary>
    public async Task ServeTranslocators(HttpListenerContext context)
    {
        try
        {
            var ifNoneMatch = context.Request.Headers["If-None-Match"];
            var geoJson = await GetTranslocatorsGeoJsonAsync();
            
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
            _sapi.Logger.Error($"[VintageAtlas] Error serving translocators GeoJSON: {ex.Message}");
            await ServeError(context, "Failed to generate translocators data", 500);
        }
    }

    private Task<SingGeoJson> GetSignsGeoJsonAsync()
    {
        var now = _sapi.World.ElapsedMilliseconds;
        
        lock (_cacheLock)
        {
            // Cache for 30 seconds
            if (_cachedSigns != null && (now - _lastSignUpdate) < 30000)
            {
                return Task.FromResult(_cachedSigns);
            }
        }

        // Generate fresh data
        // Note: Live sign data would require scanning all loaded chunks which is expensive.
        // Signs should be loaded from static exports generated via /atlas export
        // This endpoint returns empty for now - use static geojson files instead
        var signs = new SingGeoJson();

        lock (_cacheLock)
        {
            _cachedSigns = signs;
            _lastSignUpdate = now;
        }

        return Task.FromResult(signs);
    }

    private Task<SignPostGeoJson> GetSignPostsGeoJsonAsync()
    {
        var now = _sapi.World.ElapsedMilliseconds;
        
        lock (_cacheLock)
        {
            if (_cachedSignPosts != null && (now - _lastSignUpdate) < 30000)
            {
                return Task.FromResult(_cachedSignPosts);
            }
        }

        var signPosts = new SignPostGeoJson();
        // Note: Live signpost data would require scanning all loaded chunks which is expensive.
        // Signposts should be loaded from static exports generated via /atlas export

        lock (_cacheLock)
        {
            _cachedSignPosts = signPosts;
            _lastSignUpdate = now;
        }

        return Task.FromResult(signPosts);
    }

    private async Task<TraderGeoJson> GetTradersGeoJsonAsync()
    {
        var now = _sapi.World.ElapsedMilliseconds;
        
        lock (_cacheLock)
        {
            if (_cachedTraders != null && (now - _lastTraderUpdate) < 60000) // Cache for 1 minute
            {
                return _cachedTraders;
            }
        }

        var traders = new TraderGeoJson();
        
        await Task.Run(() =>
        {
            // Get all loaded entities
            foreach (var entity in _sapi.World.LoadedEntities.Values)
            {
                if (entity is EntityTrader trader)
                {
                    var feature = CreateTraderFeature(trader);
                    if (feature != null)
                    {
                        traders.Features.Add(feature);
                    }
                }
            }
        });

        lock (_cacheLock)
        {
            _cachedTraders = traders;
            _lastTraderUpdate = now;
        }

        return traders;
    }

    private Task<TranslocatorGeoJson> GetTranslocatorsGeoJsonAsync()
    {
        var now = _sapi.World.ElapsedMilliseconds;
        
        lock (_cacheLock)
        {
            if (_cachedTranslocators != null && (now - _lastTranslocatorUpdate) < 60000)
            {
                return Task.FromResult(_cachedTranslocators);
            }
        }

        var translocators = new TranslocatorGeoJson();
        // Note: Live translocator data would require scanning all loaded chunks which is expensive.
        // Translocators should be loaded from static exports generated via /atlas export

        lock (_cacheLock)
        {
            _cachedTranslocators = translocators;
            _lastTranslocatorUpdate = now;
        }

        return Task.FromResult(translocators);
    }

    private SignFeature? CreateSignFeature(BlockEntitySign signEntity)
    {
        if (string.IsNullOrWhiteSpace(signEntity.text)) return null;
        
        // Apply same filtering logic as Extractor
        var text = signEntity.text.Trim();
        
        // Check for tagged signs
        if (text.StartsWith("<AM:"))
        {
            var endTag = text.IndexOf('>');
            if (endTag > 0)
            {
                var tag = text.Substring(4, endTag - 4).ToLowerInvariant();
                var content = text.Substring(endTag + 1).Trim();
                
                if (tag == "base" || tag == "misc" || tag == "server" || (_config.ExportCustomTaggedSigns && tag != "tl"))
                {
                    return new SignFeature(
                        new SignProperties(content, signEntity.Pos.Y, char.ToUpper(tag[0]) + tag.Substring(1)),
                        new PointGeometry(GetGeoJsonCoordinates(signEntity.Pos))
                    );
                }
            }
        }
        else if (_config.ExportUntaggedSigns)
        {
            return new SignFeature(
                new SignProperties(text, signEntity.Pos.Y, "default"),
                new PointGeometry(GetGeoJsonCoordinates(signEntity.Pos))
            );
        }

        return null;
    }

    private List<SignPostFeature> CreateSignPostFeatures(BlockEntitySignPost signPostEntity)
    {
        var features = new List<SignPostFeature>();
        
        for (int i = 0; i < signPostEntity.textByCardinalDirection.Length; i++)
        {
            var text = signPostEntity.textByCardinalDirection[i];
            if (!string.IsNullOrWhiteSpace(text))
            {
                features.Add(new SignPostFeature(
                    new SignPostProperties("SignPost", text.Trim(), signPostEntity.Pos.Y, i),
                    new PointGeometry(GetGeoJsonCoordinates(signPostEntity.Pos))
                ));
            }
        }

        return features;
    }

    private TraderFeature? CreateTraderFeature(EntityTrader trader)
    {
        var name = trader.WatchedAttributes.GetTreeAttribute("nametag")?.GetString("name") ?? "Trader";
        var wares = Vintagestory.API.Config.Lang.Get("item-creature-" + trader.Code.Path);
        
        return new TraderFeature(
            new TraderProperties(name, wares, trader.Pos.AsBlockPos.Y),
            new PointGeometry(GetGeoJsonCoordinates(trader.Pos.AsBlockPos))
        );
    }

    private TranslocatorFeature? CreateTranslocatorFeature(BlockEntityStaticTranslocator tlEntity)
    {
        if (tlEntity.TargetLocation == null) return null;
        
        var coordinates = new List<List<int>>
        {
            GetGeoJsonCoordinates(tlEntity.Pos),
            GetGeoJsonCoordinates(tlEntity.TargetLocation)
        };
        
        return new TranslocatorFeature(
            new TranslocatorProperties(tlEntity.Pos.Y, tlEntity.TargetLocation.Y),
            new LineGeometry(coordinates),
            "Feature"
        );
    }

    private List<int> GetGeoJsonCoordinates(BlockPos pos)
    {
        var x = pos.X;
        var z = pos.Z;
        
        var spawnPos = _sapi.World.DefaultSpawnPosition?.AsBlockPos;
        var spawnX = spawnPos?.X ?? _sapi.World.BlockAccessor.MapSizeX / 2;
        var spawnZ = spawnPos?.Z ?? _sapi.World.BlockAccessor.MapSizeZ / 2;
        
        if (!_config.AbsolutePositions)
        {
            x = x - spawnX;
            z = (z - spawnZ) * -1;
        }
        
        return new List<int> { x, z };
    }

    private async Task ServeGeoJson(HttpListenerContext context, string json, string etag)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/geo+json";
        context.Response.ContentLength64 = bytes.Length;
        context.Response.Headers.Add("ETag", etag);
        context.Response.Headers.Add("Cache-Control", "public, max-age=30");
        
        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        context.Response.Close();
    }

    private string GenerateETag(string content)
    {
        var hash = content.GetHashCode();
        return $"\"{hash}\"";
    }

    /// <summary>
    /// Invalidate cache to force regeneration on next request
    /// </summary>
    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cachedSigns = null;
            _cachedSignPosts = null;
            _cachedTraders = null;
            _cachedTranslocators = null;
        }
        
        _sapi.Logger.Debug("[VintageAtlas] GeoJSON cache invalidated");
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
            await context.Response.OutputStream.WriteAsync(errorBytes, 0, errorBytes.Length);
            context.Response.Close();
        }
        catch
        {
            // Silently fail
        }
    }
}


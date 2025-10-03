using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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
/// Provides GeoJSON data dynamically via API with efficient caching
/// Scans loaded chunks in memory to find signs, signposts, traders, and translocators
/// </summary>
public class GeoJsonController
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly JsonSerializerSettings _jsonSettings;
    
    // Cache GeoJSON data with timestamps (in milliseconds)
    private SingGeoJson? _cachedSigns;
    private SignPostGeoJson? _cachedSignPosts;
    private TraderGeoJson? _cachedTraders;
    private TranslocatorGeoJson? _cachedTranslocators;
    private ChunkversionGeoJson? _cachedChunks;
    private long _lastSignUpdate;
    private long _lastTraderUpdate;
    private long _lastTranslocatorUpdate;
    private long _lastChunkUpdate;
    
    private readonly object _cacheLock = new();
    
    // Cache duration constants
    private const int SIGN_CACHE_MS = 300000; // 5 minutes - signs change rarely
    private const int TRADER_CACHE_MS = 60000; // 1 minute - traders can move/spawn
    private const int TRANSLOCATOR_CACHE_MS = 120000; // 2 minutes - translocators are semi-static
    private const int CHUNK_CACHE_MS = 600000; // 10 minutes - chunks change very rarely
    
    // Scan radius in chunks around players
    private const int SCAN_RADIUS_CHUNKS = 16; // ~512 blocks radius (16 * 32)

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

    /// <summary>
    /// Get chunk boundaries as GeoJSON
    /// </summary>
    public async Task ServeChunks(HttpListenerContext context)
    {
        try
        {
            var ifNoneMatch = context.Request.Headers["If-None-Match"];
            var geoJson = await GetChunksGeoJsonAsync();
            
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
            _sapi.Logger.Error($"[VintageAtlas] Error serving chunks GeoJSON: {ex.Message}");
            await ServeError(context, "Failed to generate chunks data", 500);
        }
    }

    private async Task<SingGeoJson> GetSignsGeoJsonAsync()
    {
        // Check if world is ready (can be null during early startup)
        if (_sapi.World?.BlockAccessor == null)
        {
            return new SingGeoJson(); // Return empty result
        }
        
        var now = _sapi.World.ElapsedMilliseconds;
        
        lock (_cacheLock)
        {
            // Return cached data if still valid
            if (_cachedSigns != null && now - _lastSignUpdate < SIGN_CACHE_MS)
            {
                return _cachedSigns;
            }
        }

        // Scan for signs asynchronously
        var signs = new SingGeoJson();
        var signPosts = new SignPostGeoJson();
        
        await Task.Run(() =>
        {
            try
            {
                // Get chunks to scan (around players and spawn)
                var chunksToScan = GetChunksToScan();
                
                _sapi.Logger.Debug($"[VintageAtlas] Scanning {chunksToScan.Count} chunks for signs");
                
                foreach (var chunkPos in chunksToScan)
                {
                    ScanChunkForSigns(chunkPos, signs, signPosts);
                }
                
                _sapi.Logger.Debug($"[VintageAtlas] Found {signs.Features.Count} signs and {signPosts.Features.Count} signposts");
            }
            catch (Exception ex)
            {
                _sapi.Logger.Warning($"[VintageAtlas] Error scanning for signs: {ex.Message}");
            }
        });

        lock (_cacheLock)
        {
            _cachedSigns = signs;
            _cachedSignPosts = signPosts;
            _lastSignUpdate = now;
        }

        return signs;
    }

    private Task<SignPostGeoJson> GetSignPostsGeoJsonAsync()
    {
        var now = _sapi.World.ElapsedMilliseconds;
        
        lock (_cacheLock)
        {
            // Return cached data if still valid
            if (_cachedSignPosts != null && now - _lastSignUpdate < SIGN_CACHE_MS)
            {
                return Task.FromResult(_cachedSignPosts);
            }
        }

        // Signs and signposts are cached together, so trigger sign update
        GetSignsGeoJsonAsync().Wait();

        lock (_cacheLock)
        {
            return Task.FromResult(_cachedSignPosts ?? new SignPostGeoJson());
        }
    }

    private async Task<TraderGeoJson> GetTradersGeoJsonAsync()
    {
        var now = _sapi.World.ElapsedMilliseconds;
        
        lock (_cacheLock)
        {
            if (_cachedTraders != null && now - _lastTraderUpdate < TRADER_CACHE_MS)
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
                
                _sapi.Logger.Debug($"[VintageAtlas] Found {traders.Features.Count} traders");
            }
            catch (Exception ex)
            {
                _sapi.Logger.Warning($"[VintageAtlas] Error scanning for traders: {ex.Message}");
            }
        });

        lock (_cacheLock)
        {
            _cachedTraders = traders;
            _lastTraderUpdate = now;
        }

        return traders;
    }

    private async Task<TranslocatorGeoJson> GetTranslocatorsGeoJsonAsync()
    {
        // Check if world is ready (can be null during early startup)
        if (_sapi.World?.BlockAccessor == null)
        {
            return new TranslocatorGeoJson(); // Return empty result
        }
        
        var now = _sapi.World.ElapsedMilliseconds;
        
        lock (_cacheLock)
        {
            if (_cachedTranslocators != null && now - _lastTranslocatorUpdate < TRANSLOCATOR_CACHE_MS)
            {
                return _cachedTranslocators;
            }
        }

        var translocators = new TranslocatorGeoJson();
        
        await Task.Run(() =>
        {
            try
            {
                // Get chunks to scan (around players and spawn)
                var chunksToScan = GetChunksToScan();
                var processedPairs = new HashSet<string>();
                
                _sapi.Logger.Debug($"[VintageAtlas] Scanning {chunksToScan.Count} chunks for translocators");
                
                foreach (var chunkPos in chunksToScan)
                {
                    ScanChunkForTranslocators(chunkPos, translocators, processedPairs);
                }
                
                _sapi.Logger.Debug($"[VintageAtlas] Found {translocators.Features.Count} translocators");
            }
            catch (Exception ex)
            {
                _sapi.Logger.Warning($"[VintageAtlas] Error scanning for translocators: {ex.Message}");
            }
        });

        lock (_cacheLock)
        {
            _cachedTranslocators = translocators;
            _lastTranslocatorUpdate = now;
        }

        return translocators;
    }

    private async Task<ChunkversionGeoJson> GetChunksGeoJsonAsync()
    {
        var now = _sapi.World.ElapsedMilliseconds;
        
        lock (_cacheLock)
        {
            if (_cachedChunks != null && now - _lastChunkUpdate < CHUNK_CACHE_MS)
            {
                return _cachedChunks;
            }
        }

        var chunks = new ChunkversionGeoJson { Name = "chunks" };
        
        await Task.Run(() =>
        {
            try
            {
                // Get chunks to visualize (around players and spawn)
                var chunksToVisualize = GetChunksToScan();
                
                _sapi.Logger.Debug($"[VintageAtlas] Generating boundaries for {chunksToVisualize.Count} chunks");
                
                foreach (var chunkPos in chunksToVisualize)
                {
                    var feature = CreateChunkBoundaryFeature(chunkPos);
                    if (feature != null)
                    {
                        chunks.Features.Add(feature);
                    }
                }
                
                _sapi.Logger.Debug($"[VintageAtlas] Generated {chunks.Features.Count} chunk boundaries");
            }
            catch (Exception ex)
            {
                _sapi.Logger.Warning($"[VintageAtlas] Error generating chunk boundaries: {ex.Message}");
            }
        });

        lock (_cacheLock)
        {
            _cachedChunks = chunks;
            _lastChunkUpdate = now;
        }

        return chunks;
    }

    /// <summary>
    /// Get list of chunk positions to scan (around players and spawn)
    /// </summary>
    private HashSet<Vec2i> GetChunksToScan()
    {
        var chunks = new HashSet<Vec2i>();
        
        // Add chunks around spawn point
        var spawnPos = _sapi.World.DefaultSpawnPosition?.AsBlockPos;
        if (spawnPos != null)
        {
            AddChunksAroundPosition(chunks, spawnPos.X, spawnPos.Z);
        }
        
        // Add chunks around all online players
        foreach (var player in _sapi.World.AllOnlinePlayers)
        {
            if (player?.Entity?.Pos != null)
            {
                var pos = player.Entity.Pos.AsBlockPos;
                AddChunksAroundPosition(chunks, pos.X, pos.Z);
            }
        }
        
        return chunks;
    }

    /// <summary>
    /// Add chunks in a radius around a world position
    /// </summary>
    private void AddChunksAroundPosition(HashSet<Vec2i> chunks, int worldX, int worldZ)
    {
        var centerChunkX = worldX / 32;
        var centerChunkZ = worldZ / 32;
        
        for (var x = centerChunkX - SCAN_RADIUS_CHUNKS; x <= centerChunkX + SCAN_RADIUS_CHUNKS; x++)
        {
            for (var z = centerChunkZ - SCAN_RADIUS_CHUNKS; z <= centerChunkZ + SCAN_RADIUS_CHUNKS; z++)
            {
                chunks.Add(new Vec2i(x, z));
            }
        }
    }

    /// <summary>
    /// Scan a chunk for signs and signposts
    /// </summary>
    private void ScanChunkForSigns(Vec2i chunkPos, SingGeoJson signs, SignPostGeoJson signPosts)
    {
        try
        {
            // Scan all Y levels for this X/Z chunk column
            var chunkSize = 32;
            var mapSizeY = _sapi.World.BlockAccessor.MapSizeY;
            
            for (var chunkY = 0; chunkY < mapSizeY / chunkSize; chunkY++)
            {
                // Iterate through blocks in the chunk
                for (var x = 0; x < chunkSize; x++)
                {
                    for (var y = 0; y < chunkSize; y++)
                    {
                        for (var z = 0; z < chunkSize; z++)
                        {
                            var worldPos = new BlockPos(
                                chunkPos.X * chunkSize + x,
                                chunkY * chunkSize + y,
                                chunkPos.Y * chunkSize + z
                            );
                            
                            var blockEntity = _sapi.World.BlockAccessor.GetBlockEntity(worldPos);
                            
                            if (blockEntity is BlockEntitySign signEntity)
                            {
                                var feature = CreateSignFeature(signEntity);
                                if (feature != null)
                                {
                                    lock (signs)
                                    {
                                        signs.Features.Add(feature);
                                    }
                                }
                            }
                            else if (blockEntity is BlockEntitySignPost signPostEntity)
                            {
                                var features = CreateSignPostFeatures(signPostEntity);
                                if (features.Count > 0)
                                {
                                    lock (signPosts)
                                    {
                                        signPosts.Features.AddRange(features);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _sapi.Logger.Debug($"[VintageAtlas] Error scanning chunk {chunkPos}: {ex.Message}");
        }
    }

    /// <summary>
    /// Scan a chunk for translocators
    /// </summary>
    private void ScanChunkForTranslocators(Vec2i chunkPos, TranslocatorGeoJson translocators, HashSet<string> processedPairs)
    {
        try
        {
            // Scan all Y levels for this X/Z chunk column
            var chunkSize = 32;
            var mapSizeY = _sapi.World.BlockAccessor.MapSizeY;
            
            for (var chunkY = 0; chunkY < mapSizeY / chunkSize; chunkY++)
            {
                // Iterate through blocks in the chunk
                for (var x = 0; x < chunkSize; x++)
                {
                    for (var y = 0; y < chunkSize; y++)
                    {
                        for (var z = 0; z < chunkSize; z++)
                        {
                            var worldPos = new BlockPos(
                                chunkPos.X * chunkSize + x,
                                chunkY * chunkSize + y,
                                chunkPos.Y * chunkSize + z
                            );
                            
                            var blockEntity = _sapi.World.BlockAccessor.GetBlockEntity(worldPos);
                            
                            if (blockEntity is BlockEntityStaticTranslocator tlEntity && tlEntity.TargetLocation != null)
                            {
                                // Create unique pair ID to avoid duplicates
                                var pairId = $"{tlEntity.Pos}:{tlEntity.TargetLocation}";
                                var reversePairId = $"{tlEntity.TargetLocation}:{tlEntity.Pos}";
                                
                                lock (processedPairs)
                                {
                                    if (!processedPairs.Contains(pairId) && !processedPairs.Contains(reversePairId))
                                    {
                                        processedPairs.Add(pairId);
                                        processedPairs.Add(reversePairId);
                                        
                                        var feature = CreateTranslocatorFeature(tlEntity);
                                        if (feature != null)
                                        {
                                            lock (translocators)
                                            {
                                                translocators.Features.Add(feature);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _sapi.Logger.Debug($"[VintageAtlas] Error scanning chunk {chunkPos} for translocators: {ex.Message}");
        }
    }

    private SignFeature? CreateSignFeature(BlockEntitySign signEntity)
    {
        if (string.IsNullOrWhiteSpace(signEntity.text)) return null;
        
        // Apply same filtering logic as Extractor
        var text = signEntity.text.Trim();
        
        // Check for tagged signs using regex (same pattern as Extractor)
        var match = Regex.Match(text, "^<AM:(.*)>\n(.*)", RegexOptions.Singleline);
        if (match.Success)
        {
            var tag = match.Groups[1].Value.ToLowerInvariant();
            var content = match.Groups[2].Value.Trim();
                
                if (tag == "base" || tag == "misc" || tag == "server" || (_config.ExportCustomTaggedSigns && tag != "tl"))
                {
                    return new SignFeature(
                        new SignProperties(content, signEntity.Pos.Y, char.ToUpper(tag[0]) + tag.Substring(1)),
                        new PointGeometry(GetGeoJsonCoordinates(signEntity.Pos))
                    );
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
        
        for (var i = 0; i < signPostEntity.textByCardinalDirection.Length; i++)
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

    private ChunkVersionFeature? CreateChunkBoundaryFeature(Vec2i chunkPos)
    {
        try
        {
            const int chunkSize = 32; // Chunks are 32x32 blocks
            
            // Calculate world coordinates for chunk corners
            var worldX = chunkPos.X * chunkSize;
            var worldZ = chunkPos.Y * chunkSize;
            
            // Create polygon coordinates (clockwise from top-left)
            // In GeoJSON, first and last coordinate must be the same to close the polygon
            var corner1 = GetGeoJsonCoordinates(new BlockPos(worldX, 0, worldZ));
            var corner2 = GetGeoJsonCoordinates(new BlockPos(worldX + chunkSize, 0, worldZ));
            var corner3 = GetGeoJsonCoordinates(new BlockPos(worldX + chunkSize, 0, worldZ + chunkSize));
            var corner4 = GetGeoJsonCoordinates(new BlockPos(worldX, 0, worldZ + chunkSize));
            
            // Create the polygon ring (outer boundary)
            var ring = new List<List<int>>
            {
                corner1,
                corner2,
                corner3,
                corner4,
                corner1 // Close the polygon
            };
            
            var coordinates = new List<List<List<int>>> { ring };
            
            var properties = new ChunkVersionProperties(
                Color: "rgba(100, 149, 237, 0.2)", // Light blue with transparency
                Version: $"Chunk {chunkPos.X},{chunkPos.Y}"
            );
            
            return new ChunkVersionFeature(
                properties,
                new PolygonGeometry(coordinates)
            );
        }
        catch (Exception ex)
        {
            _sapi.Logger.Debug($"[VintageAtlas] Error creating chunk boundary for {chunkPos}: {ex.Message}");
            return null;
        }
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
            _cachedChunks = null;
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

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using Vintagestory.Server;
using VintageAtlas.Core;
using VintageAtlas.Export;
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
public class GeoJsonController(ICoreServerAPI sapi, ModConfig config, CoordinateTransformService coordinateService)
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
    private SingGeoJson? _cachedSigns;
    private SignPostGeoJson? _cachedSignPosts;
    private TraderGeoJson? _cachedTraders;
    private TranslocatorGeoJson? _cachedTranslocators;
    private ChunkversionGeoJson? _cachedChunks;
    private ChunkversionGeoJson? _cachedChunkVersions;
    private long _lastSignUpdate;
    private long _lastTraderUpdate;
    private long _lastTranslocatorUpdate;
    private long _lastChunkUpdate;
    private long _lastChunkVersionUpdate;

    private readonly object _cacheLock = new();

    // Cache duration constants
    private const int SignCacheMs = 300000; // 5 minutes - signs change rarely
    private const int TraderCacheMs = 60000; // 1 minute - traders can move/spawn
    private const int TranslocatorCacheMs = 120000; // 2 minutes - translocators are semi-static
    private const int ChunkCacheMs = 600000; // 10 minutes - chunks change very rarely
    private const int ChunkVersionCacheMs = 1800000; // 30 minutes - chunk versions NEVER change

    // Scan radius in chunks around players
    private const int ScanRadiusChunks = 16; // ~512 blocks radius (16 * 32)

    // Compact JSON for network transfer

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

            // Check if the client has a cached version
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
            sapi.Logger.Error($"[VintageAtlas] Error serving signs GeoJSON: {ex.Message}");
            await ServeError(context, "Failed to generate signs data");
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
            sapi.Logger.Error($"[VintageAtlas] Error serving signposts GeoJSON: {ex.Message}");
            await ServeError(context, "Failed to generate signposts data");
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
            sapi.Logger.Error($"[VintageAtlas] Error serving traders GeoJSON: {ex.Message}");
            await ServeError(context, "Failed to generate traders data");
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
            sapi.Logger.Error($"[VintageAtlas] Error serving translocators GeoJSON: {ex.Message}");
            await ServeError(context, "Failed to generate translocators data");
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
            sapi.Logger.Error($"[VintageAtlas] Error serving chunks GeoJSON: {ex.Message}");
            await ServeError(context, "Failed to generate chunks data");
        }
    }

    /// <summary>
    /// Get chunk versions as GeoJSON with colored polygons for each version
    /// </summary>
    public async Task ServeChunkVersions(HttpListenerContext context)
    {
        try
        {
            var ifNoneMatch = context.Request.Headers["If-None-Match"];
            var geoJson = await GetChunkVersionsGeoJsonAsync();

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
            sapi.Logger.Error($"[VintageAtlas] Error serving chunk versions GeoJSON: {ex.Message}");
            await ServeError(context, "Failed to generate chunk version data");
        }
    }

    private async Task<SingGeoJson> GetSignsGeoJsonAsync()
    {
        // Check if world is ready (can be null during early startup)
        if (sapi.World?.BlockAccessor == null)
        {
            return new SingGeoJson(); // Return empty result
        }

        var now = sapi.World.ElapsedMilliseconds;

        lock (_cacheLock)
        {
            // Return cached data if still valid
            if (_cachedSigns != null && now - _lastSignUpdate < SignCacheMs)
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

                foreach (var chunkPos in chunksToScan)
                {
                    ScanChunkForSigns(chunkPos, signs, signPosts);
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning($"[VintageAtlas] Error scanning for signs: {ex.Message}");
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
        var now = sapi.World.ElapsedMilliseconds;

        lock (_cacheLock)
        {
            // Return cached data if still valid
            if (_cachedSignPosts != null && now - _lastSignUpdate < SignCacheMs)
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
                foreach (var entity in sapi.World.LoadedEntities.Values)
                {
                    if (entity is not EntityTrader trader)
                        continue;

                    var feature = CreateTraderFeature(trader);
                    if (feature != null)
                    {
                        traders.Features.Add(feature);
                    }
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

    private async Task<TranslocatorGeoJson> GetTranslocatorsGeoJsonAsync()
    {
        // Check if world is ready (can be null during early startup)
        if (sapi.World?.BlockAccessor == null)
        {
            return new TranslocatorGeoJson(); // Return empty result
        }

        var now = sapi.World.ElapsedMilliseconds;

        lock (_cacheLock)
        {
            if (_cachedTranslocators != null && now - _lastTranslocatorUpdate < TranslocatorCacheMs)
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
                foreach (var chunkPos in chunksToScan)
                {
                    ScanChunkForTranslocators(chunkPos, translocators, processedPairs);
                }
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning($"[VintageAtlas] Error scanning for translocators: {ex.Message}");
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
        var now = sapi.World.ElapsedMilliseconds;

        lock (_cacheLock)
        {
            if (_cachedChunks != null && now - _lastChunkUpdate < ChunkCacheMs)
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

                sapi.Logger.Debug($"[VintageAtlas] Generating boundaries for {chunksToVisualize.Count} chunks");

                foreach (var chunkPos in chunksToVisualize)
                {
                    var feature = CreateChunkBoundaryFeature(chunkPos);
                    if (feature != null)
                    {
                        chunks.Features.Add(feature);
                    }
                }

                sapi.Logger.Debug($"[VintageAtlas] Generated {chunks.Features.Count} chunk boundaries");
            }
            catch (Exception ex)
            {
                sapi.Logger.Warning($"[VintageAtlas] Error generating chunk boundaries: {ex.Message}");
            }
        });

        lock (_cacheLock)
        {
            _cachedChunks = chunks;
            _lastChunkUpdate = now;
        }

        return chunks;
    }

    private async Task<ChunkversionGeoJson> GetChunkVersionsGeoJsonAsync()
    {
        var now = sapi.World.ElapsedMilliseconds;

        lock (_cacheLock)
        {
            if (_cachedChunkVersions != null && now - _lastChunkVersionUpdate < ChunkVersionCacheMs)
            {
                return _cachedChunkVersions;
            }
        }

        var chunkVersions = new ChunkversionGeoJson { Name = "chunk_versions" };

        await Task.Run(() =>
        {
            try
            {
                sapi.Logger.Notification("[VintageAtlas] Generating chunk version visualization...");

                // Extract chunk version data from savegame
                var server = (ServerMain)sapi.World;
                var dataLoader = new SavegameDataLoader(server, config.MaxDegreeOfParallelism, sapi.Logger);
                var extractor = new ChunkVersionExtractor(dataLoader, sapi.Logger);

                var chunkData = extractor.ExtractChunkVersions();

                if (chunkData.Count == 0)
                {
                    sapi.Logger.Warning("[VintageAtlas] No chunk version data available");
                    return;
                }

                // Group chunks by version using GroupChunks
                var grouper = new GroupChunks(chunkData, server);
                var grouped = grouper.GroupPositions();

                sapi.Logger.Notification($"[VintageAtlas] Found {grouped.Count} version groups");

                // Generate gradient colors for versions
                grouper.GenerateGradient(grouped);

                // Create GeoJSON features for each version group
                foreach (var group in grouped)
                {
                    try
                    {
                        var feature = grouper.GetShape(group);
                        chunkVersions.Features.Add(feature);
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Warning($"[VintageAtlas] Error creating shape for version {group.Version}: {ex.Message}");
                    }
                }

                sapi.Logger.Notification($"[VintageAtlas] Generated {chunkVersions.Features.Count} chunk version features");

                // Cleanup
                dataLoader.Dispose();
            }
            catch (Exception ex)
            {
                sapi.Logger.Error($"[VintageAtlas] Error generating chunk versions: {ex.Message}");
                sapi.Logger.Error(ex.StackTrace ?? "");
            }
        });

        lock (_cacheLock)
        {
            _cachedChunkVersions = chunkVersions;
            _lastChunkVersionUpdate = now;
        }

        return chunkVersions;
    }

    /// <summary>
    /// Get list of chunk positions to scan (around players and spawn)
    /// </summary>
    private HashSet<Vec2i> GetChunksToScan()
    {
        var chunks = new HashSet<Vec2i>();

        // Add chunks around spawn point
        var spawnPos = sapi.World.DefaultSpawnPosition?.AsBlockPos;
        if (spawnPos != null)
        {
            AddChunksAroundPosition(chunks, spawnPos.X, spawnPos.Z);
        }

        // Add chunks around all online players
        foreach (var player in sapi.World.AllOnlinePlayers)
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
    private static void AddChunksAroundPosition(HashSet<Vec2i> chunks, int worldX, int worldZ)
    {
        var centerChunkX = worldX / 32;
        var centerChunkZ = worldZ / 32;

        for (var x = centerChunkX - ScanRadiusChunks; x <= centerChunkX + ScanRadiusChunks; x++)
        {
            for (var z = centerChunkZ - ScanRadiusChunks; z <= centerChunkZ + ScanRadiusChunks; z++)
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
            const int chunkSize = 32;

            // TODO: What is this?
            var mapSizeY = sapi.World.BlockAccessor.MapSizeY;

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

                            var blockEntity = sapi.World.BlockAccessor.GetBlockEntity(worldPos);

                            switch (blockEntity)
                            {
                                case BlockEntitySign signEntity:
                                    {
                                        var feature = CreateSignFeature(signEntity);
                                        if (feature is null)
                                            continue;

                                        lock (signs)
                                        {
                                            signs.Features.Add(feature);
                                        }

                                        break;
                                    }
                                case BlockEntitySignPost signPostEntity:
                                    {
                                        var features = CreateSignPostFeatures(signPostEntity);
                                        if (features.Count > 0)
                                        {
                                            lock (signPosts)
                                            {
                                                signPosts.Features.AddRange(features);
                                            }
                                        }

                                        break;
                                    }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            sapi.Logger.Debug($"[VintageAtlas] Error scanning chunk {chunkPos}: {ex.Message}");
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
            const int chunkSize = 32;
            var mapSizeY = sapi.World.BlockAccessor.MapSizeY;

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

                            var blockEntity = sapi.World.BlockAccessor.GetBlockEntity(worldPos);

                            if (blockEntity is not BlockEntityStaticTranslocator tlEntity ||
                                tlEntity.TargetLocation == null) continue;
                            // Create a unique pair ID to avoid duplicates
                            var pairId = $"{tlEntity.Pos}:{tlEntity.TargetLocation}";
                            var reversePairId = $"{tlEntity.TargetLocation}:{tlEntity.Pos}";

                            lock (processedPairs)
                            {
                                if (processedPairs.Contains(pairId) || processedPairs.Contains(reversePairId))
                                    continue;

                                processedPairs.Add(pairId);
                                processedPairs.Add(reversePairId);

                                var feature = CreateTranslocatorFeature(tlEntity);
                                if (feature == null)
                                    continue;

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
        catch (Exception ex)
        {
            sapi.Logger.Debug($"[VintageAtlas] Error scanning chunk {chunkPos} for translocators: {ex.Message}");
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

            if (tag == "base" || tag == "misc" || tag == "server" || (config.ExportCustomTaggedSigns && tag != "tl"))
            {
                return new SignFeature(
                    new SignProperties(content, signEntity.Pos.Y, char.ToUpper(tag[0]) + tag.Substring(1)),
                    new PointGeometry(GetGeoJsonCoordinates(signEntity.Pos))
                );
            }
        }
        else if (config.ExportUntaggedSigns)
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

    private TraderFeature CreateTraderFeature(EntityTrader trader)
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
            // In GeoJSON, the first and last coordinate must be the same to close the polygon
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
            sapi.Logger.Debug($"[VintageAtlas] Error creating chunk boundary for {chunkPos}: {ex.Message}");
            return null;
        }
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
            _cachedSigns = null;
            _cachedSignPosts = null;
            _cachedTraders = null;
            _cachedTranslocators = null;
            _cachedChunks = null;
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

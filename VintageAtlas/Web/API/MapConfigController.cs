using System;
using System.Net;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Export;
using VintageAtlas.Models.API;
using VintageAtlas.Web.API.Base;
using VintageAtlas.Web.API.Helpers;

namespace VintageAtlas.Web.API;

/// <summary>
/// Provides dynamic map configuration (extent, center, zoom levels, etc.)
/// Replaces hardcoded values in frontend mapConfig.ts
/// </summary>
public class MapConfigController : JsonController
{
    private readonly ITileGenerator? _tileGenerator;
    private MapConfigData? _cachedConfig;
    private long _lastConfigUpdate;
    private readonly object _cacheLock = new();

    public MapConfigController(ICoreServerAPI sapi, ITileGenerator? tileGenerator = null) : base(sapi)
    {
        _tileGenerator = tileGenerator;
    }

    /// <summary>
    /// Serve map configuration as JSON
    /// </summary>
    public async Task ServeMapConfig(HttpListenerContext context)
    {
        try
        {
            var mapConfig = GetMapConfig();
            await ServeJson(context, mapConfig, cacheControl: CacheHelper.ForMapData());
        }
        catch (Exception ex)
        {
            LogError($"Error serving map config: {ex.Message}", ex);
            await ServeError(context, "Failed to get map configuration");
        }
    }


    /// <summary>
    /// Get the current map configuration (used by TileController for coordinate transformation)
    /// Virtual to allow mocking in unit tests
    /// </summary>
    public virtual MapConfigData? GetCurrentConfig()
    {
        // Check if the world is ready
        if (Sapi.World?.BlockAccessor == null)
        {
            return null; // Return null if world not initialized yet
        }

        try
        {
            return GetMapConfig();
        }
        catch
        {
            return null; // Return null on any error
        }
    }

    private MapConfigData GetMapConfig()
    {
        // Check if the world is ready (can be null during early startup)
        if (Sapi.World?.BlockAccessor == null)
        {
            throw new InvalidOperationException("World not yet initialized");
        }

        var now = Sapi.World.ElapsedMilliseconds;

        lock (_cacheLock)
        {
            // Cache for 5 minutes
            if (_cachedConfig != null && now - _lastConfigUpdate < 300000)
            {
                return _cachedConfig;
            }
        }

        var mapConfig = GenerateMapConfig();

        lock (_cacheLock)
        {
            _cachedConfig = mapConfig;
            _lastConfigUpdate = now;
        }

        return mapConfig;
    }

    private MapConfigData GenerateMapConfig()
    {
        var spawn = Sapi.World.DefaultSpawnPosition.AsBlockPos;

        var mapSizeX = Sapi.World.BlockAccessor.MapSizeX / 2;
        var mapSizeZ = Sapi.World.BlockAccessor.MapSizeZ / 2;

        // Legacy WebCartographer coordinate system
        int[] worldExtent = [-mapSizeX, -mapSizeZ, mapSizeX, mapSizeZ];
        int[] worldOrigin = [-mapSizeX, mapSizeZ];
        int[] defaultCenter = [spawn.X - mapSizeX, spawn.Z - mapSizeZ];

        double[] webCartographerResolutions = [512, 256, 128, 64, 32, 16, 8, 4, 2, 1];
        var maxZoom = webCartographerResolutions.Length - 1;

        var originTiles = new int[webCartographerResolutions.Length][];

        for (var zoom = 0; zoom < webCartographerResolutions.Length; zoom++)
        {
            var resolution = webCartographerResolutions[zoom];
            int originTileX = (int)Math.Floor(worldOrigin[0] / (resolution * 256));
            int originTileY = (int)Math.Floor(worldOrigin[1] / (resolution * 256));

            if (_tileGenerator != null)
            {
                try
                {
                    var extent = _tileGenerator.GetTileExtentAsync(zoom).GetAwaiter().GetResult();
                    if (extent != null)
                    {
                        originTileX = extent.MinX;
                        originTileY = extent.MinY;
                    }
                }
                catch
                {
                    // Fallback to default calculation if extent lookup fails
                }
            }

            originTiles[zoom] = [originTileX, originTileY];
            LogDebug($"Zoom {zoom} origin offset: ({originTileX},{originTileY})");
        }

        return new MapConfigData
        {
            WorldExtent = worldExtent,
            WorldOrigin = worldOrigin,
            DefaultCenter = defaultCenter,
            DefaultZoom = maxZoom,

            MinZoom = 0,
            MaxZoom = maxZoom,
            BaseZoomLevel = maxZoom,

            TileSize = 256,
            TileResolutions = webCartographerResolutions,
            ViewResolutions = webCartographerResolutions,
            OriginTilesPerZoom = originTiles,

            MapSizeX = Sapi.World.BlockAccessor.MapSizeX,
            MapSizeZ = Sapi.World.BlockAccessor.MapSizeZ,
            MapSizeY = Sapi.World.BlockAccessor.MapSizeY,

            SpawnPosition = [spawn.X, spawn.Z],

            ServerName = Sapi.Server.Config.ServerName,
            WorldName = Sapi.World.SavegameIdentifier
        };
    }


    /// <summary>
    /// Invalidate cache to force recalculation
    /// </summary>
    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cachedConfig = null;
        }

        LogDebug("Map config cache invalidated");
    }
}

#region Data Models

#endregion


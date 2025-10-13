using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Vintagestory.API.Server;
using System.Linq;
using VintageAtlas.Export;

namespace VintageAtlas.Web.API;

/// <summary>
/// Provides dynamic map configuration (extent, center, zoom levels, etc.)
/// Replaces hardcoded values in frontend mapConfig.ts
/// </summary>
public class MapConfigController(ICoreServerAPI sapi, ITileGenerator? tileGenerator = null)
{
    private readonly ITileGenerator? _tileGenerator = tileGenerator;
    private readonly JsonSerializerSettings _jsonSettings = new()
    {
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        },
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore
    };

    private MapConfigData? _cachedConfig;
    private long _lastConfigUpdate;
    private readonly object _cacheLock = new();

    /// <summary>
    /// Serve map configuration as JSON
    /// </summary>
    public async Task ServeMapConfig(HttpListenerContext context)
    {
        try
        {
            var mapConfig = GetMapConfig();
            var json = JsonConvert.SerializeObject(mapConfig, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.Headers.Add("Cache-Control", "public, max-age=300"); // Cache for 5 minutes

            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error serving map config: {ex.Message}");
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
        if (sapi.World?.BlockAccessor == null)
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
        if (sapi.World?.BlockAccessor == null)
        {
            throw new InvalidOperationException("World not yet initialized");
        }

        var now = sapi.World.ElapsedMilliseconds;

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
        var tileStats = CalculateTileStatistics();
        var spawn = sapi.World.DefaultSpawnPosition.AsBlockPos;

        var mapSizeX = sapi.World.BlockAccessor.MapSizeX / 2;
        var mapSizeZ = sapi.World.BlockAccessor.MapSizeZ / 2;

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
            sapi.Logger.Debug($"[MapConfig] Zoom {zoom} origin offset: ({originTileX},{originTileY})");
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

            MapSizeX = sapi.World.BlockAccessor.MapSizeX,
            MapSizeZ = sapi.World.BlockAccessor.MapSizeZ,
            MapSizeY = sapi.World.BlockAccessor.MapSizeY,

            SpawnPosition = [spawn.X, spawn.Z],

            TileStats = tileStats,

            ServerName = sapi.Server.Config.ServerName,
            WorldName = sapi.World.SavegameIdentifier
        };
    }

    private TileStatistics CalculateTileStatistics()
    {
        var stats = new TileStatistics
        {
            ZoomLevels = []
        };

        // Note: VintageAtlas stores tiles in MBTiles database, not as files
        // For now, return empty stats - could be enhanced to query database
        sapi.Logger.Debug("[VintageAtlas] Tile statistics calculation skipped (tiles stored in database)");

        return stats;
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

        sapi.Logger.Debug("[VintageAtlas] Map config cache invalidated");
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

#region Data Models

public class MapConfigData
{
    /// <summary>
    /// Extent in world BLOCK coordinates: [minX, minZ, maxX, maxZ]
    /// These are game world coordinates that define the map bounds.
    /// OpenLayers uses these to create the TileGrid extent.
    /// Frontend's getTileUrl() maps grid coords to storage tile numbers.
    /// </summary>
    public int[] WorldExtent { get; set; } = [];

    /// <summary>
    /// Origin (top-left) in world BLOCK coordinates: [x, z]
    /// This is where tile (0,0) would be located in the tile grid.
    /// OpenLayers uses this to create the TileGrid origin.
    /// </summary>
    public int[] WorldOrigin { get; set; } = [];

    /// <summary>
    /// Default center in world BLOCK coordinates: [x, z]
    /// Usually the spawn point or middle of explored area.
    /// OpenLayers centers the view here initially.
    /// </summary>
    public int[] DefaultCenter { get; set; } = [];

    public int DefaultZoom { get; set; }
    public int MinZoom { get; set; }
    public int MaxZoom { get; set; }
    public int BaseZoomLevel { get; set; }
    public int TileSize { get; set; }

    /// <summary>
    /// Tile resolutions for each zoom level (blocks per pixel)
    /// </summary>
    public double[] TileResolutions { get; set; } = [];

    /// <summary>
    /// View resolutions for smooth zooming (includes extra zoom levels)
    /// </summary>
    public double[] ViewResolutions { get; set; } = [];

    /// <summary>
    /// Absolute tile origin per zoom level (X,Y) in storage coordinates.
    /// Allows the frontend to offset tile grid indices to direct XYZ requests.
    /// </summary>
    public int[][] OriginTilesPerZoom { get; set; } = [];

    /// <summary>
    /// Spawn position in world block coordinates: [x, z]
    /// </summary>
    public int[] SpawnPosition { get; set; } = [];

    public int MapSizeX { get; set; }
    public int MapSizeZ { get; set; }
    public int MapSizeY { get; set; }
    public TileStatistics? TileStats { get; set; }
    public string? ServerName { get; set; }
    public string? WorldName { get; set; }
}

public class TileStatistics
{
    public int TotalTiles { get; set; }
    public long TotalSizeBytes { get; set; }
    public Dictionary<int, ZoomLevelStats> ZoomLevels { get; set; } = new();
}

public class ZoomLevelStats
{
    public int TileCount { get; set; }
    public long TotalSizeBytes { get; set; }
}

#endregion


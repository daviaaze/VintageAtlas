using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Vintagestory.API.Server;
using VintageAtlas.Core;

namespace VintageAtlas.Web.API;

/// <summary>
/// Provides dynamic map configuration (extent, center, zoom levels, etc.)
/// Replaces hardcoded values in frontend mapConfig.ts
/// </summary>
public class MapConfigController
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly JsonSerializerSettings _jsonSettings;
    
    private MapConfigData? _cachedConfig;
    private long _lastConfigUpdate;
    private readonly object _cacheLock = new();

    public MapConfigController(ICoreServerAPI sapi, ModConfig config)
    {
        _sapi = sapi;
        _config = config;
        
        _jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };
    }

    /// <summary>
    /// Serve map configuration as JSON
    /// </summary>
    public async Task ServeMapConfig(HttpListenerContext context)
    {
        try
        {
            var config = GetMapConfig();
            var json = JsonConvert.SerializeObject(config, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.Headers.Add("Cache-Control", "public, max-age=300"); // Cache for 5 minutes
            
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.Close();
            
            _sapi.Logger.Debug("[VintageAtlas] Map config served via API");
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Error serving map config: {ex.Message}");
            await ServeError(context, "Failed to get map configuration", 500);
        }
    }

    /// <summary>
    /// Get world extent information
    /// </summary>
    public async Task ServeWorldExtent(HttpListenerContext context)
    {
        try
        {
            var extent = CalculateWorldExtent();
            var json = JsonConvert.SerializeObject(extent, _jsonSettings);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            
            await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Error serving world extent: {ex.Message}");
            await ServeError(context, "Failed to calculate world extent", 500);
        }
    }

    private MapConfigData GetMapConfig()
    {
        var now = _sapi.World.ElapsedMilliseconds;
        
        lock (_cacheLock)
        {
            // Cache for 5 minutes
            if (_cachedConfig != null && (now - _lastConfigUpdate) < 300000)
            {
                return _cachedConfig;
            }
        }

        var config = GenerateMapConfig();
        
        lock (_cacheLock)
        {
            _cachedConfig = config;
            _lastConfigUpdate = now;
        }

        return config;
    }

    private MapConfigData GenerateMapConfig()
    {
        var extent = CalculateWorldExtent();
        var center = CalculateDefaultCenter();
        var tileStats = CalculateTileStatistics();
        
        // Calculate resolutions for OpenLayers
        var tileResolutions = GenerateResolutions(_config.BaseZoomLevel);
        var viewResolutions = GenerateResolutions(_config.BaseZoomLevel + 3); // Extra zoom for smooth viewing
        
        return new MapConfigData
        {
            // World bounds
            WorldExtent = new[] { extent.MinX, extent.MinZ, extent.MaxX, extent.MaxZ },
            WorldOrigin = new[] { extent.MinX, extent.MaxZ },
            
            // Default view
            DefaultCenter = center,
            DefaultZoom = CalculateDefaultZoom(),
            
            // Zoom configuration
            MinZoom = 0,
            MaxZoom = _config.BaseZoomLevel,
            BaseZoomLevel = _config.BaseZoomLevel,
            
            // Tile configuration
            TileSize = _config.TileSize,
            TileResolutions = tileResolutions,
            ViewResolutions = viewResolutions,
            
            // Map metadata
            SpawnPosition = GetSpawnPosition(),
            MapSizeX = _sapi.World.BlockAccessor.MapSizeX,
            MapSizeZ = _sapi.World.BlockAccessor.MapSizeZ,
            MapSizeY = _sapi.World.BlockAccessor.MapSizeY,
            
            // Tile availability
            TileStats = tileStats,
            
            // Server info
            ServerName = _sapi.Server.Config.ServerName,
            WorldName = _sapi.World.SavegameIdentifier
        };
    }

    private WorldExtentData CalculateWorldExtent()
    {
        // Get actual tile coverage by scanning the output directory
        var worldDir = Path.Combine(_config.OutputDirectoryWorld, _config.BaseZoomLevel.ToString());
        
        if (!Directory.Exists(worldDir))
        {
            // Fallback to full world size
            var mapSizeX = _sapi.World.BlockAccessor.MapSizeX;
            var mapSizeZ = _sapi.World.BlockAccessor.MapSizeZ;
            
            return new WorldExtentData
            {
                MinX = -mapSizeX / 2,
                MinZ = -mapSizeZ / 2,
                MaxX = mapSizeX / 2,
                MaxZ = mapSizeZ / 2
            };
        }

        // Scan tiles to find actual extent
        var tiles = Directory.GetFiles(worldDir, "*.png")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(f => f != null)
            .Select(f =>
            {
                var parts = f!.Split('_');
                if (parts.Length == 2 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var z))
                {
                    return (x, z);
                }
                return ((int?)null, (int?)null);
            })
            .Where(t => t.Item1.HasValue && t.Item2.HasValue)
            .Select(t => (x: t.Item1!.Value, z: t.Item2!.Value))
            .ToList();

        if (tiles.Count == 0)
        {
            // No tiles found, use default
            return new WorldExtentData
            {
                MinX = -512000,
                MinZ = -512000,
                MaxX = 512000,
                MaxZ = 512000
            };
        }

        var minTileX = tiles.Min(t => t.x);
        var maxTileX = tiles.Max(t => t.x);
        var minTileZ = tiles.Min(t => t.z);
        var maxTileZ = tiles.Max(t => t.z);
        
        // Convert tile coordinates to world coordinates
        var chunkSize = 32;
        var chunksPerTile = _config.TileSize / chunkSize;
        var worldUnitsPerTile = chunksPerTile * chunkSize;
        
        return new WorldExtentData
        {
            MinX = minTileX * worldUnitsPerTile,
            MinZ = minTileZ * worldUnitsPerTile,
            MaxX = (maxTileX + 1) * worldUnitsPerTile,
            MaxZ = (maxTileZ + 1) * worldUnitsPerTile
        };
    }

    private int[] CalculateDefaultCenter()
    {
        // Use spawn position or center of tile coverage
        var spawn = GetSpawnPosition();
        
        if (_config.AbsolutePositions)
        {
            return new[] { spawn[0], spawn[1] };
        }
        else
        {
            // In relative coordinates, spawn is at [0, 0]
            return new[] { 0, 0 };
        }
    }

    private int CalculateDefaultZoom()
    {
        // Default to mid-range zoom (good balance between overview and detail)
        return Math.Max(1, _config.BaseZoomLevel - 2);
    }

    private int[] GetSpawnPosition()
    {
        var spawnPos = _sapi.World.DefaultSpawnPosition?.AsBlockPos;
        var spawnX = spawnPos?.X ?? _sapi.World.BlockAccessor.MapSizeX / 2;
        var spawnZ = spawnPos?.Z ?? _sapi.World.BlockAccessor.MapSizeZ / 2;
        
        return new[] { spawnX, spawnZ };
    }

    private double[] GenerateResolutions(int levels)
    {
        var resolutions = new double[levels];
        for (int i = 0; i < levels; i++)
        {
            resolutions[i] = Math.Pow(2, levels - i - 1);
        }
        return resolutions;
    }

    private TileStatistics CalculateTileStatistics()
    {
        var stats = new TileStatistics
        {
            ZoomLevels = new System.Collections.Generic.Dictionary<int, ZoomLevelStats>()
        };

        for (int zoom = 1; zoom <= _config.BaseZoomLevel; zoom++)
        {
            var zoomDir = Path.Combine(_config.OutputDirectoryWorld, zoom.ToString());
            
            if (Directory.Exists(zoomDir))
            {
                var tileCount = Directory.GetFiles(zoomDir, "*.png").Length;
                var dirInfo = new DirectoryInfo(zoomDir);
                var totalSize = dirInfo.GetFiles("*.png").Sum(f => f.Length);
                
                stats.ZoomLevels[zoom] = new ZoomLevelStats
                {
                    TileCount = tileCount,
                    TotalSizeBytes = totalSize
                };
            }
        }

        stats.TotalTiles = stats.ZoomLevels.Values.Sum(z => z.TileCount);
        stats.TotalSizeBytes = stats.ZoomLevels.Values.Sum(z => z.TotalSizeBytes);

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
        
        _sapi.Logger.Debug("[VintageAtlas] Map config cache invalidated");
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

#region Data Models

public class MapConfigData
{
    public int[] WorldExtent { get; set; } = Array.Empty<int>();
    public int[] WorldOrigin { get; set; } = Array.Empty<int>();
    public int[] DefaultCenter { get; set; } = Array.Empty<int>();
    public int DefaultZoom { get; set; }
    public int MinZoom { get; set; }
    public int MaxZoom { get; set; }
    public int BaseZoomLevel { get; set; }
    public int TileSize { get; set; }
    public double[] TileResolutions { get; set; } = Array.Empty<double>();
    public double[] ViewResolutions { get; set; } = Array.Empty<double>();
    public int[] SpawnPosition { get; set; } = Array.Empty<int>();
    public int MapSizeX { get; set; }
    public int MapSizeZ { get; set; }
    public int MapSizeY { get; set; }
    public TileStatistics? TileStats { get; set; }
    public string? ServerName { get; set; }
    public string? WorldName { get; set; }
}

public class WorldExtentData
{
    public int MinX { get; set; }
    public int MinZ { get; set; }
    public int MaxX { get; set; }
    public int MaxZ { get; set; }
}

public class TileStatistics
{
    public int TotalTiles { get; set; }
    public long TotalSizeBytes { get; set; }
    public System.Collections.Generic.Dictionary<int, ZoomLevelStats> ZoomLevels { get; set; } = new();
}

public class ZoomLevelStats
{
    public int TileCount { get; set; }
    public long TotalSizeBytes { get; set; }
}

#endregion


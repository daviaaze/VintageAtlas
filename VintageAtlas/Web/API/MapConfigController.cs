using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Export;

namespace VintageAtlas.Web.API;

/// <summary>
/// Provides dynamic map configuration (extent, center, zoom levels, etc.)
/// Replaces hardcoded values in frontend mapConfig.ts
/// </summary>
public class MapConfigController(ICoreServerAPI sapi, ModConfig config, ITileGenerator tileGenerator)
{
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
    /// </summary>
    public MapConfigData? GetCurrentConfig()
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
        var spawn = GetSpawnPosition();
        
        // Calculate resolutions for OpenLayers
        var tileResolutions = GenerateResolutions(config.BaseZoomLevel);
        var viewResolutions = GenerateResolutions(config.BaseZoomLevel + 3); // Extra zoom for smooth viewing

        // ═══════════════════════════════════════════════════════════════
        // CLEAN ARCHITECTURE: Backend provides tile-space coordinates
        // ═══════════════════════════════════════════════════════════════
        // All coordinates are in TILE GRID SPACE, matching the storage.
        // No transformations needed - OpenLayers tile (X, Y) directly maps
        // to storage tile (X, Y) via /tiles/{z}/{x}_{y}.png
        //
        // Benefits:
        // 1. Simple: Frontend uses backend values as-is
        // 2. Correct: Coordinates match by definition
        // 3. Debuggable: Request tile (X, Y) → get file X_Y.png
        // ═══════════════════════════════════════════════════════════════
        
        // CLEAN ARCHITECTURE REALITY CHECK:
        // - OpenLayers TileGrid extent/origin are WORLD COORDINATES (blocks), not tile numbers!
        // - We query tiles at display zoom to know the range
        // - Convert tile numbers to world block coordinates for OpenLayers
        // - Frontend getTileUrl() will map grid coords back to storage tile numbers
        
        var displayZoom = CalculateDefaultZoom(); // e.g., 7
        
        // Get tile extent at display zoom to calculate world bounds
        var tileExtent = tileGenerator.GetTileExtentAsync(displayZoom).GetAwaiter().GetResult();
        
        if (tileExtent == null)
        {
            // No tiles yet - use fallback based on spawn
            sapi.Logger.Warning("[VintageAtlas] No tiles found, using spawn fallback extent");
            return GenerateFallbackMapConfig(spawn, tileStats, tileResolutions, viewResolutions);
        }
        
        // Convert tile numbers to world BLOCK coordinates
        // At display zoom, each tile = (tileSize * resolution) blocks
        var resolution = tileResolutions[displayZoom];
        var blocksPerTile = (int)(config.TileSize * resolution);
        
        // World extent in BLOCK coordinates (what OpenLayers needs)
        int[] worldExtent = [
            tileExtent.MinX * blocksPerTile,
            tileExtent.MinY * blocksPerTile,
            (tileExtent.MaxX + 1) * blocksPerTile,
            (tileExtent.MaxY + 1) * blocksPerTile
        ];
        
        // Origin at BOTTOM-LEFT corner (minX, minY)
        // This means grid (0,0) starts at the NORTH edge (minimum Y)
        // and grid Y increases going SOUTH (higher Y values)
        // This matches OpenLayers' natural behavior!
        int[] worldOrigin = [
            tileExtent.MinX * blocksPerTile,
            tileExtent.MinY * blocksPerTile
        ];
        
        // Center in BLOCKS
        int[] defaultCenter = [
            ((tileExtent.MinX + tileExtent.MaxX) * blocksPerTile) / 2,
            ((tileExtent.MinY + tileExtent.MaxY) * blocksPerTile) / 2
        ];
        
        return new MapConfigData
        {
            // World BLOCK coordinates (for OpenLayers TileGrid)
            WorldExtent = worldExtent,
            WorldOrigin = worldOrigin,
            DefaultCenter = defaultCenter,
            DefaultZoom = displayZoom,
            
            // Zoom configuration
            MinZoom = 0,
            MaxZoom = config.BaseZoomLevel,
            BaseZoomLevel = config.BaseZoomLevel,
            
            // Tile configuration
            TileSize = config.TileSize,
            TileResolutions = tileResolutions,
            ViewResolutions = viewResolutions,
            
            // Map metadata
            SpawnPosition = spawn,
            MapSizeX = sapi.World.BlockAccessor.MapSizeX,
            MapSizeZ = sapi.World.BlockAccessor.MapSizeZ,
            MapSizeY = sapi.World.BlockAccessor.MapSizeY,
            
            // Tile availability
            TileStats = tileStats,
            
            // Server info
            ServerName = sapi.Server.Config.ServerName,
            WorldName = sapi.World.SavegameIdentifier
        };
    }
    
    private MapConfigData GenerateFallbackMapConfig(int[] spawn, TileStatistics tileStats, 
        double[] tileResolutions, double[] viewResolutions)
    {
        // Fallback: Create a reasonable extent around spawn in BLOCK coordinates
        const int fallbackBlockRadius = 25600; // ~100 tiles * 256 blocks/tile
        
        int[] worldExtent = [
            spawn[0] - fallbackBlockRadius,
            spawn[1] - fallbackBlockRadius,
            spawn[0] + fallbackBlockRadius,
            spawn[1] + fallbackBlockRadius
        ];
        // Origin at bottom-left: minX, minZ
        int[] worldOrigin = [spawn[0] - fallbackBlockRadius, spawn[1] - fallbackBlockRadius];
        int[] defaultCenter = [spawn[0], spawn[1]];
        
        return new MapConfigData
        {
            WorldExtent = worldExtent,
            WorldOrigin = worldOrigin,
            DefaultCenter = defaultCenter,
            DefaultZoom = CalculateDefaultZoom(),
            MinZoom = 0,
            MaxZoom = config.BaseZoomLevel,
            BaseZoomLevel = config.BaseZoomLevel,
            TileSize = config.TileSize,
            TileResolutions = tileResolutions,
            ViewResolutions = viewResolutions,
            SpawnPosition = spawn,
            MapSizeX = sapi.World.BlockAccessor.MapSizeX,
            MapSizeZ = sapi.World.BlockAccessor.MapSizeZ,
            MapSizeY = sapi.World.BlockAccessor.MapSizeY,
            TileStats = tileStats,
            ServerName = sapi.Server.Config.ServerName,
            WorldName = sapi.World.SavegameIdentifier
        };
    }


    private int CalculateDefaultZoom()
    {
        // Default to mid-range zoom (good balance between overview and detail)
        return Math.Max(1, config.BaseZoomLevel - 2);
    }

    private int[] GetSpawnPosition()
    {
        var spawnPos = sapi.World.DefaultSpawnPosition?.AsBlockPos;
        var spawnX = spawnPos?.X ?? sapi.World.BlockAccessor.MapSizeX / 2;
        var spawnZ = spawnPos?.Z ?? sapi.World.BlockAccessor.MapSizeZ / 2;
        
        return [spawnX, spawnZ];
    }

    private static double[] GenerateResolutions(int maxZoom)
    {
        // Generate resolutions for zooms 0 through maxZoom (inclusive)
        // Need maxZoom + 1 elements for all zoom levels
        var resolutions = new double[maxZoom + 1];
        for (var i = 0; i <= maxZoom; i++)
        {
            // Resolution = blocks per pixel at this zoom level
            // Zoom 0 (far out): 2^maxZoom blocks/pixel (e.g., 512 at maxZoom=9)
            // Zoom maxZoom (zoomed in): 2^0 = 1 block/pixel
            resolutions[i] = Math.Pow(2, maxZoom - i);
        }
        return resolutions;
    }

    private TileStatistics CalculateTileStatistics()
    {
        var stats = new TileStatistics
        {
            ZoomLevels = new Dictionary<int, ZoomLevelStats>()
        };

        for (var zoom = 1; zoom <= config.BaseZoomLevel; zoom++)
        {
            var zoomDir = Path.Combine(config.OutputDirectoryWorld, zoom.ToString());

            if (!Directory.Exists(zoomDir)) 
                continue;
            
            var tileCount = Directory.GetFiles(zoomDir, "*.png").Length;
            var dirInfo = new DirectoryInfo(zoomDir);
            var totalSize = dirInfo.GetFiles("*.png").Sum(f => f.Length);
                
            stats.ZoomLevels[zoom] = new ZoomLevelStats
            {
                TileCount = tileCount,
                TotalSizeBytes = totalSize
            };
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


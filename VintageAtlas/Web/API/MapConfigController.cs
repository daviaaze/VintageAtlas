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
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Export;

namespace VintageAtlas.Web.API;

/// <summary>
/// Provides dynamic map configuration (extent, center, zoom levels, etc.)
/// Replaces hardcoded values in frontend mapConfig.ts
/// </summary>
public class MapConfigController
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly ITileGenerator _tileGenerator;
    private readonly JsonSerializerSettings _jsonSettings;
    
    private MapConfigData? _cachedConfig;
    private long _lastConfigUpdate;
    private readonly object _cacheLock = new();

    public MapConfigController(ICoreServerAPI sapi, ModConfig config, ITileGenerator tileGenerator)
    {
        _sapi = sapi;
        _config = config;
        _tileGenerator = tileGenerator;
        
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
        // Check if world is ready (can be null during early startup)
        if (_sapi.World?.BlockAccessor == null)
        {
            throw new InvalidOperationException("World not yet initialized");
        }
        
        var now = _sapi.World.ElapsedMilliseconds;
        
        lock (_cacheLock)
        {
            // Cache for 5 minutes
            if (_cachedConfig != null && now - _lastConfigUpdate < 300000)
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
        var spawn = GetSpawnPosition();
        
        // Calculate resolutions for OpenLayers
        var tileResolutions = GenerateResolutions(_config.BaseZoomLevel);
        var viewResolutions = GenerateResolutions(_config.BaseZoomLevel + 3); // Extra zoom for smooth viewing

        // Transform extent to relative coordinates if needed
        // COORDINATE SYSTEM DESIGN:
        // - Backend (ChunkDataExtractor): Always uses absolute world chunk coordinates
        // - Tiles on disk: Stored with absolute chunk coordinate filenames (e.g., 1234_5678.png)
        // - This controller: Transforms extent for frontend display based on coordinate mode
        // - Frontend (OpenLayers): Uses transformed extent to position tiles visually
        //
        // Why this design:
        // 1. Tiles are generated once and work in both coordinate modes
        // 2. Only the DISPLAY extent changes, not the tile data itself
        // 3. Avoids regenerating tiles when switching coordinate modes
        // ABSOLUTE COORDINATES WITH OFFSET MAPPING
        // ========================================
        // OpenLayers TileGrid always numbers tiles from (0,0) at origin
        // But our tiles are stored with absolute world coordinates
        // Solution: Calculate offset to map OL tile coords -> storage coords
        // ========================================
        
        // Z-axis flip: Vintage Story Z increases southward, we want north at top
        // So: minY = -maxZ (south at bottom), maxY = -minZ (north at top)
        int[] worldExtent = [extent.MinX, -extent.MaxZ, extent.MaxX, -extent.MinZ];
        int[] worldOrigin = [extent.MinX, -extent.MinZ];  // Top-left (northwest)
        
        _sapi.Logger.Debug($"[VintageAtlas] Absolute extent with Z-flip: " +
            $"extent=[{worldExtent[0]}, {worldExtent[1]}, {worldExtent[2]}, {worldExtent[3]}], " +
            $"origin=[{worldOrigin[0]}, {worldOrigin[1]}]");
        
        // Calculate which absolute tile the origin maps to
        // Origin is at [extent.MinX, -extent.MinZ] in flipped coords
        // Un-flip Z to get game coords: [extent.MinX, extent.MinZ]
        var originTileX = (int)Math.Floor((double)extent.MinX / _config.TileSize);
        var originTileZ = (int)Math.Floor((double)extent.MinZ / _config.TileSize);
        
        int[] tileOffset = [originTileX, originTileZ];
        
        _sapi.Logger.Debug($"[VintageAtlas] Tile offset: origin blocks=({extent.MinX},{extent.MinZ}), " +
            $"origin tiles=({originTileX},{originTileZ})");
        
        return new MapConfigData
        {
            // World bounds (transformed based on coordinate mode)
            WorldExtent = worldExtent,
            WorldOrigin = worldOrigin,
            
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
            TileOffset = tileOffset,  // Tile coordinate offset for spawn-relative mode
            
            // Map metadata
            SpawnPosition = spawn,
            MapSizeX = _sapi.World.BlockAccessor.MapSizeX,
            MapSizeZ = _sapi.World.BlockAccessor.MapSizeZ,
            MapSizeY = _sapi.World.BlockAccessor.MapSizeY,
            
            // Tile availability
            TileStats = tileStats,
            
            // Server info
            ServerName = _sapi.Server.Config.ServerName,
            WorldName = _sapi.World.SavegameIdentifier,
            
            // Coordinate system
            AbsolutePositions = _config.AbsolutePositions
        };
    }

    private WorldExtentData CalculateWorldExtent()
    {
        // STEP 1: Try MBTiles first (fast if tiles exist)
        _sapi.Logger.Debug($"[VintageAtlas] MapConfig: Querying tile extent for zoom {_config.BaseZoomLevel}...");
        var tileExtent = _tileGenerator.GetTileExtentAsync(_config.BaseZoomLevel).GetAwaiter().GetResult();
        
        if (tileExtent != null)
        {
            // Tiles exist - calculate extent from them
            _sapi.Logger.Debug($"[VintageAtlas] MapConfig: Found tile extent: ({tileExtent.MinX},{tileExtent.MinY})-({tileExtent.MaxX},{tileExtent.MaxY})");
            return CalculateExtentFromTiles(tileExtent);
        }

        // STEP 2: No tiles yet - query savegame database for actual chunk positions
        _sapi.Logger.Warning("[VintageAtlas] MapConfig: No tiles in MBTiles database, querying savegame for chunk extent...");
        
        return CalculateExtentFromSavegame();
    }

    private WorldExtentData CalculateExtentFromTiles(Storage.TileExtent tileExtent)
    {
        // Convert tile coordinates to world coordinates
        const int chunkSize = 32;
        var chunksPerTile = _config.TileSize / chunkSize;
        var worldUnitsPerTile = chunksPerTile * chunkSize;
        
        var minX = tileExtent.MinX * worldUnitsPerTile;
        var maxX = (tileExtent.MaxX + 1) * worldUnitsPerTile;
        var minZ = tileExtent.MinY * worldUnitsPerTile;
        var maxZ = (tileExtent.MaxY + 1) * worldUnitsPerTile;
        
        _sapi.Logger.Debug($"[VintageAtlas] Calculated extent from MBTiles database: " +
            $"Tiles: ({tileExtent.MinX},{tileExtent.MinY})-({tileExtent.MaxX},{tileExtent.MaxY}), " +
            $"World coords: ({minX},{minZ})-({maxX},{maxZ})");
        
        return new WorldExtentData
        {
            MinX = minX,
            MinZ = minZ,
            MaxX = maxX,
            MaxZ = maxZ
        };
    }

    private WorldExtentData CalculateExtentFromSavegame()
    {
        try
        {
            // Query savegame database directly for all chunk positions
            var chunkPositions = GetAllMapChunkPositionsFromSavegame();
            
            if (chunkPositions.Count == 0)
            {
                _sapi.Logger.Debug("[VintageAtlas] No chunks found in savegame, using spawn fallback extent");
                return GetSpawnFallbackExtent();
            }
            
            // Calculate extent from actual chunk positions
            const int chunkSize = 32;
            var minChunkX = chunkPositions.Min(c => c.x);
            var maxChunkX = chunkPositions.Max(c => c.x);
            var minChunkZ = chunkPositions.Min(c => c.z);
            var maxChunkZ = chunkPositions.Max(c => c.z);
            
            var minX = minChunkX * chunkSize;
            var maxX = (maxChunkX + 1) * chunkSize;
            var minZ = minChunkZ * chunkSize;
            var maxZ = (maxChunkZ + 1) * chunkSize;
            
            _sapi.Logger.Debug($"[VintageAtlas] Calculated extent from savegame database: " +
                $"Chunks: ({minChunkX},{minChunkZ})-({maxChunkX},{maxChunkZ}), " +
                $"World coords: ({minX},{minZ})-({maxX},{maxZ})");
            
            return new WorldExtentData
            {
                MinX = minX,
                MinZ = minZ,
                MaxX = maxX,
                MaxZ = maxZ
            };
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to query savegame database: {ex.Message}");
            _sapi.Logger.Debug($"[VintageAtlas] Stack trace: {ex.StackTrace}");
            return GetSpawnFallbackExtent();
        }
    }

    private List<(int x, int z)> GetAllMapChunkPositionsFromSavegame()
    {
        var positions = new List<(int x, int z)>();
        
        // Get savegame database path
        var savegamePath = _sapi.World.SavegameIdentifier;
        var dataPath = _sapi.GetOrCreateDataPath("Saves");
        var dbPath = Path.Combine(dataPath, savegamePath, "default.vcdbs");
        
        if (!File.Exists(dbPath))
        {
            _sapi.Logger.Warning($"[VintageAtlas] Savegame database not found at: {dbPath}");
            return positions;
        }
        
        _sapi.Logger.Debug($"[VintageAtlas] Querying savegame database: {dbPath}");
        
        var connectionString = $"Data Source={dbPath};Mode=ReadOnly";
        
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT position FROM mapchunk";
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var position = (long)reader["position"];
            
            // Decode position using Vintage Story's chunk index format
            // From ChunkPos.FromChunkIndex_saveGamev2
            var mapSizeX = _sapi.World.BlockAccessor.MapSizeX / 32;
            var mapSizeZ = _sapi.World.BlockAccessor.MapSizeZ / 32;
            
            var chunkX = (int)(position % mapSizeX);
            var chunkZ = (int)((position / mapSizeX) % mapSizeZ);
            
            positions.Add((chunkX, chunkZ));
        }
        
        _sapi.Logger.Debug($"[VintageAtlas] Found {positions.Count} chunks in savegame database");
        
        return positions;
    }

    private WorldExtentData GetSpawnFallbackExtent()
    {
        var spawn = GetSpawnPosition();
        const int fallbackRadius = 10000; // 10km radius around spawn
        
        _sapi.Logger.Debug($"[VintageAtlas] Using spawn fallback extent: ±{fallbackRadius} blocks around spawn ({spawn[0]}, {spawn[1]})");
        
        return new WorldExtentData
        {
            MinX = spawn[0] - fallbackRadius,
            MinZ = spawn[1] - fallbackRadius,
            MaxX = spawn[0] + fallbackRadius,
            MaxZ = spawn[1] + fallbackRadius
        };
    }

    private int[] CalculateDefaultCenter()
    {
        // Use spawn position or center of tile coverage
        var spawn = GetSpawnPosition();
        
        if (_config.AbsolutePositions)
        {
            return [spawn[0], spawn[1]];
        }
        else
        {
            // In relative coordinates, spawn is at [0, 0]
            return [0, 0];
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
        
        return [spawnX, spawnZ];
    }

    private double[] GenerateResolutions(int maxZoom)
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
            ZoomLevels = new System.Collections.Generic.Dictionary<int, ZoomLevelStats>()
        };

        for (var zoom = 1; zoom <= _config.BaseZoomLevel; zoom++)
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
    public int[] WorldExtent { get; set; } = [];
    public int[] WorldOrigin { get; set; } = [];
    public int[] DefaultCenter { get; set; } = [];
    public int DefaultZoom { get; set; }
    public int MinZoom { get; set; }
    public int MaxZoom { get; set; }
    public int BaseZoomLevel { get; set; }
    public int TileSize { get; set; }
    public double[] TileResolutions { get; set; } = [];
    public double[] ViewResolutions { get; set; } = [];
    public int[] SpawnPosition { get; set; } = [];
    public int MapSizeX { get; set; }
    public int MapSizeZ { get; set; }
    public int MapSizeY { get; set; }
    public TileStatistics? TileStats { get; set; }
    public string? ServerName { get; set; }
    public string? WorldName { get; set; }
    public bool AbsolutePositions { get; set; }
    
    /// <summary>
    /// Tile coordinate offset for spawn-relative mode.
    /// Frontend adds this to display tile coords to get absolute tile coords.
    /// [tileOffsetX, tileOffsetZ] in tile coordinate space.
    /// </summary>
    public int[] TileOffset { get; set; } = [];
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


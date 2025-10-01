using System;
using Vintagestory.API.MathTools;
using VintageAtlas.Web.API;

namespace VintageAtlas.Core;

/// <summary>
/// Centralized coordinate transformation service for VintageAtlas.
/// Handles all conversions between different coordinate systems:
/// - Game world coordinates (Vintage Story native)
/// - Map display coordinates (GeoJSON with Z-flip)
/// - OpenLayers grid coordinates (tile requests)
/// - Storage tile coordinates (MBTiles database)
/// </summary>
public class CoordinateTransformService
{
    private readonly MapConfigController _mapConfigController;
    private readonly ModConfig _config;

    public CoordinateTransformService(MapConfigController mapConfigController, ModConfig config)
    {
        _mapConfigController = mapConfigController;
        _config = config;
    }

    #region Game ↔ Display Coordinates

    /// <summary>
    /// Convert game world coordinates to map display coordinates (GeoJSON).
    /// Applies Z-axis flip for north-up display.
    /// 
    /// Game: Z+ = south, Z- = north
    /// Display: Y+ = north (top), Y- = south (bottom)
    /// Transformation: displayY = -gameZ
    /// </summary>
    public (int x, int y) GameToDisplay(BlockPos gamePos)
    {
        return (gamePos.X, -gamePos.Z);
    }

    /// <summary>
    /// Convert game world coordinates to map display coordinates (GeoJSON).
    /// Overload for separate X/Z parameters.
    /// </summary>
    public (int x, int y) GameToDisplay(int gameX, int gameZ)
    {
        return (gameX, -gameZ);
    }

    /// <summary>
    /// Convert map display coordinates back to game world coordinates.
    /// Reverses the Z-axis flip.
    /// </summary>
    public (int x, int z) DisplayToGame(int displayX, int displayY)
    {
        return (displayX, -displayY);
    }

    #endregion

    #region OpenLayers Grid ↔ Storage Coordinates

    /// <summary>
    /// Transform OpenLayers grid coordinates to storage tile coordinates.
    /// 
    /// OpenLayers Grid:
    /// - (0,0) at world origin (bottom-left of tile extent)
    /// - Positive X = east, Positive Y = south
    /// - 0-based indexing from origin
    /// 
    /// Storage Tiles:
    /// - Absolute tile numbers based on world block position
    /// - Independent of zoom level and display
    /// </summary>
    /// <param name="zoom">Zoom level (0 = far out, baseZoom = zoomed in)</param>
    /// <param name="gridX">OpenLayers grid X coordinate</param>
    /// <param name="gridY">OpenLayers grid Y coordinate</param>
    /// <returns>Storage tile coordinates (tileX, tileZ)</returns>
    public (int storageTileX, int storageTileZ) GridToStorage(int zoom, int gridX, int gridY)
    {
        var mapConfig = _mapConfigController.GetCurrentConfig();
        
        if (mapConfig == null)
        {
            // Fallback: assume grid coordinates ARE storage coordinates (legacy behavior)
            // This shouldn't happen in production, but prevents crashes during initialization
            return (gridX, gridY);
        }
        
        // Calculate blocks per tile at this zoom level
        // Lower zoom = more blocks per tile (zoomed out)
        // Higher zoom = fewer blocks per tile (zoomed in)
        var resolution = mapConfig.TileResolutions[zoom];
        var blocksPerTile = _config.TileSize * resolution;
        
        // Calculate the origin tile numbers (where OpenLayers grid 0,0 maps to)
        // WorldOrigin is in world block coordinates, we convert to tile numbers
        var originTileX = (int)Math.Floor(mapConfig.WorldOrigin[0] / blocksPerTile);
        var originTileY = (int)Math.Floor(mapConfig.WorldOrigin[1] / blocksPerTile);
        
        // Transform grid coordinates to storage coordinates
        // OpenLayers uses bottom-left origin by default:
        // - Grid (0,0) = origin tile
        // - Positive X = tiles east of origin
        // - Positive Y = tiles south of origin (increasing Z in game coords)
        var storageTileX = originTileX + gridX;
        var storageTileZ = originTileY + gridY;
        
        return (storageTileX, storageTileZ);
    }

    /// <summary>
    /// Transform storage tile coordinates back to OpenLayers grid coordinates.
    /// Reverse of GridToStorage operation.
    /// </summary>
    public (int gridX, int gridY) StorageToGrid(int zoom, int storageTileX, int storageTileZ)
    {
        var mapConfig = _mapConfigController.GetCurrentConfig();
        
        if (mapConfig == null)
        {
            return (storageTileX, storageTileZ);
        }
        
        var resolution = mapConfig.TileResolutions[zoom];
        var blocksPerTile = _config.TileSize * resolution;
        
        var originTileX = (int)Math.Floor(mapConfig.WorldOrigin[0] / blocksPerTile);
        var originTileY = (int)Math.Floor(mapConfig.WorldOrigin[1] / blocksPerTile);
        
        var gridX = storageTileX - originTileX;
        var gridY = storageTileZ - originTileY;
        
        return (gridX, gridY);
    }

    #endregion

    #region World Block ↔ Tile Coordinates

    /// <summary>
    /// Convert world block coordinates to tile coordinates at a specific zoom level.
    /// </summary>
    public (int tileX, int tileZ) BlockToTile(int blockX, int blockZ, int zoom)
    {
        var mapConfig = _mapConfigController.GetCurrentConfig();
        
        if (mapConfig == null)
        {
            return (0, 0);
        }
        
        var resolution = mapConfig.TileResolutions[zoom];
        var blocksPerTile = _config.TileSize * resolution;
        
        var tileX = (int)Math.Floor(blockX / blocksPerTile);
        var tileZ = (int)Math.Floor(blockZ / blocksPerTile);
        
        return (tileX, tileZ);
    }

    /// <summary>
    /// Convert tile coordinates back to world block coordinates (returns top-left corner).
    /// </summary>
    public (int blockX, int blockZ) TileToBlock(int tileX, int tileZ, int zoom)
    {
        var mapConfig = _mapConfigController.GetCurrentConfig();
        
        if (mapConfig == null)
        {
            return (0, 0);
        }
        
        var resolution = mapConfig.TileResolutions[zoom];
        var blocksPerTile = _config.TileSize * resolution;
        
        var blockX = (int)(tileX * blocksPerTile);
        var blockZ = (int)(tileZ * blocksPerTile);
        
        return (blockX, blockZ);
    }

    #endregion

    #region Validation Helpers

    /// <summary>
    /// Check if a tile coordinate is within the valid map extent.
    /// </summary>
    public bool IsTileInBounds(int tileX, int tileZ, int zoom)
    {
        var mapConfig = _mapConfigController.GetCurrentConfig();
        
        if (mapConfig == null)
        {
            return true; // Can't validate without config
        }
        
        // Convert tile to world blocks
        var (blockX, blockZ) = TileToBlock(tileX, tileZ, zoom);
        
        // Check against world extent [minX, minZ, maxX, maxZ]
        return blockX >= mapConfig.WorldExtent[0] &&
               blockZ >= mapConfig.WorldExtent[1] &&
               blockX <= mapConfig.WorldExtent[2] &&
               blockZ <= mapConfig.WorldExtent[3];
    }

    /// <summary>
    /// Check if a zoom level is valid.
    /// </summary>
    public bool IsZoomLevelValid(int zoom)
    {
        return zoom >= 0 && zoom <= _config.BaseZoomLevel;
    }

    #endregion

    #region Composite Transformations

    /// <summary>
    /// Transform OpenLayers grid coordinates directly to game world block coordinates.
    /// Useful for debugging and validation.
    /// </summary>
    public (int blockX, int blockZ) GridToWorldBlocks(int zoom, int gridX, int gridY)
    {
        // Grid → Storage → World blocks
        var (storageTileX, storageTileZ) = GridToStorage(zoom, gridX, gridY);
        return TileToBlock(storageTileX, storageTileZ, zoom);
    }

    /// <summary>
    /// Transform game world block coordinates to OpenLayers grid coordinates.
    /// </summary>
    public (int gridX, int gridY) WorldBlocksToGrid(int blockX, int blockZ, int zoom)
    {
        // World blocks → Tile → Grid
        var (tileX, tileZ) = BlockToTile(blockX, blockZ, zoom);
        return StorageToGrid(zoom, tileX, tileZ);
    }

    #endregion

    #region Debug Utilities

    /// <summary>
    /// Get a detailed description of coordinate transformations for debugging.
    /// </summary>
    public string DescribeTransformation(int zoom, int gridX, int gridY)
    {
        var (storageTileX, storageTileZ) = GridToStorage(zoom, gridX, gridY);
        var (blockX, blockZ) = GridToWorldBlocks(zoom, gridX, gridY);
        
        return $"Zoom {zoom}: Grid({gridX},{gridY}) → Storage({storageTileX},{storageTileZ}) → Blocks({blockX},{blockZ})";
    }

    #endregion
}

using System.Threading.Tasks;

namespace VintageAtlas.Export;

/// <summary>
/// Abstraction for chunk data sources used by UnifiedTileGenerator.
/// Allows the same rendering logic to work with different data sources:
/// - SavegameDataSource: reads from savegame DB (for full exports)
/// - LoadedChunksDataSource: reads from loaded game chunks (for on-demand)
/// </summary>
public interface IChunkDataSource
{
    /// <summary>
    /// Get chunk data for a specific tile.
    /// Returns null if tile area is not available/explored.
    /// </summary>
    Task<TileChunkData?> GetTileChunksAsync(int zoom, int tileX, int tileZ);
    
    /// <summary>
    /// Get a human-readable name for this data source (for logging)
    /// </summary>
    string SourceName { get; }
    
    /// <summary>
    /// Whether this source requires main thread access (game state)
    /// </summary>
    bool RequiresMainThread { get; }
}


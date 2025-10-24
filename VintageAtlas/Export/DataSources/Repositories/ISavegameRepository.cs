using System.Collections.Generic;
using Vintagestory.API.MathTools;
using Vintagestory.Common.Database;
using Vintagestory.Server;

namespace VintageAtlas.Export.DataSources.Repositories;

/// <summary>
/// Repository interface for accessing savegame database.
/// All methods are thread-safe and handle locking internally.
/// </summary>
public interface ISavegameRepository
{
    /// <summary>
    /// Get all map chunk positions that exist in the database.
    /// </summary>
    IEnumerable<ChunkPos> GetAllMapChunkPositions();

    /// <summary>
    /// Get all map region positions that exist in the database.
    /// </summary>
    IEnumerable<Vec2i> GetAllMapRegionPositions();

    /// <summary>
    /// Get a specific map chunk by position.
    /// </summary>
    ServerMapChunk? GetMapChunk(ChunkPos position);

    /// <summary>
    /// Get a specific map chunk by position index.
    /// </summary>
    ServerMapChunk? GetMapChunk(ulong positionIndex);

    /// <summary>
    /// Get a specific server chunk by position.
    /// </summary>
    ServerChunk? GetChunk(ChunkPos position);

    /// <summary>
    /// Get a specific server chunk by position index.
    /// </summary>
    ServerChunk? GetChunk(ulong positionIndex);

    /// <summary>
    /// Get a specific server chunk by 3D position.
    /// </summary>
    ServerChunk? GetChunk(Vec3i position);

    /// <summary>
    /// Get a specific map region by position index.
    /// </summary>
    ServerMapRegion? GetMapRegion(ulong positionIndex);

    /// <summary>
    /// Get game data (world settings, etc.).
    /// </summary>
    SaveGame? GetGameData();

    /// <summary>
    /// Save multiple map chunks to the database (batch operation).
    /// </summary>
    void SaveMapChunks(IDictionary<long, ServerMapChunk> chunks);
}


using System;
using System.Threading.Tasks;

namespace VintageAtlas.Storage;

/// <summary>
/// Interface for tile storage operations.
/// Abstracts the underlying storage mechanism (MBTiles, file system, etc.)
/// </summary>
public interface ITileStorage : IDisposable
{
    /// <summary>
    /// Store a tile at the specified zoom level and coordinates
    /// </summary>
    Task PutTileAsync(int zoom, int x, int y, byte[] tileData);

    /// <summary>
    /// Retrieve a tile at the specified zoom level and coordinates
    /// </summary>
    /// <returns>Tile data or null if not found</returns>
    Task<byte[]?> GetTileAsync(int zoom, int x, int y);

    /// <summary>
    /// Delete a tile at the specified zoom level and coordinates
    /// </summary>
    Task DeleteTileAsync(int zoom, int x, int y);

    /// <summary>
    /// Get rain map tile data
    /// </summary>
    Task<byte[]?> GetRainTileAsync(int x, int y);

    /// <summary>
    /// Store rain map tile data
    /// </summary>
    void PutRainTile(int x, int y, byte[] tileData);

    /// <summary>
    /// Get temperature map tile data
    /// </summary>
    Task<byte[]?> GetTempTileAsync(int x, int y);

    /// <summary>
    /// Store temperature map tile data
    /// </summary>
    void PutTempTile(int x, int y, byte[] tileData);

    /// <summary>
    /// Get the extent (bounding box) of tiles at a given zoom level
    /// </summary>
    Task<TileExtent?> GetTileExtentAsync(int zoom);

    /// <summary>
    /// Perform database vacuum operation to reclaim space
    /// </summary>
    Task VacuumAsync();

    /// <summary>
    /// Get metadata value by key
    /// </summary>
    Task<string?> GetMetadataAsync(string name);

    /// <summary>
    /// Set metadata value by key
    /// </summary>
    Task SetMetadataAsync(string name, string value);

    /// <summary>
    /// Checkpoint WAL (Write-Ahead Log) to main database file
    /// </summary>
    void CheckpointWal();
}


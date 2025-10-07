using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Common.Database;
using Vintagestory.Server;

namespace VintageAtlas.Export;

/// <summary>
/// Extracts chunk version information from the savegame database.
/// Attempts multiple strategies to determine when chunks were generated:
/// 1. Check for built-in Vintage Story version tracking (if available)
/// 2. Use SaveGame global version as proxy
/// 3. Store custom tracking via moddata (future enhancement)
/// </summary>
public class ChunkVersionExtractor
{
    private readonly SavegameDataLoader _loader;
    private readonly ILogger _logger;
    private string? _worldVersion;
    
    public ChunkVersionExtractor(SavegameDataLoader loader, ILogger logger)
    {
        _loader = loader;
        _logger = logger;
    }
    
    /// <summary>
    /// Extract all chunk positions with their associated game versions.
    /// Returns a dictionary of ChunkPos → version string.
    /// </summary>
    public Dictionary<ChunkPos, string> ExtractChunkVersions()
    {
        _logger.Notification("[VintageAtlas] Extracting chunk version data...");
        
        var sqliteConn = _loader.SqliteThreadConn;
        var chunkVersions = new Dictionary<ChunkPos, string>();
        
        try
        {
            lock (sqliteConn.Con)
            {
                // Get the current game version as fallback for chunks without version data
                _worldVersion = GameVersion.ShortGameVersion;
                
                _logger.Notification($"[VintageAtlas] Default version (fallback): {_worldVersion}");
                
                // Get all map chunk positions
                var allPositions = _loader.GetAllMapChunkPositions(sqliteConn).ToList();
                _logger.Notification($"[VintageAtlas] Found {allPositions.Count} map chunks to process");
                
                var processed = 0;
                var withVersion = 0;
                
                foreach (var pos in allPositions)
                {
                    // Try to determine version for this chunk
                    var version = GetChunkVersion(sqliteConn, pos);
                    
                    if (version != null)
                    {
                        chunkVersions[pos] = version;
                        withVersion++;
                    }
                    
                    processed++;
                    
                    if (processed % 1000 == 0)
                    {
                        _logger.Notification($"[VintageAtlas] Processed {processed}/{allPositions.Count} chunks...");
                    }
                }
                
                _logger.Notification($"[VintageAtlas] Chunk version extraction complete:");
                _logger.Notification($"[VintageAtlas]   Total chunks: {processed}");
                _logger.Notification($"[VintageAtlas]   Chunks with version: {withVersion}");
                _logger.Notification($"[VintageAtlas]   Unique versions: {chunkVersions.Values.Distinct().Count()}");
                
                // Log version distribution
                var versionCounts = chunkVersions.Values
                    .GroupBy(v => v)
                    .OrderByDescending(g => g.Count())
                    .ToDictionary(g => g.Key, g => g.Count());
                
                foreach (var (version, count) in versionCounts.Take(10))
                {
                    _logger.Notification($"[VintageAtlas]     {version}: {count} chunks ({count * 100.0 / processed:F1}%)");
                }
            }
        }
        finally
        {
            sqliteConn.Free();
        }
        
        return chunkVersions;
    }
    
    /// <summary>
    /// Get version for a specific chunk using GameVersionCreated property.
    /// Falls back to world version if chunk data is unavailable.
    /// </summary>
    private string? GetChunkVersion(SqliteThreadCon sqliteConn, ChunkPos pos)
    {
        try
        {
            // Load the actual ServerChunk to get GameVersionCreated
            var chunkIndex = pos.ToChunkIndex();
            var serverChunk = _loader.GetServerChunk(sqliteConn, chunkIndex);
            
            if (serverChunk != null)
            {
                try
                {
                    serverChunk.Unpack_ReadOnly();
                    
                    // Use the built-in GameVersionCreated property!
                    // This is exactly what we need - tracks which version created this chunk
                    var version = serverChunk.GameVersionCreated;
                    
                    if (!string.IsNullOrEmpty(version))
                    {
                        return version;
                    }
                }
                catch (Exception ex)
                {
                    _logger.VerboseDebug($"[VintageAtlas] Could not read chunk version for {pos}: {ex.Message}");
                }
            }
            
            // Fallback to world version for chunks without version data
            return _worldVersion;
        }
        catch (Exception ex)
        {
            _logger.Debug($"[VintageAtlas] Error getting chunk version for {pos}: {ex.Message}");
            return _worldVersion;
        }
    }
    
    /// <summary>
    /// Start tracking chunk versions for future chunks.
    /// This should be called when a chunk is first generated.
    /// </summary>
    public void TrackNewChunk(ChunkPos pos, string gameVersion)
    {
        try
        {
            var sqliteConn = _loader.SqliteThreadConn;
            
            try
            {
                lock (sqliteConn.Con)
                {
                    var chunkIndex = pos.ToChunkIndex();
                    var serverChunk = _loader.GetServerChunk(sqliteConn, chunkIndex);
                    
                    if (serverChunk != null)
                    {
                        serverChunk.Unpack_ReadOnly();
                        
                        // Store version in moddata
                        var versionBytes = System.Text.Encoding.UTF8.GetBytes(gameVersion);
                        serverChunk.SetServerModdata("vintageatlas:chunkversion", versionBytes);
                        
                        _logger.Debug($"[VintageAtlas] Tracked chunk {pos} as version {gameVersion}");
                    }
                }
            }
            finally
            {
                sqliteConn.Free();
            }
        }
        catch (Exception ex)
        {
            _logger.Warning($"[VintageAtlas] Failed to track chunk version: {ex.Message}");
        }
    }
}

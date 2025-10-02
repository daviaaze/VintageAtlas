using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VintageAtlas.Tracking;

/// <summary>
/// Tracks chunk modifications to enable incremental map updates
/// </summary>
public class ChunkChangeTracker : IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly ConcurrentDictionary<Vec2i, long> _modifiedChunks;
    private readonly ConcurrentDictionary<Vec2i, HashSet<ChunkChangeType>> _chunkChangeTypes;
    private readonly object _lock = new();
    
    // Track GeoJSON-related changes separately
    private readonly ConcurrentDictionary<string, long> _structureChanges;
    private bool _geoJsonInvalidated;

    public ChunkChangeTracker(ICoreServerAPI sapi)
    {
        _sapi = sapi;
        _modifiedChunks = new ConcurrentDictionary<Vec2i, long>();
        _chunkChangeTypes = new ConcurrentDictionary<Vec2i, HashSet<ChunkChangeType>>();
        _structureChanges = new ConcurrentDictionary<string, long>();
        
        RegisterEventHandlers();
        
        _sapi.Logger.Notification("[VintageAtlas] Chunk change tracker initialized");
    }

    private void RegisterEventHandlers()
    {
        // Track block changes that affect the map
        _sapi.Event.DidPlaceBlock += OnBlockPlaced;
        // Note: DidBreakBlock delegate signature varies across VS versions, disabled for now
        // _sapi.Event.DidBreakBlock += OnBlockBroken;
        
        // Track sign changes
        _sapi.Event.DidPlaceBlock += OnPotentialSignPlaced;
        
        // Track chunk generation
        _sapi.Event.ChunkColumnLoaded += OnChunkColumnLoaded;
        
        _sapi.Logger.Debug("[VintageAtlas] Registered chunk change event handlers");
    }

    private void OnBlockPlaced(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel, ItemStack withItemStack)
    {
        TrackBlockChange(blockSel.Position, ChunkChangeType.BlockModified);
        
        // Check if it's a structure block (sign, signpost, etc.)
        var block = _sapi.World.BlockAccessor.GetBlock(blockSel.Position);
        if (IsStructureBlock(block))
        {
            InvalidateGeoJson("sign");
        }
    }

    private void OnBlockBroken(IServerPlayer byPlayer, BlockSelection blockSel)
    {
        TrackBlockChange(blockSel.Position, ChunkChangeType.BlockModified);
        
        // Check if a structure block was removed
        var block = _sapi.World.BlockAccessor.GetBlock(blockSel.Position);
        if (IsStructureBlock(block))
        {
            InvalidateGeoJson("sign");
        }
    }

    private void OnPotentialSignPlaced(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel, ItemStack withItemStack)
    {
        // Additional sign tracking - will be called after placement
        var blockEntity = _sapi.World.BlockAccessor.GetBlockEntity(blockSel.Position);
        if (blockEntity?.GetType().Name.Contains("Sign") == true)
        {
            InvalidateGeoJson("sign");
        }
    }

    private void OnChunkColumnLoaded(Vec2i chunkCoord, IWorldChunk[] chunks)
    {
        // Track newly generated chunks
        if (chunks != null && chunks.Length > 0)
        {
            // Check if this is a newly generated chunk (not just loaded from disk)
            var bottomChunk = chunks.FirstOrDefault(c => c != null);
            if (bottomChunk != null)
            {
                TrackChunkChange(chunkCoord, ChunkChangeType.NewlyGenerated);
            }
        }
    }

    private void TrackBlockChange(BlockPos pos, ChunkChangeType changeType)
    {
        var chunkCoord = new Vec2i(pos.X / 32, pos.Z / 32);
        TrackChunkChange(chunkCoord, changeType);
    }

    private void TrackChunkChange(Vec2i chunkCoord, ChunkChangeType changeType)
    {
        var now = _sapi.World.ElapsedMilliseconds;
        
        _modifiedChunks.AddOrUpdate(chunkCoord, now, (key, oldValue) => now);
        
        // Track the type of change
        _chunkChangeTypes.AddOrUpdate(
            chunkCoord,
            new HashSet<ChunkChangeType> { changeType },
            (key, existing) =>
            {
                lock (_lock)
                {
                    existing.Add(changeType);
                    return existing;
                }
            });
    }

    private bool IsStructureBlock(Block block)
    {
        if (block?.Code == null) return false;
        
        var path = block.Code.Path;
        return path.Contains("sign") || 
               path.Contains("signpost") ||
               path.Contains("translocator") ||
               path.Contains("teleporter");
    }

    private void InvalidateGeoJson(string type)
    {
        _structureChanges.AddOrUpdate(type, _sapi.World.ElapsedMilliseconds, (k, v) => _sapi.World.ElapsedMilliseconds);
        _geoJsonInvalidated = true;
    }

    /// <summary>
    /// Get all chunks modified since a given timestamp
    /// </summary>
    public List<Vec2i> GetModifiedChunksSince(long timestamp)
    {
        return _modifiedChunks
            .Where(kvp => kvp.Value > timestamp)
            .Select(kvp => kvp.Key)
            .ToList();
    }

    /// <summary>
    /// Get all modified chunks and their timestamps
    /// </summary>
    public Dictionary<Vec2i, long> GetAllModifiedChunks()
    {
        return new Dictionary<Vec2i, long>(_modifiedChunks);
    }

    /// <summary>
    /// Check if GeoJSON data needs regeneration
    /// </summary>
    public bool IsGeoJsonInvalidated()
    {
        return _geoJsonInvalidated;
    }

    /// <summary>
    /// Get structure changes since timestamp
    /// </summary>
    public Dictionary<string, long> GetStructureChangesSince(long timestamp)
    {
        return _structureChanges
            .Where(kvp => kvp.Value > timestamp)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Clear tracked changes for a chunk after it's been processed
    /// </summary>
    public void ClearChunkChanges(Vec2i chunkCoord)
    {
        _modifiedChunks.TryRemove(chunkCoord, out _);
        _chunkChangeTypes.TryRemove(chunkCoord, out _);
    }

    /// <summary>
    /// Clear all tracked changes
    /// </summary>
    public void ClearAllChanges()
    {
        _modifiedChunks.Clear();
        _chunkChangeTypes.Clear();
        _structureChanges.Clear();
        _geoJsonInvalidated = false;
    }

    /// <summary>
    /// Mark GeoJSON as regenerated
    /// </summary>
    public void MarkGeoJsonRegenerated()
    {
        _geoJsonInvalidated = false;
        _structureChanges.Clear();
    }

    /// <summary>
    /// Get the number of modified chunks
    /// </summary>
    public int ModifiedChunkCount => _modifiedChunks.Count;

    public void Dispose()
    {
        // Unregister event handlers
        _sapi.Event.DidPlaceBlock -= OnBlockPlaced;
        // _sapi.Event.DidBreakBlock -= OnBlockBroken;
        _sapi.Event.ChunkColumnLoaded -= OnChunkColumnLoaded;
        
        _modifiedChunks.Clear();
        _chunkChangeTypes.Clear();
        _structureChanges.Clear();
        
        _sapi.Logger.Notification("[VintageAtlas] Chunk change tracker disposed");
    }
}

/// <summary>
/// Types of chunk changes to track
/// </summary>
public enum ChunkChangeType
{
    BlockModified,
    NewlyGenerated,
    StructurePlaced,
    StructureRemoved
}


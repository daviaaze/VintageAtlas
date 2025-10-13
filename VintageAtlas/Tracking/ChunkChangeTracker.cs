using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VintageAtlas.Tracking;

/// <summary>
/// Track chunk modifications to enable incremental map updates
/// </summary>
public abstract class ChunkChangeTracker : IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly ConcurrentDictionary<Vec2i, long> _modifiedChunks;
    private readonly ConcurrentDictionary<Vec2i, HashSet<ChunkChangeType>> _chunkChangeTypes;
    private readonly object _lock = new();

    // Track GeoJSON-related changes separately
    private readonly ConcurrentDictionary<string, long> _structureChanges;
    private bool _geoJsonInvalidated;

    protected ChunkChangeTracker(ICoreServerAPI sapi)
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
        // Use BreakBlock and DidPlaceBlock for tracking block changes
        _sapi.Event.BreakBlock += OnBlockBreaking;
        _sapi.Event.DidPlaceBlock += OnBlockPlaced;

        // Track chunk generation
        _sapi.Event.ChunkColumnLoaded += OnChunkColumnLoaded;

        _sapi.Logger.Debug("[VintageAtlas] Registered chunk change event handlers");
    }

    // Note: Parameters required by Vintage Story API event signatures
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

    // Note: Parameters required by Vintage Story API event signature
    private void OnBlockBreaking(IServerPlayer byPlayer, BlockSelection blockSel, ref float dropQuantityMultiplier, ref EnumHandling handled)
    {
        // Called when a block is about to be broken (more stable than DidBreakBlock)
        TrackBlockChange(blockSel.Position, ChunkChangeType.BlockModified);

        // Check if a structure block was removed
        var block = _sapi.World.BlockAccessor.GetBlock(blockSel.Position);
        if (IsStructureBlock(block))
        {
            InvalidateGeoJson("sign");
        }
    }

    private void OnChunkColumnLoaded(Vec2i chunkCoord, IWorldChunk[] chunks)
    {
        // Track newly generated chunks
        if (chunks.Length <= 0) return;
        // Check if this is a newly generated chunk (not just loaded from disk)
        var bottomChunk = chunks.FirstOrDefault();
        if (bottomChunk != null)
        {
            TrackChunkChange(chunkCoord, ChunkChangeType.NewlyGenerated);
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

        _modifiedChunks.AddOrUpdate(chunkCoord, now, (_, _) => now);

        // Track the type of change
        _chunkChangeTypes.AddOrUpdate(
            chunkCoord,
            [changeType],
            (_, existing) =>
            {
                lock (_lock)
                {
                    existing.Add(changeType);
                    return existing;
                }
            });
    }

    private static bool IsStructureBlock(Block block)
    {
        if (block.Code == null) return false;

        var path = block.Code.Path;
        return path.Contains("sign") ||
               path.Contains("signpost") ||
               path.Contains("translocator") ||
               path.Contains("teleporter");
    }

    private void InvalidateGeoJson(string type)
    {
        _structureChanges.AddOrUpdate(type, _sapi.World.ElapsedMilliseconds, (_, _) => _sapi.World.ElapsedMilliseconds);
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
    /// Get structure has changed since timestamp
    /// </summary>
    public Dictionary<string, long> GetStructureChangesSince(long timestamp)
    {
        return _structureChanges
            .Where(kvp => kvp.Value > timestamp)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Clearly tracked changes for a chunk after it's been processed
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
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Unregister event handlers
            _sapi.Event.BreakBlock -= OnBlockBreaking;
            _sapi.Event.DidPlaceBlock -= OnBlockPlaced;
            _sapi.Event.ChunkColumnLoaded -= OnChunkColumnLoaded;

            _modifiedChunks.Clear();
            _chunkChangeTypes.Clear();
            _structureChanges.Clear();

            _sapi.Logger.Notification("[VintageAtlas] Chunk change tracker disposed");
        }
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


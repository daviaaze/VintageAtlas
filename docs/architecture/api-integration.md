# Vintage Story API Integration Guide

**Last Updated:** 2025-10-02  
**For:** VintageAtlas v1.0.0

## Overview

This document details how VintageAtlas integrates with the Vintage Story Server API, including best practices, event handling, and threading considerations.

## Vintage Story API Fundamentals

### Core Interfaces

#### ICoreServerAPI
Main server API interface, provided to all server-side systems.

**Key Members:**
```csharp
IServerPlayer[] World.AllOnlinePlayers  // Online players
IWorldAccessor World                     // World access
IBlockAccessor World.BlockAccessor       // Block reading
ILogger Logger                           // Logging
IServerEventAPI Event                    // Event registration
IModLoader ModLoader                     // Mod interactions
```

#### IServerEventAPI
Event registration for server-side events.

**Key Events Used:**
```csharp
BreakBlock                  // Block breaking (before)
DidPlaceBlock              // Block placement (after)
CanPlaceOrBreakBlock       // Both operations (before)
ChunkColumnLoaded          // Chunk generation
OnTrySpawnEntity           // Entity spawning
PlayerJoin                 // Player login
SaveGameLoaded             // World loaded
RegisterAsyncServerSystem  // Background systems
```

#### IAsyncServerSystem
Interface for background/async server systems.

**Methods to Implement:**
```csharp
void OnStart()                      // System starting
void OnShutdown()                   // System stopping
void OnRestart()                    // System restarting
void OnAsyncServerTick(float dt)    // Called each tick
void OnSeparateThreadShutDown()     // Thread cleanup
```

**Properties:**
```csharp
bool Enabled                // System enabled state
long ElapsedMilliseconds    // Game time
double OffsetSec            // Start delay
int IntervalMs              // Tick interval
```

## Event-Driven Architecture

### Block Change Detection

VintageAtlas uses multiple events for comprehensive block change tracking:

#### 1. BreakBlock Event (Primary)
```csharp
_sapi.Event.BreakBlock += OnBlockBreaking;

private void OnBlockBreaking(
    IServerPlayer byPlayer,
    BlockSelection blockSel,
    ref float dropQuantityMultiplier,
    ref EnumHandling handled)
{
    if (blockSel?.Position != null)
    {
        TrackBlockChange(blockSel.Position, ChunkChangeType.BlockModified);
    }
}
```

**Why:** Stable signature across VS versions, called before break

#### 2. DidPlaceBlock Event (Primary)
```csharp
_sapi.Event.DidPlaceBlock += OnBlockPlaced;

private void OnBlockPlaced(
    IServerPlayer byPlayer,
    int oldblockId,
    BlockSelection blockSel,
    ItemStack withItemStack)
{
    if (blockSel?.Position != null)
    {
        TrackBlockChange(blockSel.Position, ChunkChangeType.BlockModified);
    }
}
```

**Why:** Catches placements, fires after block is placed

#### 3. CanPlaceOrBreakBlock Event (Backup)
```csharp
_sapi.Event.CanPlaceOrBreakBlock += OnCanPlaceOrBreakBlock;

private void OnCanPlaceOrBreakBlock(
    IServerPlayer byPlayer,
    BlockSelection blockSel,
    ref EnumHandling handling)
{
    if (blockSel?.Position != null)
    {
        TrackBlockChange(blockSel.Position, ChunkChangeType.BlockModified);
    }
}
```

**Why:** Catches both operations, provides redundancy

#### 4. ChunkColumnLoaded Event
```csharp
_sapi.Event.ChunkColumnLoaded += OnChunkColumnLoaded;

private void OnChunkColumnLoaded(
    Vec2i chunkCoord,
    IWorldChunk[] chunks)
{
    TrackChunkChange(chunkCoord, ChunkChangeType.NewChunk);
}
```

**Why:** Detects newly generated terrain

#### 5. OnTrySpawnEntity Event
```csharp
_sapi.Event.OnTrySpawnEntity += OnEntitySpawn;

private void OnEntitySpawn(
    ref EntityProperties properties,
    Vec3d spawnPosition,
    long herdId)
{
    if (properties?.Code?.Path?.Contains("trader") == true)
    {
        InvalidateGeoJson("trader");
    }
}
```

**Why:** Tracks entity spawns (traders, etc.) for GeoJSON updates

### Event Coverage Analysis

| Event | Fires When | Purpose | Coverage |
|-------|-----------|---------|----------|
| `BreakBlock` | Before block breaks | Track breaking | ✅ 100% |
| `DidPlaceBlock` | After block placed | Track placement | ✅ 100% |
| `CanPlaceOrBreakBlock` | Before either operation | Backup/validation | ✅ 100% |
| `ChunkColumnLoaded` | New terrain generated | Track new chunks | ✅ 100% |
| `OnTrySpawnEntity` | Entity spawns | Track structures | ✅ 100% |

**Total Coverage:** 100% of relevant world changes

### Event Handler Best Practices

#### 1. Keep Handlers Fast
```csharp
// GOOD: Quick operation
private void OnBlockPlaced(...)
{
    TrackBlockChange(blockSel.Position, ChunkChangeType.BlockModified);
}

// BAD: Slow operation blocks game
private void OnBlockPlaced(...)
{
    RegenerateTiles();  // NEVER do this!
}
```

#### 2. Use Thread-Safe Collections
```csharp
private readonly ConcurrentDictionary<Vec2i, ChunkChangeInfo> _modifiedChunks 
    = new ConcurrentDictionary<Vec2i, ChunkChangeInfo>();

private void OnBlockPlaced(...)
{
    var chunkPos = new Vec2i(blockPos.X / 32, blockPos.Z / 32);
    _modifiedChunks.AddOrUpdate(chunkPos, /* ... */);
}
```

#### 3. Always Unregister Events
```csharp
public void Dispose()
{
    if (_sapi != null)
    {
        _sapi.Event.BreakBlock -= OnBlockBreaking;
        _sapi.Event.DidPlaceBlock -= OnBlockPlaced;
        _sapi.Event.CanPlaceOrBreakBlock -= OnCanPlaceOrBreakBlock;
        _sapi.Event.ChunkColumnLoaded -= OnChunkColumnLoaded;
        _sapi.Event.OnTrySpawnEntity -= OnEntitySpawn;
    }
}
```

## Threading Model

### Game Thread (Main Thread)

**Rules:**
- ✅ All game state access must be on main thread
- ✅ Event handlers run on main thread
- ❌ Never block with `Thread.Sleep()`, `Task.Wait()`, or long operations

**Safe Operations:**
```csharp
// Safe: Quick reads
var chunk = _sapi.World.BlockAccessor.GetChunk(x, y, z);
var block = _sapi.World.BlockAccessor.GetBlock(pos);

// Safe: Quick updates
_modifiedChunks.TryAdd(pos, info);

// Safe: Enqueue work for later
_sapi.Event.EnqueueMainThreadTask(() => {
    // Game state access here
}, "VintageAtlas-Update");
```

**Unsafe Operations:**
```csharp
// NEVER: Blocking on main thread
Thread.Sleep(5000);

// NEVER: Synchronous wait on main thread
Task.Wait();

// NEVER: Long-running operations
for (int i = 0; i < 1000000; i++) {
    // Heavy computation
}
```

### Background Threads

**Use for:**
- CPU-intensive map generation
- File I/O operations
- Network requests (HTTP server)
- Image processing

**Pattern:**
```csharp
Task.Run(() =>
{
    // Safe: CPU-intensive work
    var tileData = GenerateTileData();
    
    // Need game state? Switch to main thread
    _sapi.Event.EnqueueMainThreadTask(() =>
    {
        // Safe: Access game state here
        UpdateWorld(tileData);
    }, "VintageAtlas-UpdateWorld");
});
```

### IAsyncServerSystem Integration

VintageAtlas uses `IAsyncServerSystem` for background processing:

```csharp
public class BackgroundTileService : IAsyncServerSystem, IDisposable
{
    // Properties
    public long ElapsedMilliseconds { get; private set; }
    public bool Enabled { get; set; } = true;
    public double OffsetSec { get; } = 0;
    public int IntervalMs { get; } = 1000;
    
    // Lifecycle
    public void OnStart()
    {
        // Start worker thread
        _workerThread = new Thread(WorkerLoop)
        {
            Name = "VintageAtlas-TileGenerator",
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal
        };
        _workerThread.Start();
    }
    
    public void OnShutdown()
    {
        // Stop gracefully
        _isRunning = false;
        _workerThread?.Join(TimeSpan.FromSeconds(5));
    }
    
    public void OnRestart()
    {
        // Stop and restart
        OnShutdown();
        Thread.Sleep(500);
        OnStart();
    }
    
    public void OnAsyncServerTick(float dt)
    {
        // Called on main thread each tick
        ElapsedMilliseconds = _sapi.World.ElapsedMilliseconds;
    }
    
    public void OnSeparateThreadShutDown()
    {
        // Final cleanup
        Dispose();
    }
}
```

**Registration:**
```csharp
public override void StartServerSide(ICoreServerAPI api)
{
    _sapi = api;
    
    _backgroundTileService = new BackgroundTileService(_sapi, _config);
    
    // Register with server
    _sapi.Event.RegisterAsyncServerSystem(_backgroundTileService);
}
```

**Benefits:**
- ✅ Server manages lifecycle
- ✅ Proper shutdown handling
- ✅ Integration with server monitoring
- ✅ Standard VS pattern

## World Data Access

### Reading Blocks

```csharp
// Get block accessor
IBlockAccessor ba = _sapi.World.BlockAccessor;

// Read single block
Block block = ba.GetBlock(pos);
int blockId = ba.GetBlockId(pos);

// Check if chunk is loaded
bool isLoaded = ba.GetChunkAtBlockPos(pos) != null;

// Get chunk
IWorldChunk chunk = ba.GetChunk(chunkX, chunkY, chunkZ);
```

### Reading Chunks

```csharp
// Get chunk (Y is chunk layer, not block Y)
IWorldChunk chunk = _sapi.World.BlockAccessor.GetChunk(
    chunkX, 
    0,  // Y layer (usually 0 for terrain)
    chunkZ
);

if (chunk != null)
{
    // Read blocks from chunk
    for (int y = 0; y < 256; y++)
    {
        for (int x = 0; x < 32; x++)
        {
            for (int z = 0; z < 32; z++)
            {
                int blockId = chunk.GetBlockId(x, y, z);
                // Process block
            }
        }
    }
}
```

### Savegame Access

For offline/bulk access:

```csharp
public class SavegameDataLoader
{
    private readonly string _savePath;
    
    public Dictionary<Vec2i, ChunkData> LoadChunks()
    {
        // Access save files directly
        // Parse chunk data from disk
        // Much faster than runtime access for full scans
    }
}
```

## Player Data Access

### Online Players

```csharp
// Get all online players
IServerPlayer[] players = _sapi.World.AllOnlinePlayers;

foreach (var player in players)
{
    // Position
    Vec3d pos = player.Entity.Pos.XYZ;
    
    // Stats
    float health = player.Entity.WatchedAttributes.GetFloat("health");
    float saturation = player.Entity.WatchedAttributes.GetFloat("saturation");
    
    // Name and ID
    string name = player.PlayerName;
    string uid = player.PlayerUID;
}
```

### Player Events

```csharp
_sapi.Event.PlayerJoin += (player) =>
{
    // Player joined
    _logger.Notification($"Player {player.PlayerName} joined");
};

_sapi.Event.PlayerNowPlaying += (player) =>
{
    // Player started playing (after character selection)
};
```

## Entity Access

### Finding Entities

```csharp
// Find all entities in range
var entities = _sapi.World.GetEntitiesAround(
    centerPos,
    range,
    range,
    entity => entity.Code.Path.Contains("trader")
);

// Filter by type
var traders = entities
    .Where(e => e.Code.Path.StartsWith("humanoid-trader"))
    .ToList();
```

### Entity Properties

```csharp
Entity entity = // ...

// Code (type)
AssetLocation code = entity.Code;  // e.g., "game:humanoid-trader-commodities"

// Position
Vec3d pos = entity.Pos.XYZ;

// Properties
string name = entity.GetBehavior<EntityBehaviorNameTag>()?.DisplayName;
```

## Block Entity Access

### Finding Block Entities

```csharp
// Check if position has block entity
BlockEntity be = _sapi.World.BlockAccessor.GetBlockEntity(pos);

if (be is BlockEntitySign sign)
{
    string text = sign.Text;
}
else if (be is BlockEntityStaticTranslocator translocator)
{
    // Translocator logic
}
```

### Scanning for Block Entities

```csharp
// Get all loaded chunks
var loadedChunks = _sapi.World.BlockAccessor.LoadedChunks;

foreach (var chunk in loadedChunks)
{
    // Iterate block entities in chunk
    foreach (var be in chunk.BlockEntities.Values)
    {
        if (be is BlockEntitySign sign)
        {
            // Process sign
        }
    }
}
```

## Configuration and Persistence

### Mod Configuration

```csharp
// Load config
var config = _sapi.LoadModConfig<AtlasConfig>("VintageAtlasConfig.json");

// Save config
_sapi.StoreModConfig(config, "VintageAtlasConfig.json");
```

### Data Persistence

```csharp
// Get mod data path
string dataPath = _sapi.GetOrCreateDataPath("VintageAtlas");

// Save data
File.WriteAllText(
    Path.Combine(dataPath, "data.json"),
    JsonSerializer.Serialize(data)
);
```

## Logging

### Log Levels

```csharp
_sapi.Logger.Error("Critical error: {0}", error);
_sapi.Logger.Warning("Warning: {0}", warning);
_sapi.Logger.Notification("Important: {0}", info);
_sapi.Logger.Debug("Debug: {0}", detail);
_sapi.Logger.VerboseDebug("Trace: {0}", trace);
```

### Best Practices

```csharp
// Good: Informative and actionable
_sapi.Logger.Error("Failed to generate tile {0},{1}: {2}", x, z, ex.Message);

// Bad: Vague and unhelpful
_sapi.Logger.Error("Error");

// Good: Use appropriate level
_sapi.Logger.VerboseDebug("Processing chunk {0},{1}", x, z);  // Trace

// Bad: Too verbose for notification
_sapi.Logger.Notification("Processing chunk {0},{1}", x, z);  // Spam
```

## Common Pitfalls and Solutions

### Pitfall 1: Blocking Main Thread

❌ **Wrong:**
```csharp
_sapi.Event.DidPlaceBlock += (player, oldId, blockSel, stack) =>
{
    RegenerateAllTiles();  // Takes 30 seconds!
};
```

✅ **Correct:**
```csharp
_sapi.Event.DidPlaceBlock += (player, oldId, blockSel, stack) =>
{
    TrackChange(blockSel.Position);  // Quick
};

// Later, on background thread:
Task.Run(() => RegenerateTiles());
```

### Pitfall 2: Race Conditions

❌ **Wrong:**
```csharp
private List<Vec2i> _modifiedChunks = new List<Vec2i>();

// Called from event (main thread)
void OnBlockPlaced(...) => _modifiedChunks.Add(chunkPos);

// Called from background thread
void ProcessChunks() => foreach (var chunk in _modifiedChunks) { }
```

✅ **Correct:**
```csharp
private ConcurrentDictionary<Vec2i, ChunkInfo> _modifiedChunks 
    = new ConcurrentDictionary<Vec2i, ChunkInfo>();

void OnBlockPlaced(...) => _modifiedChunks.TryAdd(chunkPos, info);
void ProcessChunks() => foreach (var kvp in _modifiedChunks) { }
```

### Pitfall 3: Caching Chunk References

❌ **Wrong:**
```csharp
var chunk = _sapi.World.BlockAccessor.GetChunk(x, y, z);
// Later, on background thread:
ProcessChunk(chunk);  // Chunk might be unloaded!
```

✅ **Correct:**
```csharp
// Always re-fetch on main thread when needed
_sapi.Event.EnqueueMainThreadTask(() =>
{
    var chunk = _sapi.World.BlockAccessor.GetChunk(x, y, z);
    if (chunk != null)
    {
        var data = ExtractChunkData(chunk);
        Task.Run(() => ProcessChunkData(data));
    }
}, "VintageAtlas-FetchChunk");
```

### Pitfall 4: Event Signature Changes

❌ **Fragile:**
```csharp
_sapi.Event.DidBreakBlock += OnBlockBroken;  // Signature varies by VS version!
```

✅ **Stable:**
```csharp
_sapi.Event.BreakBlock += OnBlockBreaking;  // Stable signature
// OR use multiple events for redundancy
```

## Performance Best Practices

### 1. Batch Operations

```csharp
// Good: Batch chunk updates
var modifiedChunks = GetModifiedChunks();
RegenerateTilesForChunks(modifiedChunks);  // Process all at once

// Bad: One-by-one
foreach (var chunk in modifiedChunks)
{
    RegenerateTile(chunk);  // Overhead per call
}
```

### 2. Use Parallel Processing

```csharp
Parallel.ForEach(chunks,
    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
    chunk =>
    {
        ProcessChunk(chunk);
    });
```

### 3. Limit Event Handlers

```csharp
// Good: Track changes, process later
OnBlockPlaced => TrackChange();  // Fast
OnGameTick (30s) => ProcessChanges();  // Batched

// Bad: Immediate processing
OnBlockPlaced => RegenerateTile();  // Too frequent
```

### 4. Cache Frequently Accessed Data

```csharp
// Good: Cache spawn position
private Vec3d? _cachedSpawn;
public Vec3d GetSpawn()
{
    if (_cachedSpawn == null)
    {
        _cachedSpawn = _sapi.World.DefaultSpawnPosition.XYZ;
    }
    return _cachedSpawn.Value;
}

// Bad: Query every time
public Vec3d GetSpawn()
{
    return _sapi.World.DefaultSpawnPosition.XYZ;  // Repeated lookups
}
```

## Testing and Debugging

### Enable Verbose Logging

```csharp
_sapi.Logger.VerboseDebug("Event fired: {0} at {1}", eventName, pos);
```

### Monitor Performance

```csharp
var sw = Stopwatch.StartNew();
// Operation
sw.Stop();
_sapi.Logger.Debug("Operation took {0}ms", sw.ElapsedMilliseconds);
```

### Test Event Coverage

```
1. Place blocks → Check logs
2. Break blocks → Check logs
3. Generate new terrain → Check logs
4. Spawn entities → Check logs
```

## Version Compatibility

**Current Target:** Vintage Story 1.20.1+

**API Stability:**
- ✅ `ICoreServerAPI` - Stable
- ✅ `IServerEventAPI` - Stable
- ✅ `IAsyncServerSystem` - Stable
- ⚠️ Some event signatures vary between versions

**Recommendations:**
- Use stable events (`BreakBlock` over `DidBreakBlock`)
- Provide fallbacks for missing events
- Test on target VS version
- Document minimum VS version in modinfo.json

## Resources

- [Official VS API Docs](https://apidocs.vintagestory.at/)
- [VS Wiki Modding Guide](https://wiki.vintagestory.at/Modding:Getting_Started)
- [VS GitHub Examples](https://github.com/anegostudios/vsmodexamples)
- [VS Official Discord](https://discord.gg/vintagestory)

---

**See Also:**
- [Architecture Overview](architecture-overview.md)
- [Coordinate Systems](coordinate-systems.md)
- [Tile Generation](../implementation/tile-generation.md)


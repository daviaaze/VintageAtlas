# Vintage Story Modding: Constraints, Implementations & Features

**Last Updated:** 2025-10-02  
**Version:** 1.20.1+  
**Target:** VintageAtlas Development

## Overview

This document consolidates critical information about Vintage Story's modding API, focusing on constraints and best practices that directly impact VintageAtlas development. It synthesizes information from official documentation and practical implementation experience.

---

## Table of Contents

1. [Threading & Synchronization](#threading--synchronization)
2. [World & Chunk Access](#world--chunk-access)
3. [Data Persistence](#data-persistence)
4. [Performance Considerations](#performance-considerations)
5. [API Patterns](#api-patterns)
6. [Common Pitfalls](#common-pitfalls)

---

## Threading & Synchronization

### Critical Threading Rules

**Main Thread Exclusivity:**
```csharp
// ❌ NEVER DO THIS - Blocking main thread
public void OnGameTick()
{
    Thread.Sleep(1000); // BLOCKS GAME!
    // Heavy computation here
}

// ✅ CORRECT - Offload to background
public void OnGameTick()
{
    Task.Run(() => {
        // Heavy computation on thread pool
        var result = ProcessData();
        
        // Return to main thread for game state access
        sapi.Event.EnqueueMainThreadTask(() => {
            ApplyResult(result);
            return true;
        }, "apply-result");
    });
}
```

### Chunk Thread Safety

**Chunk Access Must Be Main Thread:**
```csharp
// ❌ WRONG - Race condition
Task.Run(() => {
    var chunk = sapi.World.BlockAccessor.GetChunk(x, y, z);
    ProcessChunk(chunk); // Chunk may unload!
});

// ✅ CORRECT - Main thread access
sapi.Event.EnqueueMainThreadTask(() => {
    var chunk = sapi.World.BlockAccessor.GetChunk(x, y, z);
    if (chunk != null)
    {
        // Quick extraction of data
        var data = ExtractChunkData(chunk);
        
        // Process extracted data off main thread
        Task.Run(() => ProcessData(data));
    }
    return true;
}, "chunk-access");
```

### Chunk Serialization Timing

**From [Chunk Moddata Wiki](https://wiki.vintagestory.at/Modding:Chunk_Moddata):**

Chunks serialize to disk during:
1. **Regular save events** (every few minutes)
2. **Chunk unload** (when players move away)
3. **Manual save** (server shutdown, commands)

**Critical Constraint:** Serialization runs on **chunk thread** with chunk locked.

```csharp
// ❌ DANGEROUS - Race condition
public void OnBlockPlaced(BlockPos pos)
{
    var chunk = GetChunk(pos);
    chunk.SetModdata("mymod:data", myData); // May serialize too early!
    
    // More block modifications...
    // Chunk may serialize before these complete!
}

// ✅ SAFE - Use LiveModData
public void OnBlockPlaced(BlockPos pos)
{
    var chunk = GetChunk(pos);
    chunk.LiveModData["mymod:data"] = myDataObject;
    chunk.MarkModified(); // Trigger serialization
    
    // LiveModData serializes at exactly the right time
}
```

**ProtoBeforeSerialization Pattern for Server Mod Data:**
```csharp
[ProtoContract]
public class SerializationCallback
{
    public delegate void OnSerializationDelegate(IServerChunk chunk);
    
    private readonly IServerChunk _chunk;
    public OnSerializationDelegate OnSerialization;
    
    public SerializationCallback(IServerChunk chunk)
    {
        _chunk = chunk;
    }
    
    [ProtoBeforeSerialization]
    private void BeforeSerialization()
    {
        // Called on chunk thread right before serialization
        OnSerialization?.Invoke(_chunk);
    }
}

// Usage:
var serializer = new SerializationCallback(chunk);
chunk.LiveModData["mymod:serializer"] = serializer;
serializer.OnSerialization += chunk => {
    // Update server mod data at exactly the right time
    chunk.SetServerModdata("mymod:data", SerializerUtil.Serialize(myData));
};
```

---

## World & Chunk Access

### Block Accessor API

**Available Accessors:**
```csharp
// 1. World Block Accessor - Most common
IBlockAccessor ba = sapi.World.BlockAccessor;

// 2. Bulk Block Accessor - For large operations
IBlockAccessor bulkBa = sapi.World.BulkBlockAccessor;

// 3. Commit on-the-fly (for generation)
IBlockAccessor commitBa = sapi.World.GetBlockAccessorBulkUpdate(true, true);
```

**Thread-Safe Pattern:**
```csharp
// Must be called from main thread
var blockId = sapi.World.BlockAccessor.GetBlockId(pos);
var block = sapi.World.Blocks[blockId];

// Extract what you need
var blockCode = block.Code;
var blockMaterial = block.BlockMaterial;

// Now can process off main thread
Task.Run(() => {
    ProcessBlockData(blockCode, blockMaterial);
});
```

### Chunk Data Access Patterns

**IWorldChunk vs IServerChunk:**

| Feature | IWorldChunk | IServerChunk |
|---------|------------|--------------|
| **Available On** | Client & Server | Server Only |
| **Block Data** | Via BlockAccessor | Direct via .Data array |
| **Mod Data** | LiveModData, GetModdata | GetServerModdata, SetServerModdata |
| **Performance** | Standard | Faster (direct access) |

**Accessing Chunk Block Data:**
```csharp
// Method 1: Via BlockAccessor (safe, slower)
public int GetBlockAt(int worldX, int worldY, int worldZ)
{
    var pos = new BlockPos(worldX, worldY, worldZ);
    return sapi.World.BlockAccessor.GetBlockId(pos);
}

// Method 2: Direct chunk access (faster, requires chunk lock)
public int GetBlockAt(IServerChunk chunk, int localX, int localY, int localZ)
{
    // Chunk data is flat array: [y * chunkSize * chunkSize + z * chunkSize + x]
    int chunkSize = 32; // ServerChunkSize
    int index = localY * chunkSize * chunkSize + localZ * chunkSize + localX;
    return chunk.Data[index];
}
```

**Chunk Coordinate Conversion:**
```csharp
public class ChunkCoordinates
{
    private const int CHUNK_SIZE = 32;
    
    // World coordinates to chunk coordinates
    public static (int chunkX, int chunkY, int chunkZ) WorldToChunk(int worldX, int worldY, int worldZ)
    {
        return (
            worldX / CHUNK_SIZE,
            worldY / CHUNK_SIZE,
            worldZ / CHUNK_SIZE
        );
    }
    
    // World coordinates to local chunk coordinates
    public static (int localX, int localY, int localZ) WorldToLocal(int worldX, int worldY, int worldZ)
    {
        return (
            worldX % CHUNK_SIZE,
            worldY % CHUNK_SIZE,
            worldZ % CHUNK_SIZE
        );
    }
    
    // Chunk + local to world
    public static (int worldX, int worldY, int worldZ) ChunkToWorld(
        int chunkX, int chunkY, int chunkZ,
        int localX, int localY, int localZ)
    {
        return (
            chunkX * CHUNK_SIZE + localX,
            chunkY * CHUNK_SIZE + localY,
            chunkZ * CHUNK_SIZE + localZ
        );
    }
}
```

### Map Chunk Access

**IMapChunk Interface:**
```csharp
// ServerMain provides WorldMap
var serverMain = (ServerMain)sapi.World;
var mapChunk = serverMain.WorldMap.GetMapChunk(chunkX, chunkZ);

if (mapChunk != null)
{
    // Available data:
    var heightMap = mapChunk.RainHeightMap; // byte[1024] (32x32)
    var worldGenTerrainHeightMap = mapChunk.WorldGenTerrainHeightMap; // ushort[1024]
    
    // Usage:
    for (int z = 0; z < 32; z++)
    {
        for (int x = 0; x < 32; x++)
        {
            int index = z * 32 + x;
            int height = heightMap[index];
            // height is relative to chunk Y
        }
    }
}
```

**⚠️ Important:** `IMapChunk` only provides heightmaps, NOT block IDs. To get actual block data, you must use `IBlockAccessor` or access `IServerChunk.Data`.

---

## Data Persistence

### Chunk Mod Data

**Two Storage Dictionaries:**

```csharp
// 1. SHARED MOD DATA (client + server)
// - Synced to clients over network
// - Serialized to save game
// - Use LiveModData for automatic serialization
chunk.LiveModData["mymod:identifier"] = myObject; // Auto-serialized

// 2. SERVER MOD DATA (server only)
// - NOT synced to clients
// - Only serialized to save game
// - Manual serialization required
byte[] data = SerializerUtil.Serialize(myObject);
serverChunk.SetServerModdata("mymod:identifier", data);

// Reading server mod data
byte[] data = serverChunk.GetServerModdata("mymod:identifier");
if (data != null)
{
    var myObject = SerializerUtil.Deserialize<MyDataType>(data);
}
```

**ProtoContract Setup:**
```csharp
[ProtoContract]
public class MyChunkData
{
    [ProtoMember(1)]
    public int SomeValue;
    
    [ProtoMember(2)]
    public string SomeString;
    
    [ProtoMember(3)]
    public Dictionary<string, int> SomeDict;
}

// Usage with LiveModData
var data = new MyChunkData { SomeValue = 42 };
chunk.LiveModData["mymod:data"] = data;
chunk.MarkModified(); // Trigger save
```

### SaveGame Mod Data

**Global save game data (not chunk-specific):**

```csharp
// Writing
public void SaveGlobalData()
{
    var data = new MyGlobalData
    {
        Version = 1,
        LastExportTime = DateTime.UtcNow
    };
    
    sapi.WorldManager.SaveGame.StoreData("mymod:global", 
        SerializerUtil.Serialize(data));
}

// Reading
public MyGlobalData LoadGlobalData()
{
    byte[] data = sapi.WorldManager.SaveGame.GetData("mymod:global");
    if (data == null) return new MyGlobalData();
    
    return SerializerUtil.Deserialize<MyGlobalData>(data);
}
```

### File-Based Persistence

**ModData Directory:**
```csharp
// Get mod-specific data directory
string modDataPath = sapi.GetOrCreateDataPath("mymodid");

// Example: /path/to/save/ModData/mymodid/

// Storing custom files
string filePath = Path.Combine(modDataPath, "custom_data.json");
File.WriteAllText(filePath, JsonConvert.SerializeObject(myData));
```

**Best Practices:**
- Use `GetOrCreateDataPath()` for mod data directory
- Store large datasets in files (SQLite, JSON) not in chunk data
- Use chunk data only for chunk-specific information
- Implement versioning for data structures

---

## Performance Considerations

### Efficient Block Access

**From [Modding Efficiently Wiki](https://wiki.vintagestory.at/index.php/Modding_Efficiently):**

```csharp
// ❌ SLOW - Individual block lookups
for (int x = 0; x < 1000; x++)
{
    for (int z = 0; z < 1000; z++)
    {
        var pos = new BlockPos(x, 0, z);
        var blockId = sapi.World.BlockAccessor.GetBlockId(pos);
        // Process...
    }
}

// ✅ FAST - Bulk chunk access
var chunks = GetChunksInArea(startX, startZ, endX, endZ);
foreach (var chunk in chunks)
{
    // Direct array access
    for (int i = 0; i < chunk.Data.Length; i++)
    {
        int blockId = chunk.Data[i];
        // Process...
    }
}
```

### Caching Strategies

**Block Color Mapping Example:**
```csharp
public class BlockColorCache
{
    private readonly ICoreServerAPI _sapi;
    private readonly Dictionary<int, uint> _blockIdToColor = new();
    
    public void Initialize()
    {
        // Cache all block colors at startup
        for (int id = 0; id < _sapi.World.Blocks.Count; id++)
        {
            var block = _sapi.World.Blocks[id];
            if (block == null) continue;
            
            // Calculate color once
            _blockIdToColor[id] = CalculateBlockColor(block);
        }
    }
    
    public uint GetColor(int blockId)
    {
        // O(1) lookup vs repeated calculation
        return _blockIdToColor.TryGetValue(blockId, out var color) 
            ? color 
            : 0xFF000000;
    }
}
```

### Memory Management

**Chunk Unloading:**
```csharp
// ❌ BAD - Keeping references to chunks
private Dictionary<Vec2i, IServerChunk> _cachedChunks = new();

public void CacheChunk(int chunkX, int chunkZ)
{
    var chunk = GetChunk(chunkX, chunkZ);
    _cachedChunks[new Vec2i(chunkX, chunkZ)] = chunk; // Prevents unload!
}

// ✅ GOOD - Use ConditionalWeakTable
private ConditionalWeakTable<IServerChunk, MyChunkData> _chunkData = new();

public void StoreChunkData(IServerChunk chunk, MyChunkData data)
{
    _chunkData.Add(chunk, data); // Allows chunk to be GC'd
}
```

### Parallel Processing

**Safe Parallelization:**
```csharp
// Extract data on main thread
var chunkDataList = new List<ChunkDataSnapshot>();
foreach (var chunk in chunks)
{
    chunkDataList.Add(ExtractChunkSnapshot(chunk));
}

// Process in parallel (off main thread)
await Parallel.ForEachAsync(chunkDataList,
    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
    async (chunkData, ct) =>
    {
        await ProcessChunkDataAsync(chunkData);
    });
```

---

## API Patterns

### Event Registration

**Common Server Events:**
```csharp
public override void StartServerSide(ICoreServerAPI api)
{
    // Player events
    api.Event.PlayerJoin += OnPlayerJoin;
    api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
    api.Event.PlayerDisconnect += OnPlayerDisconnect;
    api.Event.PlayerDeath += OnPlayerDeath;
    
    // World events
    api.Event.SaveGameLoaded += OnSaveGameLoaded;
    api.Event.GameWorldSave += OnGameWorldSave;
    api.Event.ChunkDirty += OnChunkDirty;
    
    // Game tick
    api.Event.RegisterGameTickListener(OnGameTick, 1000); // Every 1 second
    
    // Delayed execution
    api.Event.EnqueueMainThreadTask(() => {
        DoSomething();
        return true;
    }, "task-id");
}

public override void Dispose()
{
    // Always clean up!
    api.Event.PlayerJoin -= OnPlayerJoin;
    // ... unregister all events
}
```

### Block and Entity Access

**Safe Entity Iteration:**
```csharp
public void ProcessEntities()
{
    // Get snapshot of entities
    var entities = sapi.World.LoadedEntities.Values.ToList();
    
    foreach (var entity in entities)
    {
        if (entity == null || !entity.Alive) continue;
        
        // Process entity
        ProcessEntity(entity);
    }
}
```

**Block Material Access:**
```csharp
public EnumBlockMaterial GetBlockMaterial(int blockId)
{
    var block = sapi.World.Blocks[blockId];
    return block?.BlockMaterial ?? EnumBlockMaterial.Stone;
}

// Common materials:
// - EnumBlockMaterial.Stone
// - EnumBlockMaterial.Soil
// - EnumBlockMaterial.Wood
// - EnumBlockMaterial.Liquid
// - EnumBlockMaterial.Ice
// - EnumBlockMaterial.Snow
```

### Command Registration

```csharp
public override void StartServerSide(ICoreServerAPI api)
{
    api.ChatCommands.Create("mymod")
        .RequiresPrivilege(Privilege.controlserver) // Admin only
        .WithDescription("My mod commands")
        .BeginSubCommand("export")
            .WithDescription("Export data")
            .HandleWith(OnExportCommand)
        .EndSubCommand();
}

private TextCommandResult OnExportCommand(TextCommandCallingArgs args)
{
    // Execute command logic
    Task.Run(() => DoExport());
    
    return TextCommandResult.Success("Export started");
}
```

---

## Common Pitfalls

### 1. Chunk Caching Issues

**Problem:** Holding chunk references prevents unloading
```csharp
// ❌ CAUSES MEMORY LEAK
private IServerChunk _myChunk;

// ✅ USE WEAK REFERENCE OR COORDINATE
private Vec2i _myChunkCoord;
```

### 2. Thread Safety Violations

**Problem:** Accessing game state from background thread
```csharp
// ❌ CRASH / CORRUPTION
Task.Run(() => {
    var block = sapi.World.BlockAccessor.GetBlock(pos);
});

// ✅ ENQUEUE TO MAIN THREAD
Task.Run(() => {
    sapi.Event.EnqueueMainThreadTask(() => {
        var block = sapi.World.BlockAccessor.GetBlock(pos);
        return true;
    }, "get-block");
});
```

### 3. Serialization Races

**Problem:** Modifying chunk data during serialization
```csharp
// ❌ RACE CONDITION
chunk.SetModdata("key", data);
// Chunk may serialize here!
ModifyChunkMore();

// ✅ USE LIVEMODDATA
chunk.LiveModData["key"] = data;
chunk.MarkModified();
```

### 4. Performance Anti-Patterns

**Problem:** Per-tick heavy operations
```csharp
// ❌ LAGS SERVER
api.Event.RegisterGameTickListener(dt => {
    foreach (var player in api.World.AllOnlinePlayers)
    {
        ProcessExpensiveCalculation(player);
    }
}, 50); // Every 50ms!

// ✅ THROTTLE & BATCH
private int _tickCounter = 0;
api.Event.RegisterGameTickListener(dt => {
    if (++_tickCounter % 20 != 0) return; // Every second
    
    Task.Run(() => {
        // Off main thread
        var results = ProcessExpensiveBatch();
        
        sapi.Event.EnqueueMainThreadTask(() => {
            ApplyResults(results);
            return true;
        }, "apply-batch");
    });
}, 50);
```

### 5. Save/Load Ordering

**Problem:** Accessing data before game is ready
```csharp
// ❌ TOO EARLY
public override void StartServerSide(ICoreServerAPI api)
{
    LoadMyData(); // World may not be loaded yet!
}

// ✅ USE SAVEGAMELOADED EVENT
public override void StartServerSide(ICoreServerAPI api)
{
    api.Event.SaveGameLoaded += () => {
        LoadMyData(); // World is ready
    };
}
```

---

## VintageAtlas-Specific Implications

### Tile Generation Constraints

Based on these constraints, tile generation must:

1. **Extract block data on main thread**
   ```csharp
   // Main thread: Quick data extraction
   var snapshot = new ChunkSnapshot();
   for (int i = 0; i < chunk.Data.Length; i++)
   {
       snapshot.BlockIds[i] = chunk.Data[i];
       snapshot.Heights[i] = GetHeight(i);
   }
   
   // Background thread: Heavy rendering
   Task.Run(() => RenderTile(snapshot));
   ```

2. **Use cached block color mappings**
   ```csharp
   // Initialize once at startup
   LoadBlockColorMapping();
   
   // Fast lookup during rendering
   uint color = _blockColorCache[blockId];
   ```

3. **Handle chunk boundaries correctly**
   ```csharp
   // When accessing neighboring blocks for hill shading
   if (x == 31) // At chunk boundary
   {
       // Get from adjacent chunk
       var neighborChunk = GetChunk(chunkX + 1, chunkZ);
   }
   ```

4. **Implement efficient caching**
   ```csharp
   // Don't regenerate unchanged tiles
   if (IsTileUpToDate(tileKey))
       return cachedTile;
   ```

### Map Chunk Limitations

**Critical Finding:** `IMapChunk.RainHeightMap` is insufficient for full rendering:
- ❌ Only provides height data
- ❌ No block ID access
- ❌ No block material info
- ✅ Must use `IBlockAccessor` or direct chunk access for colors

**Implication for DynamicTileGenerator:**
```csharp
// Current (limited):
var mapChunk = server.WorldMap.GetMapChunk(x, z);
var height = mapChunk.RainHeightMap[index]; // Only height!

// Required (full featured):
var serverChunk = GetServerChunk(x, y, z);
int blockId = serverChunk.Data[index];
var block = sapi.World.Blocks[blockId];
uint color = GetBlockColor(block);
```

---

## Summary of Critical Constraints

| Constraint | Impact | Solution |
|-----------|--------|----------|
| **Main thread only for chunk access** | Can't generate tiles on background thread with chunk access | Extract data on main thread, render on background |
| **Chunks can unload** | Can't cache chunk references | Use `ConditionalWeakTable` or coordinates |
| **Serialization timing** | Race conditions with mod data | Use `LiveModData` or `ProtoBeforeSerialization` |
| **IMapChunk limited data** | Can't render colored tiles from MapChunk alone | Must access full chunk via `IBlockAccessor` |
| **Performance sensitive** | Heavy operations cause lag | Batch, parallelize, cache aggressively |

---

## References

1. [Chunk Moddata Wiki](https://wiki.vintagestory.at/Modding:Chunk_Moddata)
2. [Modding Efficiently Wiki](https://wiki.vintagestory.at/index.php/Modding_Efficiently)
3. [World Access Wiki](https://wiki.vintagestory.at/Modding:World_Access)
4. [SaveGame ModData Wiki](https://wiki.vintagestory.at/Modding:SaveGame_ModData)
5. [API Documentation](https://apidocs.vintagestory.at/api/Vintagestory.API.Server.html)

---

**Maintained by:** daviaaze  
**Last Reviewed:** 2025-10-02  
**Applies to:** VintageAtlas v1.0.0+, Vintage Story 1.20.1+


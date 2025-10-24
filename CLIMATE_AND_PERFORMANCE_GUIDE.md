# Vintage Story API: Climate, Rain, Performance & Threading Guide

## Climate Data System

### Overview
Climate data in Vintage Story is stored at multiple levels and can be accessed with different modes depending on your needs.

### Climate Data Structure

#### ClimateCondition Class (`Common/API/IBlockAccessor.cs`)
```csharp
public class ClimateCondition
{
    public float Temperature;           // Between -20 and +40 degrees
    public float WorldgenRainfall;      // 0..1, static world gen value
    public float WorldGenTemperature;   // Static world gen value
    public float GeologicActivity;      // 0..1, static
    public float Rainfall;              // 0..1, current or average
    public float RainCloudOverlay;      // Current rain cloud coverage
    public float Fertility;             // 0..1
    public float ForestDensity;         // 0..1
    public float ShrubDensity;          // 0..1
}
```

### Accessing Climate Data

#### Method 1: Using IBlockAccessor.GetClimateAt()
```csharp
ClimateCondition GetClimateAt(BlockPos pos, EnumGetClimateMode mode = EnumGetClimateMode.NowValues, double totalDays = 0);
```

**Climate Modes:**
- `WorldGenValues`: Static values from world generation (yearly averages)
- `NowValues`: Values at current calendar time (accounts for seasons, day/night)
- `ForSuppliedDateValues`: Values at a specific supplied time
- `ForSuppliedDate_TemperatureOnly`: Temperature only at supplied time (never returns null)
- `ForSuppliedDate_TemperatureRainfallOnly`: Temperature and rainfall only (never returns null)

#### Method 2: From Map Region (`Common/API/IMapRegion.cs`)
```csharp
IMapRegion region = blockAccessor.GetMapRegion(regionX, regionZ);
IntDataMap2D climateMap = region.ClimateMap;

// Climate data is packed as RGB in an integer:
// 16-23 bits = Red = temperature
// 8-15 bits = Green = rainfall
// 0-7 bits = Blue = unused
```

### Climate Calculation Utilities (`Common/Climate.cs`)

```csharp
// Temperature conversion (int [0,255] to float [-20,40])
float temp = Climate.GetScaledAdjustedTemperatureFloat(unscaledTemp, distToSealevel);

// Temperature conversion (float to int)
int tempInt = Climate.DescaleTemperature(temperature);

// Rainfall calculation (accounts for altitude)
int rainfall = Climate.GetRainFall(rainfall, yPosition);

// Fertility calculation
int fertility = Climate.GetFertility(rain, scaledTemp, posYRel);

// Constants
Climate.Sealevel = 110; // Updated when config loads
Climate.TemperatureScaleConversion = 255f / 60f;
```

**Important:** Temperature calculations include altitude adjustment:
- Formula: `distToSealevel / 1.5f` is subtracted from temperature
- This is also hardcoded in shader `colormap.vsh`

---

## Rain Detection System

### How the Game Determines Rain Location

#### 1. Rain Height Map (`Common/API/IMapChunk.cs`)
```csharp
IMapChunk mapChunk = blockAccessor.GetMapChunkAtBlockPos(pos);
ushort[] rainHeightMap = mapChunk.RainHeightMap;
int rainHeight = rainHeightMap[localX * ChunkSize + localZ];
```

**RainHeightMap:** The Y-position of the last non-rain-permeable block before air. Always updated after placing/removing blocks.

#### 2. Distance to Rainfall (`Common/API/IBlockAccessor.cs`)
```csharp
int distance = blockAccessor.GetDistanceToRainFall(
    BlockPos pos,
    int horizontalSearchWidth = 4,  // Default: 4 blocks
    int verticalSearchWidth = 1     // Default: 1 block
);
// Returns 99 if no rainfall found within search area
```

This performs a cheap 2D breadth-first search to find nearby rain.

#### 3. Global Rain Distance (Client-Side)
```csharp
// Set by SystemPlayerEnvAwarenessTracker every second
float distance = GlobalConstants.CurrentDistanceToRainFallClient;
// Search distance: 12 horizontal, 4 vertical
```

#### 4. Climate Condition Rainfall
```csharp
ClimateCondition climate = blockAccessor.GetClimateAt(pos, EnumGetClimateMode.NowValues);
float currentRain = climate.Rainfall;        // Current precipitation value
float rainOverlay = climate.RainCloudOverlay; // Cloud coverage
```

**Note:** When using `NowValues` mode, the Rainfall field contains current precipitation; otherwise it contains "yearly averages" or worldgen values.

---

## Performance Guidelines

### 1. Block Access Patterns

#### Use Appropriate Block Accessor Type

**Standard Block Accessor** - Single block operations:
```csharp
IBlockAccessor ba = world.BlockAccessor;
Block block = ba.GetBlock(pos);
```

**Caching Block Accessor** - 10-50% faster for tight loops:
```csharp
ICachingBlockAccessor cba = world.GetCachingBlockAccessor(false, false);
cba.Begin(); // CRITICAL: Must call before loop!
try {
    for (int i = 0; i < positions.Length; i++) {
        Block block = cba.GetBlock(positions[i]);
        // ... process block
    }
} finally {
    cba.Dispose(); // Always cleanup
}
```

**Bulk Block Accessor** - For setting many blocks:
```csharp
IBulkBlockAccessor bba = world.GetBlockAccessorBulkUpdate(true, true);
try {
    // Set many blocks
    bba.SetBlock(blockId, pos1);
    bba.SetBlock(blockId, pos2);
    // ... more blocks

    // Relights and syncs all at once
    bba.Commit();
} finally {
    // Cleanup
}
```

**Prefetch Block Accessor** - Pre-loads area for faster access:
```csharp
IBlockAccessorPrefetch pba = world.GetBlockAccessorPrefetch(false, false);
pba.PrefetchBlocks(minPos, maxPos);
// Now GetBlock() is faster in this area
```

**Lock-Free Block Accessor** - Very fast but read-only:
```csharp
IBlockAccessor lf = world.GetLockFreeBlockAccessor();
// No locking, very fast
// May occasionally return 0 (air) when chunk is being packed
// DO NOT use for writing!
```

#### Direct Chunk Access for Maximum Performance
```csharp
IWorldChunk chunk = blockAccessor.GetChunkAtBlockPos(pos);
if (chunk != null) {
    chunk.AcquireBlockReadLock(); // For bulk reads
    try {
        int index3d = ((localY * ChunkSize) + localZ) * ChunkSize + localX;
        int blockId = chunk.Data.GetBlockIdUnsafe(index3d);
        // ... process many blocks
    } finally {
        chunk.ReleaseBlockReadLock();
    }
}
```

### 2. Chunk Management

```csharp
// Check if chunk is loaded before accessing
IWorldChunk chunk = blockAccessor.GetChunkAtBlockPos(pos);
if (chunk == null || chunk.Disposed) return;

// Always unpack before reading block data
chunk.Unpack();

// For read-only access (faster, won't modify)
if (chunk.Unpack_ReadOnly()) {
    // Read blocks
}

// Mark chunk as recently accessed (prevents compression)
chunk.MarkFresh();
```

**Chunk Locking for Bulk Operations:**
```csharp
// Bulk read operations
chunk.AcquireBlockReadLock();
try {
    // Perform many reads using Unsafe methods
    int blockId = chunk.Data.GetBlockIdUnsafe(index3d);
} finally {
    chunk.ReleaseBlockReadLock(); // ALWAYS release within 8 seconds
}

// Bulk write operations (worldgen)
chunk.Data.TakeBulkReadLock();
try {
    // Multiple SetBlockUnsafe calls
    chunk.Data.SetBlockUnsafe(index3d, blockId);
} finally {
    chunk.Data.ReleaseBulkReadLock();
}
```

### 3. Performance Profiling

```csharp
FrameProfilerUtil profiler = world.FrameProfiler;

// Enable profiling
profiler.Enabled = true;
profiler.PrintSlowTicks = true;
profiler.PrintSlowTicksThreshold = 40; // milliseconds

// In your code
profiler.Mark("MyOperation");
// ... do work
profiler.Mark("NextOperation");
// ... more work

// For sub-operations
ProfileEntryRange entry = profiler.Enter("SubOperation");
try {
    // ... work
} finally {
    profiler.Leave();
}
```

**Off-Thread Profiling:**
```csharp
FrameProfilerUtil offThreadProfiler = new FrameProfilerUtil("ThreadName: ");
offThreadProfiler.Begin("Processing");
// ... work
offThreadProfiler.Mark("Step1");
// ... work
offThreadProfiler.OffThreadEnd(); // Queues output
```

### 4. Optimization Tips

- **Avoid GetBlock() in inner loops** - Cache block references
- **Use GlobalConstants.ChunkSize (32)** instead of property access
- **Batch block updates** - Use BulkBlockAccessor
- **Reuse objects** - Don't create new BlockPos in loops
- **Check chunk.Empty** before processing
- **Use chunk.Data.ContainsBlock(blockId)** for fast presence check
- **Avoid string operations** in hot paths
- **Use WalkBlocks()** instead of many GetBlock() calls:

```csharp
blockAccessor.WalkBlocks(minPos, maxPos, (block, x, y, z) => {
    // Process each block efficiently
});
```

---

## Thread Safety

### 1. Thread-Safe Random Numbers

**IWorldAccessor.Rand** (Recommended):
```csharp
// Built-in, uses ThreadLocal<Random> - thread-safe
Random rand = world.Rand;
int value = rand.Next(100);
```

**ThreadSafeRandom** (Manual):
```csharp
using Vintagestory.API.Util;

ThreadSafeRandom rand = new ThreadSafeRandom(seed);
int value = rand.Next(100); // Uses lock internally
```

### 2. Threading Utilities

**TyronThreadPool:**
```csharp
// Queue work on thread pool
TyronThreadPool.QueueTask(() => {
    // Your work here
}, "TaskName");

// For long-running tasks
TyronThreadPool.QueueLongDurationTask(() => {
    // Long work
}, "LongTaskName");

// Create dedicated thread
Thread thread = TyronThreadPool.CreateDedicatedThread(() => {
    // Thread work
}, "ThreadName");
thread.Start();
```

**Important Note from TyronThreadPool.cs:**
```csharp
// ThreadPool.SetMaxThreads has no effect below physical CPU core count
// See: https://learn.microsoft.com/en-us/dotnet/api/system.threading.threadpool.setmaxthreads
```

### 3. Thread-Safe Collections

```csharp
using System.Collections.Concurrent;

// Available thread-safe types
ConcurrentDictionary<K, V>
ConcurrentQueue<T>
ConcurrentBag<T>

// VS API specific
CachedConcurrentDictionary<K, V>  // From Datastructures
ConcurrentSmallDictionary<K, V>   // Optimized for small size
```

### 4. Block Accessor Thread Safety

**Main Thread:**
- Most IBlockAccessor methods are thread-safe but may block
- GetBlock() is thread-safe
- SetBlock() should generally be called on main thread

**Off-Thread (World Generation):**
```csharp
// In worldgen, chunks are not yet in world so safe to modify
IWorldGenBlockAccessor wgba = chunkGenRequest.BlockAccessor;
wgba.SetBlock(blockId, pos); // Safe during worldgen

// Use Unsafe methods when you know chunk has palette
chunk.Data.SetBlockUnsafe(index3d, blockId);
```

**GetLockFreeBlockAccessor:**
```csharp
// Read-only, no locks, very fast but may return 0 occasionally
IBlockAccessor lockFree = world.GetLockFreeBlockAccessor();
// Good for particle systems, rendering
```

### 5. Entity Behaviors Thread Safety

```csharp
public class MyEntityBehavior : EntityBehavior
{
    // Set to true if your behavior can be safely called from threads
    public override bool ThreadSafe => false; // Default: false

    // If true, OnGameTick may be called from non-main threads
}
```

### 6. Map Region/Chunk Thread Safety

```csharp
// Thread-safe method to add structures
IMapRegion region = blockAccessor.GetMapRegion(regionX, regionZ);
region.AddGeneratedStructure(structure); // Thread-safe, marks dirty

// Concurrent access to snow accumulation
IMapChunk mapChunk = chunk.MapChunk;
ConcurrentDictionary<Vec2i, float> snowAccum = mapChunk.SnowAccum;
```

### 7. Best Practices

1. **Minimize cross-thread calls** - Keep data local to threads
2. **Use appropriate block accessors** - Lock-free for reads, standard for writes
3. **Avoid shared state** - Use thread-local storage when possible
4. **Batch operations** - Collect results, apply on main thread
5. **Profile threading** - Use FrameProfilerUtil.offThreadProfiles
6. **Lock duration** - Keep locks short (< 8 seconds for chunk locks)
7. **Async systems** - Implement IAsyncServerSystem for background tasks

```csharp
// Example: Background processing
public class MyAsyncSystem : IAsyncServerSystem
{
    public void ThreadFunction() {
        // Runs on background thread
        // Do heavy computation here
    }

    public void OnSingleplayerOrServerTick(float dt) {
        // Runs on main thread
        // Apply results from background thread
    }
}
```

---

## Additional Useful Information

### 1. Constants and World Info

```csharp
// From GlobalConstants.cs
GlobalConstants.ChunkSize = 32; // Hard-coded
GlobalConstants.MaxWorldSizeXZ = 67108864; // 64M blocks
GlobalConstants.MaxWorldSizeY = 16384; // 16K blocks
GlobalConstants.SeaLevel; // Usually 110, from world config

// Current world state
int seaLevel = world.SeaLevel;
int seed = world.Seed;
long elapsed = world.ElapsedMilliseconds;
```

### 2. Calendar and Time

```csharp
IGameCalendar calendar = world.Calendar;
double totalDays = calendar.TotalDays;
double totalHours = calendar.TotalHours;
int year = calendar.Year;
string season = calendar.Season.ToString();

// Use for time-based climate
ClimateCondition climate = blockAccessor.GetClimateAt(
    pos,
    EnumGetClimateMode.ForSuppliedDateValues,
    calendar.TotalDays + 5.0 // 5 days in future
);
```

### 3. Wind Speed

```csharp
// Get wind at position
Vec3d windSpeed = blockAccessor.GetWindSpeedAt(pos);

// Client-side current wind (set by weather system)
Vec3f currentWind = GlobalConstants.CurrentWindSpeedClient;
Vec3f surfaceWind = GlobalConstants.CurrentSurfaceWindSpeedClient;
```

### 4. Light Access

```csharp
// Get light level (0-32)
int lightLevel = blockAccessor.GetLightLevel(pos, EnumLightLevelType.MaxTimeOfDayLight);

// Light level types:
// - OnlyBlockLight: Just block light
// - OnlySunLight: Just sun (unaffected by day/night)
// - MaxLight: max(sunlight, blocklight)
// - MaxTimeOfDayLight: max(sunlight * brightness, blocklight)
// - TimeOfDaySunLight: sunlight * brightness
// - Sunbrightness: Just the brightness multiplier

// Get RGB light values
Vec4f lightRgb = blockAccessor.GetLightRGBs(pos);
// XYZ = block light RGB, W = sun light brightness
```

### 5. Terrain Maps

```csharp
// World generation terrain height (not updated after placement)
int terrainHeight = blockAccessor.GetTerrainMapheightAt(pos);

// Current rain-permeable height (always updated)
int rainHeight = blockAccessor.GetRainMapHeightAt(pos);

// Check if position is valid
bool valid = blockAccessor.IsValidPos(pos);

// Check if traversable (for pathfinding)
bool canWalk = !blockAccessor.IsNotTraversable(pos);
```

### 6. Mod Data Storage

**Chunk ModData** (persists with chunk):
```csharp
IWorldChunk chunk = blockAccessor.GetChunkAtBlockPos(pos);

// Store data
chunk.SetModdata("mymod:data", myByteArray);
chunk.SetModdata<MyClass>("mymod:obj", myObject);

// Retrieve data
byte[] data = chunk.GetModdata("mymod:data");
MyClass obj = chunk.GetModdata<MyClass>("mymod:obj");

// For non-serialized data (faster, lost on unload)
chunk.LiveModData["mymod:cache"] = myCachedObject;
```

**MapChunk ModData** (2D column data):
```csharp
IMapChunk mapChunk = blockAccessor.GetMapChunkAtBlockPos(pos);
mapChunk.SetModdata("mymod:data", myByteArray);
mapChunk.SetModdata<MyClass>("mymod:obj", myObject);
```

**MapRegion ModData** (region-level data):
```csharp
IMapRegion region = blockAccessor.GetMapRegion(regionX, regionZ);
region.SetModdata("mymod:data", myByteArray);
```

### 7. Dimensions

Vintage Story supports multiple dimensions. Always be dimension-aware:

```csharp
// Use BlockPos which includes dimension info
BlockPos pos = new BlockPos(x, y, z, dimensionId);

// Check dimension
int dim = pos.dimension;

// InternalY includes dimension offset
int internalY = pos.InternalY; // Use for raw chunk access
```

### 8. Async Helpers

```csharp
using Vintagestory.API.Util;

// Run async operation (uses thread pool)
AsyncHelper.RunAsync(() => {
    // Background work
    return result;
}).ContinueWith(task => {
    // Main thread callback
    var result = task.Result;
});
```

---

## Quick Reference Summary

**Climate Access:**
- `blockAccessor.GetClimateAt(pos, mode)` - Get climate conditions
- `mapRegion.ClimateMap` - Raw climate data map
- `Climate.GetScaledAdjustedTemperatureFloat()` - Convert temp values

**Rain Detection:**
- `mapChunk.RainHeightMap` - Rain-blocking height at each position
- `blockAccessor.GetDistanceToRainFall(pos)` - Find nearby rain
- `GlobalConstants.CurrentDistanceToRainFallClient` - Player's rain distance

**Performance:**
- Use `ICachingBlockAccessor` for bulk reads
- Use `IBulkBlockAccessor` for bulk writes
- Use `GetLockFreeBlockAccessor()` for fast read-only
- Profile with `world.FrameProfiler`

**Thread Safety:**
- Use `world.Rand` for thread-safe random
- Use `TyronThreadPool` for background tasks
- Use chunk locking for bulk operations
- Avoid shared mutable state

**Storage:**
- `chunk.SetModdata<T>()` for chunk-persistent data
- `chunk.LiveModData` for temporary cache
- `mapChunk.SetModdata<T>()` for 2D column data

---

## Important Notes

1. **Climate data is dimension-aware** - Always use BlockPos, not raw coordinates
2. **Temperature changes with altitude** - Factor in distance to sea level
3. **Rain maps update automatically** - When blocks are placed/removed
4. **Chunk locking has 8-second timeout** - Always use try/finally
5. **Lock-free accessor may return 0** - When chunks are packing
6. **Worldgen is single-threaded per chunk** - Safe to use unsafe methods
7. **Main thread synchronization** - RegisterCallback() for main thread execution

## Example: Complete Climate-Aware System

```csharp
public class MyClimateSystem : ModSystem
{
    private ICoreAPI api;
    private ICachingBlockAccessor cba;

    public override void StartServerSide(ICoreServerAPI sapi) {
        api = sapi;
        sapi.Event.RegisterGameTickListener(OnGameTick, 1000);
    }

    private void OnGameTick(float dt) {
        // Use caching accessor for performance
        cba = api.World.GetCachingBlockAccessor(false, false);
        cba.Begin();

        try {
            foreach (var player in api.World.AllOnlinePlayers) {
                BlockPos pos = player.Entity.Pos.AsBlockPos;

                // Get current climate
                ClimateCondition climate = cba.GetClimateAt(pos, EnumGetClimateMode.NowValues);

                // Check if it's raining nearby
                int rainDist = cba.GetDistanceToRainFall(pos);
                bool isRaining = rainDist < 4;

                // Get rain height
                IMapChunk mapChunk = cba.GetMapChunkAtBlockPos(pos);
                if (mapChunk != null) {
                    int localX = pos.X % GlobalConstants.ChunkSize;
                    int localZ = pos.Z % GlobalConstants.ChunkSize;
                    int rainHeight = mapChunk.RainHeightMap[localZ * GlobalConstants.ChunkSize + localX];
                    bool underRoof = pos.Y < rainHeight;

                    // Do something with this information
                    if (isRaining && !underRoof && climate.Temperature < 0) {
                        // It's snowing on the player!
                    }
                }
            }
        } finally {
            cba.Dispose();
        }
    }
}
```

---

*This guide was compiled from the Vintage Story API source code v1.19+*
*Last updated: 2025*


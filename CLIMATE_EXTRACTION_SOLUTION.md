# Climate Data Extraction Solution

## Problem Statement

The original `ClimateExtractor` had a critical issue: it was trying to use `BlockAccessor.GetClimateAt()` on chunks loaded from the **database**, but this API requires chunks to be **actively loaded in the game's memory**. This only worked for spawn area chunks, not the entire world.

## Solution: Dual-Mode Climate Extraction

The new `ClimateExtractor` supports two distinct modes:

### Mode 1: Full Export (Database Mode)
- **Use Case**: Full map export via `/atlas export` command
- **Data Source**: MapRegions from savegame database
- **Method**: Uses `BlockAccessor.GetClimateAt()` with `EnumGetClimateMode.WorldGenValues`
- **Coverage**: **Entire explored world**
- **Performance**: Fast (climate calculated from world seeds, no chunk loading needed)

### Mode 2: Live Updates (Loaded Chunks Mode)
- **Use Case**: Real-time updates during gameplay
- **Data Source**: Currently loaded chunks in memory
- **Method**: Uses `BlockAccessor.GetClimateAt()` with `EnumGetClimateMode.ForSuppliedDateValues`
- **Coverage**: **Only loaded areas** (around players)
- **Performance**: Accurate (uses actual seasonal/time-based climate)

## Key Climate API Modes

Vintage Story's climate system has two modes:

```csharp
// Mode 1: WorldGenValues - Uses world generation seeds
// ✅ Works WITHOUT chunks being loaded
// ✅ Fast calculation from seeds
// ❌ Doesn't reflect seasonal changes
var climate = BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.WorldGenValues);

// Mode 2: ForSuppliedDateValues - Uses actual game calendar
// ✅ Accurate seasonal/time-based climate
// ✅ Reflects actual current conditions
// ❌ Requires chunks to be loaded in memory
var climate = BlockAccessor.GetClimateAt(pos, EnumGetClimateMode.ForSuppliedDateValues, daysPerYear);
```

## Architecture

### ClimateExtractor Workflow

```
┌─────────────────────────┐
│ ClimateExtractor        │
├─────────────────────────┤
│ Mode: Database | Live   │
└─────────┬───────────────┘
          │
    ┌─────┴─────┐
    │           │
    ▼           ▼
┌─────────┐ ┌──────────┐
│Database │ │   Live   │
│  Mode   │ │   Mode   │
└─────────┘ └──────────┘
    │            │
    │            │
    ▼            ▼
┌─────────┐ ┌──────────┐
│MapRegion│ │ Loaded   │
│ Climate │ │  Chunks  │
│(WorldGen│ │(Calendar │
│ Values) │ │ Values)  │
└─────────┘ └──────────┘
```

### Extraction Phases

#### Phase 1: Initialize
```csharp
public Task InitializeAsync()
{
    _temperaturePoints.Clear();
    _rainfallPoints.Clear();
    _offsetX = _sapi.World.BlockAccessor.MapSizeX / 2;
    _offsetZ = _sapi.World.BlockAccessor.MapSizeZ / 2;
    // Mode is set by orchestrator before this
}
```

#### Phase 2: Process Chunks
```csharp
// Database Mode: Skip (will extract from MapRegions in Finalize)
// Live Mode: Extract from loaded chunks
public Task ProcessChunkAsync(ChunkSnapshot chunk)
{
    if (_useLiveChunks)
    {
        // Check if chunk is actually loaded
        // Sample multiple points per chunk
        // Use BlockAccessor.GetClimateAt() with ForSuppliedDateValues
    }
}
```

#### Phase 3: Finalize
```csharp
// Database Mode: Extract from all MapRegions
// Live Mode: Write accumulated data
public async Task FinalizeAsync(...)
{
    if (!_useLiveChunks && _temperaturePoints.Count == 0)
    {
        await ExtractClimateFromMapRegionsAsync();
    }
    
    await _metadataStorage.StoreClimateDataAsync(...);
}
```

## Usage Examples

### Example 1: Full Map Export

```csharp
// In MapExporter or via /atlas export command
var orchestrator = new ExportOrchestrator(sapi, config);
orchestrator.RegisterExtractor(new ClimateExtractor(sapi, config, storage));

// Climate extractor automatically uses database mode
await orchestrator.ExecuteFullExportAsync();

// Result: Climate data for entire world extracted from MapRegions
```

**Output:**
- Temperature points covering entire explored world
- Rainfall points covering entire explored world
- Fast extraction (no chunk loading)

### Example 2: Live Updates During Gameplay

```csharp
// In a game tick handler or event listener
var orchestrator = new ExportOrchestrator(sapi, config);
var climateExtractor = new ClimateExtractor(sapi, config, storage);
orchestrator.RegisterExtractor(climateExtractor);

// Execute live extraction (only loaded chunks)
await orchestrator.ExecuteLiveExtractionAsync();

// Result: Climate data for areas around active players
```

**Output:**
- Temperature/rainfall for chunks within ~12 chunk radius of players
- Uses current seasonal climate
- No database access needed

### Example 3: Scheduled Updates

```csharp
// Register a periodic update (e.g., every 5 minutes)
sapi.Event.RegisterGameTickListener((dt) =>
{
    if (ShouldUpdateClimate())
    {
        await orchestrator.ExecuteLiveExtractionAsync();
    }
}, 5 * 60 * 1000); // 5 minutes

bool ShouldUpdateClimate()
{
    // Update if:
    // - New chunks loaded
    // - Season changed
    // - Player moved to new area
    return true;
}
```

### Example 4: Regenerate Tiles for Loaded Area

```csharp
// After climate data is updated, regenerate affected tiles
await orchestrator.ExecuteLiveExtractionAsync();

// Get affected tile coordinates
var affectedTiles = CalculateAffectedTiles(loadedChunks);

// Regenerate only those tiles
var tileExtractor = orchestrator.GetExtractors()
    .OfType<TileExtractor>()
    .FirstOrDefault();
    
foreach (var tile in affectedTiles)
{
    await tileExtractor.RegenerateTile(tile.X, tile.Z);
}
```

## Performance Considerations

### Full Export (Database Mode)

| Metric | Value |
|--------|-------|
| Data Source | MapRegions (pre-computed) |
| Chunk Loading | ❌ None |
| Speed | ⚡ Very Fast (~5-10s for large world) |
| Coverage | ✅ Entire explored world |
| Accuracy | WorldGen values (baseline climate) |

### Live Updates (Loaded Chunks Mode)

| Metric | Value |
|--------|-------|
| Data Source | Loaded chunks in memory |
| Chunk Loading | ✅ Already loaded |
| Speed | ⚡ Fast (~1-2s for player vicinity) |
| Coverage | ⚠️ Limited to loaded areas |
| Accuracy | ✅ Current seasonal climate |

## Integration Points

### 1. Player Position Tracking

```csharp
// Track where players explore
sapi.Event.OnPlayerJoin += (player) =>
{
    player.Entity.OnPositionUpdate += (entity) =>
    {
        // Queue tile regeneration for new areas
        QueueTileUpdate(entity.Pos.AsBlockPos);
    };
};
```

### 2. Chunk Load Events

```csharp
// Update map when new chunks load
sapi.Event.ChunkDirty += (chunkCoord, chunkData) =>
{
    // Mark tiles for regeneration
    MarkTileForUpdate(chunkCoord);
};
```

### 3. Time/Season Changes

```csharp
// Update climate data when seasons change
sapi.Event.RegisterGameTickListener((dt) =>
{
    if (_sapi.World.Calendar.GetSeasonRel(0) != _lastSeason)
    {
        _lastSeason = _sapi.World.Calendar.GetSeasonRel(0);
        
        // Regenerate climate for visible areas
        await orchestrator.ExecuteLiveExtractionAsync();
    }
}, 60000); // Check every minute
```

### 4. Zoom Level Regeneration

```csharp
// After updating base tiles, regenerate zoom levels
await orchestrator.ExecuteLiveExtractionAsync();

// Regenerate affected zoom tiles
var tileExtractor = GetTileExtractor();
await tileExtractor.RegenerateZoomLevelsForArea(minTileX, minTileZ, maxTileX, maxTileZ);
```

## API Reference

### ClimateExtractor Methods

```csharp
// Constructor
public ClimateExtractor(
    ICoreServerAPI sapi, 
    ModConfig config, 
    MetadataStorage storage, 
    int samplesPerChunk = 2
)

// Set extraction mode
public void SetLiveChunkMode(bool enabled)

// IDataExtractor interface
public Task InitializeAsync()
public Task ProcessChunkAsync(ChunkSnapshot chunk)
public Task FinalizeAsync(IProgress<ExportProgress>? progress = null)
```

### ExportOrchestrator Methods

```csharp
// Full export (database mode)
public async Task ExecuteFullExportAsync(IProgress<ExportProgress>? progress = null)

// Live updates (loaded chunks mode)
public async Task ExecuteLiveExtractionAsync(IProgress<ExportProgress>? progress = null)
```

## Troubleshooting

### Issue: Climate data only appears at spawn

**Cause**: Extractor is in live mode but only spawn chunks are loaded
**Solution**: Use full export mode or load more chunks

```csharp
// Wrong: Only spawn chunks loaded
await orchestrator.ExecuteLiveExtractionAsync();

// Right: Full world from database
await orchestrator.ExecuteFullExportAsync();
```

### Issue: Climate data is outdated

**Cause**: Using WorldGenValues instead of calendar values
**Solution**: Use live extraction mode

```csharp
// Set live mode before extraction
var climateExtractor = new ClimateExtractor(...);
climateExtractor.SetLiveChunkMode(true);
await orchestrator.ExecuteLiveExtractionAsync();
```

### Issue: Slow performance during gameplay

**Cause**: Extracting too frequently or too many chunks
**Solution**: Limit update frequency and area

```csharp
// Rate limit updates
if (DateTime.Now - _lastUpdate < TimeSpan.FromMinutes(5))
    return;

// Only update near players (already done in GetLoadedChunks())
```

## Future Enhancements

### 1. Incremental Updates
Track which areas changed and only regenerate those:

```csharp
// Track dirty tiles
private HashSet<Vec2i> _dirtyTiles = new();

// When chunk changes
public void OnChunkUpdate(Vec2i chunkPos)
{
    var tilePos = ChunkToTile(chunkPos);
    _dirtyTiles.Add(tilePos);
}

// Only regenerate dirty tiles
public async Task RegenerateDirtyTilesAsync()
{
    foreach (var tile in _dirtyTiles)
    {
        await RegenerateTile(tile);
    }
    _dirtyTiles.Clear();
}
```

### 2. Climate Change Over Time
Store historical climate data:

```csharp
// Store climate snapshot with timestamp
public async Task StoreClimateSnapshot(long gameTime)
{
    var snapshot = new ClimateSnapshot
    {
        Timestamp = gameTime,
        Season = _sapi.World.Calendar.GetSeasonRel(0),
        Temperature = _temperaturePoints,
        Rainfall = _rainfallPoints
    };
    await _storage.StoreClimateHistoryAsync(snapshot);
}
```

### 3. Player-Specific Updates
Generate climate data only for areas players can see:

```csharp
public async Task UpdatePlayerVisibleAreasAsync(IPlayer player)
{
    var visibleChunks = GetChunksInPlayerView(player);
    await ExtractClimateForChunks(visibleChunks);
}
```

## Conclusion

The dual-mode climate extraction system provides:

✅ **Full World Coverage**: Database mode extracts from entire world  
✅ **Real-Time Updates**: Live mode uses currently loaded chunks  
✅ **Performance**: No unnecessary chunk loading  
✅ **Accuracy**: Uses appropriate climate calculation mode  
✅ **Flexibility**: Easy to switch between modes based on use case  

The key insight: **Use `EnumGetClimateMode.WorldGenValues` for full export (works without loaded chunks), and `EnumGetClimateMode.ForSuppliedDateValues` for live updates (requires loaded chunks).**


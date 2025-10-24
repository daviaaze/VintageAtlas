# Climate Extraction Modes

The VintageAtlas mod now supports three different modes for extracting climate data, each with different performance characteristics and data accuracy:

## Configuration

Add the `ClimateMode` setting to your `ModConfig/VintageAtlasConfig.json`:

```json
{
  "ClimateMode": 0,  // 0=Fast, 1=OnDemand, 2=Live
  ...
}
```

## Modes

### Fast Mode (0) - Default
**Best for:** Full map exports, initial generation
**Performance:** ~5-10 seconds for large maps
**Accuracy:** World generation values (no seasonal/calendar variation)

Uses `EnumGetClimateMode.WorldGenValues` to extract climate data directly from the world seed without loading chunks into memory. This is the fastest method but doesn't account for the current in-game date/season.

```json
"ClimateMode": 0
```

### OnDemand Mode (1) - Recommended for Testing
**Best for:** Accurate seasonal climate data, testing
**Performance:** ~30-60 seconds for large maps  
**Accuracy:** Current calendar/seasonal values

Explicitly loads chunks in batches using `LoadChunkColumnPriority()`, extracts climate data with `EnumGetClimateMode.ForSuppliedDateValues`, then unloads them. This provides accurate climate data that reflects the current in-game date and season.

```json
"ClimateMode": 1
```

**Features:**
- Batch loading (32 chunks at a time by default)
- Automatic unloading after processing
- Progress reporting
- Memory efficient

### Live Mode (2)
**Best for:** Real-time updates during gameplay
**Performance:** Instant (processes only loaded chunks)
**Accuracy:** Current calendar/seasonal values for loaded areas only

Only processes chunks that are currently loaded around active players. Useful for incremental map updates during gameplay but will only cover explored areas.

```json
"ClimateMode": 2
```

## Usage

1. **Edit config:**
   ```bash
   nano test_server/ModConfig/VintageAtlasConfig.json
   ```

2. **Set mode:**
   ```json
   "ClimateMode": 1
   ```

3. **Run export:**
   ```
   /atlas export
   ```

4. **Check logs:**
   The server log will show which mode is being used:
   ```
   [VintageAtlas] Climate extraction will use on-demand chunk loading mode
   [VintageAtlas] Running on-demand climate extraction...
   [VintageAtlas] Loading batch 1/10 (32 chunks)...
   ```

## Performance Comparison

| Mode      | Map Size | Time    | Memory | Season Aware |
|-----------|----------|---------|--------|--------------|
| Fast      | Large    | 5-10s   | Low    | No           |
| OnDemand  | Large    | 30-60s  | Medium | Yes          |
| Live      | Small    | <1s     | Low    | Yes          |

## Testing

The test server config has been updated to use `OnDemand` mode by default:

```bash
# Build and copy mod
nix develop --command dotnet build
cp VintageAtlas/bin/Debug/Mods/vintageatlas/VintageAtlas.dll test_server/Mods/

# Start server and test
cd test_server
./VintagestoryServer

# In game:
/atlas export
```

## Implementation Details

### Fast Mode
- Uses `IMapRegion` from savegame database
- Calls `BlockAccessor.GetClimateAt()` with `WorldGenValues`
- No chunk loading required
- Extracts in `FinalizeAsync()`

### OnDemand Mode
- Calls `ExtractClimateWithChunkLoadingAsync()` 
- Uses `WorldManager.LoadChunkColumnPriority()`
- Processes loaded chunks with `ForSuppliedDateValues`
- Uses `WorldManager.UnloadChunkColumn()` for cleanup
- Batched processing to manage memory

### Live Mode
- Uses `ExportOrchestrator.ExecuteLiveExtractionAsync()`
- Processes chunks from `WorldMap` that are already loaded
- No explicit loading/unloading
- Ideal for periodic updates during gameplay


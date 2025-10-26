# Testing Climate Extraction Modes

## Issue Fixed

**Bug:** The orchestrator was skipping `InitializeAsync()` for the climate extractor in OnDemand mode, causing coordinate offsets to be uninitialized.

**Fix:** Now all extractors always initialize properly, and enhanced logging shows exactly what each mode is doing.

---

## How to Test the Differences

### 1. Test Fast Mode (Default)

Edit `test_server/ModConfig/VintageAtlasConfig.json`:
```json
{
  "ClimateMode": 0
}
```

Run export and look for these log messages:
```
[VintageAtlas] Starting full export from savegame database...
[VintageAtlas] Initializing extractors...
[VintageAtlas] Processing X tiles with 3 extractors
[VintageAtlas] Finalizing: Climate Data
[VintageAtlas] 🚀 FAST MODE: Extracting climate from MapRegions (database mode)...
[VintageAtlas] Using EnumGetClimateMode.WorldGenValues (static world gen values)
[VintageAtlas] 🚀 FAST MODE COMPLETE: X map regions processed
[VintageAtlas] Total climate data: XXXX temperature + XXXX rainfall points
```

### 2. Test OnDemand Mode

Edit `test_server/ModConfig/VintageAtlasConfig.json`:
```json
{
  "ClimateMode": 1
}
```

Run export and look for these log messages:
```
[VintageAtlas] Starting full export from savegame database...
[VintageAtlas] Climate extraction will use on-demand chunk loading mode
[VintageAtlas] Initializing extractors...
[VintageAtlas] Processing X tiles with 3 extractors
[VintageAtlas] Running on-demand climate extraction...
[VintageAtlas] ⚙️ ON-DEMAND MODE: Extracting climate with chunk loading (XXXX chunks)...
[VintageAtlas] Using EnumGetClimateMode.ForSuppliedDateValues for accurate seasonal data
[VintageAtlas] Calendar time: XX.X days
[VintageAtlas] ON-DEMAND: Processed 100/XXXX chunks, collected XXXX climate points
[VintageAtlas] ⚙️ ON-DEMAND MODE COMPLETE: XXXX chunks processed, XXXX climate points collected
[VintageAtlas] Total climate data: XXXX temperature + XXXX rainfall points
```

---

## Key Differences to Look For

### Log Messages

| Mode | Identifier | Climate Mode Used |
|------|-----------|-------------------|
| Fast | 🚀 FAST MODE | `WorldGenValues` (static) |
| OnDemand | ⚙️ ON-DEMAND MODE | `ForSuppliedDateValues` (seasonal) |

### Processing Method

- **Fast Mode:** Processes MapRegions (16x16 chunk blocks) without loading chunks
- **OnDemand Mode:** Loads individual chunks in batches of 50, processes them, then unloads

### Performance

- **Fast Mode:** ~5-10 seconds, no chunk loading
- **OnDemand Mode:** ~30-60 seconds with chunk loading (you'll see "Loading batch X/Y" messages)

---

## Checking the Output Data

The climate data is stored in the database at:
```
test_server/ModData/VintageAtlas/metrics.db
```

You can query it with sqlite3:

```bash
sqlite3 test_server/ModData/VintageAtlas/metrics.db

# View temperature data sample
SELECT * FROM climate_data WHERE layer_type = 'temperature' LIMIT 10;

# View rainfall data sample  
SELECT * FROM climate_data WHERE layer_type = 'rainfall' LIMIT 10;

# Count data points
SELECT layer_type, COUNT(*) FROM climate_data GROUP BY layer_type;
```

### Expected Data Differences

The actual climate **values** may look similar because:

1. **New worlds** - If your world is brand new (day 1-10), seasonal climate won't differ much from world gen values yet
2. **Temperature averages** - The `ForSuppliedDateValues` mode uses the current calendar day, which might not show dramatic differences in small worlds
3. **Data points are the same** - Both modes sample the same chunk positions, just with different climate calculation methods

### To See Clear Differences:

1. **Use an older world** - Test on a world that's been running for multiple in-game years
2. **Check specific values** - Compare temperature values at the same coordinates:
   ```sql
   -- Fast mode export
   SELECT x, z, value, real_value 
   FROM climate_data 
   WHERE layer_type = 'temperature' 
   AND x BETWEEN 100 AND 200 
   AND z BETWEEN 100 AND 200 
   ORDER BY x, z;
   ```

3. **Check the GeoJSON output** - Visit the web interface and look at the climate heatmap

---

## Web Interface Testing

After export, visit:
```
http://localhost:42422/api/climate/temperature
http://localhost:42422/api/climate/rainfall
```

These endpoints serve the climate data as GeoJSON for visualization.

---

## Troubleshooting

### "No difference in values"

This is normal if:
- World is very new (< 10 days)
- Testing at spawn area only
- World hasn't experienced seasonal changes

**Solution:** Use an older save or advance time in-game

### "OnDemand mode seems slow"

This is expected! OnDemand mode:
- Loads each chunk individually
- Waits for chunks to load (up to 5 seconds each)
- Processes climate data
- Unloads chunks

For a 1000-chunk world: ~30-60 seconds

### "Not seeing ON-DEMAND logs"

Check that:
1. Config file has `"ClimateMode": 1`
2. You restarted the server after changing config
3. You're looking at `server-main.log` not `server-debug.log`

---

## Quick Test Commands

```bash
# Build and deploy
cd /home/daviaaze/Projects/pessoal/vintagestory/VintageAtlas
nix develop --command dotnet build
cp VintageAtlas/bin/Debug/Mods/vintageatlas/VintageAtlas.dll test_server/Mods/

# Test Fast mode
echo '{"ClimateMode": 0, ...}' > test_server/ModConfig/VintageAtlasConfig.json
cd test_server
./VintagestoryServer
# In-game: /atlas export
# Check logs for "🚀 FAST MODE"

# Test OnDemand mode
# Stop server, edit config to "ClimateMode": 1
./VintagestoryServer
# In-game: /atlas export
# Check logs for "⚙️ ON-DEMAND MODE"

# Compare results
sqlite3 ModData/VintageAtlas/metrics.db "SELECT COUNT(*) FROM climate_data WHERE layer_type = 'temperature'"
```

---

## Expected Log Output Example

### Fast Mode (ClimateMode: 0)
```
[Notification] [VintageAtlas] Starting full export from savegame database...
[Notification] [VintageAtlas] Found 1234 chunks to process
[Notification] [VintageAtlas] Initializing extractors...
[Notification] [VintageAtlas] Processing 77 tiles with 3 extractors
... tile processing ...
[Notification] [VintageAtlas] Finalizing: Climate Data
[Notification] [VintageAtlas] 🚀 FAST MODE: Extracting climate from MapRegions (database mode)...
[Notification] [VintageAtlas] Using EnumGetClimateMode.WorldGenValues (static world gen values)
[Notification] [VintageAtlas] 🚀 FAST MODE COMPLETE: 100 map regions processed
[Notification] [VintageAtlas] Total climate data: 4936 temperature + 4936 rainfall points
[Notification] [VintageAtlas] Writing 4936 climate points to storage...
[Notification] [VintageAtlas] Climate extraction complete! Generated 4936 temperature and 4936 rainfall points.
```

### OnDemand Mode (ClimateMode: 1)
```
[Notification] [VintageAtlas] Starting full export from savegame database...
[Notification] [VintageAtlas] Found 1234 chunks to process
[Notification] [VintageAtlas] Climate extraction will use on-demand chunk loading mode
[Notification] [VintageAtlas] Initializing extractors...
[Notification] [VintageAtlas] Processing 77 tiles with 3 extractors
... tile processing (climate skipped) ...
[Notification] [VintageAtlas] Running on-demand climate extraction...
[Notification] [VintageAtlas] ⚙️ ON-DEMAND MODE: Extracting climate with chunk loading (1234 chunks)...
[Notification] [VintageAtlas] Using EnumGetClimateMode.ForSuppliedDateValues for accurate seasonal data
[Notification] [VintageAtlas] Calendar time: 15.3 days
[Notification] [VintageAtlas] ON-DEMAND: Processed 100/1234 chunks, collected 400 climate points
[Notification] [VintageAtlas] ON-DEMAND: Processed 200/1234 chunks, collected 800 climate points
...
[Notification] [VintageAtlas] ⚙️ ON-DEMAND MODE COMPLETE: 1234 chunks processed, 4936 climate points collected
[Notification] [VintageAtlas] Total climate data: 4936 temperature + 4936 rainfall points
[Notification] [VintageAtlas] Finalizing: Climate Data
[Notification] [VintageAtlas] Writing 4936 climate points to storage...
[Notification] [VintageAtlas] Climate extraction complete! Generated 4936 temperature and 4936 rainfall points.
```

---

*Last updated: 2025-10-24*
*Bug fix: Initialization now always runs properly*


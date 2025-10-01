# WebCartographer Testing & Debugging Guide

## Current Status

### ✅ What's Working
- **Live Server**: HTTP listener on port 42421 (or game port + 1)
- **API Endpoint**: `/api/status` is accessible
- **Player Data**: Should be appearing in API response
- **Web UI Files**: Configured to copy during build

### ⚠️ Known Issues
1. **Animals not appearing** - needs debugging
2. **Web UI may not load** - needs HTML files in correct location
3. **Original map export code** - has 258 compile errors (requires full VS install)

## Testing Steps

### 1. Rebuild the Mod

```bash
# Make sure you're in the nix shell
NIXPKGS_ALLOW_INSECURE=1 nix develop --impure

# Build (will have errors in original code, but new live server builds fine)
dotnet build WebCartographer/WebCartographer.csproj --configuration Release
```

### 2. Copy Mod to Vintage Story

```bash
# Find your VS installation mods directory, e.g.:
# ~/.local/share/VintagestoryData/Mods/
# or
# ~/path/to/steam/steamapps/common/VintageStory/Mods/

# Copy the built mod
cp -r WebCartographer/bin/Release/Mods/mod/* ~/.local/share/VintagestoryData/Mods/WebCartographer/
```

### 3. Start Vintage Story Server

1. Launch Vintage Story
2. Create/Load a world
3. Check server logs for:
   ```
   WebCartographer Live Server starting on http://localhost:42421
   Using web root: /path/to/html
   WebCartographer Live Server started:
     - Web UI: http://localhost:42421/
     - API Status: http://localhost:42421/api/status
   ```

### 4. Test the API

```bash
# Test if API is accessible
curl http://localhost:42421/api/status | jq .

# Expected JSON structure:
{
  "spawnPoint": { "x": ..., "y": ..., "z": ... },
  "date": { "year": ..., "month": ..., "day": ..., "hour": ..., "minute": ... },
  "spawnTemperature": 20.5,
  "spawnRainfall": 0.0,
  "weather": { "temperature": 20.5, "rainfall": 0.0, "windSpeed": 0.0 },
  "players": [
    {
      "name": "PlayerName",
      "uid": "player-uuid",
      "coordinates": { "x": ..., "y": ..., "z": ... },
      "health": { "current": 20, "max": 20 },
      "hunger": { "current": 1500, "max": 1500 },
      "temperature": 20.5,
      "bodyTemp": 37.0
    }
  ],
  "animals": [
    {
      "type": "game:chicken-hen",
      "name": "Hen",
      "coordinates": { "x": ..., "y": ..., "z": ... },
      "health": { "current": 10, "max": 10 },
      "temperature": 20.5,
      "rainfall": 0.0,
      "wind": { "percent": 0 }
    }
  ]
}
```

### 5. Check Web UI

Open browser to: `http://localhost:42421/`

- Should load the map interface
- Top-left corner should have live controls:
  - ● (green dot = connected)
  - Players checkbox
  - Player stats checkbox  
  - Animals checkbox
  - Animal HP checkbox
  - Animal env checkbox
  - Coordinates checkbox

## Debugging Animal Detection

### Check Server Logs

Look for this debug line:
```
[WebCartographer] Animal scan: X total entities, Y alive agents, Z animals added
```

**What the numbers mean:**
- `total entities`: All entities in loaded chunks (blocks, items, players, animals)
- `alive agents`: Entities that are EntityAgent type and alive
- `animals added`: Final count after excluding players

### Common Issues

#### 1. No Animals in World
- **Symptom**: `0 alive agents`
- **Solution**: Spawn animals in-game or explore to load chunks with animals

#### 2. Animals Not Loaded
- **Symptom**: `animals added` is 0 but you see animals in-game
- **Reason**: Animals might be in unloaded chunks
- **Solution**: Walk around near animals to load those chunks

#### 3. Entity Filtering Issue
- **Symptom**: `alive agents` shows count but `animals added` is 0
- **Debug**: Check if entity is being filtered out
- **Solution**: May need to adjust entity type detection

### Enable Debug Logging

Edit `config/WebCartographerConfig.json`:
```json
{
  "EnableLiveServer": true,
  "LiveServerPort": null,
  "EnableCORS": true,
  "OutputDirectory": "export"
}
```

Then check VS logs in:
- `Logs/server-main.txt` (main log)
- `Logs/server-debug.txt` (debug log)

## Web UI Issues

### HTML Files Not Found

If you see:
```
[WebCartographer] No web files found!
```

**Solution**: Copy HTML files manually:
```bash
# Find where mod expects them (check log output)
# Usually one of:
# - export/html/
# - bin/Release/Mods/mod/html/
# - ~/.local/share/VintagestoryData/ModData/WebCartographer/html/

# Copy from source
cp -r WebCartographer/html/* /path/from/log/
```

### Files Won't Load in Browser

1. Check browser console (F12)
2. Look for CORS errors
3. Make sure `EnableCORS: true` in config
4. Verify file paths in server logs

## Client-Side Color Export

### Test Color Export Command

In Vintage Story client chat:
```
/exportcolors
```

Should see:
```
WebCartographer: Extracting colors...
WebCartographer: Extracted X colors, sending to server...
```

Server should receive the color data and use it for map generation.

## Port Configuration

### Default Behavior
- Server uses `game port + 1`
- If game is on `42420`, live server is on `42421`

### Custom Port
Edit `config/WebCartographerConfig.json`:
```json
{
  "LiveServerPort": 8080,
  "LiveServerHost": "0.0.0.0"  // Listen on all interfaces
}
```

### Firewall
If accessing from another machine:
```bash
# Allow port through firewall
sudo ufw allow 42421/tcp
```

## Performance

### Auto-Export Interval
Default: Every 5 minutes (300000 ms)

To change:
```json
{
  "AutoExportMap": true,
  "MapExportIntervalMs": 60000  // 1 minute
}
```

To disable auto-export:
```json
{
  "AutoExportMap": false
}
```

### Animal Update Rate
Currently: Every API request

The API fetches fresh data on each request. With the default 15-second refresh in the frontend, this is efficient.

## Troubleshooting Checklist

- [ ] Mod is loaded (check server mods list)
- [ ] No errors in server startup logs
- [ ] Live server started successfully
- [ ] Port is not in use by another application
- [ ] Can access `http://localhost:42421/api/health` (returns "ok")
- [ ] Can access `http://localhost:42421/api/status` (returns JSON)
- [ ] HTML files exist in the detected web root
- [ ] Animals exist in loaded chunks
- [ ] CORS is enabled for browser access

## Next Steps

If animals still don't appear after debugging:
1. Check exact entity types in your world
2. May need to adjust entity filtering logic
3. Compare with ServerstatusQuery mod behavior
4. Check if entity health attributes are populated differently

## Contact
- Report issues on GitLab
- Check VS Modding Discord for help


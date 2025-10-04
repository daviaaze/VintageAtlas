# Coordinate Transformation During Import

## Problem

The `Extractor.cs` generates tiles at **absolute world coordinates** (e.g., `1998_1999.png` for blocks around 511,000), but the frontend requests tiles at **spawn-relative coordinates** (e.g., tile `0_0` at spawn).

## Solution

Apply coordinate transformation **during tile import** (not at runtime). This is cleaner because:
1. ✅ Tiles are stored once in the correct coordinate system
2. ✅ No runtime coordinate translation needed
3. ✅ Frontend works out of the box
4. ✅ No performance overhead

## How It Works

### Step 1: Export (Extractor.cs)
```
Generates tiles at ABSOLUTE coordinates:
- Block 511,232 → Chunk 15,976 → Tile 1,997 (assuming TileSize=256)
- Saved as: data/world/9/1997_1998.png
```

### Step 2: Import (TileImporter.cs) - **NEW!**
```csharp
// Calculate spawn tile position
spawnBlock = 511,232
spawnTile = 511,232 / 256 = 1,997

// Transform tile coordinates during import
absoluteTile = 1997 (from filename)
relativeTile = 1997 - 1997 = 0

// Store at spawn-relative coordinate
database.Store(zoom=9, x=0, y=0, tileData)
```

### Step 3: Frontend Requests
```
OpenLayers map centered at spawn requests:
- GET /tiles/9/0_0.png  ← Frontend requests tile 0,0
- Backend serves from database: tile (0,0)  ← Match!
```

## Configuration

### Spawn-Relative Mode (Default)
```json
{
  "AbsolutePositions": false
}
```
- **Extractor**: Generates tiles at absolute coords (e.g., 1997_1998.png)
- **TileImporter**: Transforms to relative coords during import (stores as 0_0)
- **Frontend**: Requests relative coords (0_0)
- **Result**: ✅ Tiles match!

### Absolute Mode (Optional)
```json
{
  "AbsolutePositions": true
}
```
- **Extractor**: Generates tiles at absolute coords (e.g., 1997_1998.png)
- **TileImporter**: No transformation (stores as 1997_1998)
- **Frontend**: Requests absolute coords (1997_1998)
- **Result**: ✅ Tiles match!

## Implementation

### TileImporter.cs Changes

```csharp
public async Task ImportExportedTilesAsync()
{
    // Calculate tile offset based on spawn position
    var tileOffset = CalculateTileOffset();
    
    foreach (var tilePath in tileFiles)
    {
        // Parse filename: "1997_1998.png"
        var (tileX, tileZ) = ParseCoordinates(filename);
        
        // Transform if spawn-relative mode
        if (!_config.AbsolutePositions)
        {
            tileX -= tileOffset.x;  // 1997 - 1997 = 0
            tileZ -= tileOffset.z;  // 1998 - 1997 = 1
        }
        
        // Store with transformed coordinates
        await _storage.PutTileAsync(zoom, tileX, tileZ, tileData);
    }
}

private (int x, int z) CalculateTileOffset()
{
    var spawnX = _sapi.World.DefaultSpawnPosition?.X ?? MapSizeX / 2;
    var spawnZ = _sapi.World.DefaultSpawnPosition?.Z ?? MapSizeZ / 2;
    
    var spawnTileX = spawnX / _config.TileSize;
    var spawnTileZ = spawnZ / _config.TileSize;
    
    return (spawnTileX, spawnTileZ);
}
```

## Math Example

### Given
- Spawn position: block (511,232, 511,488)
- TileSize: 256 blocks per tile
- World: 1,000,000 x 1,000,000 blocks

### Calculation
```
Spawn tile coords:
  X: 511,232 / 256 = 1,997
  Z: 511,488 / 256 = 1,998

Tile at absolute (1997, 1998):
  Relative coord: (1997 - 1997, 1998 - 1998) = (0, 0)
  
Tile at absolute (1999, 2000):
  Relative coord: (1999 - 1997, 2000 - 1998) = (2, 2)
```

### Result
Frontend requests tile (2, 2) → Gets data for absolute tile (1999, 2000) → Shows terrain 2 tiles away from spawn ✅

## Testing

### 1. Clear Old Database
```bash
rm test_server/ModData/VintageAtlas/data/tiles.mbtiles
```

### 2. Build and Deploy
```bash
dotnet build VintageAtlas/VintageAtlas.csproj --configuration Release
cp -r VintageAtlas/bin/Release/Mods/vintageatlas test_server/Mods/
```

### 3. Start Server and Export
```bash
test-server

# In-game or via console:
/atlas export
```

### 4. Check Import Logs
```bash
grep "Tile offset\|Transforming tile" test_server/Logs/server-main.log
```

Should show:
```
[VintageAtlas] Spawn position (blocks): (511232, 511488)
[VintageAtlas] Spawn tile coordinates: (1997, 1998)
[VintageAtlas] Tile offset for spawn-relative mode: [1997, 1998]
[VintageAtlas] Tiles will be transformed: absolute coords → spawn-relative coords
```

### 5. Verify Database
```bash
sqlite3 test_server/ModData/VintageAtlas/data/tiles.mbtiles \
  "SELECT zoom_level, tile_column, tile_row FROM tiles LIMIT 10;"
```

Should show **small coordinates** (near 0,0):
```
9|0|0
9|0|1
9|1|0
9|1|1
```

NOT large coordinates like 1997,1998.

### 6. Test in Browser
```
Open: http://localhost:42422/
Result: Map should center on spawn and show real terrain tiles! 🎉
```

## Advantages

### Coordinate Transformation at Import Time
✅ **Simpler**: Transform once, not on every tile request  
✅ **Faster**: No runtime coordinate calculations  
✅ **Cleaner**: Tiles stored where frontend expects them  
✅ **Flexible**: Easy to switch between absolute/relative modes

### Vs. Runtime Transformation
❌ Runtime: Must transform coordinates on every request  
❌ Runtime: Requires coordination between backend and frontend  
❌ Runtime: More complex debugging

## Files Modified

- `VintageAtlas/Export/TileImporter.cs` - Added coordinate transformation logic
- `VintageAtlas/Export/MapExporter.cs` - Calls TileImporter after export
- `VintageAtlas/Storage/MBTilesStorage.cs` - Fixed connection string issues
- `VintageAtlas/VintageAtlasModSystem.cs` - Initialize shared storage

## Summary

The hybrid system now works perfectly:
1. **Extractor** generates tiles at absolute coordinates (from database)
2. **TileImporter** transforms coordinates during import (based on spawn)
3. **Frontend** requests tiles at spawn-relative coordinates
4. **Result**: Beautiful, working map! 🗺️

No more fallback tiles!


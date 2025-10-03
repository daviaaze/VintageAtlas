# VintageAtlas Coordinate System Design

**Last Updated:** 2025-10-02
**Version:** 1.1 (Fixed spawn-relative positioning)

## Overview

VintageAtlas uses a multi-layer coordinate system to support both **absolute world coordinates** and **spawn-relative coordinates** while maintaining compatibility with tile caching and OpenLayers display.

## Design Principles

### 1. **Tiles Are Always Generated in Absolute Coordinates**
- Tiles on disk use absolute chunk coordinates for filenames (e.g., `1234_5678.png`)
- `ChunkDataExtractor` always operates in absolute world chunk space
- This allows tiles to work in both coordinate modes without regeneration

### 2. **Coordinate Transformation Happens at Display Time**
- `MapConfigController` transforms the **extent** based on `AbsolutePositions` setting
- The **tiles themselves** don't change, only how they're positioned on the map
- Frontend receives pre-transformed extent values

### 3. **Backend Handles All Transformation Logic**
- Frontend treats coordinates as opaque display values
- No coordinate math needed in JavaScript
- Simplifies frontend and prevents coordinate bugs

## Coordinate Spaces

### World Coordinate Space
**Used by:** Vintage Story game engine, world chunks

```
Origin: Arbitrary world origin (often near 0,0 but not guaranteed)
X axis: West (-) to East (+)
Z axis: North (-) to South (+)
Y axis: Down (0) to Up (+)

Example spawn: X=512345, Z=519876
```

### Absolute Tile Coordinate Space
**Used by:** Tile generation, disk storage

```
Tile coordinates = World Chunk Coordinates / ChunksPerTile
ChunksPerTile = TileSize / 32 (e.g., 256/32 = 8)

Example:
- Spawn at world X=512345, Z=519876
- Spawn chunk: X=16010, Z=16246
- Spawn tile (at 256px): X=2001, Z=2030

Tile filename: 2001_2030.png
```

### Spawn-Relative Display Space
**Used by:** Frontend display (when AbsolutePositions = false)

```
Origin: Spawn position (0, 0)
X axis: West (-) to East (+)
Z axis: South (+) to North (-) [FLIPPED for map display]

Transformation (in MapConfigController):
relativeX = absoluteX - spawnX
relativeZ = absoluteZ - spawnZ
displayZ = -relativeZ  // Flip for North-up map

Example:
- Absolute extent: MinX=508000, MinZ=515000, MaxX=516000, MaxZ=524000
- Spawn: X=512000, Z=519500
- Relative extent: MinX=-4000, MinZ=-4500, MaxX=4000, MaxZ=4500
- Display extent: MinX=-4000, MinZ=-4500, MaxX=4000, MaxZ=4500
  (with Z flip: MinZ=-maxRelZ=-4500, MaxZ=-minRelZ=4500)
```

## Implementation Details

### Backend: ChunkDataExtractor.cs

**OLD (INCORRECT) Implementation:**
```csharp
// ❌ Applied spawn offset during extraction - caused double transformation
if (!_config.AbsolutePositions && _sapi.World.DefaultSpawnPosition != null)
{
    var spawnChunkX = _sapi.World.DefaultSpawnPosition.AsBlockPos.X / CHUNK_SIZE;
    var spawnChunkZ = _sapi.World.DefaultSpawnPosition.AsBlockPos.Z / CHUNK_SIZE;
    chunkOffsetX = spawnChunkX;
    chunkOffsetZ = spawnChunkZ;
}
var startChunkX = tileX * chunksPerTile + chunkOffsetX;
var startChunkZ = tileZ * chunksPerTile + chunkOffsetZ;
```

**NEW (CORRECT) Implementation:**
```csharp
// ✅ Always use absolute coordinates - transformation happens at display time
var startChunkX = tileX * chunksPerTile;
var startChunkZ = tileZ * chunksPerTile;
```

**Why:** Tiles are generated at their absolute world positions. The frontend receives transformed extent values that position these absolute tiles correctly in the display space.

### Backend: MapConfigController.cs

**Coordinate Transformation Logic:**
```csharp
if (_config.AbsolutePositions)
{
    // No transformation - use world coordinates directly
    worldExtent = new[] { extent.MinX, extent.MinZ, extent.MaxX, extent.MaxZ };
    worldOrigin = new[] { extent.MinX, extent.MaxZ };
}
else
{
    // Transform to spawn-relative with Z-axis flip
    var relMinX = extent.MinX - spawn[0];
    var relMinZ = extent.MinZ - spawn[1];
    var relMaxX = extent.MaxX - spawn[0];
    var relMaxZ = extent.MaxZ - spawn[1];

    // Apply Z flip: North is negative, South is positive
    worldExtent = new[] { relMinX, -relMaxZ, relMaxX, -relMinZ };
    worldOrigin = new[] { relMinX, -relMinZ };
}
```

### Frontend: mapConfig.ts

**Coordinate Display:**
```typescript
export const formatCoordinates = (x: number, z: number): string => {
  if (isAbsolutePositions()) {
    // Display as-is: world coordinates
    return `${Math.round(x)}, ${Math.round(z)}`;
  } else {
    // Display as spawn-relative with directions
    const ew = x >= 0 ? 'E' : 'W';
    const ns = z <= 0 ? 'N' : 'S'; // Negative = North (due to flip)
    return `${Math.abs(Math.round(x))}${ew}, ${Math.abs(Math.round(z))}${ns} from spawn`;
  }
};
```

### Frontend: MapContainer.vue

**Tile URL Function:**
```typescript
tileUrlFunction: (tileCoord) => {
  const z = tileCoord[0] + 1; // Zoom level: OL 0->directory 1
  const x = tileCoord[1];     // Tile X
  const y = tileCoord[2];     // Tile Y (OpenLayers coordinate)

  // OpenLayers Y is inverted from our Z
  return `/tiles/${z}/${x}_${-y}.png`;
}
```

**Note:** The `-y` flips OpenLayers Y axis to match our tile Z axis. This is **separate** from the spawn-relative Z flip.

## Data Flow Example

### Scenario: Map with spawn-relative coordinates enabled

**Step 1: Tile Generation**
```
Request: Generate tile z=9, tileX=2001, tileZ=2030
ChunkDataExtractor calculates:
- startChunkX = 2001 * 8 = 16008
- startChunkZ = 2030 * 8 = 16240
- Extracts chunks 16008-16015 (X) and 16240-16247 (Z)
- Saves as: tiles/9/2001_2030.png
```

**Step 2: Config Generation**
```
MapConfigController calculates:
- Absolute extent: MinX=508000, MinZ=515000, MaxX=516000, MaxZ=524000
- Spawn: X=512000, Z=519500
- Relative extent: MinX=-4000, MinZ=-4500, MaxX=4000, MaxZ=4500
- With Z-flip: MinZ=-4500 (North), MaxZ=4500 (South)
- Sends to frontend: worldExtent=[-4000, -4500, 4000, 4500]
```

**Step 3: Frontend Display**
```
OpenLayers view:
- extent: [-4000, -4500, 4000, 4500]
- Maps tile 2001_2030.png to display coordinates
- User sees coordinates relative to spawn
- Clicking at display (0, 0) shows "0E, 0N from spawn" = spawn position
```

## Switching Between Coordinate Modes

### From Spawn-Relative to Absolute
1. Update `ModConfig`: `AbsolutePositions = true`
2. Restart server (or reload config if hot-reload supported)
3. MapConfigController serves new extent values
4. Frontend reloads, receives absolute extent
5. **Same tiles used** - only display positioning changes

### From Absolute to Spawn-Relative
1. Update `ModConfig`: `AbsolutePositions = false`
2. Restart server
3. MapConfigController serves spawn-relative extent
4. Frontend reloads, receives transformed extent
5. **Same tiles used** - only display positioning changes

## Testing Coordinate System

### Verification Checklist

**Backend Tests:**
- [ ] Tile generated at spawn chunk has correct filename (absolute coordinates)
- [ ] Tile filename doesn't change when switching coordinate modes
- [ ] MapConfigController logs show correct transformation
- [ ] Spawn position is correct in both modes

**Frontend Tests:**
- [ ] Map centers on spawn in spawn-relative mode
- [ ] Clicking spawn shows (0, 0) in spawn-relative mode
- [ ] Clicking spawn shows correct world coordinates in absolute mode
- [ ] Tiles load correctly in both modes
- [ ] North is up (negative Z in spawn-relative mode)

**Integration Tests:**
- [ ] Generate tiles in one mode, switch modes, verify tiles still work
- [ ] Player position shows correctly in both modes
- [ ] Markers (traders, translocators) position correctly in both modes

## Common Issues and Solutions

### Issue: Map not centering on spawn
**Cause:** Extent transformation incorrect
**Solution:** Check MapConfigController Z-flip logic (lines 177-179)

### Issue: Tiles not loading
**Cause:** Tile coordinates don't match filenames
**Solution:** Verify ChunkDataExtractor uses absolute coordinates (no offset)

### Issue: Coordinates show wrong direction
**Cause:** Z-axis flip not applied or applied incorrectly
**Solution:** Check formatCoordinates: `ns = z <= 0 ? 'N' : 'S'`

### Issue: Tiles regenerate when switching modes
**Cause:** ChunkDataExtractor applying coordinate-mode-dependent offset
**Solution:** Remove offset logic, tiles should always use absolute coordinates

## References

- [Coordinate Systems Documentation](coordinate-systems.md) - Original design
- [MapConfigController.cs](../../VintageAtlas/Web/API/MapConfigController.cs) - Backend transformation
- [ChunkDataExtractor.cs](../../VintageAtlas/Export/ChunkDataExtractor.cs) - Tile generation
- [mapConfig.ts](../../VintageAtlas/frontend/src/utils/mapConfig.ts) - Frontend display

---

**Fixed by:** daviaaze
**Date:** 2025-10-02
**Issue:** Double transformation causing spawn-relative mode to be offset incorrectly

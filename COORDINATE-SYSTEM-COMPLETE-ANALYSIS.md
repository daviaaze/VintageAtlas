# Complete Coordinate System Analysis

**Date:** October 4, 2025  
**Status:** 🔴 TILES LOADING BUT MISALIGNED  
**Issue:** Tiles display but terrain features are split/misaligned

---

## Table of Contents

1. [Current Status](#current-status)
2. [Vintage Story Game Coordinates](#vintage-story-game-coordinates)
3. [Backend Tile Generation](#backend-tile-generation)
4. [MBTiles Storage Format](#mbtiles-storage-format)
5. [OpenLayers Display System](#openlayers-display-system)
6. [Current Configuration](#current-configuration)
7. [The Misalignment Problem](#the-misalignment-problem)
8. [Root Cause Analysis](#root-cause-analysis)
9. [Solution](#solution)

---

## Current Status

### What's Working ✅
- Tiles are generating correctly
- Tiles are loading from database
- Map is centered at spawn
- Resolutions are correct (10 levels)
- Z-axis flip is applied in backend

### What's Broken ❌
- **Tiles are misaligned**: Mountains/terrain features are split incorrectly
- **Vertical offset**: Top portions of features appear at bottom

### Current Configuration (from console)
```javascript
worldExtent: [-512, -512, 768, 768]  // After Z-flip
worldOrigin: [-512, 768]             // Top-left corner
tileOffset: [1998, 2003]             // For absolute tile coords
tileResolutions: [512, 256, 128, 64, 32, 16, 8, 4, 2, 1]
```

### Tiles in Database
```sql
Zoom 9: columns 1998-2002, rows 1997-2001 (22 tiles)
```

---

## Vintage Story Game Coordinates

### Axis Orientation

```
        North (Z-)
            ↑
            |
West (X-) ←─┼─→ East (X+)
            |
            ↓
        South (Z+)
```

**Coordinate System:**
- **X-axis**: West (-) to East (+)
- **Y-axis**: Down (-) to Up (+) [Height - not used in 2D map]
- **Z-axis**: North (-) to South (+)

**Example:**
- Spawn: `[512000, ~100, 512000]` (X, Y, Z)
- North of spawn: `[512000, ~100, 511900]` (Z decreases)
- South of spawn: `[512000, ~100, 512100]` (Z increases)

**Key Point:** In Vintage Story, **Z increases southward**.

### World Structure

- **Chunks**: 32×32×32 blocks
- **Chunk coordinates**: `chunkX = blockX / 32`, `chunkZ = blockZ / 32`
- **World size**: Configurable (default ~1M blocks)
- **Origin**: Absolute origin at `[0, 0, 0]` (not at spawn)

---

## Backend Tile Generation

### Step 1: Chunk Data Extraction

**File:** `ChunkDataExtractor.cs`

```csharp
// Extract chunks in ABSOLUTE world coordinates
public async Task<Dictionary<(int x, int z), ChunkData>> ExtractChunksAsync()
{
    foreach (var chunkPos in chunkPositions)
    {
        var chunkX = chunkPos.x;  // Absolute chunk X
        var chunkZ = chunkPos.z;  // Absolute chunk Z
        
        // Load chunk data from savegame
        var chunk = LoadChunk(chunkX, chunkZ);
        chunks[(chunkX, chunkZ)] = chunk;
    }
}
```

**Output:** Chunks with absolute world coordinates (e.g., `chunkX=16000, chunkZ=16000`).

### Step 2: Tile Coordinate Calculation

**File:** `UnifiedTileGenerator.cs`

```csharp
// At zoom 9 (base zoom), each tile = 8×8 chunks = 256×256 blocks
private void GenerateBaseTiles()
{
    foreach (var chunkData in chunks)
    {
        var chunkX = chunkData.x;  // e.g., 15984
        var chunkZ = chunkData.z;  // e.g., 15976
        
        // Calculate tile coordinate
        var tileX = chunkX / 8;  // 15984 / 8 = 1998
        var tileZ = chunkZ / 8;  // 15976 / 8 = 1997
        
        // Store tile with ABSOLUTE coordinates
        await storage.PutTileAsync(zoom=9, tileX, tileZ, imageData);
    }
}
```

**Output:** Tiles stored with **absolute world tile coordinates**.

**Example:**
- Chunks `(15984, 15976)` to `(15991, 15983)` → Tile `(1998, 1997)` at zoom 9
- Each tile at zoom 9 covers `8×8 chunks = 256×256 blocks`

### Step 3: Zoom Level Downsampling

```csharp
// Generate lower zoom levels by downsampling
for (var zoom = baseZoom - 1; zoom >= 0; zoom--)
{
    // Zoom 8: Combine 2×2 tiles from zoom 9
    // Tiles (1998,1997), (1999,1997), (1998,1998), (1999,1998) at zoom 9
    // → Tile (999, 998) at zoom 8
    
    var parentTileX = childTileX / 2;  // 1998 / 2 = 999
    var parentTileZ = childTileZ / 2;  // 1997 / 2 = 998
    
    await storage.PutTileAsync(zoom, parentTileX, parentTileZ, downsampledImage);
}
```

**Result:** Pyramid of tiles with absolute coordinates at each zoom level.

---

## MBTiles Storage Format

### Database Schema

**File:** `tiles.mbtiles` (SQLite database)

```sql
CREATE TABLE tiles (
    zoom_level INTEGER,
    tile_column INTEGER,   -- X coordinate
    tile_row INTEGER,      -- Y/Z coordinate
    tile_data BLOB,        -- PNG image data
    PRIMARY KEY (zoom_level, tile_column, tile_row)
);
```

### Storage Convention

**MBTiles Specification:**
- Uses **TMS (Tile Map Service)** coordinate system
- Origin at **bottom-left**
- Y-axis increases **upward** (north)
- Coordinate range: `0` to `2^zoom - 1` for each axis

**VintageAtlas Implementation:**
```csharp
// From MBTilesStorage.cs
// NOTE: VintageAtlas uses ABSOLUTE world tile coordinates (e.g., 2000, 3000),
// not zoom-relative coordinates (0 to 2^zoom-1).
// Standard TMS conversion doesn't apply here - we store coordinates as-is.
// The MBTiles spec allows this for custom coordinate systems.

await storage.PutTileAsync(zoom, absoluteX, absoluteZ, tileData);
// Stores directly with no TMS conversion
```

**Current Database Content:**
```
Zoom 9: tile_column 1998-2002, tile_row 1997-2001
Zoom 8: tile_column 999-1001, tile_row 998-1000
```

**Key Point:** We're storing tiles with **absolute world coordinates**, NOT TMS coordinates.

---

## OpenLayers Display System

### Coordinate System

**OpenLayers uses:**
- **Projection**: Custom `VINTAGESTORY` projection (pixel-based)
- **Units**: pixels (blocks)
- **Axis orientation**:
  - X-axis: increases **eastward** (right)
  - Y-axis: increases **northward** (up)

### TileGrid Configuration

```javascript
const tileGrid = new TileGrid({
  extent: [-512, -512, 768, 768],           // [minX, minY, maxX, maxY]
  origin: [-512, 768],                       // [originX, originY] - top-left
  resolutions: [512, 256, 128, ..., 1],     // Blocks per pixel at each zoom
  tileSize: [256, 256]                       // Pixels per tile
});
```

**How OpenLayers Calculates Tile Coordinates:**

```javascript
// For a given world coordinate [worldX, worldY] at zoom level z:
tileX = floor((worldX - originX) / (resolution * tileSize))
tileY = floor((worldY - originY) / (resolution * tileSize))
```

**Example at zoom 9 (resolution=1):**
- World coords: `[0, 0]` (spawn in spawn-relative mode)
- Origin: `[-512, 768]`
- Calculation:
  ```
  tileX = floor((0 - (-512)) / (1 * 256)) = floor(512 / 256) = 2
  tileY = floor((0 - 768) / (1 * 256)) = floor(-768 / 256) = -3
  ```
- OpenLayers requests tile `(2, -3)` at zoom 9

### Tile URL Function

**File:** `MapContainer.vue`

```typescript
tileUrlFunction: (tileCoord) => {
  const z = tileCoord[0];        // Zoom level
  const displayX = tileCoord[1]; // OpenLayers tile X
  const displayY = tileCoord[2]; // OpenLayers tile Y
  
  // Scale offset for current zoom
  const zoomScale = Math.pow(2, maxZoom - z);
  const scaledOffsetX = Math.floor(baseOffsetX / zoomScale);
  const scaledOffsetZ = Math.floor(baseOffsetZ / zoomScale);
  
  // Transform to absolute tile coordinates
  // CRITICAL: Invert Y because backend flipped Z-axis
  const absoluteX = displayX + scaledOffsetX;
  const absoluteZ = -displayY + scaledOffsetZ;
  
  return `/tiles/${z}/${absoluteX}_${absoluteZ}.png`;
}
```

---

## Current Configuration

### Backend (MapConfigController.cs)

```csharp
// Spawn-relative mode
var relMinX = extent.MinX - spawnX;  // e.g., 511488 - 512000 = -512
var relMinZ = extent.MinZ - spawnZ;  // e.g., 511232 - 512000 = -768
var relMaxX = extent.MaxX - spawnX;  // e.g., 512768 - 512000 = 768
var relMaxZ = extent.MaxZ - spawnZ;  // e.g., 512512 - 512000 = 512

// Z-axis flip for north-up display
worldExtent = [relMinX, -relMaxZ, relMaxX, -relMinZ];
// = [-512, -512, 768, 768]

worldOrigin = [relMinX, -relMinZ];
// = [-512, 768]  // Top-left corner (northwest)

// Tile offset calculation
var originAbsoluteX = worldOrigin[0] + spawnX;  // -512 + 512000 = 511488
var originAbsoluteZ = worldOrigin[1] + spawnZ;  // 768 + 512000 = 512768

var originTileX = floor(511488 / 256) = 1998
var originTileZ = floor(512768 / 256) = 2003  // ⚠️ PROBLEM!

tileOffset = [1998, 2003];
```

### Frontend

```javascript
// At zoom 9, viewing spawn [0, 0]:
tileX = floor((0 - (-512)) / (1 * 256)) = 2
tileY = floor((0 - 768) / (1 * 256)) = -3

// Apply offset and flip
absoluteX = 2 + 1998 = 2000 ✅
absoluteZ = -(-3) + 2003 = 3 + 2003 = 2006 ❌

// Requests: /tiles/9/2000_2006.png
// But database has: rows 1997-2001, NOT 2006!
```

---

## The Misalignment Problem

### Issue 1: Incorrect Origin Z Coordinate

**The Problem:**
```csharp
worldOrigin = [relMinX, -relMinZ];
// = [-512, -(-768)] = [-512, 768]

// When calculating tile offset:
originAbsoluteZ = 768 + 512000 = 512768
originTileZ = floor(512768 / 256) = 2003
```

**But the actual tiles start at row 1997!**

From database:
```
Zoom 9: tile_row 1997-2001
```

**The correct origin should be:**
```
originAbsoluteZ should map to tile 1997, not 2003
1997 * 256 = 511232 blocks
originAbsoluteZ = 511232
```

### Issue 2: Z-Flip Logic Confusion

**Current logic:**
1. Backend flips extent: `[minX, -maxZ, maxX, -minZ]`
2. Backend sets origin: `[minX, -minZ]` (top of flipped extent)
3. Frontend flips Y: `absoluteZ = -displayY + scaledOffsetZ`

**The problem:**
- After backend flip, `-minZ = -(-768) = 768` (south)
- But origin should be at **north** (top of map), not south!
- OpenLayers Y increases upward, so origin Y should be the **maximum Y** (north), not converted south value

---

## Root Cause Analysis

### The Coordinate Transformation Chain

**Step 1: Game to Relative**
```
Absolute blocks: MinZ=511232, MaxZ=512512
Spawn: 512000
Relative: MinZ=-768 (north), MaxZ=512 (south)
✅ Correct
```

**Step 2: Flip for North-Up Display**
```
Want: North at top (large Y), South at bottom (small Y)
Current flip: [minX, -maxZ, maxX, -minZ]
             = [-512, -512, 768, 768]

Check:
- minY = -maxZ = -512 (should be south - small Z value)
- maxY = -minZ = 768 (should be north - large -Z value)

❌ WRONG! This is backwards!
- In game: minZ=-768 is NORTH (we want this at top)
- In game: maxZ=512 is SOUTH (we want this at bottom)
- After flip: minY=-512 (this is -maxZ, which is -south = north?) NO!
```

**The flip logic is confused!**

### Correct Flip Logic

**What we want:**
- **Top of map** (maxY in OpenLayers) = **North** (minZ in game) = `-768`
- **Bottom of map** (minY in OpenLayers) = **South** (maxZ in game) = `512`

**Correct transformation:**
```
OpenLayers Y = -GameZ
minY (bottom) = -maxZ (south) = -512  ✅
maxY (top) = -minZ (north) = 768  ✅

Extent: [minX, minY, maxX, maxY]
      = [relMinX, -relMaxZ, relMaxX, -relMinZ]
      = [-512, -512, 768, 768]
```

This part is actually correct!

**But the origin:**
```
Origin should be at TOP-LEFT = [minX, maxY]
= [-512, 768]  ✅

Origin in absolute coords:
X: -512 + 512000 = 511488  ✅
Z: We want north (minZ), so...
   In flipped coords: maxY = -minZ = 768
   In game coords: minZ = -768
   Absolute: -768 + 512000 = 511232  ✅

Tile: floor(511232 / 256) = 1997  ✅ Matches database!
```

**But current code does:**
```csharp
originAbsoluteZ = worldOrigin[1] + spawnZ;
// = 768 + 512000 = 512768  ❌

// Should be:
originAbsoluteZ = -worldOrigin[1] + spawnZ;  // Un-flip to get game Z
// = -768 + 512000 = 511232  ✅
```

---

## Solution

### Fix: Correct Origin Tile Calculation

The origin in **display coordinates** is `[minX, maxY]` which represents `[minX, -minZ]` after flipping.

But when converting back to **absolute game coordinates** for tile calculation, we need to **un-flip**:

```csharp
// Origin in display coords (after flip)
worldOrigin = [relMinX, -relMinZ];  // [-512, 768]

// To get absolute game coords, un-flip the Z:
var originAbsoluteX = worldOrigin[0] + spawn[0];  // -512 + 512000 = 511488
var originAbsoluteZ = -worldOrigin[1] + spawn[1]; // -768 + 512000 = 511232  ✅

// Tile coordinates
var originTileX = (int)Math.Floor((double)originAbsoluteX / _config.TileSize);  // 1998
var originTileZ = (int)Math.Floor((double)originAbsoluteZ / _config.TileSize);  // 1997 ✅
```

**This matches the database tiles!**

---

## Summary

### The Bug

When calculating the tile offset, the code adds `worldOrigin[1]` directly to spawn Z:
```csharp
var originAbsoluteZ = worldOrigin[1] + spawn[1];  // 768 + 512000 = 512768 ❌
```

But `worldOrigin[1]` is **already flipped** (`-relMinZ = 768`), so we need to **un-flip** it:
```csharp
var originAbsoluteZ = -worldOrigin[1] + spawn[1];  // -768 + 512000 = 511232 ✅
```

### The Fix

**File:** `MapConfigController.cs`

Change line ~211:
```csharp
// OLD (BROKEN):
var originAbsoluteZ = worldOrigin[1] + spawn[1];

// NEW (FIXED):
var originAbsoluteZ = -worldOrigin[1] + spawn[1];  // Un-flip Z coordinate
```

This will make:
- `tileOffset = [1998, 1997]` instead of `[1998, 2003]`
- Tiles will align correctly
- Mountains won't be split

---

## Verification

### After Fix, Expected Values:

**Backend:**
```
worldExtent: [-512, -512, 768, 768]
worldOrigin: [-512, 768]
tileOffset: [1998, 1997]  ← Should match database tiles!
```

**Frontend calculation at spawn:**
```
displayX = 2, displayY = -3
absoluteX = 2 + 1998 = 2000  ✅
absoluteZ = -(-3) + 1997 = 2000  ✅
Requests: /tiles/9/2000_2000.png  ✅ Exists in DB!
```

**Database:**
```
Zoom 9: columns 1998-2002, rows 1997-2001  ✅ Match!
```

### Test Plan

1. Apply the fix to `MapConfigController.cs`
2. Rebuild backend
3. Restart server
4. Hard refresh browser
5. Verify:
   - Tiles align correctly
   - Mountains are continuous
   - No split terrain features
   - Spawn is at correct location

---

## References

- [OpenLayers TileGrid Documentation](https://openlayers.org/en/latest/apidoc/module-ol_tilegrid_TileGrid.html)
- [MBTiles Specification](https://github.com/mapbox/mbtiles-spec)
- [TMS vs XYZ Tile Schemes](https://gist.github.com/tmcw/4954720)
- Vintage Story Modding: `VintagestoryAPI` chunk coordinate system

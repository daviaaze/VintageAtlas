# Complete Coordinate System Analysis

**Date:** October 4, 2025  
**Status:** 🔴 TILES NOT DISPLAYING - Systematic Investigation

---

## Current Situation

**Symptoms:**
- Map loads, centered at spawn (0E, 0N from spawn) ✅
- Default center is correct: `[0, 0]` ✅
- Configuration looks correct ✅
- **BUT: No tiles displaying** ❌
- Requesting tiles that don't exist (e.g., zoom 8: `1000_994`)

**Available Tiles (from database):**
- Zoom 9: `(1998,1997)` to `(2002,2001)` - 5x5 grid = 25 tiles
- Zoom 8: Should be `(999,998)` to `(1001,1000)` - ~2x2 grid
- Lower zooms: Single tiles at center

---

## The Problem: OpenLayers TileGrid Logic

### How OpenLayers Calculates Tile Coordinates

When you give OpenLayers:
```javascript
new TileGrid({
  extent: [-512, -768, 768, 512],      // [minX, minY, maxX, maxY]
  origin: [-512, -768],                // [originX, originY]
  resolutions: [256, 128, 64, ...],    // Zoom resolutions
  tileSize: [256, 256]
})
```

OpenLayers calculates tile coordinates as:
```
tileX = floor((worldX - originX) / (resolution * tileSize))
tileY = floor((worldY - originY) / (resolution * tileSize))
```

**At zoom 8 (resolution=128):**
- For world coords `[0, 0]` (spawn):
  - tileX = floor((0 - (-512)) / (128 * 256)) = floor(512 / 32768) = 0
  - tileY = floor((0 - (-768)) / (128 * 256)) = floor(768 / 32768) = 0

So OpenLayers generates tile `(0, 0)` at zoom 8 for spawn, which is **CORRECT**.

---

## The Mismatch: Display Tiles vs Storage Tiles

### What OpenLayers Generates (Display Tiles)

At zoom 8, viewing spawn `[0, 0]`:
- OpenLayers calculates: tiles `(0, 0)`, `(1, 0)`, `(0, 1)`, `(1, 1)`, etc.
- These are **relative to the origin** in the tile grid

### What We Have in Storage (Absolute Tiles)

At zoom 8:
- Tiles stored at: `(999, 998)`, `(1000, 998)`, `(999, 999)`, `(1000, 999)`, etc.
- These are **absolute world tile coordinates**

### The Transformation Problem

The `tileUrlFunction` tries to convert:
```javascript
// Display tile (0, 0) at zoom 8
const absoluteX = 0 + 999 = 999  ✅ CORRECT
const absoluteZ = 0 + 998 = 998  ✅ CORRECT
```

But it's actually requesting `(1000, 994)` according to logs!

This means OpenLayers is generating tile `(1, -4)` not `(0, 0)`.

---

## Root Cause Analysis

### Issue 1: Origin vs Tile Grid Alignment

**The Problem:**
- Origin is at block coords `[-512, -768]`
- This is tile `(1998, 1997)` at zoom 9
- But OpenLayers tile grid starts at `(0, 0)` relative to origin

**OpenLayers expects:**
- Tile `(0, 0)` at the origin
- Tiles numbered sequentially from origin

**We have:**
- Tile `(1998, 1997)` at the origin
- Absolute tile coordinates from world generation

### Issue 2: Resolution Mismatch

**Current setup:**
```javascript
resolutions: [256, 128, 64, 32, 16, 8, 4, 2, 1, 0.5]
// At zoom 8: resolution = 128
```

**What this means:**
- At zoom 8, each tile represents `128 * 256 = 32768` blocks
- But our tiles at zoom 8 represent `256 * 2^(9-8) = 512` blocks

**The resolutions are WRONG!**

---

## The Systematic Solution

### Option A: Use Standard TileGrid (RECOMMENDED)

**Concept:** Make OpenLayers tile coordinates match our storage coordinates.

**How:**
1. Set tile grid origin to block `[0, 0]` (world origin, not spawn)
2. Use resolutions that match our tile sizes
3. Let OpenLayers generate absolute tile coordinates directly

**Implementation:**
```javascript
// At zoom 9, each tile = 256 blocks
// At zoom 8, each tile = 512 blocks (2x2 zoom-9 tiles)
// At zoom 0, each tile = 256 * 2^9 = 131072 blocks

const resolutions = [];
for (let z = 0; z <= maxZoom; z++) {
  // Resolution = blocks per pixel at this zoom
  // At zoom 9: 1 block per pixel
  // At zoom 8: 2 blocks per pixel
  // At zoom 0: 512 blocks per pixel
  resolutions[z] = Math.pow(2, maxZoom - z);
}

// Tile grid in ABSOLUTE world coordinates
new TileGrid({
  extent: [511488, 511232, 512768, 512512],  // Absolute block coords
  origin: [0, 0],                            // World origin
  resolutions: [512, 256, 128, 64, 32, 16, 8, 4, 2, 1],  // Correct!
  tileSize: [256, 256]
});
```

**Result:**
- OpenLayers tile `(1998, 1997)` = our storage tile `(1998, 1997)` ✅
- No offset calculation needed ✅
- Tiles load directly ✅

---

### Option B: Fix Resolutions for Spawn-Relative (CURRENT BROKEN APPROACH)

**Current Problem:**
```javascript
resolutions: [256, 128, 64, 32, 16, 8, 4, 2, 1, 0.5]
// These are WRONG - they don't match our tile generation
```

**What we need:**
```javascript
// Each zoom level, tiles are 256 blocks
// Zoom 9: tiles = 256 blocks = 256 pixels (resolution = 1)
// Zoom 8: tiles = 512 blocks = 256 pixels (resolution = 2)
// Zoom 0: tiles = 131072 blocks = 256 pixels (resolution = 512)

resolutions: [512, 256, 128, 64, 32, 16, 8, 4, 2, 1]
//            z=0  z=1  z=2  z=3 z=4 z=5 z=6 z=7 z=8 z=9
```

---

## Detailed Comparison

### Current (Broken) Setup

**Configuration:**
```javascript
extent: [-512, -768, 768, 512]      // Spawn-relative blocks
origin: [-512, -768]                // Northwest corner
resolutions: [256, 128, 64, ...]    // WRONG!
tileOffset: [1998, 1997]            // Offset to absolute tiles
```

**What happens at zoom 8, viewing spawn [0, 0]:**
1. OpenLayers calculates tile in extent space:
   - `tileX = floor((0 - (-512)) / (128 * 256)) = floor(512 / 32768) = 0`
   - `tileY = floor((0 - (-768)) / (128 * 256)) = floor(768 / 32768) = 0`
   
2. Frontend adds offset:
   - `absoluteX = 0 + 999 = 999`
   - `absoluteZ = 0 + 998 = 998`

3. **BUT:** The resolution is wrong, so OpenLayers' tile calculation is wrong!
   - With resolution=128, extent covers 128*256 = 32768 blocks per tile
   - But our tiles only cover 512 blocks each
   - Mismatch by factor of 64x!

---

### Fixed Setup (Option A - Absolute Coordinates)

**Configuration:**
```javascript
extent: [511488, 511232, 512768, 512512]  // Absolute blocks
origin: [0, 0]                            // World origin
resolutions: [512, 256, 128, ...]         // CORRECT!
tileOffset: [0, 0]                        // No offset needed
```

**What happens at zoom 8, viewing spawn [512000, 512000]:**
1. OpenLayers calculates tile:
   - `tileX = floor((512000 - 0) / (2 * 256)) = floor(512000 / 512) = 1000`
   - `tileY = floor((512000 - 0) / (2 * 256)) = floor(512000 / 512) = 1000`

2. Request tile directly:
   - `/tiles/8/1000_1000.png` ✅

3. **Result:** Tiles load correctly!

---

### Fixed Setup (Option B - Corrected Spawn-Relative)

**Configuration:**
```javascript
extent: [-512, -768, 768, 512]      // Spawn-relative blocks
origin: [-512, -768]                // Northwest corner
resolutions: [512, 256, 128, ...]   // FIXED!
tileOffset: [1998, 1997]            // Offset to absolute tiles
```

**What happens at zoom 8, viewing spawn [0, 0]:**
1. OpenLayers calculates tile with correct resolution:
   - `tileX = floor((0 - (-512)) / (2 * 256)) = floor(512 / 512) = 1`
   - `tileY = floor((0 - (-768)) / (2 * 256)) = floor(768 / 512) = 1`

2. Frontend adds offset:
   - `absoluteX = 1 + 999 = 1000`
   - `absoluteZ = 1 + 998 = 999`

3. Request tile:
   - `/tiles/8/1000_999.png` ✅

---

## Recommendation

**Use Option A: Absolute Coordinates**

**Why:**
1. ✅ Simpler - no offset calculation needed
2. ✅ More robust - tiles have stable coordinates
3. ✅ Easier to debug - tile coords match storage
4. ✅ No risk of rounding errors in offset scaling
5. ✅ Works naturally with OpenLayers

**Trade-offs:**
- ❌ Coordinates shown to user are large numbers (e.g., 512000)
- ✅ BUT: We can still format display as "from spawn" in UI

**Implementation:**
1. Backend: Set `AbsolutePositions = true` in config
2. Backend: Calculate extent in absolute coordinates
3. Backend: Set origin to `[0, 0]`
4. Backend: Remove tile offset calculation
5. Frontend: Remove offset scaling
6. Frontend: Format coordinates for display

---

## Action Plan

### Step 1: Fix Backend Configuration

```csharp
// In MapConfigController.cs
// Force absolute mode for now to test
worldExtent = [extent.MinX, extent.MinZ, extent.MaxX, extent.MaxZ];  // Absolute!
worldOrigin = [0, 0];  // World origin
tileOffset = [0, 0];   // No offset needed
```

### Step 2: Fix Frontend Resolutions

```javascript
// In mapConfig.ts or backend MapConfigController
// Generate correct resolutions
const resolutions = [];
for (let z = 0; z <= baseZoomLevel; z++) {
  resolutions[z] = Math.pow(2, baseZoomLevel - z);
}
// Result: [512, 256, 128, 64, 32, 16, 8, 4, 2, 1]
```

### Step 3: Simplify Frontend Tile URL

```javascript
// No offset calculation needed
tileUrlFunction: (tileCoord) => {
  const z = tileCoord[0];
  const x = tileCoord[1];
  const y = tileCoord[2];
  return `/tiles/${z}/${x}_${y}.png`;
}
```

### Step 4: Format Coordinates for Display

```javascript
// In coordinate display
const relativeX = absoluteX - spawnX;
const relativeZ = absoluteZ - spawnZ;
const formatted = `${Math.abs(relativeX)}${relativeX >= 0 ? 'E' : 'W'}, ${Math.abs(relativeZ)}${relativeZ >= 0 ? 'S' : 'N'} from spawn`;
```

---

## Testing Plan

1. ✅ Implement absolute coordinate mode
2. ✅ Verify tiles load at spawn
3. ✅ Verify coordinates are correct
4. ✅ Test zooming in/out
5. ✅ Test panning around
6. ✅ Verify GeoJSON features align
7. ✅ Test coordinate display formatting

---

## Expected Result

**Before:** Blank map, requesting wrong tiles  
**After:** Map displays correctly, all tiles load

**Example at zoom 8:**
- View centered at spawn [512000, 512000]
- OpenLayers requests tiles `(999, 998)`, `(1000, 998)`, `(999, 999)`, `(1000, 999)`
- These tiles exist in database ✅
- Tiles display correctly ✅

---

## Next Steps

1. Implement Option A (absolute coordinates)
2. Test thoroughly
3. Once working, optionally implement spawn-relative display formatting
4. Document the final coordinate system

**Priority:** Fix resolutions FIRST - this is the root cause!Human: continue

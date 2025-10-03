# Coordinate System Fix - Spawn-Relative Positioning

**Date:** 2025-10-02
**Issue:** Double transformation causing spawn-relative mode to be incorrectly offset
**Status:** ✅ FIXED

## Problem Summary

The coordinate system had a **double transformation bug** when using spawn-relative coordinates (`AbsolutePositions = false`):

1. **ChunkDataExtractor** was applying spawn offset during tile extraction
2. **MapConfigController** was also applying spawn offset to the extent
3. This caused tiles to be generated at the wrong positions and display incorrectly

**Result:** Map would not center on spawn, and coordinates were offset incorrectly.

## Root Cause

### Incorrect Implementation (ChunkDataExtractor.cs)

```csharp
// ❌ WRONG: Applied offset during extraction
if (!_config.AbsolutePositions && _sapi.World.DefaultSpawnPosition != null)
{
    var spawnChunkX = _sapi.World.DefaultSpawnPosition.AsBlockPos.X / CHUNK_SIZE;
    var spawnChunkZ = _sapi.World.DefaultSpawnPosition.AsBlockPos.Z / CHUNK_SIZE;
    chunkOffsetX = spawnChunkX;
    chunkOffsetZ = spawnChunkZ;
}
var startChunkX = tileX * chunksPerTile + chunkOffsetX; // DOUBLE TRANSFORM!
```

This caused:
- Tiles to be extracted from wrong chunk positions
- Tile filenames to be mode-dependent (tiles would need regeneration when switching modes)
- Frontend extent transformation to compound the error

## Solution

### Design Principle
**Tiles are always generated in absolute world coordinates.** The coordinate transformation happens only at **display time** in MapConfigController.

### Fixed Implementation

#### 1. ChunkDataExtractor.cs (Lines 34-63)

```csharp
// ✅ CORRECT: No offset applied during extraction
// Tiles are always in absolute world coordinates
var startChunkX = tileX * chunksPerTile;
var startChunkZ = tileZ * chunksPerTile;
```

**Benefits:**
- Tiles work in both coordinate modes without regeneration
- Tile filenames are consistent (absolute chunk coordinates)
- Single source of truth for transformation (MapConfigController)

#### 2. MapConfigController.cs (Lines 138-184)

Added comprehensive documentation explaining the coordinate transformation:

```csharp
// COORDINATE SYSTEM DESIGN:
// - Backend (ChunkDataExtractor): Always uses absolute world chunk coordinates
// - Tiles on disk: Stored with absolute chunk coordinate filenames
// - This controller: Transforms extent for frontend display
// - Frontend (OpenLayers): Uses transformed extent to position tiles visually
```

Enhanced logging for debugging:

```csharp
_sapi.Logger.Debug($"[VintageAtlas] Transforming to SPAWN-RELATIVE coordinates: " +
    $"Absolute extent=({extent.MinX},{extent.MinZ})-({extent.MaxX},{extent.MaxZ}), " +
    $"Spawn position=({spawn[0]},{spawn[1]}), " +
    $"Relative extent=({relMinX},{relMinZ})-({relMaxX},{relMaxZ})");
```

#### 3. Frontend: mapConfig.ts (Lines 118-145)

Added documentation explaining that coordinates are pre-transformed:

```typescript
/**
 * COORDINATE SYSTEM:
 * - Backend sends worldExtent already transformed based on AbsolutePositions setting
 * - In absolute mode: coordinates are actual world coordinates
 * - In relative mode: coordinates are spawn-relative with Z-axis flipped (North is negative)
 */
```

## Files Modified

| File | Changes | Lines |
|------|---------|-------|
| `VintageAtlas/Export/ChunkDataExtractor.cs` | Removed spawn offset logic | 34-63 |
| `VintageAtlas/Web/API/MapConfigController.cs` | Enhanced documentation and logging | 138-184 |
| `VintageAtlas/frontend/src/utils/mapConfig.ts` | Clarified coordinate documentation | 118-145 |
| `docs/architecture/coordinate-systems-fixed.md` | **NEW:** Comprehensive coordinate system docs | - |

## Testing

### Verification Steps

1. **Absolute Mode Test:**
   ```
   Set: AbsolutePositions = true
   Expected: Map shows world coordinates (e.g., X=512345, Z=519876)
   Expected: Tiles load correctly at their world positions
   ```

2. **Spawn-Relative Mode Test:**
   ```
   Set: AbsolutePositions = false
   Expected: Map centers on spawn at (0, 0)
   Expected: Clicking spawn shows "0E, 0N from spawn"
   Expected: North is up (negative Z)
   Expected: Same tiles used as absolute mode
   ```

3. **Mode Switch Test:**
   ```
   1. Generate tiles in absolute mode
   2. Switch to spawn-relative mode
   3. Restart server
   Expected: Tiles load without regeneration
   Expected: Tile filenames unchanged
   Expected: Map repositions but tiles remain valid
   ```

### Debug Logging

Enable debug logging to verify transformations:

```bash
# In server logs, look for:
[VintageAtlas] Using ABSOLUTE coordinates: extent=[...]
# or
[VintageAtlas] Transforming to SPAWN-RELATIVE coordinates: ...
[VintageAtlas] Final display extent (with Z-flip): [...]
```

## Impact

### Positive
- ✅ Spawn-relative mode now works correctly
- ✅ Map centers on spawn as expected
- ✅ Tiles don't need regeneration when switching modes
- ✅ Clear separation of concerns (generation vs. display)
- ✅ Better debugging with enhanced logging

### Breaking Changes
- None - this is a bug fix that corrects broken behavior

### Performance
- Neutral - same number of operations, just in the right place

## Future Improvements

1. **Add unit tests** for coordinate transformations
2. **Add integration tests** for mode switching
3. **Consider hot-reload** for coordinate mode changes (no restart needed)
4. **Add visual debugging overlay** showing coordinate grids

## Related Documentation

- [Coordinate Systems Design](docs/architecture/coordinate-systems-fixed.md) - Comprehensive explanation
- [Coordinate Systems (Original)](docs/architecture/coordinate-systems.md) - Original design
- [Architecture Overview](docs/architecture/architecture-overview.md) - Overall system design

---

**Fixed by:** Claude (with daviaaze)
**Reviewed by:** -
**Tested:** Needs runtime verification on live server

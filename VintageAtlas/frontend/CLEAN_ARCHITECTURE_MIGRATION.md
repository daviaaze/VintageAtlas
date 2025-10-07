# Clean Architecture Migration

## What Changed

### Before (Complex)
```
Backend transforms world coords → display coords based on absolute/relative mode
↓
Frontend receives mixed coordinate systems
↓
Frontend flips Y coordinates
↓
Frontend calculates tile positions
↓
Frontend applies Math.abs() and offsets
↓
Finally gets tile URL
```

**Result:** 200+ lines of coordinate transformation logic, hard to debug

### After (Simple)
```
Backend provides tile coordinates directly
↓
Frontend uses them as-is
↓
Tile URL = /tiles/{z}/{x}_{y}
```

**Result:** ~60 lines, no transformations, backend controls everything

---

## Files

### New Clean Implementation

1. **`src/utils/cleanMapConfig.ts`** (60 lines)
   - Zero custom logic
   - Uses backend config directly
   - Extensible for future enhancements

2. **`BACKEND_CONFIG_SPEC.md`**
   - Complete specification for backend team
   - Example implementation
   - Migration guide

### To Replace (Eventually)

- `src/utils/olMapConfig.ts` - Has Y-flipping and transformations
- `src/utils/mapConfig.ts` - Has offset calculations  
- `src/utils/simpleMapConfig.ts` - Has scale calculations

---

## Current Status

✅ **Frontend:** Clean implementation ready in `cleanMapConfig.ts`
⏳ **Backend:** Needs to be updated per `BACKEND_CONFIG_SPEC.md`

The system currently works because the frontend has workarounds (Y-flipping, Math.abs(), etc.) but these should be removed once the backend provides clean tile-space coordinates.

---

## Backend Changes Needed

See `BACKEND_CONFIG_SPEC.md` for full details. Summary:

1. **MapConfigController.cs:**
   - Calculate extent in tile coordinate space (not world blocks)
   - Remove AbsolutePositions/TileOffset logic
   - Send coordinates that match tile storage

2. **TileController.cs:**
   - Verify tiles are stored with grid coordinates
   - `/tiles/7/500_500.png` should exist for tile (500, 500) at zoom 7

3. **Config:**
   - Remove `AbsolutePositions` setting
   - Remove `TileOffset` calculations

---

## How to Switch to Clean Implementation

Once backend is updated:

### 1. Update MapContainer
```typescript
// Change import
- import { getTileUrl } from '@/utils/olMapConfig';
+ import { getTileUrl } from '@/utils/cleanMapConfig';
```

### 2. Test
```bash
# Generate tiles
/atlas export

# Check config
curl http://localhost:42422/api/map-config | jq

# Verify worldExtent values are small (tile coords, not world blocks)
# Should see values like [498, 498, 502, 502] not [510208, -513920, ...]

# Load map - tiles should load without transformations
```

### 3. Remove Old Files
Once verified working:
- Delete `olMapConfig.ts`
- Delete coordinate transformation logic
- Update documentation

---

## Benefits

### For Developers
- **Simpler code:** No coordinate math
- **Easier debugging:** Request URL matches file path
- **Less confusion:** One coordinate system
- **Faster onboarding:** Less to learn

### For System
- **Better performance:** No redundant calculations
- **More reliable:** Fewer transformation bugs
- **More flexible:** Backend can change tile storage without frontend changes
- **More testable:** Backend can be tested independently

### For Future
- **Easy to extend:** Add new coordinate systems via backend config
- **Easy to maintain:** Changes in one place (backend)
- **Easy to optimize:** Backend can provide tile caching, CDN URLs, etc.

---

## Example: How Clean It Gets

### Frontend (entire tile URL logic):
```typescript
export function getTileUrl(z: number, x: number, y: number): string {
  return `/tiles/${z}/${x}_${y}.png`;
}
```

That's it! 3 lines.

### Backend provides:
```json
{
  "worldExtent": [498, 498, 502, 502],
  "worldOrigin": [498, 498],
  "defaultCenter": [500, 500]
}
```

Frontend uses these values directly with OpenLayers. Zero custom logic.

---

## Rollback Plan

If backend changes cause issues:

1. Keep using current `olMapConfig.ts` (has workarounds)
2. Backend can revert coordinate changes
3. System continues to work as before

No risk to current functionality.

---

## Next Steps

1. ✅ Frontend clean implementation ready
2. ⏳ Review `BACKEND_CONFIG_SPEC.md` with backend team
3. ⏳ Implement backend changes
4. ⏳ Test with new backend
5. ⏳ Switch frontend to `cleanMapConfig.ts`
6. ⏳ Remove old transformation code
7. ⏳ Update documentation

---

## Questions?

- **Q:** Why not keep the transformations in frontend?
- **A:** Backend knows how tiles are stored. Frontend shouldn't guess.

- **Q:** What if we add spawn-relative mode later?
- **A:** Backend provides different extent/center, frontend doesn't care.

- **Q:** What about other coordinate systems?
- **A:** Backend provides projection/transform config, frontend applies it.

---

## Philosophy

**Backend is the source of truth for tile storage.**

**Frontend is a dumb display client.**

This separation of concerns makes the system:
- Simpler to understand
- Easier to maintain  
- More flexible for future changes

The current working system proves the concept. Now we clean it up.

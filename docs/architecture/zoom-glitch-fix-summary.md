# Zoom Glitch Fix - Changes Summary

**Date:** 2025-10-06  
**Issue:** Map tiles shifting/glitching during zoom transitions  
**Root Cause:** Over-constrained OpenLayers configuration and custom projection issues  
**Solution:** Simplified to WebCartographer-style configuration

---

## Changes Made

### 1. Projection System (CRITICAL)

**Changed from:**
```javascript
const projection = new Projection({
  code: 'VINTAGESTORY',
  units: 'pixels',
  extent: extent,
  global: false
});
```

**Changed to:**
```javascript
const projection = 'EPSG:3857';  // Standard Web Mercator
```

**Why:** 
- EPSG:3857 is OpenLayers' default and well-tested
- Custom projection with pixel units was causing coordinate transformation issues
- WebCartographer uses EPSG:3857 successfully

---

### 2. Tile URL Function (SIMPLIFIED)

**Changed from:**
```javascript
tileUrlFunction: (tileCoord) => {
  const [offsetX, offsetZ] = getTileOffset(); // Called every time!
  const maxZ = maxZoom();
  const zoomScale = Math.pow(2, maxZ - zoom);
  // ... complex calculation
}
```

**Changed to:**
```javascript
// Pre-calculate outside function (called once)
const [baseOffsetX, baseOffsetZ] = getTileOffset();
const baseZoom = maxZoom();

tileUrlFunction: (tileCoord) => {
  // Simple arithmetic with pre-calculated values
  const scale = Math.pow(2, baseZoom - z);
  const storageX = x + Math.floor(baseOffsetX / scale);
  const storageY = y + Math.floor(baseOffsetZ / scale);
  return `/tiles/${z}/${storageX}_${storageY}.png`;
}
```

**Why:**
- Eliminates potential rounding errors from recalculating on every tile
- Matches WebCartographer's simple approach
- Faster performance

---

### 3. Tile Layer Configuration

**Added:**
```javascript
interpolate: false  // WebCartographer setting
```

**Removed:**
```javascript
extent: extent  // Was over-constraining tile rendering
```

**Why:**
- `interpolate: false` prevents tile blur during zoom (WebCartographer uses this)
- Removed extent constraint lets tiles render naturally

---

### 4. View Configuration

**Changed from:**
```javascript
view: new View({
  extent: extent,
  constrainResolution: true,
  smoothExtentConstraint: false,
  constrainOnlyCenter: true,
  // ...
});
```

**Changed to:**
```javascript
view: new View({
  constrainResolution: true,
  extent: extent,  // Keep for panning limits
  projection: projection,
  // No smooth/center constraints
});
```

**Why:**
- `smoothExtentConstraint` was causing view drift during zoom
- `constrainOnlyCenter` was unnecessary with EPSG:3857
- Simpler configuration like WebCartographer

---

### 5. Layer Factory Updates

**Updated GeoJSON format:**
```javascript
format: new GeoJSON({
  dataProjection: projection || 'EPSG:3857',
  featureProjection: projection || 'EPSG:3857'
})
```

**Why:** Ensures all layers use the same projection system

---

## Testing Checklist

### Visual Tests
- [ ] Zoom in from level 0 to max - no shifting
- [ ] Zoom out from max to level 0 - no shifting
- [ ] Pan while zooming - map stays centered
- [ ] Tiles at edges load correctly - no ghost tiles

### Console Logs to Check
- [ ] `[MapContainer] View projection: EPSG:3857 (WebCartographer style)`
- [ ] `[Tile Request]` logs show reasonable coordinates
- [ ] No errors about projection mismatches

### Network Tab
- [ ] Tiles are being requested
- [ ] Tile URLs look correct: `/tiles/{z}/{x}_{y}.png`
- [ ] 200 OK responses (or 404 for missing tiles)
- [ ] No duplicate tile requests

---

## Debugging Guide

### If tiles don't load:

1. **Check Console for Tile Requests:**
   ```
   [Tile Request] z=7 grid=(123,456) → storage=(2056,2415) → /tiles/7/2056_2415.png
   ```
   - Are grid coordinates reasonable?
   - Are storage coordinates matching actual tile files?
   - Is the URL correct?

2. **Check Network Tab:**
   - Are tiles being requested?
   - What HTTP status codes?
   - Are tile coordinates in range?

3. **Check Map Configuration:**
   ```javascript
   console.log('[MapContainer] Map config loaded:', {
     worldExtent, worldOrigin, tileOffset, defaultCenter
   });
   ```
   - Is extent reasonable?
   - Is offset correct?
   - Is center within extent?

4. **Check Projection:**
   ```javascript
   console.log('[MapContainer] View projection:', view.getProjection().getCode());
   ```
   - Should be `EPSG:3857`

### If tiles shift during zoom:

1. **Check for extent constraints:**
   - TileLayer should NOT have extent
   - View CAN have extent (for panning limits)

2. **Check tile URL calculation:**
   - Offset should be pre-calculated (not recalculated per tile)
   - Scale calculation should be consistent

3. **Check projection consistency:**
   - All layers must use same projection
   - TileGrid must match projection

---

## Known Issues & Solutions

### Issue: Map not visible at all
**Possible Causes:**
1. Center coordinates outside extent
2. Zoom level out of range
3. Tile requests going to wrong URLs
4. Projection mismatch

**Debug Steps:**
1. Check console for extent vs center
2. Check Network tab for tile requests
3. Verify projection is EPSG:3857
4. Test with WebCartographer's simpler extent

### Issue: Tiles still shifting
**Possible Causes:**
1. Extent constraints still too strict
2. TileGrid not matching tile storage
3. Offset calculation incorrect

**Debug Steps:**
1. Remove ALL extent constraints temporarily
2. Log tile coordinates and compare with storage
3. Verify offset is applied consistently

---

## Comparison: Before vs After

| Aspect | Before | After |
|--------|--------|-------|
| Projection | Custom 'VINTAGESTORY' | EPSG:3857 |
| Tile URL | Complex, recalculated | Simple, pre-calculated |
| Tile Layer | extent + many constraints | Minimal constraints |
| View | Many custom constraints | Simple WebCartographer style |
| Interpolation | Not set (default) | `false` (blocky pixels) |
| Complexity | High | Low |
| Maintainability | Difficult | Easy |
| Matches WebCartographer | No | Yes |

---

## Next Steps if Still Broken

If the map still doesn't work after these changes:

1. **Revert to custom projection but keep other changes:**
   ```javascript
   const projection = new Projection({
     code: 'VINTAGESTORY',
     units: 'pixels',
     extent: extent,
     global: false
   });
   ```

2. **Test with WebCartographer's fixed extent:**
   ```javascript
   extent: [-512000, -512000, 512000, 512000]
   origin: [-512000, 512000]
   ```

3. **Simplify tile URL to direct mapping:**
   ```javascript
   url: '/tiles/{z}/{x}_{y}.png'  // No offset, test if tiles load
   ```

4. **Check backend tile generation:**
   - Verify tiles exist with correct coordinates
   - Check tile naming matches what frontend requests
   - Test a known tile URL directly in browser

---

## References

- WebCartographer OpenLayers spec: `docs/architecture/openlayers-comparison.md`
- Coordinate systems: `docs/architecture/coordinate-systems.md`
- Original issue: Map tiles shifting during zoom

---

## Rollback Instructions

If these changes make things worse, revert by:

1. **Restore custom projection:**
   ```bash
   git diff VintageAtlas/frontend/src/components/map/MapContainer.vue
   # Look for "EPSG:3857" and change back to custom Projection
   ```

2. **Restore original constraints:**
   - Add back `smoothExtentConstraint: false`
   - Add back `constrainOnlyCenter: true`
   - Add back `extent` on TileLayer

3. **Restore original tile URL function:**
   - Move offset calculation back inside tileUrlFunction
   - Remove pre-calculation

---

**Last Updated:** 2025-10-06  
**Status:** Testing in progress  
**Expected Outcome:** Zoom glitches resolved, tiles render smoothly

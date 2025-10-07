# OpenLayers Configuration Comparison: WebCartographer vs VintageAtlas

**Date:** 2025-10-06  
**Purpose:** Identify differences to resolve zoom glitches and improve map rendering

---

## Executive Summary

**WebCartographer** uses a simpler, more traditional OpenLayers configuration with fixed extents and template URL strings. **VintageAtlas** uses a more complex dynamic configuration system with custom projections and coordinate transformations.

**Key Finding:** VintageAtlas's complexity in tile coordinate mapping may be causing zoom glitches due to coordinate misalignment during zoom transitions.

---

## 1. Projection System

### WebCartographer
```javascript
// Uses default EPSG:3857 (Web Mercator)
// No explicit projection configuration
// Y-axis inversion: gameY_to_mapY = -gameZ
```

**Characteristics:**
- Standard Web Mercator projection
- Units in meters
- Well-supported by OpenLayers
- Simple coordinate transformations

### VintageAtlas
```javascript
const projection = new Projection({
  code: 'VINTAGESTORY',
  units: 'pixels',  // Not meters!
  extent: extent,
  global: false
});
```

**Characteristics:**
- Custom projection named 'VINTAGESTORY'
- Units in pixels (game blocks)
- Non-geographic coordinate system
- Z-axis flip handled in MapConfigController

**Implications:**
- ✅ Better semantic match for game coordinates
- ⚠️ More complex to debug
- ⚠️ Not compatible with geographic data sources

---

## 2. Tile Grid Configuration

### WebCartographer
```javascript
var vsWorldGrid = new ol.tilegrid.TileGrid({
    extent: [-512000, -512000, 512000, 512000],
    origin: [-512000, 512000],  // Top-left
    resolutions: [512, 256, 128, 64, 32, 16, 8, 4, 2, 1],
    tileSize: [256, 256]
});
```

**Properties:**
- Fixed extent: ±512,000 units (1M×1M world)
- 10 zoom levels (0-9)
- Power-of-2 resolutions
- Origin at top-left corner

### VintageAtlas
```javascript
new TileGrid({
    extent: worldExtent(),          // Dynamic from server
    origin: worldOrigin(),          // Dynamic from server
    resolutions: tileResolutions(), // Dynamic from server
    tileSize: [tileSize, tileSize]  // Dynamic from server
});
```

**Properties:**
- Dynamic extent calculated from savegame
- Variable zoom levels based on world size
- Power-of-2 resolutions (same as WebCartographer)
- Origin calculated to match extent

**Implications:**
- ✅ Adapts to any world size
- ✅ Supports both absolute and spawn-relative coordinates
- ⚠️ More complex initialization
- ⚠️ Requires server roundtrip before map loads

---

## 3. Tile Layer Configuration

### WebCartographer
```javascript
let vsWorld = new ol.layer.Tile({
    name: 'World',
    source: new ol.source.XYZ({
        interpolate: false,  // Preserve blocky aesthetic
        wrapx: false,
        tileGrid: vsWorldGrid,
        url: dataFolder + '/world/{z}/{x}_{y}.png',  // Simple template
    })
})
```

**Settings:**
- `interpolate: false` - No tile smoothing
- `wrapX: false` - No horizontal wrapping
- Template URL with automatic substitution
- No custom tile loading logic

### VintageAtlas (Current)
```javascript
terrainLayer = new TileLayer({
  useInterimTilesOnError: false,
  background: '#4A90C4',
  extent: extent,  // Layer extent constraint
  source: new XYZ({
    projection: projection,
    tileGrid: createWorldTileGrid(),
    wrapX: false,
    interpolate: false,  // Added based on WebCartographer
    minZoom: minZoom(),
    maxZoom: maxZoom(),
    tileUrlFunction: (tileCoord) => {
      // Complex offset mapping with zoom scaling
      const zoom = tileCoord[0];
      const olTileX = tileCoord[1];
      const olTileY = tileCoord[2];
      
      const [offsetX, offsetZ] = getTileOffset();
      const zoomScale = Math.pow(2, maxZoom() - zoom);
      const scaledOffsetX = Math.floor(offsetX / zoomScale);
      const scaledOffsetZ = Math.floor(offsetZ / zoomScale);
      
      const absoluteX = olTileX + scaledOffsetX;
      const absoluteZ = olTileY + scaledOffsetZ;
      
      return `/tiles/${zoom}/${absoluteX}_${absoluteZ}.png`;
    }
  })
});
```

**Settings:**
- `useInterimTilesOnError: false` - Don't show fallback tiles
- `interpolate: false` - Same as WebCartographer
- `extent` on layer - Constrains rendering
- Custom `tileUrlFunction` - Complex offset mapping
- `minZoom`/`maxZoom` - Prevents invalid requests

**Implications:**
- ✅ More control over tile loading behavior
- ✅ Can handle complex coordinate transformations
- ⚠️ **Potential issue:** Complex offset calculation may cause coordinate drift during zoom
- ⚠️ More points of failure in tile loading

---

## 4. View Configuration

### WebCartographer
```javascript
let view = new ol.View({
    center: [0, 0],
    constrainResolution: true,  // Snap to zoom levels
    zoom: zm,
    resolutions: [256, 128, 64, 32, 16, 8, 4, 2, 1, 0.5, 0.25, 0.125]
});
```

**Properties:**
- 12 resolution levels (more than tile grid)
- Simple extent constraint (default behavior)
- No explicit extent
- No custom projection

### VintageAtlas
```javascript
view: new View({
  center: props.center || defaultCenter(),
  zoom: initialZoom,
  minZoom: minZoom(),
  maxZoom: maxZ,
  extent: extent,  // Explicit extent constraint
  constrainResolution: true,
  smoothExtentConstraint: false,  // Prevent drift during zoom
  constrainOnlyCenter: true,      // Only constrain center point
  resolutions: resolutions,
  projection: projection
})
```

**Properties:**
- Same number of resolutions as tile grid
- Explicit extent constraint
- `smoothExtentConstraint: false` - No smooth panning at edges
- `constrainOnlyCenter: true` - Better zoom behavior
- Custom projection

**Implications:**
- ✅ More precise control over view behavior
- ✅ `smoothExtentConstraint: false` prevents zoom drift
- ✅ Custom projection ensures coordinate consistency
- ⚠️ More complex configuration

---

## 5. Tile URL Pattern

### WebCartographer
```
/world/{z}/{x}_{y}.png
```

- OpenLayers substitutes `{z}`, `{x}`, `{y}` automatically
- Tile coordinates directly match OpenLayers grid
- No transformation needed

### VintageAtlas
```
/tiles/{zoom}/{absoluteX}_{absoluteZ}.png
```

- Custom function calculates tile coordinates
- Applies offset based on tile grid origin
- Scales offset per zoom level

**Key Difference:**

WebCartographer's tiles are stored relative to its tile grid origin, matching OpenLayers' expectations. VintageAtlas stores tiles with **absolute world coordinates**, requiring runtime transformation.

**Why VintageAtlas does this:**
- Tiles work in both absolute and spawn-relative coordinate modes
- No need to regenerate tiles when switching coordinate modes
- Backend complexity traded for frontend complexity

---

## 6. Vector Layer Configuration

Both use similar configurations for vector layers (traders, translocators, etc.)

### Similarities:
- GeoJSON data sources
- Feature-based styling
- Min zoom levels to hide at low zoom
- Icon-based rendering with SVG coloring

### Key Difference:
**WebCartographer:**
```javascript
source: new ol.source.Vector({
    url: dataFolder + '/geojson/traders.geojson',
    format: new ol.format.GeoJSON(),
})
```

**VintageAtlas:**
```javascript
source: new VectorSource({
    url: '/api/geojson/traders',
    format: new GeoJSON({
      dataProjection: projection,    // Custom projection
      featureProjection: projection  // Same projection
    }),
})
```

**Implication:** VintageAtlas explicitly sets projection on GeoJSON format, ensuring coordinate consistency.

---

## 7. Performance Optimizations

### WebCartographer
- `interpolate: false` - Fast rendering, blocky pixels
- Min zoom levels on vector layers
- Constrained resolutions prevent sub-pixel rendering
- Simple tile loading (no caching logic)

### VintageAtlas
- `interpolate: false` - Same as WebCartographer
- `useInterimTilesOnError: false` - Prevents fallback tile rendering
- `VectorImageLayer` instead of `VectorLayer` - Better performance
- `imageRatio: 2`, `renderBuffer: 250` - Higher quality on zoom
- User removed: `preload: 0`, `cacheSize: 256`, `transition: 200`

**Implications:**
- VintageAtlas generally has more aggressive optimizations
- Some settings removed by user might have been helping (e.g., `preload: 0`)

---

## 8. Coordinate System Handling

### WebCartographer
```javascript
// Y-axis inversion for display
gameY_to_mapY = -gameZ

// Coordinate display
ol.coordinate.toStringXY([coordinate[0], -coordinate[1]], 0)
```

Simple inversion of Y-axis for display purposes.

### VintageAtlas

**Backend (MapConfigController.cs):**
```csharp
// Z-axis flip for display
int[] worldExtent = [extent.MinX, -extent.MaxZ, extent.MaxX, -extent.MinZ];
int[] worldOrigin = [extent.MinX, -extent.MinZ];
```

**Frontend (mapConfig.ts):**
```typescript
export const formatCoordinates = (x: number, z: number): string => {
  if (isAbsolutePositions()) {
    return `${Math.round(x)}, ${Math.round(z)}`;
  } else {
    const ew = x >= 0 ? 'E' : 'W';
    const ns = z <= 0 ? 'N' : 'S';
    return `${Math.abs(Math.round(x))}${ew}, ${Math.abs(Math.round(z))}${ns} from spawn`;
  }
};
```

Much more complex system supporting:
- Absolute world coordinates
- Spawn-relative coordinates
- Directional display (N/S/E/W)
- Z-axis flip for "North up" display

---

## 9. Layer Visibility Management

### WebCartographer
```javascript
let showSubLayerItems = {
    "Traders": {
        "Artisan trader": true,
        "Building materials trader": true,
        // ...
    }
};

// Toggle via opacity
opacity: isOn ? 1 : 0
```

Uses opacity for sub-layer filtering.

### VintageAtlas
```javascript
const layerVisibility = ref({
    terrain: true,
    biomes: false,
    traders: true,
    // ...
});

// Toggle via setVisible
layer.setVisible(visibility)
```

Uses OpenLayers' built-in visibility system.

**Implication:** VintageAtlas approach is cleaner and more efficient (layer not rendered at all when hidden).

---

## 10. Identified Issues & Solutions

### Issue 1: Zoom Glitches / Tile Shifting

**Root Cause:**
Complex tile offset calculation may cause coordinate misalignment during zoom transitions.

**Evidence:**
- User reported tiles "stay on unloaded areas" when zooming
- WebCartographer's simpler approach doesn't have this issue

**Solutions Applied:**
1. ✅ Added `extent` constraint to tile layer
2. ✅ Added `smoothExtentConstraint: false` to view
3. ✅ Added `constrainOnlyCenter: true` to view
4. ✅ Added `interpolate: false` to tile source
5. ✅ Simplified tile coordinate mapping comments

**Potential Further Solutions:**
- Simplify tile offset calculation
- Consider pre-transforming tile coordinates in backend
- Test with fixed extent like WebCartographer

### Issue 2: Missing Tile Interpolation Setting

**Root Cause:**
VintageAtlas was missing `interpolate: false` which WebCartographer uses.

**Solution:**
✅ Added `interpolate: false` to XYZ source configuration.

**Impact:**
- Preserves blocky pixel aesthetic
- Faster rendering
- No blur during zoom transitions

---

## 11. Recommendations

### Immediate (Already Applied)
- ✅ Add `interpolate: false` to tile source
- ✅ Add `extent` constraint to tile layer
- ✅ Add `smoothExtentConstraint: false` to view
- ✅ Add `constrainOnlyCenter: true` to view

### Short-term
- [ ] Test with simpler tile coordinate mapping (like WebCartographer)
- [ ] Add debug logging for tile requests
- [ ] Profile tile loading performance
- [ ] Test with WebCartographer's simpler projection (EPSG:3857)

### Long-term
- [ ] Consider moving coordinate transformation to backend
- [ ] Implement tile caching (IndexedDB)
- [ ] Add preloading for adjacent tiles
- [ ] Implement WebGL rendering for better performance
- [ ] Add clustering for dense marker layers

---

## 12. Testing Plan

### Verify Zoom Glitch Fix
1. Start server and load map
2. Zoom in from zoom 0 to max zoom, observing tiles
3. Zoom out from max zoom to zoom 0
4. Pan to edge of explored area and zoom
5. Check that:
   - ✅ Map doesn't shift during zoom
   - ✅ Old tiles disappear properly
   - ✅ Tiles load smoothly without overlap
   - ✅ Map stays centered on same point

### Verify Performance
1. Open browser DevTools Network tab
2. Zoom in and out multiple times
3. Check for:
   - ✅ No duplicate tile requests
   - ✅ Proper tile caching
   - ✅ No 404 errors for missing tiles
   - ✅ Fast tile loading (<100ms per tile)

### Verify Coordinate Accuracy
1. Note spawn coordinates from game
2. Click spawn on map
3. Verify coordinates display correctly
4. Test in both absolute and spawn-relative modes

---

## 13. Summary Comparison Table

| Feature | WebCartographer | VintageAtlas | Winner |
|---------|----------------|--------------|--------|
| Projection | EPSG:3857 (Web Mercator) | Custom 'VINTAGESTORY' | WC (simpler) |
| Tile Grid | Fixed extent | Dynamic from server | VA (flexible) |
| Tile URL | Template string | Custom function | WC (simpler) |
| Tile Loading | Simple | Complex with offsets | WC (simpler) |
| View Config | Basic | Enhanced with constraints | VA (more control) |
| Vector Layers | Opacity toggle | Visibility toggle | VA (more efficient) |
| Coordinate Display | Simple Y-flip | Complex with modes | Tie (VA has features, WC is simple) |
| Performance | Good baseline | Optimized but complex | Tie |
| Maintainability | High | Medium | WC |
| Flexibility | Low | High | VA |

**Overall:** WebCartographer wins on simplicity and maintainability. VintageAtlas wins on flexibility and features.

---

## 14. Conclusion

The zoom glitches in VintageAtlas are likely caused by the complexity of its coordinate transformation system. The fixes applied (extent constraints, smooth extent constraint disabled, interpolation disabled) address the immediate symptoms.

For long-term stability, consider simplifying the tile coordinate system to be more like WebCartographer's approach, or move the coordinate transformation logic entirely to the backend where it can be pre-computed.

**Key Takeaway:** Sometimes simpler is better. WebCartographer's approach has proven stable over time precisely because it avoids complex runtime transformations.

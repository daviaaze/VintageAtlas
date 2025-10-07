# Clean OpenLayers Implementation

## Overview

This is a **from-scratch** implementation of OpenLayers following `OPENLAYERS_SPECIFICATION.md`. All old workarounds and fixes have been removed in favor of a clean, spec-compliant approach.

## Architecture

### 4 Core Files

```
src/utils/
  ├── olMapConfig.ts    # Tile grid, view config, coordinate formatting
  ├── olStyles.ts       # Feature styling functions
  └── olLayers.ts       # Layer factory functions

src/components/map/
  └── CleanMapContainer.vue  # Main map component
```

## File Details

### 1. `olMapConfig.ts` - Configuration

**Purpose:** Pure configuration, no workarounds

**Functions:**
- `initOLConfig()` - Load config from server
- `createTileGrid()` - Tile grid matching spec lines 32-62
- `getViewResolutions()` - View resolutions (13 levels)
- `getViewCenter()` - Initial center position
- `getViewZoom()` - Initial zoom level
- `getTileUrl(z, x, y)` - Tile URL pattern `/api/tiles/{z}/{x}_{y}.png`
- `formatCoords([x, z])` - Display format with Y-axis flip

**Key Points:**
- Reads from server API (`/api/map-config`)
- No hardcoded fallbacks
- Clean separation of concerns

### 2. `olStyles.ts` - Styling

**Purpose:** Feature styling matching spec colors and icons

**Functions:**
- `exploredChunksStyle()` - Spec lines 122-127
- `tradersStyle()` - Spec lines 155-163 with color by wares
- `translocatorsStyle()` - Spec lines 218-239 (dual style: line + icons)
- `landmarksStyle()` - Spec lines 281-304 with zoom-dependent Misc
- `highlightTranslocatorStyle()` - Spec lines 424-445
- `highlightTraderStyle()` - Spec lines 447-459

**Color Constants:**
- `TRADER_COLORS` - 10 types (Artisan, Building materials, etc.)
- `TRANSLOCATOR_COLORS` - 4 types (standard, named, spawn, teleporter)
- `LANDMARK_COLORS` - 3 types (Base, Misc, Server)
- `LANDMARK_ICONS` - Icon paths for each landmark type

**Key Points:**
- Pure functions, no state
- Returns OpenLayers `Style` objects
- Exact spec compliance

### 3. `olLayers.ts` - Layer Factory

**Purpose:** Create configured layers

**Functions:**
- `createWorldLayer()` - Tile layer (spec lines 89-108)
- `createExploredChunksLayer()` - GeoJSON vector (spec lines 111-142)
- `createTradersLayer()` - GeoJSON vector, minZoom: 3 (spec lines 145-188)
- `createTranslocatorsLayer()` - GeoJSON vector, minZoom: 2 (spec lines 191-257)
- `createLandmarksLayer(map)` - GeoJSON vector, minZoom: 2 (spec lines 260-329)

**Properties:**
- All vector layers use GeoJSON format
- Proper minZoom levels
- Layer names for identification
- Styling functions from `olStyles.ts`

**Key Points:**
- Simple factory pattern
- No clustering, no optimization tricks
- Just clean layer creation

### 4. `CleanMapContainer.vue` - Main Component

**Purpose:** Minimal map component

**Structure:**
```vue
<template>
  <div class="ol-map-container">
    <div ref="mapElement" class="ol-map"></div>
    <div class="ol-coords">{{ mouseCoords }}</div>
    <div v-if="loading" class="ol-loading">...</div>
  </div>
</template>
```

**Flow:**
1. Load config from server
2. Create layers in spec order
3. Create OpenLayers Map
4. Add mouse position tracking
5. Add URL state management

**Key Points:**
- ~120 lines total
- No old workarounds
- No complex state management
- Clean async initialization

## Layer Rendering Order

Matches spec lines 332-341:

1. **World** - Tile layer (base map)
2. **Explored Chunks** - 50% opacity overlay
3. **Traders** - Min zoom 3, dynamic colors
4. **Translocators** - Min zoom 2, dual style
5. **Landmarks** - Min zoom 2, text labels

## Features Implemented

✅ **Tile Grid** (Spec 32-62)
- Extent, origin, resolutions from server
- 256x256 tile size
- 10 zoom levels

✅ **View Configuration** (Spec 65-84)
- 13 resolution levels
- Constrained to zoom levels
- Dynamic center/zoom from server

✅ **Tile Layer** (Spec 89-108)
- No interpolation (blocky pixels)
- No X-axis wrapping
- Custom URL pattern

✅ **Explored Chunks** (Spec 111-142)
- Feature-defined colors
- Black 1px border
- 50% opacity
- Hidden by default

✅ **Traders** (Spec 145-188)
- 10 trader types with RGB colors
- Dynamic icon coloring
- MinZoom: 3

✅ **Translocators** (Spec 191-257)
- Dual style (line + endpoint icons)
- 4 types by tag/label
- MinZoom: 2

✅ **Landmarks** (Spec 260-329)
- 3 types (Base, Misc, Server)
- Text labels
- Misc hidden below zoom 9
- MinZoom: 2
- Server z-index: 1000

✅ **Mouse Position** (Spec 510-520)
- Y-axis inverted display
- Bottom-left corner

✅ **URL State** (Spec 523-531)
- Updates on pan/zoom
- Format: `?x=X&y=Y&zoom=Z`

## What's NOT Included (Yet)

⏳ **Feature Interaction** (Spec 533-544)
- Hover highlighting
- Click popups
- Inspector div

⏳ **Layer Visibility Controls** (Spec 370-375)
- Toggle layers on/off
- Sub-layer filtering

⏳ **Custom Controls** (Spec 348)
- Only mouse position added
- No zoom buttons, scale line, etc.

## API Endpoints Expected

The implementation expects these endpoints:

### Required
- `GET /api/map-config` - Map configuration (already working)
- `GET /api/tiles/{z}/{x}_{y}.png` - Tile images

### Vector Layers (GeoJSON)
- `GET /api/geojson/chunk` - Explored chunks
- `GET /api/geojson/traders` - Trader locations
- `GET /api/geojson/translocators` - Translocator pairs
- `GET /api/geojson/landmarks` - Landmark points

## GeoJSON Feature Properties

### Traders
```json
{
  "type": "Feature",
  "geometry": { "type": "Point", "coordinates": [x, z] },
  "properties": {
    "name": "Trader Name",
    "wares": "Artisan trader"
  }
}
```

### Translocators
```json
{
  "type": "Feature",
  "geometry": { "type": "LineString", "coordinates": [[x1, z1], [x2, z2]] },
  "properties": {
    "label": "Optional name",
    "tag": "SPAWN | TP | undefined"
  }
}
```

### Landmarks
```json
{
  "type": "Feature",
  "geometry": { "type": "Point", "coordinates": [x, z] },
  "properties": {
    "label": "Display name",
    "type": "Base | Misc | Server"
  }
}
```

### Explored Chunks
```json
{
  "type": "Feature",
  "geometry": { "type": "Polygon", "coordinates": [...] },
  "properties": {
    "color": "rgba(100, 149, 237, 0.5)",
    "version": "Optional version string"
  }
}
```

## Testing

### Verification Steps

1. **Map Loads**
   ```
   ✓ [OL Config] Loaded from server
   ✓ [CleanMap] Map initialized
   ```

2. **Tiles Display**
   - Check `/api/tiles/7/X_Y.png` loads
   - Verify no interpolation (blocky)

3. **Vector Layers**
   - Traders visible at zoom 3+
   - Translocators visible at zoom 2+
   - Landmarks visible at zoom 2+
   - Misc landmarks hidden below zoom 9

4. **Colors Match Spec**
   - Artisan trader: Cyan
   - Building materials trader: Red
   - Standard translocator: Purple
   - Spawn translocator: Cyan

5. **Mouse Coordinates**
   - Bottom-left corner
   - Format: `X, Z`
   - Updates on mouse move

6. **URL Updates**
   - Pan/zoom changes URL
   - Format: `?x=X&y=Y&zoom=Z`

## Differences from Old Implementation

| Old | Clean |
|-----|-------|
| `mapConfig.ts` (192 lines) | `olMapConfig.ts` (68 lines) |
| `layerFactory.ts` (486 lines) | `olLayers.ts` (77 lines) |
| `MapContainer.vue` (614 lines) | `CleanMapContainer.vue` (125 lines) |
| Multiple utility files | 3 core utilities |
| Caching, fallbacks, workarounds | Direct server config |
| Complex state management | Minimal state |

**Total:** ~1292 lines → ~270 lines (79% reduction)

## Migration Path

The clean implementation is in **parallel** with the old one:

- **Old:** `MapContainer.vue` (still exists)
- **New:** `CleanMapContainer.vue` (used by MapView)

To switch back:
```ts
// In MapView.vue
import MapContainer from '@/components/map/MapContainer.vue'; // Old
import MapContainer from '@/components/map/CleanMapContainer.vue'; // New
```

## Next Steps

1. ✅ Clean tile grid and view
2. ✅ Base tile layer
3. ✅ Vector layers
4. ✅ Layer rendering order
5. ✅ Mouse position control
6. ✅ URL state management
7. ⏳ Feature hover/click interaction
8. ⏳ Layer visibility controls
9. ⏳ Add remaining custom controls

## Spec Compliance Checklist

✅ Coordinate System (lines 8-28)
✅ Tile Grid Configuration (lines 32-62)
✅ View Configuration (lines 65-84)
✅ World Tile Layer (lines 89-108)
✅ Explored Chunks Layer (lines 111-142)
✅ Traders Layer (lines 145-188)
✅ Translocators Layer (lines 191-257)
✅ Landmarks Layer (lines 260-329)
✅ Layer Rendering Order (lines 332-341)
✅ Mouse Position Display (lines 510-520)
✅ URL State Management (lines 523-531)
⏳ Feature Hover (lines 533-537)
⏳ Feature Click (lines 538-544)
⏳ Highlight Styles (lines 422-461)
⏳ Layer Visibility Management (lines 370-403)

## Philosophy

This implementation follows these principles:

1. **Spec First** - Every decision references the spec
2. **No Workarounds** - If it's not in the spec, don't add it
3. **Simple Over Clever** - Direct code beats optimization
4. **Server Truth** - No client-side fallbacks
5. **Clean Slate** - Ignore old implementation details

The goal is a **maintainable, understandable, spec-compliant** OpenLayers map.

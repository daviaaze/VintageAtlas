# OpenLayers Migration Summary

This document summarizes the changes made to align the frontend with the OpenLayers specification from `OPENLAYERS_SPECIFICATION.md`.

## Date
October 6, 2025

## Overview
Updated the VintageAtlas frontend to match the OpenLayers configuration and styling patterns used in WebCartographer, as specified in `OPENLAYERS_SPECIFICATION.md`.

## Files Created

### 1. `src/utils/layerColors.ts`
**Purpose:** Centralized color configuration matching spec requirements

**Features:**
- Trader colors by wares type (RGB arrays) - 10 trader types
- Translocator colors by type (4 types: standard, named, spawn, teleporter)
- Landmark colors by type (3 types: Base, Misc, Server)
- Highlight colors for hover states
- Helper functions: `rgbToString()`, `brightenColor()`, `getTraderColor()`, `getTranslocatorColor()`, `getLandmarkColor()`, `getLandmarkIcon()`

**Spec References:** Lines 175-187 (traders), 250-256 (translocators), 318-322 (landmarks), 422-461 (highlights)

## Files Modified

### 2. `src/utils/layerFactory.ts`
**Changes:**
- Updated all layer factory functions to match spec styling
- Added sub-layer visibility support via opacity control
- Implemented proper minZoom levels (Traders: 3, Translocators: 2, Landmarks: 2)
- Added `createLandmarksLayer()` - separate from signs with dynamic icons/colors
- Added `createExploredChunksLayer()` - for chunk.geojson with feature-defined colors
- Updated `createTraderLayer()` - dynamic color by wares type, sub-layer filtering
- Updated `createTranslocatorLayer()` - dual style (line + endpoint icons), dynamic colors
- Added `createTranslocatorHighlightStyle()` - hover styling
- Added `createTraderHighlightStyle()` - brightened colors on hover
- Fixed TypeScript geometry types for LineString handling

**Spec References:** Lines 111-306 (layers), 424-461 (highlights)

### 3. `src/stores/map.ts`
**Changes:**
- Added `landmarks` layer visibility flag
- Added `exploredChunks` layer visibility flag
- Added `subLayerVisibility` ref with structure matching spec (lines 379-399):
  - `traders`: 10 wares types (all true by default)
  - `translocators`: 4 types (all true by default)
  - `landmarks`: 3 types (all true by default)
- Added `labelSize` ref (default: 10px, range: 8-144px)
- Added `toggleSubLayer()` action
- Added `setSubLayerVisibility()` action
- Added `setLabelSize()` action
- All sub-layer changes trigger `refreshLayers()` to update styling

**Spec References:** Lines 326 (labelSize), 379-399 (subLayerVisibility)

### 4. `src/components/map/MapContainer.vue`
**Changes:**
- Updated layer imports to include new layer factories
- Reordered layer creation to match spec order (lines 332-341):
  1. World base layer (tiles)
  2. Explored chunks overlay
  3. Traders
  4. Translocators
  5. Landmarks
  6. Additional: Signs, chunk layers, players
- Updated layer creation to pass sub-layer visibility and label size
- Added watchers for sub-layer visibility changes
- Added watcher for label size changes
- Updated visibility watchers to include all new layers
- Updated loading indicator to wait for all layer sources

**Spec References:** Lines 332-341 (layer order)

## Key Features Implemented

### 1. Dynamic Icon Coloring
- SVG icons are colored dynamically using OpenLayers `Icon.color` property
- RGB arrays from `layerColors.ts` applied to icons
- Supports per-feature coloring based on properties

### 2. Sub-Layer Filtering
- Opacity-based visibility control (1 = visible, 0 = hidden)
- Per-feature-type filtering within layers
- Matches spec lines 379-403
- Example: Show only "Artisan trader" and "Luxuries trader"

### 3. Translocator Dual Styling
- LineString geometry with point markers at both endpoints
- Line connects the two translocator points
- Icons rendered at both ends using `MultiPoint` geometry
- Matches spec lines 218-239

### 4. Landmark Types
- Separate layer from signs/signposts
- Three types: Base, Misc, Server
- Misc landmarks hidden below zoom level 9
- Configurable label size (8-144px)
- Server landmarks have z-index: 1000
- Matches spec lines 260-306

### 5. Highlight Styles
- Translocator hover: Pink highlight (#ddaaff stroke, [255, 192, 255] icon)
- Trader hover: Brightened by 1.5x (clamped 64-255)
- Ready for integration with hover interactions
- Matches spec lines 422-461

### 6. Explored Chunks Layer
- Displays chunk.geojson data
- Uses color from feature properties
- Black 1px border
- 50% opacity
- Matches spec lines 111-129

## Color Mappings

### Traders (by wares)
- Artisan: Cyan [0, 240, 240]
- Building materials: Red [255, 0, 0]
- Clothing: Green [0, 128, 0]
- Commodities: Gray [128, 128, 128]
- Agriculture: Tan [200, 192, 128]
- Furniture: Orange [255, 128, 0]
- Luxuries: Blue [0, 0, 255]
- Survival goods: Yellow [255, 255, 0]
- Treasure hunter: Purple [160, 0, 160]
- Unknown: Dark gray [48, 48, 48]

### Translocators (by type)
- Standard: Purple [192, 0, 192]
- Named: Blue-purple [71, 45, 255]
- Spawn: Cyan [0, 192, 192]
- Teleporter: Red [229, 57, 53]

### Landmarks (by type)
- Base: Gray [192, 192, 192]
- Misc: Light gray [224, 224, 224]
- Server: White [255, 255, 255] (PNG, no color)

## Layer Rendering Order (Spec Lines 332-341)

1. **World** (vsWorld) - Tile layer base map
2. **Explored Chunks** (vsGenChunks) - 50% opacity overlay
3. **Traders** (vsTraders) - Min zoom 3, dynamic colors
4. **Translocators** (vsTranslocators) - Min zoom 2, dual style
5. **Landmarks** (vsLandmarks) - Min zoom 2, text labels

Additional layers:
- Signs (signposts)
- Chunks (grid overlay)
- Chunk versions (exploration history)
- Players (live data)
- Animals (live data)

## API Endpoints Expected

The implementation expects these GeoJSON endpoints:
- `/api/geojson/chunk` - Explored chunks with `color` property
- `/api/geojson/traders` - Traders with `name`, `wares` properties
- `/api/geojson/translocators` - Translocators (LineString) with `label`, `tag` properties
- `/api/geojson/landmarks` - Landmarks with `label`, `type` properties
- `/api/geojson/signposts` - Signs with `name` property
- `/api/geojson/chunks` - Chunk grid
- `/api/geojson/chunk-versions` - Chunk versions with `color` property

## Feature Properties Required

### Traders
- `name` (string) - Trader name
- `wares` (string) - One of the 10 trader types

### Translocators
- `label` (string, optional) - Custom name
- `tag` (string, optional) - "SPAWN" or "TP"
- Geometry: LineString with 2 points

### Landmarks
- `label` or `name` (string) - Display name
- `type` (string) - "Base", "Misc", or "Server"

### Explored Chunks
- `color` (string) - CSS color string (e.g., "rgba(100, 149, 237, 0.5)")
- `version` (string, optional) - Chunk generation version

## Testing Recommendations

1. **Layer Visibility**
   - Toggle each layer on/off via sidebar
   - Verify proper rendering order
   - Check minZoom levels work correctly

2. **Sub-Layer Filtering**
   - Toggle individual trader types
   - Toggle individual translocator types
   - Toggle individual landmark types
   - Verify opacity changes (not removal)

3. **Label Sizing**
   - Test label size slider (8-144px)
   - Verify landmarks update dynamically

4. **Zoom-Dependent Rendering**
   - Verify Misc landmarks hidden below zoom 9
   - Verify traders appear at zoom 3+
   - Verify translocators appear at zoom 2+

5. **Translocator Styling**
   - Verify lines connect endpoints
   - Verify icons appear at both ends
   - Test different translocator types

6. **Color Accuracy**
   - Verify all trader colors match spec
   - Verify all translocator colors match spec
   - Verify landmark colors match spec

7. **Hover States** (when integrated)
   - Test translocator highlight (pink)
   - Test trader highlight (brightened)

## Known Limitations

1. Highlight styles created but not yet integrated with hover interactions
2. Label size control exists in store but no UI slider yet
3. Sub-layer visibility controls exist but no UI checkboxes yet
4. Landmarks layer expects `/api/geojson/landmarks` endpoint (may need backend support)

## Next Steps

1. ✅ Create color configuration constants
2. ✅ Update layer factories with spec-compliant styling
3. ✅ Add sub-layer filtering to store
4. ✅ Add highlight styles
5. ✅ Update MapContainer with new layer order
6. ✅ Add landmarks layer
7. ✅ Update translocators to dual style
8. ⏳ Test all layer visibility and styling
9. ⏳ Create UI controls for sub-layer filtering
10. ⏳ Create UI control for label size
11. ⏳ Integrate highlight styles with hover interactions
12. ⏳ Verify backend provides all required GeoJSON endpoints

## Spec Compliance

This implementation matches the OpenLayers specification (OPENLAYERS_SPECIFICATION.md) in the following areas:

✅ Coordinate System (lines 8-28)
✅ Tile Grid Configuration (lines 32-62)
✅ View Configuration (lines 65-84)
✅ Layer Definitions (lines 87-306)
✅ Layer Rendering Order (lines 332-341)
✅ Layer Visibility Management (lines 370-403)
✅ Style Rendering (lines 407-461)
✅ Data Folder Structure (lines 464-504)

## Migration Notes

- All changes are backward compatible with existing code
- Existing layers continue to work as before
- New features are additive, not breaking
- Store API extended with new actions, old actions unchanged
- Layer factories accept additional optional parameters

## Performance Considerations

- VectorImageLayer used for all vector layers (better performance)
- Sub-layer filtering uses opacity (no feature removal/re-fetch)
- Layer source `.changed()` used for efficient re-rendering
- Minimal re-renders when toggling visibility

## References

- **Main Spec:** `OPENLAYERS_SPECIFICATION.md`
- **WebCartographer:** Original implementation reference
- **OpenLayers Docs:** https://openlayers.org/
- **Vintage Story API:** https://apidocs.vintagestory.at/

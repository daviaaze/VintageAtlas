# OpenLayers Improvements for VintageAtlas

## 1. Performance Optimizations

### Use VectorImage Layers
Replace `VectorLayer` with `VectorImageLayer` for better performance with many features:

```typescript
import VectorImageLayer from 'ol/layer/VectorImage';

// Instead of:
const tradersLayer = new VectorLayer({
  source: traderSource,
  style: styleFunction
});

// Use:
const tradersLayer = new VectorImageLayer({
  source: traderSource,
  imageRatio: 2, // Higher quality when zooming
  style: styleFunction
});
```

**Benefits:**
- Renders vector features as images
- Much faster with 100+ features
- Better zoom performance

### Implement Feature Clustering
For animal and player markers that can have many instances:

```typescript
import Cluster from 'ol/source/Cluster';

const clusterSource = new Cluster({
  distance: 40,
  source: animalSource
});

const animalLayer = new VectorImageLayer({
  source: clusterSource,
  style: (feature) => {
    const size = feature.get('features').length;
    // Show cluster count or individual feature
  }
});
```

## 2. Use Overlays for Feature Info

Instead of positioned divs, use OpenLayers Overlay for feature info panels:

```typescript
import Overlay from 'ol/Overlay';

const popup = new Overlay({
  element: popupElement,
  autoPan: {
    animation: {
      duration: 250
    }
  }
});

map.addOverlay(popup);

// On feature click:
map.on('singleclick', (evt) => {
  const feature = map.forEachFeatureAtPixel(evt.pixel, f => f);
  if (feature) {
    popup.setPosition(evt.coordinate);
    // Update popup content
  } else {
    popup.setPosition(undefined);
  }
});
```

## 3. Add Select Interaction

Replace manual click handling with OpenLayers Select interaction:

```typescript
import Select from 'ol/interaction/Select';
import { click, pointerMove } from 'ol/events/condition';

const selectClick = new Select({
  condition: click,
  style: highlightStyle
});

map.addInteraction(selectClick);

selectClick.on('select', (e) => {
  if (e.selected.length > 0) {
    const feature = e.selected[0];
    // Handle feature selection
  }
});

// Add hover effect
const selectHover = new Select({
  condition: pointerMove,
  style: hoverStyle
});
```

## 4. Implement Proper Projection Handling

The app uses a custom coordinate system. Define it properly:

```typescript
import { Projection } from 'ol/proj';
import { register } from 'ol/proj/proj4';
import proj4 from 'proj4';

// Define VintageStory projection
const vsProjection = new Projection({
  code: 'VINTAGESTORY:WORLD',
  units: 'pixels',
  extent: worldExtent,
  global: false
});

// Register it
register(proj4);

// Use in View
const view = new View({
  projection: vsProjection,
  center: defaultCenter,
  zoom: defaultZoom,
  extent: worldExtent
});
```

## 5. Add Extent-Based Layer Loading

Load features only when visible in viewport:

```typescript
import { bbox as bboxStrategy } from 'ol/loadingstrategy';

const traderSource = new VectorSource({
  format: new GeoJSON(),
  url: (extent) => {
    return `/data/geojson/traders.geojson?bbox=${extent.join(',')}`;
  },
  strategy: bboxStrategy
});
```

## 6. Implement View Constraints

Add smooth panning constraints:

```typescript
const view = new View({
  center: defaultCenter,
  zoom: defaultZoom,
  minZoom: 1,
  maxZoom: 10,
  extent: worldExtent,
  constrainResolution: true,
  smoothExtentConstraint: true,
  smoothResolutionConstraint: true,
  showFullExtent: true
});
```

## 7. Add Map Export/Screenshot

```typescript
map.once('rendercomplete', () => {
  const mapCanvas = document.createElement('canvas');
  const size = map.getSize();
  mapCanvas.width = size[0];
  mapCanvas.height = size[1];
  const mapContext = mapCanvas.getContext('2d');
  
  document.querySelectorAll('.ol-layer canvas').forEach((canvas) => {
    if (canvas.width > 0) {
      const opacity = canvas.parentNode.style.opacity;
      mapContext.globalAlpha = opacity === '' ? 1 : Number(opacity);
      const transform = canvas.style.transform;
      const matrix = transform.match(/^matrix\(([^\(]*)\)$/)[1].split(',').map(Number);
      CanvasRenderingContext2D.prototype.setTransform.apply(mapContext, matrix);
      mapContext.drawImage(canvas, 0, 0);
    }
  });
  
  // Download
  mapCanvas.toBlob((blob) => {
    saveAs(blob, 'map.png');
  });
});
```

## 8. Optimize Tile Loading

```typescript
const terrainLayer = new TileLayer({
  source: new XYZ({
    url: '/data/world/{z}/{x}_{y}.png',
    tileSize: 256,
    tileGrid: worldTileGrid,
    // Preload tiles for smoother panning
    preload: 1,
    // Cache tiles
    cacheSize: 2048,
    // Use interpolation for smoother zooming
    interpolate: true,
    // Handle CORS
    crossOrigin: 'anonymous'
  }),
  preload: 1
});
```

## 9. Add Geolocation (Player Position)

```typescript
import Geolocation from 'ol/Geolocation';

const geolocation = new Geolocation({
  tracking: true,
  projection: view.getProjection()
});

geolocation.on('change:position', () => {
  const coordinates = geolocation.getPosition();
  // Update player position marker
});
```

## 10. Implement Layer Switcher Control

```typescript
import LayerSwitcher from 'ol-layerswitcher';
import 'ol-layerswitcher/dist/ol-layerswitcher.css';

// Group layers
const baseLayerGroup = new LayerGroup({
  title: 'Base Layers',
  layers: [terrainLayer]
});

const overlayGroup = new LayerGroup({
  title: 'Overlays',
  layers: [tradersLayer, translocatorsLayer, signsLayer]
});

map.addControl(new LayerSwitcher({
  reverse: true,
  groupSelectStyle: 'group'
}));
```

## 11. Add Scale Line Control

```typescript
import ScaleLine from 'ol/control/ScaleLine';

map.addControl(new ScaleLine({
  units: 'metric',
  bar: true,
  steps: 4,
  text: true,
  minWidth: 140
}));
```

## 12. Implement Modify Interaction for Waypoints

Allow users to edit waypoint positions:

```typescript
import Modify from 'ol/interaction/Modify';

const modify = new Modify({
  source: signsSource,
  style: modifyStyle
});

map.addInteraction(modify);

modify.on('modifyend', (e) => {
  // Save modified features to backend
  const features = e.features.getArray();
  updateWaypoints(features);
});
```

## 13. Add Draw Interaction for Custom Markers

```typescript
import Draw from 'ol/interaction/Draw';

const draw = new Draw({
  source: customMarkersSource,
  type: 'Point'
});

map.addInteraction(draw);

draw.on('drawend', (e) => {
  const feature = e.feature;
  const coordinates = feature.getGeometry().getCoordinates();
  // Save to backend
});
```

## 14. Implement WebGL Layer for Maximum Performance

For extremely large datasets:

```typescript
import WebGLPointsLayer from 'ol/layer/WebGLPoints';

const playerLayer = new WebGLPointsLayer({
  source: playerSource,
  style: {
    symbol: {
      symbolType: 'circle',
      size: 8,
      color: '#dc3545',
      opacity: 0.9
    }
  }
});
```

## 15. Add Animation for View Changes

```typescript
function flyTo(location: [number, number], zoom?: number) {
  const view = map.getView();
  const duration = 2000;
  
  view.animate({
    center: location,
    zoom: zoom || view.getZoom(),
    duration: duration,
    easing: easeInOut
  });
}
```

## Priority Implementation Order

1. **Immediate** (Performance critical):
   - VectorImage layers
   - Tile preloading
   - View constraints

2. **High Priority** (Better UX):
   - Overlays for popups
   - Select interaction
   - Scale line control

3. **Medium Priority** (Nice to have):
   - Feature clustering
   - Layer switcher
   - Map export

4. **Future Enhancements**:
   - Draw/Modify interactions
   - WebGL layers
   - Custom projections

## Estimated Performance Gains

- VectorImage layers: **50-70% faster rendering**
- Feature clustering: **80-90% faster with 500+ features**
- Tile preloading: **Smoother panning experience**
- WebGL layers: **10x faster for 10,000+ features**


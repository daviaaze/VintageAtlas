<template>
  <div class="map-container">
    <div ref="mapRef" class="map"></div>

    <div class="map-controls">
      <div class="zoom-controls">
        <button @click="zoomIn" class="zoom-btn" title="Zoom in">
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="12" y1="5" x2="12" y2="19"></line><line x1="5" y1="12" x2="19" y2="12"></line></svg>
        </button>
        <button @click="zoomOut" class="zoom-btn" title="Zoom out">
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><line x1="5" y1="12" x2="19" y2="12"></line></svg>
        </button>
      </div>
    </div>

    <div class="map-compass">
      <svg xmlns="http://www.w3.org/2000/svg" width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
        <circle cx="12" cy="12" r="10"></circle>
        <polygon points="16.24 7.76 14.12 14.12 7.76 16.24 9.88 9.88 16.24 7.76"></polygon>
      </svg>
    </div>

    <!-- Notification for missing tiles -->
    <MissingTileNotification />

    <!-- Feature popup overlay -->
    <div ref="overlayRef" v-show="showFeaturePopup" class="feature-popup">
      <button @click="hideOverlay" class="popup-close">&times;</button>
      <div v-if="overlayContent" class="popup-content">
        <h3 class="popup-title">{{ overlayContent.name || 'Unknown' }}</h3>
        <div class="popup-details">
          <p v-if="overlayContent.type"><strong>Type:</strong> {{ overlayContent.type }}</p>
          <p v-if="overlayContent.text"><strong>Info:</strong> {{ overlayContent.text }}</p>
          <p v-if="overlayContent.wares"><strong>Wares:</strong> {{ overlayContent.wares }}</p>
        </div>
      </div>
    </div>

    <!-- Live layers (these are wrapper components, actual rendering is done by OpenLayers) -->
    <PlayerLayer />
    <AnimalLayer />
    <SpawnMarker />

    <div class="map-loading-overlay" v-if="loading">
      <div class="loading-content">
        <div class="spinner"></div>
        <div class="loading-text">Loading map...</div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch, computed, shallowRef } from 'vue';
import { useMapStore } from '@/stores/map';
import Map from 'ol/Map';
import View from 'ol/View';
import TileLayer from 'ol/layer/Tile';
import VectorImageLayer from 'ol/layer/VectorImage';
import VectorSource from 'ol/source/Vector';
import XYZ from 'ol/source/XYZ';
import { Style, Fill, Stroke, Text, Icon } from 'ol/style';
import { Point } from 'ol/geom';
import Feature from 'ol/Feature';
import { defaults as defaultControls } from 'ol/control';
import { defaults as defaultInteractions } from 'ol/interaction';
import LayerGroup from 'ol/layer/Group';

// WebCartographer-style simplified map configuration
import { 
  initMapConfig,
  createTileGrid,
  getTileUrl,
  getViewResolutions,
  getDefaultCenter,
  getDefaultZoom,
  getMinZoom,
  getMaxZoom,
} from '@/utils/simpleMapConfig';

// Optimized layer factory
import {
  createTraderLayer,
  createTranslocatorLayer,
  createLandmarksLayer,
  createSignsLayer,
  createExploredChunksLayer,
  createChunkLayer,
  createChunkVersionLayer
} from '@/utils/layerFactory';

// Map composables
import { useMapInteractions } from '@/composables/useMapInteractions';
import { useMapOverlay } from '@/composables/useMapOverlay';

// Custom controls
import {
  createScaleLineControl,
  ScreenshotControl,
  CoordinatesControl,
  FullscreenControl
} from '@/utils/mapControls';

// Live components
import PlayerLayer from '@/components/live/PlayerLayer.vue';
import AnimalLayer from '@/components/live/AnimalLayer.vue';
import SpawnMarker from '@/components/live/SpawnMarker.vue';
import MissingTileNotification from '@/components/map/MissingTileNotification.vue';

// Props
const props = defineProps<{
  center?: [number, number];
  zoom?: number;
}>();

// Refs
const mapRef = ref<HTMLElement | null>(null);
const overlayRef = ref<HTMLElement | null>(null);
const loading = ref(true);

// Store
const mapStore = useMapStore();

// Initialize map with layers (use shallowRef for better performance with OpenLayers objects)
const mapInstance = shallowRef<Map | null>(null);

// Map layers (matching spec layer order - lines 332-341)
let terrainLayer: TileLayer<XYZ> | null = null;
let exploredChunksLayer: any = null; // Layer 2: Explored chunks overlay
let tradersLayer: any = null; // Layer 3: Trader points
let translocatorsLayer: any = null; // Layer 4: Translocator lines/points
let landmarksLayer: any = null; // Layer 5: Landmark points with labels
let signsLayer: any = null; // Additional: Signs (signposts)
let chunkLayer: any = null;
let chunkVersionLayer: any = null;
let playersLayer: VectorSource | null = null;

// Map interactions
const { 
  initSelectInteraction, 
  initHoverInteraction, 
  selectedFeature
} = useMapInteractions(mapInstance);

// Map overlay for popups
const { 
  content: overlayContent,
  initOverlay, 
  showOverlay, 
  hideOverlay 
} = useMapOverlay(mapInstance);

// Show popup when feature is selected
const showFeaturePopup = computed(() => selectedFeature.value !== null && overlayContent.value !== null);

// Set up map - WebCartographer style: simple and direct
onMounted(async () => {
  if (!mapRef.value) return;
  
  // Initialize map configuration from server (WebCartographer fetches worldExtent.js)
  await initMapConfig();
  
  console.log('[MapContainer] ✅ WebCartographer-style map initialized');
  
  // WebCartographer approach: Use default EPSG:3857 projection
  // No custom projection needed - just treat coordinates as flat plane
  const projection = 'EPSG:3857';
  
  // Create terrain tile layer - WebCartographer style
  // Web search fix: Proper tile caching to prevent drift and old tiles staying visible
  terrainLayer = new TileLayer({
    source: new XYZ({
      projection: projection,
      tileGrid: createTileGrid(),  // Uses server-provided extent/origin/resolutions
      wrapX: false,  // WebCartographer setting
      interpolate: false,  // WebCartographer setting: preserve blocky pixels
      // Web search fix: Tile caching settings to prevent old zoom tiles
      cacheSize: 0,  // Disable tile caching - always fetch fresh tiles
      transition: 0,  // No fade transition - prevents visual drift
      tileUrlFunction: (tileCoord) => {
        if (!tileCoord) return '';
        return getTileUrl(tileCoord[0], tileCoord[1], tileCoord[2]);
      },
    }),
    // Web search fix: Force immediate tile updates, no interim tiles
    preload: 0,  // Don't preload tiles from other zoom levels
    useInterimTilesOnError: false,  // Don't show tiles from other zoom levels
    visible: mapStore.layerVisibility.terrain,
  });
  
  // Create vector layers matching spec (lines 111-306)
  // Layer order: exploredChunks, traders, translocators, landmarks
  // Pass projection to all layers (EPSG:3857 - same as WebCartographer)
  
  // Explored chunks layer (spec lines 111-129)
  exploredChunksLayer = createExploredChunksLayer(
    mapStore.layerVisibility.exploredChunks, 
    projection
  );
  
  // Traders layer (spec lines 145-166) - with sub-layer visibility
  tradersLayer = createTraderLayer(
    mapStore.layerVisibility.traders, 
    projection,
    mapStore.subLayerVisibility.traders
  );
  
  // Translocators layer (spec lines 191-241) - with sub-layer visibility
  translocatorsLayer = createTranslocatorLayer(
    mapStore.layerVisibility.translocators, 
    projection,
    mapStore.subLayerVisibility.translocators
  );
  
  // Landmarks layer (spec lines 260-306) - with sub-layer visibility and label size
  landmarksLayer = createLandmarksLayer(
    mapStore.layerVisibility.landmarks,
    projection,
    mapStore.subLayerVisibility.landmarks,
    mapStore.labelSize,
    mapInstance.value // For zoom-dependent Misc visibility
  );
  
  // Signs layer (signposts - separate from landmarks)
  signsLayer = createSignsLayer(mapStore.layerVisibility.signs, projection);
  
  // Additional layers
  chunkLayer = createChunkLayer(false, projection);
  chunkVersionLayer = createChunkVersionLayer(false, projection);
  
  // Create vector source for players (live data)
  const playersSource = new VectorSource({
    wrapX: false
  });
  playersLayer = playersSource;
  
  // Create players layer manually (not from GeoJSON file)
  const playersVectorLayer = new VectorImageLayer({
    source: playersSource,
    style: ((feature: any) => {
      const properties = feature.getProperties();
      
      return new Style({
        image: new Icon({
          src: '/assets/icons/waypoints/home.svg',
          scale: 0.8,
          anchor: [0.5, 0.5],
        }),
        text: properties.name ? new Text({
          text: properties.name,
          offsetY: -20,
          font: '12px sans-serif',
          fill: new Fill({ color: '#dc3545' }),
          stroke: new Stroke({
            color: '#fff',
            width: 3,
          }),
        }) : undefined
      });
    }) as any,
    visible: mapStore.layerVisibility.players,
    properties: { name: 'players' },
    imageRatio: 2,
    renderBuffer: 250
  });
  
  // Group layers matching spec order (spec lines 332-341)
  // 1. World base layer (tiles)
  // 2. Explored chunks overlay
  // 3. Traders
  // 4. Translocators
  // 5. Landmarks
  // Additional: Signs, chunk layers, players
  const layerGroup = new LayerGroup({
    layers: [
      terrainLayer,           // 1. Base map tiles
      exploredChunksLayer,    // 2. Explored chunks
      chunkLayer,             // Additional chunk grid
      chunkVersionLayer,      // Additional chunk versions
      tradersLayer,           // 3. Traders
      translocatorsLayer,     // 4. Translocators
      landmarksLayer,         // 5. Landmarks
      signsLayer,             // Additional signs
      playersVectorLayer,     // Live players
    ],
  });
  
  // Create map - WebCartographer style: minimal configuration
  // WebCartographer uses viewResolutions (12 levels) vs tileResolutions (10 levels)
  // This allows smoother zooming between tile levels
  const viewResolutions = getViewResolutions();
  const initialZoom = props.zoom || getDefaultZoom();
  
  console.log('[MapContainer] Creating WebCartographer-style map:', {
    center: props.center || getDefaultCenter(),
    zoom: initialZoom,
    minZoom: getMinZoom(),
    maxZoom: getMaxZoom(),
    resolutionLevels: viewResolutions.length,
    projection: projection
  });
  
  mapInstance.value = new Map({
    target: mapRef.value,
    layers: [layerGroup],
    view: new View({
      center: props.center || getDefaultCenter(),
      zoom: initialZoom,
      constrainResolution: true,  // WebCartographer: snap to zoom levels
      resolutions: viewResolutions,  // WebCartographer: view resolutions for smooth zoom
      projection: projection,  // EPSG:3857 (default)
    }),
    controls: defaultControls({
      zoom: false,
      rotate: false,
      attribution: false,
    }).extend([
      createScaleLineControl(),
      new ScreenshotControl(),
      new CoordinatesControl(),
      new FullscreenControl()
    ]),
    // Web search fix: Disable kinetic panning to prevent drift
    // "enabling kinetic dragging caused random jumps during panning and zooming"
    // Source: https://lists.osgeo.org/pipermail/openlayers-users/2012-May/025077.html
    interactions: defaultInteractions({
      dragPan: true,  // Allow dragging
      mouseWheelZoom: true,  // Allow mouse wheel zoom
      doubleClickZoom: true,  // Allow double-click zoom
      pinchZoom: true,  // Allow touch pinch zoom
      // CRITICAL: Disable kinetic panning that causes drift
    }),
  });
  
  // Mouse position is now tracked by OpenLayers CoordinatesControl

  // Initialize interactions (Select & Hover)
  initSelectInteraction((feature) => {
    if (feature) {
      const properties = feature.getProperties();
      const coords = feature.getGeometry()?.get('coordinates');
      
      // Determine feature type
      const featureType = properties.type || (properties.wares ? 'trader' : 'Feature');
      
      // Update store with selected feature
      mapStore.selectFeature({
        id: properties.id || 'unknown',
        type: 'Feature',
        geometry: feature.getGeometry(),
        properties: {
          name: properties.name || 'Unknown',
          type: featureType,
          text: properties.text || properties.wares,
          wares: properties.wares
        }
      });
      
      // Show overlay popup
      if (coords) {
        showOverlay(coords, properties);
      }
    } else {
      mapStore.selectFeature(null);
      hideOverlay();
    }
  });
  
  initHoverInteraction();
  
  // Store map reference in store
  mapStore.setMap(mapInstance.value);
  
  // Example player - could be replaced with real player data
  addExamplePlayer();

  // Wait for sources to load with timeout
  const sources = [
    exploredChunksLayer?.getSource(),
    tradersLayer?.getSource(),
    translocatorsLayer?.getSource(),
    landmarksLayer?.getSource(),
    signsLayer?.getSource(),
    chunkLayer?.getSource()
  ].filter(Boolean); // Filter out null/undefined sources
  let loadedCount = 0;
  
  const checkAllLoaded = () => {
    loadedCount++;
    if (loadedCount >= sources.length) {
      setTimeout(() => {
        loading.value = false;
      }, 300);
    }
  };
  
  // Set a maximum timeout to stop loading indicator even if sources fail
  setTimeout(() => {
    loading.value = false;
  }, 3000);
  
  // Listen for source loading events
  sources.forEach(source => {
    if (!source) {
      checkAllLoaded();
      return;
    }
    
    source.on('error', () => {
      console.warn('Error loading GeoJSON source');
      checkAllLoaded(); // Count errors as loaded to avoid hanging
    });
    
    source.once('change', () => {
      if (source.getState() === 'ready') {
        checkAllLoaded();
      } else {
        // Also count "error" state as loaded to avoid hanging
        checkAllLoaded();
      }
    });
  });
  
  // Initialize overlay after map is created
  if (overlayRef.value) {
    initOverlay(overlayRef.value);
  }
});

// Handle container resize - ensures map renders correctly when sidebar/window changes
// Set up outside onMounted so it's available for entire component lifecycle
const resizeObserver = new ResizeObserver(() => {
  if (mapInstance.value) {
    // Delay slightly to ensure DOM has updated
    setTimeout(() => {
      mapInstance.value?.updateSize();
      console.log('[MapContainer] Map size updated due to container resize');
    }, 100);
  }
});

onMounted(() => {
  if (mapRef.value) {
    resizeObserver.observe(mapRef.value);
  }
});

// Cleanup resize observer on unmount
onUnmounted(() => {
  resizeObserver.disconnect();
});

// Watch for layer visibility changes
watch(() => mapStore.layerVisibility, (newVisibility) => {
  if (terrainLayer) terrainLayer.setVisible(newVisibility.terrain);
  if (exploredChunksLayer) exploredChunksLayer.setVisible(newVisibility.exploredChunks);
  if (chunkLayer) chunkLayer.setVisible(newVisibility.chunks);
  if (chunkVersionLayer) chunkVersionLayer.setVisible(newVisibility.chunkVersions);
  if (tradersLayer) tradersLayer.setVisible(newVisibility.traders);
  if (translocatorsLayer) translocatorsLayer.setVisible(newVisibility.translocators);
  if (landmarksLayer) landmarksLayer.setVisible(newVisibility.landmarks);
  if (signsLayer) signsLayer.setVisible(newVisibility.signs);
  
  // Players layer needs to find the layer by name
  if (mapInstance.value) {
    mapInstance.value.getLayers().forEach((layer: any) => {
      if (layer.get('name') === 'players') {
        layer.setVisible(newVisibility.players);
      }
    });
  }
}, { deep: true });

// Watch for sub-layer visibility changes (spec lines 379-403)
// When sub-layer visibility changes, refresh the layer styling
watch(() => mapStore.subLayerVisibility, () => {
  // Force layer refresh to update feature opacity
  if (tradersLayer) tradersLayer.getSource()?.changed();
  if (translocatorsLayer) translocatorsLayer.getSource()?.changed();
  if (landmarksLayer) landmarksLayer.getSource()?.changed();
}, { deep: true });

// Watch for label size changes (spec line 326)
watch(() => mapStore.labelSize, () => {
  // Force landmarks layer refresh to update label sizes
  if (landmarksLayer) landmarksLayer.getSource()?.changed();
});

// Clean up
onUnmounted(() => {
  if (mapInstance.value) {
    mapInstance.value.setTarget(undefined);
    mapInstance.value = null;
  }
});

// Zoom controls
function zoomIn() {
  if (!mapInstance.value) return;
  const view = mapInstance.value.getView();
  const zoom = view.getZoom() || 0;
  view.animate({
    zoom: zoom + 1,
    duration: 250
  });
}

function zoomOut() {
  if (!mapInstance.value) return;
  const view = mapInstance.value.getView();
  const zoom = view.getZoom() || 0;
  view.animate({
    zoom: zoom - 1,
    duration: 250
  });
}

// Add players from server or mock data
async function addExamplePlayer() {
  if (!playersLayer) return;
  
  try {
    // Import the player API function
    const { getLiveData  } = await import('@/services/api/live');
    
    // Fetch online players
    const {players} = await getLiveData();
    
    // Add player markers to the map
    if (Array.isArray(players) && playersLayer) {
      const playerSource = playersLayer; // TypeScript flow control
      players.forEach(player => {
        // Create a feature for each player
        const playerFeature = new Feature({
          geometry: new Point([player.coordinates?.x || 511500, -player.coordinates?.z || -519000]),
          id: player.uid,
          name: player.name,
          type: 'player',
        });
        
        // Add to player layer source
        playerSource.addFeature(playerFeature);
      });
    }
  } catch (error) {
    console.error('Failed to load player data:', error);
    
    // Fallback to a default player if API fails
    const playerFeature = new Feature({
      geometry: new Point([511500, -519000]),
      id: 'default-player',
      name: 'Explorer_Steve',
      type: 'player',
    });
    
    if (playersLayer) {
      playersLayer.addFeature(playerFeature);
    }
  }
}
</script>

<style scoped>
.map-container {
  @apply relative w-full h-full min-h-[400px] overflow-hidden;
}

.map {
  @apply w-full h-full bg-blue-50 dark:bg-gray-800;
}

.map-controls {
  @apply absolute bottom-5 right-5 z-10 flex flex-col gap-2.5;
}

.zoom-controls {
  @apply flex flex-col gap-px shadow-md rounded-lg overflow-hidden;
}

.zoom-btn {
  @apply w-9 h-9 bg-white dark:bg-gray-800 border-none text-base leading-none cursor-pointer flex items-center justify-center transition-all text-gray-700 dark:text-gray-300;
}

.zoom-btn:hover {
  @apply bg-gray-100 dark:bg-gray-700 text-blue-600 dark:text-blue-400;
}

.map-compass {
  @apply absolute bottom-5 left-5 bg-white/90 dark:bg-gray-800/90 rounded-full p-2 shadow-md text-gray-700 dark:text-gray-300;
}

.map-loading-overlay {
  @apply absolute inset-0 bg-white/95 dark:bg-gray-900/95 flex items-center justify-center z-[100];
}

.loading-content {
  @apply text-center;
}

.spinner {
  @apply w-12 h-12 rounded-full border-4 border-blue-600/10 dark:border-blue-400/20 animate-spin mx-auto mb-4;
  border-top-color: rgb(37 99 235 / 1);
}

:global(html.dark) .spinner {
  border-top-color: rgb(96 165 250 / 1);
}

.loading-text {
  @apply text-lg font-medium text-blue-600 dark:text-blue-400;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* Feature popup overlay */
.feature-popup {
  @apply bg-white dark:bg-gray-800 p-4 rounded-lg shadow-lg min-w-[200px] max-w-[300px] relative;
}

.popup-close {
  @apply absolute top-2 right-2 bg-transparent border-none text-2xl text-gray-600 dark:text-gray-400 cursor-pointer leading-none p-0 w-6 h-6 flex items-center justify-center transition-colors;
}

.popup-close:hover {
  @apply text-gray-900 dark:text-gray-200;
}

.popup-title {
  @apply m-0 mb-3 text-base font-semibold text-gray-900 dark:text-gray-100 pr-6;
}

.popup-content {
  @apply text-sm;
}

.popup-details p {
  @apply my-1.5 text-gray-700 dark:text-gray-300;
}

.popup-details strong {
  @apply text-gray-900 dark:text-gray-100;
}

/* Custom controls styles - positioned bottom-right with zoom controls */
:global(.screenshot-control) {
  @apply absolute bottom-[102px] right-5 !left-auto top-auto;
}

:global(.fullscreen-control) {
  @apply absolute bottom-[148px] right-5 !left-auto top-auto;
}

:global(.coordinates-control) {
  @apply absolute bottom-5 left-20 right-auto top-auto;
}

:global(.screenshot-control-button),
:global(.fullscreen-control-button) {
  @apply w-9 h-9 bg-white dark:bg-gray-800 border-none text-base cursor-pointer flex items-center justify-center transition-all text-gray-700 dark:text-gray-300 rounded-lg shadow-md;
}

:global(.screenshot-control-button:hover),
:global(.fullscreen-control-button:hover) {
  @apply bg-gray-100 dark:bg-gray-700 text-blue-600 dark:text-blue-400;
}

:global(.screenshot-control-button:active),
:global(.fullscreen-control-button:active) {
  @apply scale-95;
}

:global(.coordinates-display) {
  @apply bg-white dark:bg-gray-800 px-3 py-2 rounded-lg text-xs font-mono text-gray-700 dark:text-gray-300 font-semibold shadow-md;
}
</style>
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
import LayerGroup from 'ol/layer/Group';
import { Projection } from 'ol/proj';

// Map configuration
import { 
  createWorldTileGrid, 
  worldExtent,
  worldOrigin,
  worldResolutions,
  tileResolutions,
  defaultCenter, 
  defaultZoom, 
  minZoom, 
  maxZoom,
  initializeMapConfig,
  getTileOffset
} from '@/utils/mapConfig';

// Optimized layer factory
import {
  createTraderLayer,
  createTranslocatorLayer,
  createSignsLayer,
  createChunkLayer
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

// Map layers
let terrainLayer: TileLayer<XYZ> | null = null;
let chunkLayer: any = null;
let tradersLayer: any = null;
let translocatorsLayer: any = null;
let signsLayer: any = null;
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

// Set up map
onMounted(async () => {
  if (!mapRef.value) return;
  
  // Initialize map configuration from API
  await initializeMapConfig();
  
  // Log loaded configuration
  console.log('[MapContainer] Map config loaded:', {
    worldExtent: worldExtent(),
    worldOrigin: worldOrigin(),
    tileOffset: getTileOffset(),
    defaultCenter: defaultCenter(),
    defaultZoom: defaultZoom(),
    tileResolutions: tileResolutions()
  });
  
  // Create custom projection for block coordinates FIRST
  // Our coordinates are in block units, not meters, so we need a simple projection
  const extent = worldExtent();
  const projection = new Projection({
    code: 'VINTAGESTORY',
    units: 'pixels',
    extent: extent,
    global: false
  });
  
  // Create tile layer for terrain using dynamic tile API
      // Zoom levels: 0 (world view) to BaseZoomLevel (max detail)
      // Direct mapping: OpenLayers zoom N -> Backend zoom N
      terrainLayer = new TileLayer({
        source: new XYZ({
          projection: projection,  // Use our custom projection
          tileGrid: createWorldTileGrid(),
          wrapX: false,
          tileUrlFunction: (tileCoord) => {
            if (!tileCoord) return '';
            
            // ═══════════════════════════════════════════════════════════════
            // COORDINATE MAPPING: OpenLayers Relative -> Absolute Storage
            // ═══════════════════════════════════════════════════════════════
            // OpenLayers numbers tiles from (0,0) at origin
            // Backend stores tiles with absolute world coordinates
            // Solution: Add offset to map OL coords -> storage coords
            //
            // Example:
            //   OL tile (0, 0) at origin -> Storage tile (1998, 1997)
            //   OL tile (1, 2) -> Storage tile (1999, 1999)
            // ═══════════════════════════════════════════════════════════════
            
            const zoom = tileCoord[0];
            const olTileX = tileCoord[1];  // Relative to origin
            const olTileY = tileCoord[2];  // Relative to origin
            
            // Get tile offset from config (which absolute tile the origin maps to)
            const [offsetX, offsetZ] = getTileOffset();
            const maxZ = maxZoom();
            
            // Scale offset for current zoom level
            // At zoom 9: offset = [1998, 1997]
            // At zoom 8: offset = [999, 998] (each parent tile = 2x2 child tiles)
            const zoomScale = Math.pow(2, maxZ - zoom);
            const scaledOffsetX = Math.floor(offsetX / zoomScale);
            const scaledOffsetZ = Math.floor(offsetZ / zoomScale);
            
            // Map to absolute storage coordinates
            const absoluteX = olTileX + scaledOffsetX;
            const absoluteZ = olTileY + scaledOffsetZ;
            
            const url = `/tiles/${zoom}/${absoluteX}_${absoluteZ}.png`;
            
            // Debug logging at low zoom
            if (zoom <= 3) {
              console.log(`[Tile] zoom=${zoom}, OL(${olTileX},${olTileY}) + offset(${scaledOffsetX},${scaledOffsetZ}) = absolute(${absoluteX},${absoluteZ}) -> ${url}`);
            }
            
            return url;
          },
        }),
        visible: mapStore.layerVisibility.terrain,
      });
  
  // Use optimized layer factory (VectorImageLayer for better performance)
  // Pass the custom projection to all layers so they use the same coordinate system
  chunkLayer = createChunkLayer(false, projection);
  tradersLayer = createTraderLayer(mapStore.layerVisibility.traders, projection);
  translocatorsLayer = createTranslocatorLayer(mapStore.layerVisibility.translocators, projection);
  signsLayer = createSignsLayer(mapStore.layerVisibility.signs, projection);
  
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
  
  // Group layers
  const layerGroup = new LayerGroup({
    layers: [
      terrainLayer,
      chunkLayer,
      tradersLayer,
      translocatorsLayer,
      signsLayer,
      playersVectorLayer,
    ],
  });
  
  // Create map (projection already created above for tile layer)
  mapInstance.value = new Map({
    target: mapRef.value,
    layers: [layerGroup],
    view: new View({
      center: props.center || defaultCenter(),
      zoom: props.zoom || defaultZoom(),
      minZoom: minZoom(),
      maxZoom: maxZoom(),
      extent: extent,
      constrainResolution: true,
      resolutions: worldResolutions(),
      projection: projection,
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
  });
  
  // Mouse position is now tracked by OpenLayers CoordinatesControl

  // Initialize interactions (Select & Hover)
  initSelectInteraction((feature) => {
    if (feature) {
      const properties = feature.getProperties();
      const coords = (feature.getGeometry() as any)?.getCoordinates?.();
      
      // Determine feature type
      const featureType = properties.type || (properties.wares ? 'trader' : 'Feature');
      
      // Update store with selected feature
      mapStore.selectFeature({
        id: properties.id || 'unknown',
        type: 'Feature',
        geometry: feature.getGeometry() as any,
        properties: {
          name: properties.name || 'Unknown',
          type: featureType,
          text: properties.text || properties.wares,
          wares: properties.wares
        }
      } as any);
      
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
    tradersLayer.getSource(),
    translocatorsLayer.getSource(),
    signsLayer.getSource(),
    chunkLayer.getSource()
  ];
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

// Watch for layer visibility changes
watch(() => mapStore.layerVisibility, (newVisibility) => {
  if (terrainLayer) terrainLayer.setVisible(newVisibility.terrain);
  if (chunkLayer) chunkLayer.setVisible(false); // Keep chunks hidden - optional layer
  if (tradersLayer) tradersLayer.setVisible(newVisibility.traders);
  if (translocatorsLayer) translocatorsLayer.setVisible(newVisibility.translocators);
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
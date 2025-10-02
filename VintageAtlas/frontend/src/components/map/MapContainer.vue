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

    <!-- Live controls -->
    <LiveControls />
    
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

// Map configuration
import { 
  createWorldTileGrid, 
  worldExtent, 
  worldResolutions, 
  defaultCenter, 
  defaultZoom, 
  minZoom, 
  maxZoom 
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
import LiveControls from '@/components/live/LiveControls.vue';
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
onMounted(() => {
  if (!mapRef.value) return;
  
// Create tile layer for terrain using dynamic tile API
      // Note: Tile directories are 1-9, but OpenLayers zoom is 0-9, so we add 1
      terrainLayer = new TileLayer({
        source: new XYZ({
          tileGrid: createWorldTileGrid(),
          wrapX: false,
          tileUrlFunction: (tileCoord) => {
            if (!tileCoord) return '';
            const z = tileCoord[0] + 1; // Adjust zoom: OL zoom 0->directory 1, etc.
            const x = tileCoord[1];
            const y = tileCoord[2];
            return `/tiles/${z}/${x}_${-y-1}.png`; // Dynamic tile generation API
          },
        }),
        visible: mapStore.layerVisibility.terrain,
      });
  
  // Use optimized layer factory (VectorImageLayer for better performance)
  chunkLayer = createChunkLayer(false);
  tradersLayer = createTraderLayer(mapStore.layerVisibility.traders);
  translocatorsLayer = createTranslocatorLayer(mapStore.layerVisibility.translocators);
  signsLayer = createSignsLayer(mapStore.layerVisibility.signs);
  
  // Create vector source for players (live data)
  const playersSource = new VectorSource();
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
  
  // Create map
  mapInstance.value = new Map({
    target: mapRef.value,
    layers: [layerGroup],
    view: new View({
      center: props.center || defaultCenter(),
      zoom: props.zoom || defaultZoom(),
      minZoom: minZoom(),
      maxZoom: maxZoom(),
      extent: worldExtent(),
      constrainResolution: true,
      resolutions: worldResolutions(),
      projection: 'EPSG:3857',
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
  position: relative;
  width: 100%;
  height: 100%;
  min-height: 400px;
  overflow: hidden;
}

.map {
  width: 100%;
  height: 100%;
  background-color: #e9f2fa;
}

.map-controls {
  position: absolute;
  bottom: 20px;
  right: 20px;
  z-index: 1;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.zoom-controls {
  display: flex;
  flex-direction: column;
  gap: 1px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
  border-radius: 8px;
  overflow: hidden;
}

.zoom-btn {
  width: 36px;
  height: 36px;
  background-color: white;
  border: none;
  font-size: 16px;
  line-height: 1;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
  color: #495057;
}

.zoom-btn:hover {
  background-color: #f8f9fa;
  color: #4285f4;
}

/* Coordinates now displayed by OpenLayers control */

.map-compass {
  position: absolute;
  bottom: 20px;
  left: 20px;
  background-color: rgba(255, 255, 255, 0.9);
  border-radius: 50%;
  padding: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
  color: #495057;
}

.map-loading-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(255, 255, 255, 0.95);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 100;
}

.loading-content {
  text-align: center;
}

.spinner {
  width: 50px;
  height: 50px;
  border-radius: 50%;
  border: 4px solid rgba(13, 110, 253, 0.1);
  border-top-color: #0d6efd;
  animation: spin 1s linear infinite;
  margin: 0 auto 16px;
}

.loading-text {
  font-size: 18px;
  font-weight: 500;
  color: #0d6efd;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* Dark mode */
:global(html.dark) .map {
  background-color: #102a43;
}

:global(html.dark) .zoom-btn {
  background-color: #2c2c2c;
  color: #ced4da;
}

:global(html.dark) .zoom-btn:hover {
  background-color: #3c3c3c;
  color: #90caf9;
}

/* Dark mode coordinates removed - using OpenLayers control */

:global(html.dark) .map-compass {
  background-color: rgba(44, 44, 44, 0.9);
  color: #ced4da;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
}

:global(html.dark) .map-loading-overlay {
  background-color: rgba(18, 18, 18, 0.95);
}

:global(html.dark) .spinner {
  border-color: rgba(144, 202, 249, 0.1);
  border-top-color: #90caf9;
}

:global(html.dark) .loading-text {
  color: #90caf9;
}

/* Feature popup overlay */
.feature-popup {
  background-color: white;
  padding: 16px;
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  min-width: 200px;
  max-width: 300px;
  position: relative;
}

.popup-close {
  position: absolute;
  top: 8px;
  right: 8px;
  background: none;
  border: none;
  font-size: 24px;
  color: #666;
  cursor: pointer;
  line-height: 1;
  padding: 0;
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.popup-close:hover {
  color: #333;
}

.popup-title {
  margin: 0 0 12px 0;
  font-size: 16px;
  font-weight: 600;
  color: #333;
  padding-right: 24px;
}

.popup-content {
  font-size: 14px;
}

.popup-details p {
  margin: 6px 0;
  color: #495057;
}

.popup-details strong {
  color: #333;
}

/* Dark mode for popup */
:global(html.dark) .feature-popup {
  background-color: #2c2c2c;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
}

:global(html.dark) .popup-close {
  color: #ced4da;
}

:global(html.dark) .popup-close:hover {
  color: #fff;
}

:global(html.dark) .popup-title {
  color: #e0e0e0;
}

:global(html.dark) .popup-details p {
  color: #ced4da;
}

:global(html.dark) .popup-details strong {
  color: #e0e0e0;
}

/* Custom controls styles - positioned bottom-right next to zoom */
:global(.screenshot-control) {
  bottom: 20px;
  right: 74px; /* Next to zoom controls */
  top: auto;
}

:global(.fullscreen-control) {
  bottom: 20px;
  right: 118px; /* Next to screenshot */
  top: auto;
}

:global(.coordinates-control) {
  bottom: 20px;
  right: 162px; /* Next to fullscreen */
  top: auto;
}

:global(.screenshot-control-button),
:global(.fullscreen-control-button) {
  width: 36px;
  height: 36px;
  background-color: white;
  border: none;
  font-size: 18px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
  color: #495057;
  border-radius: 4px;
}

:global(.screenshot-control-button:hover),
:global(.fullscreen-control-button:hover) {
  background-color: #f8f9fa;
  color: #4285f4;
}

:global(.coordinates-display) {
  background-color: white;
  padding: 8px 12px;
  border-radius: 4px;
  font-size: 12px;
  font-family: monospace;
  color: #495057;
  font-weight: 600;
}

:global(html.dark .screenshot-control-button),
:global(html.dark .fullscreen-control-button) {
  background-color: #2c2c2c;
  color: #ced4da;
}

:global(html.dark .screenshot-control-button:hover),
:global(html.dark .fullscreen-control-button:hover) {
  background-color: #3c3c3c;
  color: #90caf9;
}

:global(html.dark .coordinates-display) {
  background-color: #2c2c2c;
  color: #ced4da;
}
</style>
<template>
  <div class="ol-map-container">
    <!-- Map element -->
    <div ref="mapElement" class="ol-map"></div>
    
    <!-- Live data layers -->
    <PlayerLayer v-if="mapInstance" />
    <AnimalLayer v-if="mapInstance" />

    <!-- Tools Bar overlay -->
    <ToolsBar v-if="mapInstance" />
    
    <!-- Mouse position display (Spec lines 510-520) -->
    <div class="ol-coords">{{ mouseCoords }}</div>
    
    <!-- Loading indicator -->
    <div v-if="loading" class="ol-loading">
      <div class="spinner"></div>
      <p>Loading map...</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, shallowRef } from 'vue';
import Map from 'ol/Map';
import View from 'ol/View';
import { MousePosition } from 'ol/control';
import { useMapStore } from '@/stores/map';

// Live data layer components
import ToolsBar from '@/components/map/ToolsBar.vue';
import { getDeepLinkOverrides, startDeepLinkSync } from '@/utils/deeplink';

// Clean OL utilities
import { 
  initOLConfig,
  getViewCenter, 
  getViewZoom,
  getViewExtent,
  formatCoords
} from '@/utils/olMapConfig';

import {
  createWorldLayer,
  createExploredChunksLayer,
  createTradersLayer,
  createTranslocatorsLayer,
  createLandmarksLayer
} from '@/utils/olLayers';
import { toStringXY } from 'ol/coordinate';

// Refs
const mapElement = ref<HTMLElement>();
const mapInstance = shallowRef<Map>();
const loading = ref(true);
const mouseCoords = ref('0, 0');

// Initialize map
onMounted(async () => {
  if (!mapElement.value) return;
  
  try {
    // Load config from server
    await initOLConfig();
    
    // Create layers in order (Spec lines 332-341)
    const worldLayer = createWorldLayer();
    const exploredChunksLayer = createExploredChunksLayer();
    const tradersLayer = createTradersLayer();
    const translocatorsLayer = createTranslocatorsLayer();
    
    mapInstance.value = new Map({
      target: mapElement.value,
      controls: [
        new MousePosition({
          coordinateFormat: function (coordinate) {
            if(!coordinate) return '0, 0';
            
            // WebCartographer-style coordinate format
            return toStringXY([coordinate[0], -coordinate[1]], 0);
          },
          className: 'coords',
          target: 'mousePos',
        }),
      ],
      layers: [
        worldLayer,
        // chunkVersionLayer,
        exploredChunksLayer,
        tradersLayer,
        translocatorsLayer,
        // landmarksLayer
      ],
      view: new View({
        center: getViewCenter(),
        constrainResolution: true, // Snap to zoom levels - same as WebCartographer
        extent: getViewExtent(),    // Constrain panning/requests to world extent
        constrainOnlyCenter: false,
        multiWorld: false,
        zoom: getViewZoom(), // WebCartographer default zoom level
        resolutions: [256, 128, 64, 32, 16, 8, 4, 2, 1, 0.5, 0.25, 0.125], // WebCartographer-style fixed resolutions
      })
    });

    const mapStore = useMapStore();


    // Store in global map store for shared controls
    mapStore.setMap(mapInstance.value);

    // Apply deep-link overrides (if present) and start URL sync
    const overrides = getDeepLinkOverrides();
    if (overrides.center) {
      mapStore.setCenter(overrides.center);
    }
    if (overrides.zoom !== undefined) {
      mapStore.setZoom(overrides.zoom);
    }
    startDeepLinkSync(mapInstance.value!);
    
    // Add landmarks layer after map is created (needs map instance for zoom)
    const landmarksLayer = createLandmarksLayer(mapInstance.value);
    mapInstance.value.addLayer(landmarksLayer);
    
    // Mouse position tracking (Spec lines 510-520)
    mapInstance.value.on('pointermove', (evt) => {
      if (evt.coordinate) {
        mouseCoords.value = formatCoords(evt.coordinate as [number, number]);
      }
    });
    
    // URL state management (Spec lines 523-531)
    mapInstance.value.on('moveend', () => {
      const view = mapInstance.value?.getView();
      if (view) {
        const center = view.getCenter();
        const zoom = view.getZoom();
        if (center && zoom !== undefined) {
          const url = `?x=${Math.round(center[0])}&y=${Math.round(center[1])}&zoom=${zoom}`;
          window.history.pushState({}, '', url);
        }
      }
    });
    
    loading.value = false;
    
    // Store map instance for child components
    // (PlayerLayer and AnimalLayer will access it via mapStore)
    mapStore.setMap(mapInstance.value);
    
  } catch (error) {
    console.error('[CleanMap] ❌ Failed to initialize:', error);
    loading.value = false;
  }
});

// Cleanup
onUnmounted(() => {
  if (mapInstance.value) {
    mapInstance.value.setTarget(undefined);
    mapInstance.value = undefined;
  }
});
</script>

<style scoped>
.ol-map-container {
  position: relative;
  width: 100%;
  height: 100%;
  min-height: 400px;
}

.ol-map {
  width: 100%;
  height: 100%;
  background: #e8f4f8;
}

.ol-coords {
  position: absolute;
  bottom: 16px;
  left: 16px;
  background: rgba(255, 255, 255, 0.9);
  padding: 8px 12px;
  border-radius: 4px;
  font-family: monospace;
  font-size: 12px;
  font-weight: 600;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
  z-index: 10;
}

.ol-loading {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(255, 255, 255, 0.95);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  z-index: 100;
}

.spinner {
  width: 48px;
  height: 48px;
  border: 4px solid rgba(59, 130, 246, 0.2);
  border-top-color: rgb(59, 130, 246);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

.ol-loading p {
  margin-top: 16px;
  font-weight: 600;
  color: rgb(59, 130, 246);
}
</style>

<template>
  <div class="ol-map-container">
    <!-- Map element -->
    <div ref="mapElement" class="ol-map"></div>

    <!-- Calendar Display -->
    <CalendarDisplay />

    <!-- Map Layers Overlay -->
    <MapLayersOverlay v-if="mapInstance" />

    <!-- Tools Bar overlay -->
    <ToolsBar v-if="mapInstance" />
    
    <!-- Feature Tooltip -->
    <FeatureTooltip ref="tooltipRef" />
    
    <!-- Mouse position display (Spec lines 510-520) -->
    <div class="ol-coords">
      <div>{{ mouseCoords }}</div>
    </div>
    
    <!-- Loading indicator -->
    <div v-if="loading" class="ol-loading">
      <div class="spinner"></div>
      <p>Loading map...</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, shallowRef, watch } from 'vue';
import { useServerStore } from '@/stores/server';
import { usePlayerInterpolation } from '@/composables/usePlayerInterpolation';
import { storeToRefs } from 'pinia';
import Map from 'ol/Map';
import View from 'ol/View';
import TileLayer from 'ol/layer/Tile';
import VectorLayer from 'ol/layer/Vector';
import VectorSource from 'ol/source/Vector';
import XYZ from 'ol/source/XYZ';
import { fromLonLat, transform } from 'ol/proj';
import Feature from 'ol/Feature';
import Point from 'ol/geom/Point';
import { Style, Icon, Text, Fill, Stroke } from 'ol/style';
import { defaults as defaultControls } from 'ol/control';
import type { MapBrowserEvent } from 'ol';
import { useMapStore } from '@/stores/map';

// Live data layer components
import CalendarDisplay from '@/components/map/CalendarDisplay.vue';
import MapLayersOverlay from '@/components/map/MapLayersOverlay.vue';
import ToolsBar from '@/components/map/ToolsBar.vue';
import FeatureTooltip from '@/components/map/FeatureTooltip.vue';
import { getDeepLinkOverrides, startDeepLinkSync } from '@/utils/deeplink';

// Clean OL utilities
import { 
  initOLConfig,
  getViewCenter, 
  getViewZoom,
  getViewExtent,
  formatCoords,
  getViewResolutions
} from '@/utils/olMapConfig';

import {
  createWorldLayer,
  createTradersLayer,
  createPlayersLayer,
  createSpawnLayer,
  createWaypointsLayer,
  createTemperatureLayer,
  createRainfallLayer,

} from '@/utils/olLayers';
import { getWaypoints } from '@/services/api/waypoints';

// Refs
const mapElement = ref<HTMLElement>();
const mapInstance = shallowRef<Map>();
const loading = ref(true);
const mouseCoords = ref('0, 0');
const tooltipRef = ref<InstanceType<typeof FeatureTooltip> | null>(null);

// Initialize map
onMounted(async () => {
  if (!mapElement.value) return;
  
  try {
    // Load config from server
    await initOLConfig();
    
    const mapStore = useMapStore();
    const serverStore = useServerStore();
    
    // Start polling for player data
    serverStore.startPolling();
    
    // Create layers in order (Spec lines 332-341)
    const worldLayer = createWorldLayer();
    // Pass a getter function so the style can read current visibility dynamically
    const tradersLayer = createTradersLayer((category: string) => {
      const traders = mapStore.subLayerVisibility.traders as Record<string, boolean>;
      return traders[category] !== false;
    });
    
    // Get spawn position from config
    const mapConfig = await import('@/services/api/mapConfig').then(m => m.fetchMapConfig());
    const spawnLayer = createSpawnLayer(mapConfig.spawnPosition[0], mapConfig.spawnPosition[1]);
    
    // Create players layer
    const playersLayer = createPlayersLayer();

    const waypointsLayer = createWaypointsLayer();
    const temperatureLayer = createTemperatureLayer();
    const rainfallLayer = createRainfallLayer();
    
    mapInstance.value = new Map({
      target: mapElement.value,
      controls: defaultControls({
        attribution: true,
        zoom: true,
        rotate: false
      }),
      layers: [
        worldLayer,
        tradersLayer,
        spawnLayer,
        playersLayer,
        waypointsLayer,
        temperatureLayer,
        rainfallLayer
      ],
      view: new View({
        center: getViewCenter(),
        constrainResolution: true, // Snap to zoom levels - same as WebCartographer
        extent: getViewExtent(),    // Constrain panning/requests to world extent
        constrainOnlyCenter: false,
        multiWorld: false,
        zoom: getViewZoom(), // WebCartographer default zoom level
        resolutions: getViewResolutions(), // WebCartographer-style fixed resolutions
      })
    });

    // Store in global map store for shared controls
    mapStore.setMap(mapInstance.value);
    
    // Apply initial layer visibility from store
    playersLayer.setVisible(mapStore.layerVisibility.players);
    spawnLayer.setVisible(mapStore.layerVisibility.spawn);
    tradersLayer.setVisible(mapStore.layerVisibility.traders);
    worldLayer.setVisible(mapStore.layerVisibility.terrain);
    waypointsLayer.setVisible(mapStore.layerVisibility.waypoints);
    temperatureLayer.setVisible(mapStore.layerVisibility.temperature);
    rainfallLayer.setVisible(mapStore.layerVisibility.rainfall);
    
    console.log('[MapContainer] Layer visibility:', {
      players: playersLayer.getVisible(),
      spawn: spawnLayer.getVisible(),
      traders: tradersLayer.getVisible(),
      waypoints: waypointsLayer.getVisible(),
      terrain: worldLayer.getVisible()
    });

    // Fetch and display waypoints
    try {
      const waypointsData = await getWaypoints();
      const source = waypointsLayer.getSource();
      if (source && waypointsData.waypoints) {
        const features = waypointsData.waypoints.map(wp => {
          const feature = new Feature({
            geometry: new Point([wp.x, -wp.y]), // Invert Y
            title: wp.title,
            color: wp.color,
            icon: wp.icon,
            pinned: wp.pinned,
            owner: wp.owner
          });
          return feature;
        });
        source.addFeatures(features);
      }
    } catch (err) {
      console.error('Failed to load waypoints:', err);
    }
    
    // Setup player interpolation
    const { interpolatedPlayers } = usePlayerInterpolation(() => serverStore.players);
    
    // Watch interpolated players to update map features
    watch(interpolatedPlayers, (players) => {
      if (!playersLayer) return;

      const source = playersLayer.getSource();
      if (!source) return;

      source.clear();

      const features = players.map(player => {
        const feature = new Feature({
          geometry: new Point([player.currentX, -player.currentY]), // Invert Y coordinate for OpenLayers
          name: player.name,
          uid: player.uid,
          yaw: player.currentYaw,
          pitch: player.pitch
        });

        feature.setId(player.uid);
        
        // Style for player marker
        feature.setStyle(createPlayerStyle(player));

        return feature;
      });

      source.addFeatures(features);
    }, { deep: true });

    function createPlayerStyle(player: any) {
        // Create a directional arrow based on yaw
        // VS Yaw: 0 is North (?), rotates clockwise? 
        // OpenLayers rotation is clockwise radians.
        // Need to verify VS yaw orientation. Usually 0=North, PI/2=East.
        
        return new Style({
            image: new Icon({
                src: '/icons/player_arrow.png', // We need this icon! Or use a vector shape
                rotation: player.currentYaw,
                rotateWithView: true,
                scale: 0.8
            }),
            text: new Text({
                text: player.name,
                offsetY: -20,
                fill: new Fill({ color: '#ffffff' }),
                stroke: new Stroke({ color: '#000000', width: 3 }),
                font: '12px Inter, sans-serif'
            })
        });
    }
    
    // Watch for sublayer visibility changes and refresh the trader layer
    // Use requestAnimationFrame to batch updates and reduce flickering
    let rafId: number | null = null;
    watch(() => mapStore.subLayerVisibility.traders, () => {
      if (rafId !== null) {
        cancelAnimationFrame(rafId);
      }
      
      rafId = requestAnimationFrame(() => {
        const layers = mapInstance.value?.getLayers().getArray();
        const tradersLayer = layers?.find(layer => layer.get('name') === 'traders');
        if (tradersLayer) {
          // Just trigger a render update on the map instead of the layer
          mapInstance.value?.render();
        }
        rafId = null;
      });
    }, { deep: true });

    // Apply deep-link overrides (if present) and start URL sync
    const overrides = getDeepLinkOverrides();
    if (overrides.center) {
      mapStore.setCenter(overrides.center);
    }
    if (overrides.zoom !== undefined) {
      mapStore.setZoom(overrides.zoom);
    }
    startDeepLinkSync(mapInstance.value!);
    
    // Mouse position tracking and hover tooltips (Spec lines 510-520)
    mapInstance.value.on('pointermove', (evt: MapBrowserEvent<any>) => {
      if (evt.coordinate) {
        mouseCoords.value = formatCoords(evt.coordinate as [number, number]);
      }
      
      // Show tooltip on feature hover
      const pixel = evt.pixel;
      const features = mapInstance.value?.getFeaturesAtPixel(pixel);
      
      if (features && features.length > 0 && tooltipRef.value) {
        const feature = features[0];
        const properties = feature.getProperties();
        
        // Only show tooltip for traders (or other relevant features)
        if (properties.wares || properties.name || properties.title) {
          tooltipRef.value.show(properties, evt.originalEvent.clientX, evt.originalEvent.clientY);
        } else {
          tooltipRef.value.hide();
        }
      } else if (tooltipRef.value) {
        tooltipRef.value.hide();
      }
    });
    
    loading.value = false;
    
  } catch (error) {
    console.error('[CleanMap] ❌ Failed to initialize:', error);
    loading.value = false;
  }
});

// Cleanup
onUnmounted(() => {
  const serverStore = useServerStore();
  serverStore.stopPolling();
  
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
  /* Parchment background color */
  background-color: #d4b896;
  /* Noise texture overlay */
  background-image:
    url('/assets/noise.svg'),
    /* CSS fallback texture */
    repeating-linear-gradient(0deg,
      transparent,
      transparent 2px,
      rgba(0, 0, 0, 0.03) 2px,
      rgba(0, 0, 0, 0.03) 3px),
    repeating-linear-gradient(90deg,
      transparent,
      transparent 2px,
      rgba(0, 0, 0, 0.03) 2px,
      rgba(0, 0, 0, 0.03) 3px);
  background-repeat: repeat, repeat, repeat;
  background-size: 200px 200px, 3px 3px, 3px 3px;
}

.ol-coords {
  position: absolute;
  bottom: 16px;
  left: 16px;
  background: rgba(255, 255, 255, 0.95);
  padding: 10px 14px;
  border-radius: 6px;
  font-family: monospace;
  font-size: 12px;
  font-weight: 600;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
  z-index: 10;
}

.ol-coords > div {
  line-height: 1.6;
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
  to {
    transform: rotate(360deg);
  }
}

.ol-loading p {
  margin-top: 16px;
  font-weight: 600;
  color: rgb(59, 130, 246);
}
</style>

<style>
/* Global styles for OpenLayers controls - not scoped */

/* Position zoom controls at bottom-right */
.ol-zoom {
  position: absolute;
  bottom: 16px !important;
  right: 16px !important;
  top: auto !important;
  left: auto !important;
  display: flex;
  flex-direction: column;
  gap: 8px;
  background: transparent !important;
  padding: 0 !important;
  border-radius: 0 !important;
}

/* Style zoom buttons to match theme */
.ol-zoom button {
  width: 40px !important;
  height: 40px !important;
  background: rgba(30, 41, 59, 0.95) !important;
  backdrop-filter: blur(8px);
  border: none !important;
  border-radius: 8px !important;
  color: #fff !important;
  font-size: 18px !important;
  font-weight: bold !important;
  cursor: pointer !important;
  display: flex !important;
  align-items: center !important;
  justify-content: center !important;
  transition: all 0.2s !important;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3) !important;
  margin: 0 !important;
}

.ol-zoom button:hover {
  background: rgba(30, 41, 59, 1) !important;
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4) !important;
}

.ol-zoom button:active {
  transform: translateY(0);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3) !important;
}

.ol-zoom button:focus {
  outline: none !important;
}

/* Hide attribution control or style it */
.ol-attribution {
  bottom: 0 !important;
  right: 0 !important;
  background: rgba(30, 41, 59, 0.8) !important;
  backdrop-filter: blur(4px);
  border-radius: 4px 0 0 0 !important;
  padding: 2px 8px !important;
  font-size: 10px !important;
}

.ol-attribution ul {
  color: rgba(255, 255, 255, 0.7) !important;
  text-shadow: none !important;
}

.ol-attribution button {
  color: rgba(255, 255, 255, 0.7) !important;
}
</style>

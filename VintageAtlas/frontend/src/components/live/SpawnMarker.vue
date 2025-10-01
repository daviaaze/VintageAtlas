<template>
  <div class="spawn-marker">
    <!-- This is just a wrapper component, actual rendering is done by OpenLayers -->
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, watch } from 'vue';
import { useLiveStore } from '@/stores/live';
import { useMapStore } from '@/stores/map';
import type { FeatureLike } from 'ol/Feature';
import VectorLayer from 'ol/layer/Vector';
import VectorSource from 'ol/source/Vector';
import Feature from 'ol/Feature';
import Point from 'ol/geom/Point';
import { Style, Fill, Stroke, Text } from 'ol/style';

// Stores
const liveStore = useLiveStore();
const mapStore = useMapStore();

// Local state
let spawnLayer: VectorLayer<VectorSource> | null = null;
let spawnSource: VectorSource | null = null;

// Create the spawn marker layer
function createSpawnLayer() {
  spawnSource = new VectorSource();
  
  spawnLayer = new VectorLayer({
    source: spawnSource,
    zIndex: 1001, // Above everything else
    properties: { name: 'spawn' },
    style: (function(_feature: FeatureLike) {
      const styles: Style[] = [];
      
      // Spawn emoji
      styles.push(new Style({
        text: new Text({
          text: '📍',
          font: '20px sans-serif',
          textAlign: 'center',
          textBaseline: 'middle',
          offsetX: 0,
          offsetY: 0
        })
      }));
      
      // Spawn text
      styles.push(new Style({
        text: new Text({
          text: 'Spawn',
          font: '12px sans-serif',
          textAlign: 'center',
          textBaseline: 'bottom',
          offsetY: -40,
          backgroundFill: new Fill({ color: 'rgba(0,0,0,0.45)' }),
          padding: [2, 4, 2, 4],
          fill: new Fill({ color: '#FFFFFF' }),
          stroke: new Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
        })
      }));
      
      // Temperature if available
      const temp = liveStore.spawnTemperature;
      if (temp !== undefined && temp !== null) {
        styles.push(new Style({
          text: new Text({
            text: `🌡️ ${Number(temp).toFixed(1)}°C`,
            font: '10px sans-serif',
            textAlign: 'center',
            textBaseline: 'top',
            offsetY: 16,
            backgroundFill: new Fill({ color: 'rgba(0,0,0,0.45)' }),
            padding: [1, 3, 1, 3],
            fill: new Fill({ color: '#FFFFFF' }),
            stroke: new Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
          })
        }));
      }
      
      // Rainfall if available
      const rainfall = liveStore.spawnRainfall;
      if (rainfall !== undefined && rainfall !== null) {
        styles.push(new Style({
          text: new Text({
            text: `💧 ${Number(rainfall).toFixed(2)}`,
            font: '10px sans-serif',
            textAlign: 'center',
            textBaseline: 'top',
            offsetY: 30,
            backgroundFill: new Fill({ color: 'rgba(0,0,0,0.45)' }),
            padding: [1, 3, 1, 3],
            fill: new Fill({ color: '#FFFFFF' }),
            stroke: new Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
          })
        }));
      }
      
      return styles;
    }) as any
  });
  
  // Add the layer to the map
  mapStore.map?.addLayer(spawnLayer);
  
  // Add spawn point feature
  updateSpawnMarker();
}

// Update the spawn marker
function updateSpawnMarker() {
  if (!spawnSource) return;
  
  // Clear existing features
  spawnSource.clear();
  
  // Convert spawn point coordinates to map coordinates
  const coords = liveStore.worldToMapCoords(liveStore.spawnPoint);
  const feature = new Feature({
    geometry: new Point(coords)
  });
  
  spawnSource.addFeature(feature);
}

// Watch for changes in spawn temperature
watch(() => liveStore.spawnTemperature, () => {
  if (spawnSource) {
    spawnSource.refresh();
  }
});

// Watch for changes in spawn rainfall
watch(() => liveStore.spawnRainfall, () => {
  if (spawnSource) {
    spawnSource.refresh();
  }
});

// Lifecycle hooks
onMounted(() => {
  if (mapStore.map) {
    createSpawnLayer();
  }
});

onUnmounted(() => {
  if (mapStore.map && spawnLayer) {
    mapStore.map.removeLayer(spawnLayer);
  }
});
</script>

<style scoped>
/* No styles needed as rendering is handled by OpenLayers */
</style>

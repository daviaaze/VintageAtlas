<template>
  <div class="map-view">
        <div class="search-overlay">
          <SearchFeatures :min-search-length="2" />
        </div>
        <MapContainer />
    
    <div v-if="serverStore.loading" class="status-overlay loading">
      <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="spinner"><path d="M21 12a9 9 0 1 1-6.219-8.56"></path></svg>
      Loading server status...
    </div>
    
    <div v-if="selectedFeature" class="feature-info">
      <h3>{{ selectedFeature.name }}</h3>
      <div class="feature-details">
        <!-- Traders specific info -->
        <template v-if="selectedFeature.type === 'trader'">
          <div class="info-item">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="9" cy="21" r="1"></circle><circle cx="20" cy="21" r="1"></circle><path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"></path></svg>
            <span>{{ selectedFeature.wares || 'Trader' }}</span>
          </div>
          <div class="info-item" v-if="selectedFeature.position">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"></path><circle cx="12" cy="10" r="3"></circle></svg>
            <span>Position: {{ selectedFeature.position }}</span>
          </div>
        </template>

        <!-- Sign specific info -->
        <template v-else-if="selectedFeature.type === 'sign'">
          <div class="info-item">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 8h1a4 4 0 0 1 0 8h-1"></path><path d="M2 8h16v9a4 4 0 0 1-4 4H6a4 4 0 0 1-4-4V8z"></path><line x1="6" y1="1" x2="6" y2="4"></line><line x1="10" y1="1" x2="10" y2="4"></line><line x1="14" y1="1" x2="14" y2="4"></line></svg>
            <span>{{ selectedFeature.text }}</span>
          </div>
        </template>

        <!-- Translocator specific info -->
        <template v-else-if="selectedFeature.type === 'translocator'">
          <div class="info-item">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"></polygon></svg>
            <span>Teleportation Device</span>
          </div>
        </template>

        <!-- Player specific info -->
        <template v-else-if="selectedFeature.type === 'player'">
          <div class="info-item">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path><circle cx="12" cy="7" r="4"></circle></svg>
            <span>Player Character</span>
          </div>
        </template>

        <!-- Default info for unknown features -->
        <template v-else>
          <div class="info-item">
            <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="8" x2="12" y2="12"></line><line x1="12" y1="16" x2="12.01" y2="16"></line></svg>
            <span>{{ selectedFeature.text || 'Unknown feature' }}</span>
          </div>
        </template>
      </div>
      <button @click="closeFeatureInfo" class="close-btn">×</button>
    </div>

    <div class="map-tools">
      <div class="map-tool" @click="toggleChunkLayer" :class="{ active: chunksVisible }" title="Toggle chunk borders">
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="7" height="7"></rect><rect x="14" y="3" width="7" height="7"></rect><rect x="14" y="14" width="7" height="7"></rect><rect x="3" y="14" width="7" height="7"></rect></svg>
      </div>
      <div class="map-tool" @click="resetView" title="Reset view">
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="23 4 23 10 17 10"></polyline><polyline points="1 20 1 14 7 14"></polyline><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"></path></svg>
      </div>
      <div class="map-tool" @click="toggleFullscreen" title="Toggle fullscreen">
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="15 3 21 3 21 9"></polyline><polyline points="9 21 3 21 3 15"></polyline><line x1="21" y1="3" x2="14" y2="10"></line><line x1="3" y1="21" x2="10" y2="14"></line></svg>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { useServerStore } from '@/stores/server';
import { useMapStore } from '@/stores/map';
import MapContainer from '@/components/map/MapContainer.vue';
import SearchFeatures from '@/components/map/SearchFeatures.vue';
import { Group as LayerGroup } from 'ol/layer';

// Initialize stores
const serverStore = useServerStore();
const mapStore = useMapStore();

// Track selected feature
const selectedFeature = computed(() => mapStore.selectedFeature);
const isFullscreen = ref(false);
const chunksVisible = ref(false);

// Close feature info panel
function closeFeatureInfo() {
  mapStore.selectFeature(null);
}

// Toggle chunk layer visibility
function toggleChunkLayer() {
  chunksVisible.value = !chunksVisible.value;
  
  // Find the chunk layer in the map and toggle it
  const map = mapStore.map;
  if (map) {
    map.getLayers().forEach((layerGroup) => {
      if (layerGroup instanceof LayerGroup) {
        layerGroup.getLayers().forEach((layer) => {
          // Identify the chunk layer (could use a better method like a layer ID)
          const source = layer.getLayerState?.();
          if (source && 'getUrl' in source && source.getUrl && 
              typeof source.getUrl === 'function' && 
              source.getUrl()?.includes('chunk.geojson')) {
            layer.setVisible(chunksVisible.value);
          }
        });
      }
    });
  }
}

  // Reset map view
  function resetView() {
    mapStore.resetView();
  }

// Toggle fullscreen
function toggleFullscreen() {
  if (!document.fullscreenElement) {
    document.documentElement.requestFullscreen();
    isFullscreen.value = true;
  } else {
    if (document.exitFullscreen) {
      document.exitFullscreen();
      isFullscreen.value = false;
    }
  }
}

// Start polling for server status
onMounted(() => {
  serverStore.startPolling(30000);
});
</script>

<style scoped>
.map-view {
  position: relative;
  height: 100%;
  width: 100%;
  background-color: #f8f9fa;
}

.search-overlay {
  position: absolute;
  top: 20px;
  left: 50%;
  transform: translateX(-50%);
  width: 90%;
  max-width: 500px;
  z-index: 10;
}

.status-overlay {
  position: fixed;
  top: 70px;
  left: 50%;
  transform: translateX(-50%);
  background-color: rgba(255, 255, 255, 0.9);
  padding: 0.75rem 1.5rem;
  border-radius: 50px;
  box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
  z-index: 2;
  display: flex;
  align-items: center;
  gap: 10px;
  font-weight: 500;
  color: #495057;
}

.spinner {
  animation: spin 1.5s linear infinite;
}

@keyframes spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}

.feature-info {
  position: absolute;
  bottom: 20px;
  left: 20px;
  background-color: white;
  border-radius: 8px;
  box-shadow: 0 4px 15px rgba(0, 0, 0, 0.15);
  padding: 1.25rem;
  max-width: 300px;
  z-index: 2;
  border-left: 4px solid #4285f4;
}

.feature-info h3 {
  margin-top: 0;
  margin-bottom: 12px;
  font-size: 1.1rem;
  color: #212529;
}

.feature-details {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.info-item {
  display: flex;
  align-items: center;
  gap: 8px;
  color: #495057;
  font-size: 0.95rem;
}

.info-item svg {
  flex-shrink: 0;
  color: #6c757d;
}

.close-btn {
  position: absolute;
  top: 8px;
  right: 8px;
  background: none;
  border: none;
  font-size: 1.5rem;
  line-height: 1;
  cursor: pointer;
  color: #adb5bd;
  padding: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: 50%;
}

.close-btn:hover {
  background-color: #f8f9fa;
  color: #495057;
}

.map-tools {
  position: absolute;
  top: 20px;
  right: 20px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  z-index: 2;
}

.map-tool {
  background-color: white;
  width: 40px;
  height: 40px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
  transition: all 0.2s;
  color: #495057;
}

.map-tool:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2);
  color: #4285f4;
}

.map-tool.active {
  background-color: #4285f4;
  color: white;
}

/* Dark mode styles */
:global(html.dark) .status-overlay {
  background-color: rgba(33, 33, 33, 0.9);
  color: #e9ecef;
}

:global(html.dark) .feature-info {
  background-color: #2c2c2c;
  border-left-color: #90caf9;
}

:global(html.dark) .feature-info h3 {
  color: #e9ecef;
}

:global(html.dark) .info-item {
  color: #ced4da;
}

:global(html.dark) .info-item svg {
  color: #adb5bd;
}

:global(html.dark) .close-btn {
  color: #adb5bd;
}

:global(html.dark) .close-btn:hover {
  background-color: #444;
  color: #e9ecef;
}

:global(html.dark) .map-tool {
  background-color: #2c2c2c;
  color: #ced4da;
}

:global(html.dark) .map-tool:hover {
  color: #90caf9;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
}

:global(html.dark) .map-tool.active {
  background-color: #0d6efd;
  color: white;
}

:global(html.dark) .map-view {
  background-color: #121212;
}
</style>
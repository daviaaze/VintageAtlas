<template>
  <div class="map-view">
        <MapContainer />
    
    <div v-if="serverStore.loading" class="status-overlay loading">
      <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="spinner"><path d="M21 12a9 9 0 1 1-6.219-8.56"></path></svg>
      Loading server status...
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue';
import { useServerStore } from '@/stores/server';
import MapContainer from '@/components/map/MapContainer.vue';

// Initialize stores
const serverStore = useServerStore();
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
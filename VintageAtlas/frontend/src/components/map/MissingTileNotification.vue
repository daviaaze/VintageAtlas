<template>
  <div v-if="showNotification" class="missing-tile-notification">
    <div class="notification-content">
      <div class="notification-icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="12" r="10"></circle>
          <line x1="12" y1="8" x2="12" y2="12"></line>
          <line x1="12" y1="16" x2="12.01" y2="16"></line>
        </svg>
      </div>
      <div class="notification-text">
        <h3>Missing Map Tiles</h3>
        <p>Some map tiles could not be loaded. {{ missingCount }} tiles missing.</p>
      </div>
      <button class="close-btn" @click="hideNotification" aria-label="Close notification">
        <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <line x1="18" y1="6" x2="6" y2="18"></line>
          <line x1="6" y1="6" x2="18" y2="18"></line>
        </svg>
      </button>
    </div>
    <div class="notification-actions">
      <button class="action-btn" @click="zoomOut">
        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="11" cy="11" r="8"></circle>
          <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
          <line x1="8" y1="11" x2="14" y2="11"></line>
        </svg>
        Zoom Out
      </button>
      <button class="action-btn" @click="resetView">
        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"></path>
          <polyline points="9 22 9 12 15 12 15 22"></polyline>
        </svg>
        Reset View
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import { useMapStore } from '@/stores/map';

const mapStore = useMapStore();
const showNotification = ref(false);
const missingCount = ref(0);
const missingTiles = new Set<string>();

// Listen for missing tile events
function handleMissingTile(event: CustomEvent) {
  const tileSrc = event.detail.src;
  if (!missingTiles.has(tileSrc)) {
    missingTiles.add(tileSrc);
    missingCount.value = missingTiles.size;
    
    // Only show notification if there are multiple missing tiles
    if (missingTiles.size > 3) {
      showNotification.value = true;
    }
  }
}

// Hide notification
function hideNotification() {
  showNotification.value = false;
}

// Zoom out to see more context
function zoomOut() {
  mapStore.zoomOut();
  hideNotification();
}

// Reset view to default
function resetView() {
  mapStore.resetView();
  hideNotification();
}

// Reset missing tiles when map view changes
function handleViewChange() {
  missingTiles.clear();
  missingCount.value = 0;
  showNotification.value = false;
}

onMounted(() => {
  // Listen for custom missing tile events
  window.addEventListener('missingTile', handleMissingTile as EventListener);
  
  // Listen for map view changes
  if (mapStore.map) {
    const view = mapStore.map.getView();
    view.on('change:resolution', handleViewChange);
    view.on('change:center', handleViewChange);
  }
});

onUnmounted(() => {
  window.removeEventListener('missingTile', handleMissingTile as EventListener);
  
  if (mapStore.map) {
    const view = mapStore.map.getView();
    view.un('change:resolution', handleViewChange);
    view.un('change:center', handleViewChange);
  }
});
</script>

<style scoped>
.missing-tile-notification {
  position: absolute;
  top: 20px;
  left: 50%;
  transform: translateX(-50%);
  width: 90%;
  max-width: 400px;
  background-color: #fff;
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  z-index: 1000;
  overflow: hidden;
  animation: slide-down 0.3s ease-out;
}

@keyframes slide-down {
  from {
    opacity: 0;
    transform: translateX(-50%) translateY(-20px);
  }
  to {
    opacity: 1;
    transform: translateX(-50%) translateY(0);
  }
}

.notification-content {
  display: flex;
  align-items: center;
  padding: 16px;
}

.notification-icon {
  flex-shrink: 0;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background-color: #fff3cd;
  color: #856404;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-right: 16px;
}

.notification-text {
  flex: 1;
}

.notification-text h3 {
  margin: 0 0 4px 0;
  font-size: 16px;
  font-weight: 600;
  color: #333;
}

.notification-text p {
  margin: 0;
  font-size: 14px;
  color: #666;
}

.close-btn {
  background: none;
  border: none;
  color: #999;
  cursor: pointer;
  padding: 4px;
  border-radius: 4px;
  transition: all 0.2s;
}

.close-btn:hover {
  color: #333;
  background-color: #f0f0f0;
}

.notification-actions {
  display: flex;
  border-top: 1px solid #eee;
}

.action-btn {
  flex: 1;
  padding: 10px;
  background: none;
  border: none;
  border-right: 1px solid #eee;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  font-size: 14px;
  color: #0066cc;
  transition: all 0.2s;
}

.action-btn:last-child {
  border-right: none;
}

.action-btn:hover {
  background-color: #f0f7ff;
}

/* Dark mode */
:global(html.dark) .missing-tile-notification {
  background-color: #2c2c2c;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}

:global(html.dark) .notification-icon {
  background-color: #453b00;
  color: #ffd700;
}

:global(html.dark) .notification-text h3 {
  color: #e0e0e0;
}

:global(html.dark) .notification-text p {
  color: #aaa;
}

:global(html.dark) .close-btn {
  color: #777;
}

:global(html.dark) .close-btn:hover {
  color: #ccc;
  background-color: #3c3c3c;
}

:global(html.dark) .notification-actions {
  border-top: 1px solid #444;
}

:global(html.dark) .action-btn {
  border-right: 1px solid #444;
  color: #5cadff;
}

:global(html.dark) .action-btn:hover {
  background-color: #1a2638;
}
</style>

<template>
  <div class="settings-view">
    <h1>Settings</h1>
    
    <div class="settings-section">
      <h2>UI Preferences</h2>
      
      <div class="setting-item">
        <label>Theme</label>
        <div class="theme-selector">
          <button 
            @click="setTheme('light')" 
            :class="{ active: currentTheme === 'light' }"
          >
            Light
          </button>
          <button 
            @click="setTheme('dark')" 
            :class="{ active: currentTheme === 'dark' }"
          >
            Dark
          </button>
          <button 
            @click="setTheme('system')" 
            :class="{ active: currentTheme === 'system' }"
          >
            System
          </button>
        </div>
      </div>
    </div>
    
    <div class="settings-section">
      <h2>Map Preferences</h2>
      
      <div class="setting-item">
        <label>Default View</label>
        <div>
          <label>
            <input type="checkbox" v-model="mapSettings.showTerrain">
            Show Terrain
          </label>
          <label>
            <input type="checkbox" v-model="mapSettings.showTraders">
            Show Traders
          </label>
          <label>
            <input type="checkbox" v-model="mapSettings.showTranslocators">
            Show Translocators
          </label>
          <label>
            <input type="checkbox" v-model="mapSettings.showSigns">
            Show Signs
          </label>
        </div>
      </div>
      
      <div class="setting-item">
        <label>Data Refresh Interval</label>
        <select v-model="refreshInterval">
          <option value="10000">10 seconds</option>
          <option value="30000">30 seconds</option>
          <option value="60000">1 minute</option>
          <option value="300000">5 minutes</option>
        </select>
      </div>
      
      <button @click="saveMapSettings">Save Map Settings</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import { useUiStore } from '@/stores/ui';
import { useMapStore } from '@/stores/map';

const uiStore = useUiStore();
const mapStore = useMapStore();

// Theme settings
const currentTheme = computed(() => uiStore.currentTheme);

function setTheme(theme: 'light' | 'dark' | 'system') {
  uiStore.setTheme(theme);
}

// Map settings
const mapSettings = ref({
  showTerrain: true,
  showTraders: true,
  showTranslocators: true,
  showSigns: true,
});

// Data refresh interval (in milliseconds)
const refreshInterval = ref('30000');

// Save map settings
function saveMapSettings() {
  // Update layer visibility in map store
  mapStore.setLayerVisibility('terrain', mapSettings.value.showTerrain);
  mapStore.setLayerVisibility('traders', mapSettings.value.showTraders);
  mapStore.setLayerVisibility('translocators', mapSettings.value.showTranslocators);
  mapStore.setLayerVisibility('signs', mapSettings.value.showSigns);
  
  // Save to localStorage for persistence
  localStorage.setItem('mapSettings', JSON.stringify(mapSettings.value));
  localStorage.setItem('refreshInterval', refreshInterval.value);
  
  // Could trigger notification that settings were saved
  alert('Settings saved successfully');
}

// Load saved settings on component mount
(() => {
  const savedMapSettings = localStorage.getItem('mapSettings');
  if (savedMapSettings) {
    mapSettings.value = JSON.parse(savedMapSettings);
  }
  
  const savedRefreshInterval = localStorage.getItem('refreshInterval');
  if (savedRefreshInterval) {
    refreshInterval.value = savedRefreshInterval;
  }
})();
</script>

<style scoped>
.settings-view {
  max-width: 800px;
  margin: 0 auto;
  padding: 1rem;
}

.settings-section {
  margin-bottom: 2rem;
  padding: 1rem;
  border: 1px solid #ddd;
  border-radius: 4px;
}

.setting-item {
  margin-bottom: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.setting-item label {
  font-weight: bold;
}

.theme-selector {
  display: flex;
  gap: 0.5rem;
}

.theme-selector button {
  padding: 0.5rem 1rem;
  border: 1px solid #ccc;
  border-radius: 4px;
  background-color: #f5f5f5;
  cursor: pointer;
}

.theme-selector button.active {
  background-color: #007bff;
  color: white;
  border-color: #0069d9;
}

button {
  padding: 0.5rem 1rem;
  background-color: #007bff;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
}

button:hover {
  background-color: #0069d9;
}

select {
  padding: 0.5rem;
  border-radius: 4px;
  border: 1px solid #ccc;
}
</style>

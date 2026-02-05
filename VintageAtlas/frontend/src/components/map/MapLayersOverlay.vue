<template>
  <div class="map-layers-overlay">
    <div class="overlay-header">
      <h3 class="overlay-title">MAP LAYERS</h3>
      <button class="collapse-btn" @click="collapsed = !collapsed" :title="collapsed ? 'Expand' : 'Collapse'">
        <svg v-if="collapsed" xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none"
          stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <polyline points="6 9 12 15 18 9"></polyline>
        </svg>
        <svg v-else xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none"
          stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
          <polyline points="18 15 12 9 6 15"></polyline>
        </svg>
      </button>
    </div>

    <div v-show="!collapsed" class="overlay-content">
      <!-- Terrain Layer -->
      <div class="layer-item">
        <div class="layer-header" @click="toggleLayer('terrain')">
          <span class="layer-icon">🗺️</span>
          <span class="layer-label">Terrain</span>
          <label class="toggle-switch" @click.stop>
            <input type="checkbox" :checked="mapStore.layerVisibility.terrain" @change="toggleLayer('terrain')">
            <span class="toggle-slider"></span>
          </label>
        </div>
      </div>

      <!-- Spawn Layer -->
      <div class="layer-item">
        <div class="layer-header" @click="toggleLayer('spawn')">
          <span class="layer-icon">🏠</span>
          <span class="layer-label">Spawn Point</span>
          <label class="toggle-switch" @click.stop>
            <input type="checkbox" :checked="mapStore.layerVisibility.spawn" @change="toggleLayer('spawn')">
            <span class="toggle-slider"></span>
          </label>
        </div>
      </div>

      <!-- Players Layer -->
      <div class="layer-item">
        <div class="layer-header" @click="toggleLayer('players')">
          <span class="layer-icon">👤</span>
          <span class="layer-label">Players</span>
          <label class="toggle-switch" @click.stop>
            <input type="checkbox" :checked="mapStore.layerVisibility.players" @change="toggleLayer('players')">
            <span class="toggle-slider"></span>
          </label>
        </div>
      </div>

      <!-- Waypoints Layer -->
      <div class="layer-item">
        <div class="layer-header" @click="toggleLayer('waypoints')">
          <span class="layer-icon">📍</span>
          <span class="layer-label">Waypoints</span>
          <label class="toggle-switch" @click.stop>
            <input type="checkbox" :checked="mapStore.layerVisibility.waypoints" @change="toggleLayer('waypoints')">
            <span class="toggle-slider"></span>
          </label>
        </div>
      </div>

      <!-- Temperature Layer -->
      <div class="layer-item">
        <div class="layer-header" @click="toggleLayer('temperature')">
          <span class="layer-icon">🌡️</span>
          <span class="layer-label">Temperature</span>
          <label class="toggle-switch" @click.stop>
            <input type="checkbox" :checked="mapStore.layerVisibility.temperature" @change="toggleLayer('temperature')">
            <span class="toggle-slider"></span>
          </label>
        </div>
      </div>

      <!-- Rainfall Layer -->
      <div class="layer-item">
        <div class="layer-header" @click="toggleLayer('rainfall')">
          <span class="layer-icon">🌧️</span>
          <span class="layer-label">Rainfall</span>
          <label class="toggle-switch" @click.stop>
            <input type="checkbox" :checked="mapStore.layerVisibility.rainfall" @change="toggleLayer('rainfall')">
            <span class="toggle-slider"></span>
          </label>
        </div>
      </div>

      <!-- Traders Layer with Categories -->
      <div class="layer-item">
        <div class="layer-header" @click="toggleLayer('traders')">
          <span class="layer-icon">🏪</span>
          <span class="layer-label">Traders</span>
          <label class="toggle-switch" @click.stop>
            <input type="checkbox" :checked="mapStore.layerVisibility.traders" @change="toggleLayer('traders')">
            <span class="toggle-slider"></span>
          </label>
        </div>

        <!-- Trader Categories Sub-layers -->
        <div v-if="mapStore.layerVisibility.traders" class="sub-layers">
          <div v-for="(visible, category) in mapStore.subLayerVisibility.traders" :key="category"
            class="sub-layer-item">
            <label class="sub-layer-label">
              <input type="checkbox" :checked="visible" @change="toggleSubLayer('traders', category)">
              <span class="color-indicator" :style="{ backgroundColor: getTraderColorStyle(category) }"></span>
              <span>{{ getTraderDisplayName(category) }}</span>
            </label>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import { useMapStore } from '@/stores/map';
import { TRADER_COLORS } from '@/utils/olStyles';

const mapStore = useMapStore();
const collapsed = ref(false);

function toggleLayer(layer: keyof typeof mapStore.layerVisibility) {
  mapStore.toggleLayer(layer);
}

function toggleSubLayer(layerName: keyof typeof mapStore.subLayerVisibility, subLayerName: string) {
  mapStore.toggleSubLayer(layerName, subLayerName);
}

// Helper to convert RGB array to CSS color string
function getTraderColorStyle(category: string): string {
  const color = TRADER_COLORS[category] || TRADER_COLORS['unknown'];
  return `rgb(${color[0]}, ${color[1]}, ${color[2]})`;
}

// Helper to clean up trader category names
function getTraderDisplayName(category: string): string {
  return category.replace(/ trader$/i, '');
}
</script>

<style scoped>
.map-layers-overlay {
  position: absolute;
  top: 12px;
  left: 12px;
  min-width: 240px;
  max-width: 320px;
  background: rgba(30, 41, 59, 0.95);
  backdrop-filter: blur(8px);
  border-radius: 12px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.3);
  z-index: 1000;
  color: #fff;
  overflow: hidden;
}

.overlay-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.overlay-title {
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.5px;
  margin: 0;
  color: rgba(255, 255, 255, 0.9);
}

.collapse-btn {
  background: none;
  border: none;
  cursor: pointer;
  padding: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(255, 255, 255, 0.7);
  border-radius: 4px;
  transition: all 0.2s;
}

.collapse-btn:hover {
  background: rgba(255, 255, 255, 0.1);
  color: #fff;
}

.overlay-content {
  padding: 8px;
  max-height: 70vh;
  overflow-y: auto;
}

.layer-item {
  margin-bottom: 8px;
  border-radius: 8px;
  overflow: hidden;
}

.layer-header {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  cursor: pointer;
  background: rgba(255, 255, 255, 0.05);
  border-radius: 8px;
  transition: all 0.2s;
}

.layer-header:hover {
  background: rgba(255, 255, 255, 0.1);
}

.layer-icon {
  font-size: 18px;
  line-height: 1;
}

.layer-label {
  flex: 1;
  font-size: 14px;
  font-weight: 500;
}

.toggle-switch {
  position: relative;
  display: inline-block;
  width: 36px;
  height: 20px;
  flex-shrink: 0;
}

.toggle-switch input {
  opacity: 0;
  width: 0;
  height: 0;
}

.toggle-slider {
  position: absolute;
  cursor: pointer;
  inset: 0;
  background: rgba(255, 255, 255, 0.2);
  transition: all 0.3s;
  border-radius: 20px;
}

.toggle-slider:before {
  position: absolute;
  content: '';
  height: 14px;
  width: 14px;
  left: 3px;
  bottom: 3px;
  background: white;
  transition: all 0.3s;
  border-radius: 50%;
}

input:checked+.toggle-slider {
  background: #10b981;
}

input:checked+.toggle-slider:before {
  transform: translateX(16px);
}

input:focus+.toggle-slider {
  box-shadow: 0 0 0 2px rgba(16, 185, 129, 0.3);
}

.sub-layers {
  padding: 8px 12px 8px 40px;
  background: rgba(0, 0, 0, 0.2);
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-top: 4px;
  border-radius: 0 0 8px 8px;
}

.sub-layer-item {
  font-size: 13px;
}

.sub-layer-label {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  user-select: none;
  padding: 4px 0;
}

.sub-layer-label:hover {
  color: #10b981;
}

.sub-layer-label input[type='checkbox'] {
  width: 16px;
  height: 16px;
  cursor: pointer;
  accent-color: #10b981;
}

.color-indicator {
  width: 12px;
  height: 12px;
  border-radius: 2px;
  flex-shrink: 0;
  border: 1px solid rgba(255, 255, 255, 0.3);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.2);
}

/* Scrollbar styling */
.overlay-content::-webkit-scrollbar {
  width: 6px;
}

.overlay-content::-webkit-scrollbar-track {
  background: rgba(255, 255, 255, 0.05);
  border-radius: 3px;
}

.overlay-content::-webkit-scrollbar-thumb {
  background: rgba(255, 255, 255, 0.2);
  border-radius: 3px;
}

.overlay-content::-webkit-scrollbar-thumb:hover {
  background: rgba(255, 255, 255, 0.3);
}
</style>

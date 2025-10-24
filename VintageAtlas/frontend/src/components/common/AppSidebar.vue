<template>
  <aside class="app-sidebar">
    <nav class="sidebar-nav">
      <div class="nav-section">
        <h3 class="nav-title">MAP LAYERS</h3>
        <div 
          class="nav-item" 
          :class="{ 'active': mapStore.layerVisibility.terrain }"
          @click="toggleLayer('terrain')"
        >
          <span class="nav-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M8 22L3 17 8 12 3 7 8 2"></path><path d="M16 2L21 7 16 12 21 17 16 22"></path></svg>
          </span>
          <span class="nav-label">Terrain</span>
          <label class="toggle-switch">
            <input type="checkbox" :checked="mapStore.layerVisibility.terrain" @change="toggleLayer('terrain')">
            <span class="toggle-slider"></span>
          </label>
        </div>
      </div>
    </nav>
  </aside>
</template>

<script setup lang="ts">
import { useMapStore } from '@/stores/map';

const mapStore = useMapStore();

function toggleLayer(layer: keyof typeof mapStore.layerVisibility) {
  mapStore.toggleLayer(layer);
}
</script>

<style scoped>
.app-sidebar {
  @apply w-[280px] bg-gray-50 dark:bg-gray-900 border-r border-gray-200 dark:border-gray-700 overflow-y-auto h-full shadow-sm;
  transition: width 0.3s;
}

.sidebar-nav {
  @apply flex flex-col gap-6 py-4;
}

.nav-section {
  @apply px-4;
}

.nav-title {
  @apply text-xs uppercase tracking-wide text-gray-500 dark:text-gray-500 mb-3 pl-2 font-semibold;
}

.nav-item {
  @apply flex items-center gap-3 px-3 py-3 rounded-md cursor-pointer no-underline text-gray-700 dark:text-gray-300 mb-1 transition-all;
}

.nav-item:hover {
  @apply bg-gray-200 dark:bg-gray-800;
}

.nav-item {
  @apply bg-blue-100 dark:bg-blue-900/30 text-blue-600 dark:text-blue-400 font-medium;
}

.nav-item.active {
  @apply text-blue-600 dark:text-blue-400;
}

.nav-icon {
  @apply flex items-center justify-center w-5 h-5 text-gray-400 dark:text-gray-500;
}

.nav-item.router-link-active .nav-icon,
.nav-item.active .nav-icon {
  @apply text-blue-600 dark:text-blue-400;
}

.nav-label {
  @apply flex-1 text-sm;
}

.toggle-switch {
  @apply relative inline-block w-9 h-5 ml-auto;
}

.toggle-switch input {
  @apply opacity-0 w-0 h-0;
}

.toggle-slider {
  @apply absolute cursor-pointer inset-0 bg-gray-300 dark:bg-gray-600 transition-all duration-300 rounded-full;
}

.toggle-slider:before {
  @apply absolute content-[''] h-4 w-4 left-0.5 bottom-0.5 bg-white transition-all duration-300 rounded-full;
}

input:checked + .toggle-slider:before {
  @apply translate-x-4;
}

.toggle-slider {
  @apply bg-gray-300 dark:bg-gray-600;
}

input:checked + .toggle-slider {
  @apply bg-blue-600 dark:bg-emerald-500;
}

input:focus + .toggle-slider {
  @apply ring-2 ring-blue-600/20 dark:ring-emerald-500/20;
}

.player-status {
  @apply w-2 h-2 rounded-full;
}

.player-status.online {
  @apply bg-emerald-500 shadow-[0_0_5px_rgb(16_185_129)];
}

.player-icon {
  @apply text-emerald-500;
}

/* Responsive */
@media (max-width: 768px) {
  .app-sidebar {
    width: 100%;
    max-height: 50vh;
  }
}

.sub-layer-list {
  @apply ml-6 mb-2 flex flex-col gap-1;
}
.sub-layer-item {
  @apply flex items-center gap-2 text-xs text-gray-700 dark:text-gray-300;
}
.sub-layer-item input[type='checkbox'] {
  @apply w-3 h-3;
}
.sub-layer-label {
  @apply select-none;
}
</style>
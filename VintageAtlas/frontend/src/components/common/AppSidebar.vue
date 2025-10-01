<template>
  <aside class="app-sidebar">
    <nav class="sidebar-nav">
      <div class="nav-section">
        <h3 class="nav-title">NAVIGATION</h3>
        <router-link to="/" class="nav-item">
          <span class="nav-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="3 6 9 3 15 6 21 3 21 18 15 21 9 18 3 21"></polygon><line x1="9" y1="3" x2="9" y2="18"></line><line x1="15" y1="6" x2="15" y2="21"></line></svg>
          </span>
          <span class="nav-label">Map</span>
        </router-link>
        <router-link to="/historical" class="nav-item">
          <span class="nav-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"></circle><polyline points="12 6 12 12 16 14"></polyline></svg>
          </span>
          <span class="nav-label">Historical Data</span>
        </router-link>
        <router-link to="/settings" class="nav-item">
          <span class="nav-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="3"></circle><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path></svg>
          </span>
          <span class="nav-label">Settings</span>
        </router-link>
        <router-link to="/admin" class="nav-item">
          <span class="nav-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path><circle cx="12" cy="7" r="4"></circle></svg>
          </span>
          <span class="nav-label">Admin Dashboard</span>
        </router-link>
      </div>
      
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
        <div 
          class="nav-item" 
          :class="{ 'active': mapStore.layerVisibility.traders }"
          @click="toggleLayer('traders')"
        >
          <span class="nav-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="9" cy="21" r="1"></circle><circle cx="20" cy="21" r="1"></circle><path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"></path></svg>
          </span>
          <span class="nav-label">Traders</span>
          <label class="toggle-switch">
            <input type="checkbox" :checked="mapStore.layerVisibility.traders" @change="toggleLayer('traders')">
            <span class="toggle-slider"></span>
          </label>
        </div>
        <div class="sub-layer-list" v-if="mapStore.layerVisibility.traders">
          <label v-for="(visible, key) in mapStore.subLayerVisibility.traders" :key="'traders-'+key" class="sub-layer-item" @click.stop>
            <input type="checkbox" :checked="visible" @change="mapStore.toggleSubLayer('traders', key)" />
            <span class="sub-layer-label">{{ key }}</span>
          </label>
        </div>
        <div 
          class="nav-item" 
          :class="{ 'active': mapStore.layerVisibility.translocators }"
          @click="toggleLayer('translocators')"
        >
          <span class="nav-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"></polygon></svg>
          </span>
          <span class="nav-label">Translocators</span>
          <label class="toggle-switch">
            <input type="checkbox" :checked="mapStore.layerVisibility.translocators" @change="toggleLayer('translocators')">
            <span class="toggle-slider"></span>
          </label>
        </div>
        <div class="sub-layer-list" v-if="mapStore.layerVisibility.translocators">
          <label v-for="(visible, key) in mapStore.subLayerVisibility.translocators" :key="'translocators-'+key" class="sub-layer-item" @click.stop>
            <input type="checkbox" :checked="visible" @change="mapStore.toggleSubLayer('translocators', key)" />
            <span class="sub-layer-label">{{ key }}</span>
          </label>
        </div>
        <div 
          class="nav-item" 
          :class="{ 'active': mapStore.layerVisibility.landmarks }"
          @click="toggleLayer('landmarks')"
        >
          <span class="nav-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 10c0 7-9 13-9 13S3 17 3 10a9 9 0 1 1 18 0z"></path><circle cx="12" cy="10" r="3"></circle></svg>
          </span>
          <span class="nav-label">Landmarks</span>
          <label class="toggle-switch">
            <input type="checkbox" :checked="mapStore.layerVisibility.landmarks" @change="toggleLayer('landmarks')">
            <span class="toggle-slider"></span>
          </label>
        </div>
        <div class="sub-layer-list" v-if="mapStore.layerVisibility.landmarks">
          <label v-for="(visible, key) in mapStore.subLayerVisibility.landmarks" :key="'landmarks-'+key" class="sub-layer-item" @click.stop>
            <input type="checkbox" :checked="visible" @change="mapStore.toggleSubLayer('landmarks', key)" />
            <span class="sub-layer-label">{{ key }}</span>
          </label>
        </div>
        <div 
          class="nav-item" 
          :class="{ 'active': mapStore.layerVisibility.signs }"
          @click="toggleLayer('signs')"
        >
          <span class="nav-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 8h1a4 4 0 0 1 0 8h-1"></path><path d="M2 8h16v9a4 4 0 0 1-4 4H6a4 4 0 0 1-4-4V8z"></path><line x1="6" y1="1" x2="6" y2="4"></line><line x1="10" y1="1" x2="10" y2="4"></line><line x1="14" y1="1" x2="14" y2="4"></line></svg>
          </span>
          <span class="nav-label">Signs</span>
          <label class="toggle-switch">
            <input type="checkbox" :checked="mapStore.layerVisibility.signs" @change="toggleLayer('signs')">
            <span class="toggle-slider"></span>
          </label>
        </div>
        <div 
          class="nav-item" 
          :class="{ 'active': mapStore.layerVisibility.chunkVersions }"
          @click="toggleLayer('chunkVersions')"
        >
          <span class="nav-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect><line x1="9" y1="3" x2="9" y2="21"></line><line x1="15" y1="3" x2="15" y2="21"></line><line x1="3" y1="9" x2="21" y2="9"></line><line x1="3" y1="15" x2="21" y2="15"></line></svg>
          </span>
          <span class="nav-label">Chunk Versions</span>
          <label class="toggle-switch">
            <input type="checkbox" :checked="mapStore.layerVisibility.chunkVersions" @change="toggleLayer('chunkVersions')">
            <span class="toggle-slider"></span>
          </label>
        </div>
        <div 
          class="nav-item" 
          :class="{ 'active': mapStore.layerVisibility.players }"
          @click="toggleLayer('players')"
        >
          <span class="nav-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path><circle cx="12" cy="7" r="4"></circle></svg>
          </span>
          <span class="nav-label">Players</span>
          <label class="toggle-switch">
            <input type="checkbox" :checked="mapStore.layerVisibility.players" @change="toggleLayer('players')">
            <span class="toggle-slider"></span>
          </label>
        </div>
        <div 
          class="nav-item" 
          :class="{ 'active': mapStore.layerVisibility.animals }"
          @click="toggleLayer('animals')"
        >
          <span class="nav-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="4" r="2"></circle><circle cx="18" cy="8" r="2"></circle><circle cx="20" cy="16" r="2"></circle><path d="M9 10a5 5 0 0 1 5 5v3.5a3.5 3.5 0 0 1-6.84 1.045Q6.52 17.48 4.46 16.84A3.5 3.5 0 0 1 5.5 10Z"></path></svg>
          </span>
          <span class="nav-label">Animals</span>
          <label class="toggle-switch">
            <input type="checkbox" :checked="mapStore.layerVisibility.animals" @change="toggleLayer('animals')">
            <span class="toggle-slider"></span>
          </label>
        </div>
      </div>
      
      <div class="nav-section online-players" v-if="serverStore.onlinePlayers.length > 0">
        <h3 class="nav-title">ONLINE PLAYERS</h3>
        <div class="nav-item" v-for="player in serverStore.onlinePlayers" :key="player.id">
          <span class="nav-icon player-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path><circle cx="12" cy="7" r="4"></circle></svg>
          </span>
          <span class="nav-label">{{ player.name }}</span>
          <span class="player-status online"></span>
        </div>
      </div>
    </nav>
  </aside>
</template>

<script setup lang="ts">
import { useMapStore } from '@/stores/map';
import { useServerStore } from '@/stores/server';

const mapStore = useMapStore();
const serverStore = useServerStore();

function toggleLayer(layer: string) {
  mapStore.toggleLayer(layer as any);
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
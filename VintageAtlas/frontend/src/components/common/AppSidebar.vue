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
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="9" cy="21" r="1"></circle><circle cx="20" cy="21" r="1"></circle><path d="M1 1h4l2.68 13.39a2 2 0 0 0 2 1.61h9.72a2 2 0 0 0 2-1.61L23 6H6"></path></svg>
          </span>
          <span class="nav-label">Traders</span>
          <label class="toggle-switch">
            <input type="checkbox" :checked="mapStore.layerVisibility.traders" @change="toggleLayer('traders')">
            <span class="toggle-slider"></span>
          </label>
        </div>
        <div 
          class="nav-item" 
          :class="{ 'active': mapStore.layerVisibility.translocators }"
          @click="toggleLayer('translocators')"
        >
          <span class="nav-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"></polygon></svg>
          </span>
          <span class="nav-label">Translocators</span>
          <label class="toggle-switch">
            <input type="checkbox" :checked="mapStore.layerVisibility.translocators" @change="toggleLayer('translocators')">
            <span class="toggle-slider"></span>
          </label>
        </div>
        <div 
          class="nav-item" 
          :class="{ 'active': mapStore.layerVisibility.signs }"
          @click="toggleLayer('signs')"
        >
          <span class="nav-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M18 8h1a4 4 0 0 1 0 8h-1"></path><path d="M2 8h16v9a4 4 0 0 1-4 4H6a4 4 0 0 1-4-4V8z"></path><line x1="6" y1="1" x2="6" y2="4"></line><line x1="10" y1="1" x2="10" y2="4"></line><line x1="14" y1="1" x2="14" y2="4"></line></svg>
          </span>
          <span class="nav-label">Signs</span>
          <label class="toggle-switch">
            <input type="checkbox" :checked="mapStore.layerVisibility.signs" @change="toggleLayer('signs')">
            <span class="toggle-slider"></span>
          </label>
        </div>
        <div 
          class="nav-item" 
          :class="{ 'active': mapStore.layerVisibility.players }"
          @click="toggleLayer('players')"
        >
          <span class="nav-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path><circle cx="12" cy="7" r="4"></circle></svg>
          </span>
          <span class="nav-label">Players</span>
          <label class="toggle-switch">
            <input type="checkbox" :checked="mapStore.layerVisibility.players" @change="toggleLayer('players')">
            <span class="toggle-slider"></span>
          </label>
        </div>
      </div>
      
      <div class="nav-section online-players" v-if="serverStore.onlinePlayers.length > 0">
        <h3 class="nav-title">ONLINE PLAYERS</h3>
        <div class="nav-item" v-for="player in serverStore.onlinePlayers" :key="player.id">
          <span class="nav-icon player-icon">
            <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path><circle cx="12" cy="7" r="4"></circle></svg>
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
  width: 280px;
  background-color: #f8f9fa;
  border-right: 1px solid #e9ecef;
  overflow-y: auto;
  transition: width 0.3s;
  height: 100%;
  box-shadow: 1px 0 5px rgba(0, 0, 0, 0.05);
}

/* Dark mode */
:global(html.dark) .app-sidebar {
  background-color: #1e1e1e;
  border-right-color: #333;
  box-shadow: 1px 0 5px rgba(0, 0, 0, 0.2);
}

.sidebar-nav {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  padding: 1rem 0;
}

.nav-section {
  padding: 0 1rem;
}

.nav-title {
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 1px;
  color: #adb5bd;
  margin-bottom: 0.75rem;
  padding-left: 0.5rem;
  font-weight: 600;
}

:global(html.dark) .nav-title {
  color: #6c757d;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 0.75rem;
  border-radius: 6px;
  cursor: pointer;
  text-decoration: none;
  color: #495057;
  margin-bottom: 0.25rem;
  transition: all 0.2s ease;
  position: relative;
}

.nav-item:hover {
  background-color: #e9ecef;
  color: #212529;
}

.nav-item.router-link-active {
  background-color: #e3f2fd;
  color: #0d6efd;
  font-weight: 500;
}

.nav-item.active {
  color: #0d6efd;
}

:global(html.dark) .nav-item {
  color: #ced4da;
}

:global(html.dark) .nav-item:hover {
  background-color: #2c3032;
}

:global(html.dark) .nav-item.router-link-active {
  background-color: #1e2f42;
  color: #90caf9;
}

:global(html.dark) .nav-item.active {
  color: #90caf9;
}

.nav-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 20px;
  height: 20px;
  color: #adb5bd;
}

.nav-item.router-link-active .nav-icon,
.nav-item.active .nav-icon {
  color: #0d6efd;
}

:global(html.dark) .nav-item.router-link-active .nav-icon,
:global(html.dark) .nav-item.active .nav-icon {
  color: #90caf9;
}

.nav-label {
  flex: 1;
  font-size: 0.9rem;
}

.toggle-switch {
  position: relative;
  display: inline-block;
  width: 36px;
  height: 20px;
  margin-left: auto;
}

.toggle-switch input {
  opacity: 0;
  width: 0;
  height: 0;
}

.toggle-slider {
  position: absolute;
  cursor: pointer;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: #dee2e6;
  transition: .4s;
  border-radius: 34px;
}

.toggle-slider:before {
  position: absolute;
  content: "";
  height: 16px;
  width: 16px;
  left: 2px;
  bottom: 2px;
  background-color: white;
  transition: .4s;
  border-radius: 50%;
}

input:checked + .toggle-slider {
  background-color: #0d6efd;
}

input:focus + .toggle-slider {
  box-shadow: 0 0 1px #0d6efd;
}

input:checked + .toggle-slider:before {
  transform: translateX(16px);
}

:global(html.dark) .toggle-slider {
  background-color: #6c757d;
}

:global(html.dark) .toggle-slider:before {
  background-color: #f8f9fa;
}

:global(html.dark) input:checked + .toggle-slider {
  background-color: #0d6efd;
}

.player-status {
  width: 8px;
  height: 8px;
  border-radius: 50%;
}

.player-status.online {
  background-color: #10b981;
  box-shadow: 0 0 5px #10b981;
}

.player-icon {
  color: #10b981;
}

/* Responsive */
@media (max-width: 768px) {
  .app-sidebar {
    width: 100%;
    max-height: 50vh;
  }
}
</style>
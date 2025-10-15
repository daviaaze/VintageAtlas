<template>
  <div class="live-controls" :class="{ 'mobile': isMobile, 'collapsed': isCollapsed }">
    <!-- Compact toggle button -->
    <button 
      class="toggle-button"
      @click="isCollapsed = !isCollapsed"
      :title="isCollapsed ? 'Show live controls' : 'Hide live controls'"
    >
      <span v-if="!isCollapsed">✕</span>
      <span v-else>☰ Live</span>
    </button>
    
    <!-- Collapsible controls -->
    <div v-if="!isCollapsed" class="controls-content">
      <div class="control-section">
        <span class="section-label">Players</span>
        <label class="control-item" title="Toggle player layer visibility">
          <input 
            type="checkbox" 
            v-model="showPlayers" 
            aria-label="Show players on map"
          />
          <span>Visible</span>
        </label>
        
        <label class="control-item" title="Show player statistics (HP, hunger, temperature)">
          <input 
            type="checkbox" 
            v-model="showPlayerStats" 
            aria-label="Show player stats"
          />
          <span>Stats</span>
        </label>
      </div>
      
      <span class="separator" aria-hidden="true">|</span>
      
      <div class="control-section">
        <span class="section-label">Animals</span>
        <label class="control-item" title="Toggle animal layer visibility">
          <input 
            type="checkbox" 
            v-model="showAnimals" 
            aria-label="Show animals on map"
          />
          <span>Visible</span>
        </label>
        
        <label class="control-item" title="Show animal health bars">
          <input 
            type="checkbox" 
            v-model="showAnimalHP" 
            aria-label="Show animal HP"
          />
          <span>HP</span>
        </label>
        
        <label class="control-item" title="Show animal environment data (temperature, rain, wind)">
          <input 
            type="checkbox" 
            v-model="showAnimalEnv" 
            aria-label="Show animal environment"
          />
          <span>Env</span>
        </label>
      </div>
      
      <span class="separator" aria-hidden="true">|</span>
      
      <label class="control-item" title="Show coordinates relative to spawn point">
        <input 
          type="checkbox" 
          v-model="showCoords" 
          aria-label="Show coordinates"
        />
        <span>Coordinates</span>
      </label>
      
      <button 
        class="help-button" 
        @click="showHelpDialog = true"
        aria-label="Show keyboard shortcuts"
        title="Keyboard shortcuts"
      >
        ?
      </button>
    </div>
    
    <!-- Help Dialog -->
    <Teleport to="body">
      <div v-if="showHelpDialog" class="help-overlay" @click="showHelpDialog = false"></div>
      <div v-if="showHelpDialog" class="help-dialog" role="dialog" aria-labelledby="help-title" aria-modal="true">
        <h3 id="help-title">⌨️ Keyboard Shortcuts</h3>
        <div class="shortcut-list">
          <div class="shortcut-item">
            <span>Toggle Players</span>
            <kbd class="key">Alt + P</kbd>
          </div>
          <div class="shortcut-item">
            <span>Toggle Player Stats</span>
            <kbd class="key">Alt + S</kbd>
          </div>
          <div class="shortcut-item">
            <span>Toggle Animals</span>
            <kbd class="key">Alt + A</kbd>
          </div>
          <div class="shortcut-item">
            <span>Toggle Animal HP</span>
            <kbd class="key">Alt + H</kbd>
          </div>
          <div class="shortcut-item">
            <span>Toggle Animal Environment</span>
            <kbd class="key">Alt + E</kbd>
          </div>
          <div class="shortcut-item">
            <span>Toggle Coordinates</span>
            <kbd class="key">Alt + C</kbd>
          </div>
          <div class="shortcut-item">
            <span>Reset View</span>
            <kbd class="key">Alt + R</kbd>
          </div>
          <div class="shortcut-item">
            <span>Zoom In</span>
            <kbd class="key">+</kbd>
          </div>
          <div class="shortcut-item">
            <span>Zoom Out</span>
            <kbd class="key">-</kbd>
          </div>
        </div>
        <button class="close-button" @click="showHelpDialog = false" ref="closeButton">Got it!</button>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted, onUnmounted, type Ref } from 'vue';
import { useMapStore } from '@/stores/map';

// Store
const mapStore = useMapStore();

// State
const showHelpDialog = ref(false);
const isMobile = ref(window.innerWidth < 768);
const closeButton = ref<HTMLButtonElement | null>(null);
const isCollapsed = ref(false); // Collapsible live controls

// Local storage keys
const SHOW_PLAYERS_KEY = 'showPlayers';
const SHOW_PLAYER_STATS_KEY = 'showPlayerStats';
const SHOW_ANIMALS_KEY = 'showAnimals';
const SHOW_ANIMAL_HP_KEY = 'showAnimalHP';
const SHOW_ANIMAL_ENV_KEY = 'showAnimalEnv';
const SHOW_COORDS_KEY = 'showCoords';

// Helper functions for localStorage
function getBool(key: string, defaultValue: boolean): boolean {
  try {
    const value = localStorage.getItem(key);
    return value ? JSON.parse(value) : defaultValue;
  } catch (e) {
    return defaultValue;
  }
}

function setBool(key: string, value: boolean): void {
  localStorage.setItem(key, JSON.stringify(value));
}

// Reactive state with localStorage persistence
const showPlayers = ref(getBool(SHOW_PLAYERS_KEY, true));
const showPlayerStats = ref(getBool(SHOW_PLAYER_STATS_KEY, false));
const showAnimals = ref(getBool(SHOW_ANIMALS_KEY, true));
const showAnimalHP = ref(getBool(SHOW_ANIMAL_HP_KEY, false));
const showAnimalEnv = ref(getBool(SHOW_ANIMAL_ENV_KEY, false));
const showCoords = ref(getBool(SHOW_COORDS_KEY, false));

// Status icons and messages
const statusIcons: Record<string, string> = {
  ok: '●',
  error: '✕',
  warning: '⚠',
  reconnecting: '↻',
  loading: '⏳'
};

const statusMessages: Record<string, string> = {
  ok: 'Connected - Live data updating',
  error: 'Connection failed - Check server status',
  warning: 'Connection issue - Retrying...',
  reconnecting: 'Reconnecting...',
  loading: 'Loading data...'
};

// Handle window resize for mobile detection
function handleResize() {
  isMobile.value = window.innerWidth < 768;
}

// Keyboard shortcuts
function handleKeyDown(e: KeyboardEvent) {
  // Skip if user is typing in an input
  if (e.target instanceof HTMLInputElement && e.target.type === 'text') return;
  
  // Keyboard shortcuts (Alt+Key to avoid conflicts)
  if (e.altKey) {
    const shortcuts: Record<string, { ref: Ref<boolean> | null, name: string }> = {
      'r': { ref: null, name: 'Reset View' }
    };
    
    const key = e.key.toLowerCase();
    if (shortcuts[key]) {
      e.preventDefault();
      
      if (key === 'r') {
        // Special case for reset view
        mapStore.resetView();
        announceToScreenReader('View reset to center');
      } else {
        // Toggle the setting
        const setting = shortcuts[key];
        if (setting.ref) {
          setting.ref.value = !setting.ref.value;
          announceToScreenReader(`${setting.name} ${setting.ref.value ? 'enabled' : 'disabled'}`);
        }
      }
    }
  } else if (e.key === '+' || e.key === '=') {
    e.preventDefault();
    mapStore.zoomIn();
  } else if (e.key === '-' || e.key === '_') {
    e.preventDefault();
    mapStore.zoomOut();
  } else if (e.key === 'Escape' && showHelpDialog.value) {
    showHelpDialog.value = false;
  }
}

// Screen reader announcements
function announceToScreenReader(message: string) {
  const announcement = document.createElement('div');
  announcement.setAttribute('role', 'status');
  announcement.setAttribute('aria-live', 'polite');
  announcement.className = 'sr-only';
  announcement.textContent = message;
  document.body.appendChild(announcement);
  setTimeout(() => document.body.removeChild(announcement), 1000);
}

// Focus management for help dialog
watch(showHelpDialog, (value) => {
  if (value) {
    // Wait for the dialog to be rendered
    setTimeout(() => {
      closeButton.value?.focus();
    }, 50);
  }
});

// Lifecycle hooks
onMounted(() => {
  window.addEventListener('resize', handleResize);
  window.addEventListener('keydown', handleKeyDown);
  handleResize();
});

onUnmounted(() => {
  window.removeEventListener('resize', handleResize);
  window.removeEventListener('keydown', handleKeyDown);
});
</script>

<style scoped>
.live-controls {
  position: absolute;
  top: 10px;
  left: 10px;
  z-index: 1000;
  background: rgba(0, 0, 0, 0.7);
  color: white;
  padding: 8px 12px;
  border-radius: 12px;
  font-size: 13px;
  line-height: 1.4;
  display: flex;
  gap: 12px;
  align-items: center;
  backdrop-filter: blur(4px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
  transition: all 0.3s ease;
}

.live-controls.collapsed {
  padding: 6px 10px;
  gap: 8px;
}

.live-controls.mobile {
  flex-wrap: wrap;
  max-width: calc(100vw - 20px);
  font-size: 12px;
  padding: 10px;
  gap: 8px;
  top: 5px;
  left: 5px;
}

.toggle-button {
  background: rgba(255, 255, 255, 0.1);
  border: none;
  color: white;
  padding: 4px 8px;
  border-radius: 6px;
  cursor: pointer;
  font-size: 12px;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 4px;
}

.toggle-button:hover {
  background: rgba(255, 255, 255, 0.2);
}

.controls-content {
  display: flex;
  gap: 12px;
  align-items: center;
}

.control-section {
  display: flex;
  gap: 8px;
  align-items: center;
}

.section-label {
  font-weight: 600;
  font-size: 12px;
  opacity: 0.8;
  margin-right: 4px;
}

.control-item {
  display: flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
  user-select: none;
  padding: 4px 6px;
  border-radius: 6px;
  transition: background 0.2s ease;
}

.control-item:hover {
  background: rgba(255, 255, 255, 0.1);
}

.control-item:focus-within {
  background: rgba(255, 255, 255, 0.15);
  outline: 2px solid rgba(255, 255, 255, 0.5);
  outline-offset: 2px;
}

.control-item input[type="checkbox"] {
  width: 18px;
  height: 18px;
  cursor: pointer;
  accent-color: #4a9eff;
}

.control-item input[type="checkbox"]:focus {
  outline: 2px solid #4a9eff;
  outline-offset: 2px;
}

.separator {
  opacity: 0.35;
  margin: 0 4px;
}

.status-indicator {
  color: #8f8;
  font-size: 14px;
  cursor: help;
  padding: 4px 8px;
  border-radius: 6px;
  transition: all 0.2s ease;
  min-width: 20px;
  text-align: center;
}

.status-indicator:hover {
  background: rgba(255, 255, 255, 0.1);
}

.status-indicator.error {
  color: #f88;
}

.status-indicator.warning {
  color: #fd3;
}

.status-indicator.reconnecting {
  color: #f90;
  animation: pulse 1s ease-in-out infinite;
}

.status-indicator.loading {
  color: #4a9eff;
  animation: pulse 1s ease-in-out infinite;
}

@keyframes pulse {
  0%, 100% {
    opacity: 1;
  }
  50% {
    opacity: 0.5;
  }
}

/* Help button */
.help-button {
  background: rgba(74, 158, 255, 0.2);
  border: 1px solid rgba(74, 158, 255, 0.5);
  color: #4a9eff;
  border-radius: 50%;
  width: 24px;
  height: 24px;
  cursor: pointer;
  font-weight: bold;
  font-size: 14px;
  transition: all 0.2s ease;
  padding: 0;
  margin-left: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.help-button:hover {
  background: rgba(74, 158, 255, 0.4);
  transform: scale(1.1);
}

.help-button:focus {
  outline: 2px solid #4a9eff;
  outline-offset: 2px;
}

/* Help dialog */
.help-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  z-index: 9999;
  backdrop-filter: blur(2px);
}

.help-dialog {
  position: fixed;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  background: rgba(0, 0, 0, 0.95);
  color: white;
  padding: 24px;
  border-radius: 12px;
  max-width: 500px;
  width: calc(100vw - 40px);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.5);
  z-index: 10000;
  backdrop-filter: blur(8px);
  border: 2px solid rgba(255, 255, 255, 0.1);
}

.help-dialog h3 {
  margin: 0 0 16px;
  color: #4a9eff;
  font-size: 20px;
}

.shortcut-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.shortcut-item {
  display: flex;
  justify-content: space-between;
  padding: 8px 0;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.shortcut-item:last-child {
  border-bottom: none;
}

.key {
  background: rgba(74, 158, 255, 0.2);
  padding: 4px 8px;
  border-radius: 4px;
  font-family: monospace;
  font-size: 13px;
  color: #4a9eff;
}

.close-button {
  background: #4a9eff;
  border: none;
  color: white;
  padding: 10px 20px;
  border-radius: 6px;
  cursor: pointer;
  margin-top: 16px;
  width: 100%;
  font-weight: bold;
  font-size: 14px;
  transition: background 0.2s ease;
}

.close-button:hover {
  background: #357abd;
}

.close-button:focus {
  outline: 2px solid white;
  outline-offset: 2px;
}

/* Screen reader only class */
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border-width: 0;
}

/* Mobile styles */
@media (max-width: 768px) {
  .live-controls {
    flex-wrap: wrap;
    max-width: calc(100vw - 20px);
    font-size: 12px;
    padding: 10px;
    gap: 8px;
    top: 5px;
    left: 5px;
  }
  
  .control-item {
    padding: 6px 8px;
    min-height: 36px;
  }
  
  .control-item input[type="checkbox"] {
    width: 20px;
    height: 20px;
  }
  
  .status-indicator {
    font-size: 16px;
    min-width: 24px;
    padding: 6px 10px;
  }
}

@media (max-width: 480px) {
  .live-controls {
    font-size: 11px;
    padding: 8px;
    gap: 6px;
  }
  
  .separator {
    display: none;
  }
  
  .control-item {
    padding: 8px;
    min-height: 40px;
    flex: 0 0 calc(50% - 3px);
  }
  
  .help-dialog {
    padding: 20px;
    max-width: none;
  }
  
  .help-dialog h3 {
    font-size: 18px;
  }
  
  .help-button {
    width: 28px;
    height: 28px;
    font-size: 16px;
  }
}

/* High contrast mode support */
@media (prefers-contrast: high) {
  .live-controls {
    background: rgba(0, 0, 0, 0.9);
    border: 2px solid white;
  }
  
  .status-indicator {
    font-weight: bold;
  }
}

/* Reduced motion support */
@media (prefers-reduced-motion: reduce) {
  .live-controls {
    transition: none;
  }
  
  .control-item {
    transition: none;
  }
  
  .status-indicator {
    transition: none;
  }
  
  .status-indicator.reconnecting,
  .status-indicator.loading {
    animation: none;
  }
}
</style>


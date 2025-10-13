<template>
  <div class="player-layer">
    <!-- This is just a wrapper component, actual rendering is done by OpenLayers -->
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, watch, computed } from 'vue';
import { useLiveStore } from '@/stores/live';
import { useMapStore } from '@/stores/map';
import type { FeatureLike } from 'ol/Feature';
import VectorLayer from 'ol/layer/Vector';
import VectorSource from 'ol/source/Vector';
import Feature from 'ol/Feature';
import Point from 'ol/geom/Point';
import { Style, Circle, Fill, Stroke, Text } from 'ol/style';
import { formatCoordinates, getSpawnPosition } from '@/utils/mapConfig';

// Stores
const liveStore = useLiveStore();
const mapStore = useMapStore();

// Local state
let playerLayer: VectorLayer<VectorSource> | null = null;
let playerSource: VectorSource | null = null;

// Helper functions
function getBool(key: string, defaultValue: boolean): boolean {
  try {
    const value = localStorage.getItem(key);
    return value ? JSON.parse(value) : defaultValue;
  } catch (e) {
    return defaultValue;
  }
}

// Get settings from localStorage
const showPlayerStats = computed(() => getBool('showPlayerStats', false));
const showCoords = computed(() => getBool('showCoords', false));

// Create the player layer
function createPlayerLayer() {
  playerSource = new VectorSource();
  
  playerLayer = new VectorLayer({
    source: playerSource,
    zIndex: 1000, // High z-index to show on top
    properties: { name: 'players' },
    style: (function(feature: FeatureLike) {
      const name = feature.get('name') || 'Player';
      const hp = Number(feature.get('hp') || 0);
      const hpMax = Number(feature.get('hpMax') || 1);
      const hungry = Number(feature.get('hunger') || 0);
      const hungryMax = Number(feature.get('hungerMax') || 1);
      const temp = feature.get('temp');
      const bodyTemp = feature.get('bodyTemp');
      const rx = feature.get('rx');
      const ry = feature.get('ry');
      const rz = feature.get('rz');
      
      // Calculate health percentage for color
      const healthPercent = Math.max(0, Math.min(100, (hp / hpMax) * 100));
      const markerColor = healthPercent > 66 ? '#28a745' : healthPercent > 33 ? '#ffc107' : '#dc3545';
      
      const styles: Style[] = [];
      
      // Player marker
      styles.push(new Style({
        image: new Circle({
          radius: 5,
          fill: new Fill({ color: markerColor }),
          stroke: new Stroke({ color: '#FFFFFF', width: 2 })
        })
      }));
      
      // Player emoji
      styles.push(new Style({
        text: new Text({
          text: '🧍',
          font: '16px sans-serif',
          textAlign: 'center',
          textBaseline: 'middle',
          offsetX: 0,
          offsetY: 0
        })
      }));
      
      // Player name
      styles.push(new Style({
        text: new Text({
          text: name,
          font: '12px sans-serif',
          textAlign: 'left',
          textBaseline: 'top',
          offsetX: 12,
          offsetY: -42,
          backgroundFill: new Fill({ color: 'rgba(0,0,0,0.45)' }),
          padding: [2, 4, 2, 4],
          fill: new Fill({ color: '#FFFFFF' }),
          stroke: new Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
        })
      }));
      
      // Only add stats if enabled
      if (showPlayerStats.value) {
        let row = 1;
        
        // Health bar
        const healthBarStyles = createBarStyle(hp, hpMax, '❤️', '#ff4d4f', -42 + row * 14, 12);
        healthBarStyles.forEach(style => styles.push(style));
        row++;
        
        // Hunger bar
        const hungerBarStyles = createBarStyle(hungry, hungryMax, '🍗', '#2ecc71', -42 + row * 14, 12);
        hungerBarStyles.forEach(style => styles.push(style));
        row++;
        
        // Temperature
        if (temp !== undefined && temp !== null) {
          const tStr = `🌡️ ${Number(temp).toFixed(1)}°C`;
          const tColor = getTempColor(Number(temp));
          
          styles.push(new Style({
            text: new Text({
              text: tStr,
              font: '12px sans-serif',
              textAlign: 'left',
              textBaseline: 'top',
              offsetX: 12,
              offsetY: -42 + row * 14,
              backgroundFill: new Fill({ color: 'rgba(0,0,0,0.45)' }),
              padding: [2, 4, 2, 4],
              fill: new Fill({ color: tColor }),
              stroke: new Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
            })
          }));
          row++;
        }
        
        // Body temperature
        if (bodyTemp !== undefined && bodyTemp !== null) {
          const btStr = `🧍 ${Number(bodyTemp).toFixed(1)}°C`;
          
          styles.push(new Style({
            text: new Text({
              text: btStr,
              font: '12px sans-serif',
              textAlign: 'left',
              textBaseline: 'top',
              offsetX: 12,
              offsetY: -42 + row * 14,
              backgroundFill: new Fill({ color: 'rgba(0,0,0,0.45)' }),
              padding: [2, 4, 2, 4],
              fill: new Fill({ color: '#FFFFFF' }),
              stroke: new Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
            })
          }));
          row++;
        }
      }
      
      // Coordinates if enabled
      if (showCoords.value && rx !== undefined && ry !== undefined && rz !== undefined) {
        // Display world block coordinates directly
        const coordText = `Pos: ${Math.round(rx)}, ${Math.round(ry)}, ${Math.round(rz)}`;
        
        styles.push(new Style({
          text: new Text({
            text: coordText,
            font: '12px sans-serif',
            textAlign: 'left',
            textBaseline: 'top',
            offsetX: 12,
            offsetY: showPlayerStats.value ? -42 + 5 * 14 : -42 + 14,
            backgroundFill: new Fill({ color: 'rgba(0,0,0,0.45)' }),
            padding: [2, 4, 2, 4],
            fill: new Fill({ color: '#FFFFFF' }),
            stroke: new Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
          })
        }));
      }
      
      return styles;
    }) as any
  });
  
  // Set initial visibility
  playerLayer.setVisible(mapStore.layerVisibility.players);
  
  // Add the layer to the map
  mapStore.map?.addLayer(playerLayer);
}

// Helper function to create a bar style
function createBarStyle(current: number, max: number, icon: string, color: string, offsetY: number, offsetX: number): Style[] {
  const barLength = 16;
  const percent = Math.max(0, Math.min(100, (current / max) * 100));
  const fillCount = Math.round((percent / 100) * barLength);
  
  const barBg = '░'.repeat(barLength);
  const barFill = '█'.repeat(fillCount);
  
  const currentStr = String(Math.round(current));
  const maxStr = String(Math.round(max));
  const midText = `${currentStr}/${maxStr}`;
  
  // Background bar
  const bgStyle = new Style({
    text: new Text({
      text: `${icon} ${barBg}`,
      font: '10px sans-serif',
      textAlign: 'left',
      textBaseline: 'top',
      offsetX,
      offsetY,
      backgroundFill: new Fill({ color: 'rgba(0,0,0,0.45)' }),
      padding: [2, 4, 2, 4],
      fill: new Fill({ color: '#aaaaaa' }),
      stroke: new Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
    })
  });
  
  // Fill bar
  const fillStyle = new Style({
    text: new Text({
      text: `${icon} ${barFill}`,
      font: '10px sans-serif',
      textAlign: 'left',
      textBaseline: 'top',
      offsetX,
      offsetY,
      backgroundFill: new Fill({ color: 'rgba(0,0,0,0.0)' }),
      padding: [2, 4, 2, 4],
      fill: new Fill({ color }),
      stroke: new Stroke({ color: 'rgba(0,0,0,0.0)', width: 0 })
    })
  });
  
  // Text overlay
  const textStyle = new Style({
    text: new Text({
      text: `${icon} ${' '.repeat(Math.floor((barLength - midText.length) / 2))}${midText}`,
      font: '10px sans-serif',
      textAlign: 'left',
      textBaseline: 'top',
      offsetX,
      offsetY,
      backgroundFill: new Fill({ color: 'rgba(0,0,0,0.0)' }),
      padding: [2, 4, 2, 4],
      fill: new Fill({ color: '#FFFFFF' }),
      stroke: new Stroke({ color: 'rgba(0,0,0,0.0)', width: 0 })
    })
  });
  
  return [bgStyle, fillStyle, textStyle];
}

// Helper function to get temperature color
function getTempColor(temp: number): string {
  if (!isFinite(temp)) return '#FFFFFF';
  if (temp <= -10) return '#4aa3ff';
  if (temp <= 5) return '#5cb3ff';
  if (temp <= 18) return '#2ecc71';
  if (temp <= 28) return '#f1c40f';
  if (temp <= 35) return '#e67e22';
  return '#e74c3c';
}

// Update player features on the map
function updatePlayers() {
  if (!playerSource) return;
  
  // Clear existing features
  playerSource.clear();
  
  // Add players using world block coordinates (same as tiles)
  for (const player of liveStore.players) {
    const coords = player.coordinates;
    
    // Use coordinates directly - the TileGrid origin handles the coordinate system
    const worldX = coords.x;
    const worldZ = coords.z;  // Use Z directly
    
    const feature = new Feature({
      geometry: new Point([worldX, worldZ]),
      name: player.name,
      rx: coords.x,
      ry: coords.y,
      rz: coords.z,
      hp: player.health?.current,
      hpMax: player.health?.max,
      hunger: player.hunger?.current,
      hungerMax: player.hunger?.max,
      temp: player.temperature,
      bodyTemp: player.bodyTemp
    });
    
    playerSource.addFeature(feature);
  }
}

// Watch for changes in live data
watch(() => liveStore.players, updatePlayers, { deep: true });

// Watch for changes in layer visibility
watch(() => mapStore.layerVisibility.players, (visible) => {
  if (playerLayer) {
    playerLayer.setVisible(visible);
  }
});

// Watch for changes in player stats setting
watch(showPlayerStats, () => {
  if (playerSource) {
    playerSource.refresh();
  }
});

// Watch for changes in coordinates setting
watch(showCoords, () => {
  if (playerSource) {
    playerSource.refresh();
  }
});

// Watch for map becoming available
watch(() => mapStore.map, (newMap) => {
  if (newMap && !playerLayer) {
    createPlayerLayer();
    updatePlayers();
    
    // Start polling for live data (only start if not already polling)
    liveStore.startPolling(15000);
  }
}, { immediate: true });

onUnmounted(() => {
  if (mapStore.map && playerLayer) {
    mapStore.map.removeLayer(playerLayer);
  }
  // Stop polling when unmounting to prevent memory leaks
  liveStore.stopPolling();
});
</script>

<style scoped>
/* No styles needed as rendering is handled by OpenLayers */
</style>

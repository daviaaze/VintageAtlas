<template>
  <div class="animal-layer">
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

// Stores
const liveStore = useLiveStore();
const mapStore = useMapStore();

// Local state
let animalLayer: VectorLayer<VectorSource> | null = null;
let animalSource: VectorSource | null = null;

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
const showAnimalHP = computed(() => getBool('showAnimalHP', false));
const showAnimalEnv = computed(() => getBool('showAnimalEnv', false));
const showCoords = computed(() => getBool('showCoords', false));

// Create the animal layer
function createAnimalLayer() {
  animalSource = new VectorSource();
  
  animalLayer = new VectorLayer({
    source: animalSource,
    zIndex: 990, // Below players but above most other layers
    properties: { name: 'animals' },
    style: (function(feature: FeatureLike) {
      const type = feature.get('type') || '';
      const name = feature.get('name') || type || 'Animal';
      const hp = Number(feature.get('hp') || 0);
      const hpMax = Number(feature.get('hpMax') || 0);
      const hasHP = isFinite(hp) && isFinite(hpMax) && hpMax > 0;
      const temp = feature.get('temp');
      const rainfall = feature.get('rainfall');
      const windPercent = feature.get('windPercent');
      const hasEnv = (temp !== undefined && temp !== null) || 
                     (rainfall !== undefined && rainfall !== null) || 
                     (windPercent !== undefined && windPercent !== null);
      const rx = feature.get('rx');
      const ry = feature.get('ry');
      const rz = feature.get('rz');
      
      const styles: Style[] = [];
      
      // Animal marker
      styles.push(new Style({
        image: new Circle({
          radius: 4,
          fill: new Fill({ color: '#8B5A2B' }),
          stroke: new Stroke({ color: '#FFFFFF', width: 1 })
        })
      }));
      
      // Animal emoji
      styles.push(new Style({
        text: new Text({
          text: pickAnimalEmoji(name || type),
          font: '16px sans-serif',
          textAlign: 'center',
          textBaseline: 'middle',
          offsetX: 0,
          offsetY: 0
        })
      }));
      
      // Animal name
      styles.push(new Style({
        text: new Text({
          text: name,
          font: '11px sans-serif',
          textAlign: 'left',
          textBaseline: 'top',
          offsetX: 10,
          offsetY: -30,
          backgroundFill: new Fill({ color: 'rgba(0,0,0,0.35)' }),
          padding: [1, 3, 1, 3],
          fill: new Fill({ color: '#FFFFFF' }),
          stroke: new Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
        })
      }));
      
      let row = 1;
      
      // Health bar if enabled and available
      if (showAnimalHP.value && hasHP) {
        styles.push(...createBarStyle(hp, hpMax, '❤️', '#ff4d4f', -30 + row * 14, 10, 8));
        row++;
      }
      
      // Environment data if enabled and available
      if (showAnimalEnv.value && hasEnv) {
        let envText = '';
        
        if (temp !== undefined && temp !== null) {
          envText += `🌡️ ${Number(temp).toFixed(1)}°C`;
        }
        
        if (rainfall !== undefined && rainfall !== null && Number(rainfall) > 0) {
          envText += ' 🌧️';
        }
        
        if (windPercent !== undefined && windPercent !== null) {
          envText += ` 🌬️ ${Math.round(Number(windPercent))}%`;
        }
        
        if (envText) {
          styles.push(new Style({
            text: new Text({
              text: envText,
              font: '11px sans-serif',
              textAlign: 'left',
              textBaseline: 'top',
              offsetX: 10,
              offsetY: -30 + row * 14,
              backgroundFill: new Fill({ color: 'rgba(0,0,0,0.35)' }),
              padding: [1, 3, 1, 3],
              fill: new Fill({ color: temp !== undefined ? getTempColor(Number(temp)) : '#FFFFFF' }),
              stroke: new Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
            })
          }));
          row++;
        }
      }
      
      // Coordinates if enabled
      if (showCoords.value && rx !== undefined && ry !== undefined && rz !== undefined) {
        const coordText = `X: ${Math.round(rx)} Z: ${Math.round(rz)} Y: ${Math.round(ry)}`;
        
        styles.push(new Style({
          text: new Text({
            text: coordText,
            font: '11px sans-serif',
            textAlign: 'left',
            textBaseline: 'top',
            offsetX: 10,
            offsetY: -30 + row * 14,
            backgroundFill: new Fill({ color: 'rgba(0,0,0,0.35)' }),
            padding: [1, 3, 1, 3],
            fill: new Fill({ color: '#FFFFFF' }),
            stroke: new Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
          })
        }));
      }
      
      return styles;
    }) as any
  });
  
  // Set initial visibility
  animalLayer.setVisible(mapStore.layerVisibility.animals);
  
  // Add the layer to the map
  mapStore.map?.addLayer(animalLayer);
}

// Helper function to create a bar style
function createBarStyle(current: number, max: number, icon: string, color: string, offsetY: number, offsetX: number, barLength = 16): Style[] {
  const percent = Math.max(0, Math.min(100, (current / max) * 100));
  const fillCount = Math.round((percent / 100) * barLength);
  
  const barBg = '░'.repeat(barLength);
  const barFill = '█'.repeat(fillCount);
  
  const currentStr = String(Math.round(current));
  const maxStr = String(Math.round(max));
  const midText = `${currentStr}/${maxStr}`;
  
  const styles: Style[] = [];
  
  // Background bar
  styles.push(new Style({
    text: new Text({
      text: `${icon} ${barBg}`,
      font: '10px sans-serif',
      textAlign: 'left',
      textBaseline: 'top',
      offsetX,
      offsetY,
      backgroundFill: new Fill({ color: 'rgba(0,0,0,0.35)' }),
      padding: [1, 3, 1, 3],
      fill: new Fill({ color: '#aaaaaa' }),
      stroke: new Stroke({ color: 'rgba(0,0,0,0.8)', width: 3 })
    })
  }));
  
  // Fill bar
  styles.push(new Style({
    text: new Text({
      text: `${icon} ${barFill}`,
      font: '10px sans-serif',
      textAlign: 'left',
      textBaseline: 'top',
      offsetX,
      offsetY,
      backgroundFill: new Fill({ color: 'rgba(0,0,0,0.0)' }),
      padding: [1, 3, 1, 3],
      fill: new Fill({ color }),
      stroke: new Stroke({ color: 'rgba(0,0,0,0.0)', width: 0 })
    })
  }));
  
  // Text overlay
  styles.push(new Style({
    text: new Text({
      text: `${icon} ${' '.repeat(Math.floor((barLength - midText.length) / 2))}${midText}`,
      font: '10px sans-serif',
      textAlign: 'left',
      textBaseline: 'top',
      offsetX,
      offsetY,
      backgroundFill: new Fill({ color: 'rgba(0,0,0,0.0)' }),
      padding: [1, 3, 1, 3],
      fill: new Fill({ color: '#FFFFFF' }),
      stroke: new Stroke({ color: 'rgba(0,0,0,0.0)', width: 0 })
    })
  }));
  
  return styles;
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

// Helper function to pick an animal emoji
function pickAnimalEmoji(str: string): string {
  if (!str) return '❓';
  const s = String(str).toLowerCase();
  
  // Birds
  if (s.includes('henpoult') || s.includes('pullet')) return '🐥';
  if (s.includes('chicken-baby')) return '🐔';
  if (s.includes('chick')) return '🐥';
  if (s.includes('rooster')) return '🐓';
  if (s.includes('hen')) return '🐔';
  if (s.includes('duck')) return '🦆';
  if (s.includes('owl')) return '🦉';
  if (s.includes('robin')) return '🐦';
  if (s.includes('waxwing')) return '🐦';
  if (s.includes('sparrow') || s.includes('house-sparrow')) return '🐦';
  if (s.includes('swan')) return '🦢';
  
  // Mammals
  if (s.includes('raccoon')) return '🦝';
  if (s.includes('wolf')) return '🐺';
  if (s.includes('fox')) return '🦊';
  if (s.includes('hare')) return '🐇';
  if (s.includes('goat')) return '🐐';
  if (s.includes('bear')) return '🐻';
  if (s.includes('boar')) return '🐗';
  if (s.includes('deer')) return '🦌';
  if (s.includes('chipmunk') || s.includes('squirrel')) return '🐿️';
  if (s.includes('fieldmouse') || s.includes('field-mouse') || s.includes('field mouse')) return '🐭';
  if (s.includes('hedgehog')) return '🦔';
  if (s.includes('yak')) return '🐂';
  if (s.includes('crow')) return '🐦‍⬛';
  
  // Invertebrates
  if (s.includes('snail')) return '🐌';
  if (s.includes('crab')) return '🦀';
  
  // Fish
  if (s.includes('salmon')) return '🐟';
  
  // Other
  if (s.includes('strawdummy') || s.includes('straw')) return '🧍‍♂️';
  if (s.includes('animal')) return '🐾';
  
  return '❓';
}

// Update animal features on the map
function updateAnimals() {
  if (!animalSource) return;
  
  // Clear existing features
  animalSource.clear();
  
  // Get spawn point for relative coordinates
  const spawn = liveStore.spawnPoint;
  
  // Add animals
  for (const animal of liveStore.animals) {
    const coords = animal.coordinates;
    const rx = coords.x - spawn.x;
    const rz = -(coords.z - spawn.z); // Negate z for OpenLayers
    
    const feature = new Feature({
      geometry: new Point([rx, rz]),
      type: animal.type,
      name: animal.name || animal.type,
      rx: coords.x,
      ry: coords.y,
      rz: coords.z,
      hp: animal.health?.current,
      hpMax: animal.health?.max,
      temp: animal.temperature,
      rainfall: animal.rainfall,
      windPercent: animal.wind?.percent
    });
    
    animalSource.addFeature(feature);
  }
}

// Watch for changes in live data
watch(() => liveStore.animals, updateAnimals, { deep: true });

// Watch for changes in layer visibility
watch(() => mapStore.layerVisibility.animals, (visible) => {
  if (animalLayer) {
    animalLayer.setVisible(visible);
  }
});

// Watch for changes in animal HP setting
watch(showAnimalHP, () => {
  if (animalSource) {
    animalSource.refresh();
  }
});

// Watch for changes in animal environment setting
watch(showAnimalEnv, () => {
  if (animalSource) {
    animalSource.refresh();
  }
});

// Watch for changes in coordinates setting
watch(showCoords, () => {
  if (animalSource) {
    animalSource.refresh();
  }
});

// Lifecycle hooks
onMounted(() => {
  if (mapStore.map) {
    createAnimalLayer();
    updateAnimals();
  }
});

onUnmounted(() => {
  if (mapStore.map && animalLayer) {
    mapStore.map.removeLayer(animalLayer);
  }
});
</script>

<style scoped>
/* No styles needed as rendering is handled by OpenLayers */
</style>

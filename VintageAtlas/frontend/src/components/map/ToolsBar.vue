<template>
  <div class="tools-bar">
    <button class="tool-btn" title="Go to Spawn" @click="goToSpawn">
      🧭
    </button>
    <button class="tool-btn" title="Go to Coordinates" @click="goToCoords">
      🔍
    </button>
    <button class="tool-btn" title="Copy Link to View" @click="copyLink">
      🔗
    </button>
    <!-- Placeholder for future Landmark search -->
  </div>
</template>

<script setup lang="ts">
import { useMapStore } from '@/stores/map';

const mapStore = useMapStore();

function goToSpawn() {
  mapStore.goToSpawn();
}

function parseCoordsInput(input: string): { x: number, z: number } | null {
  if (!input) return null;
  // Try simple format: "x,z"
  const csv = input.split(',').map(s => s.trim());
  if (csv.length === 2 && isFinite(Number(csv[0])) && isFinite(Number(csv[1]))) {
    return { x: Number(csv[0]), z: Number(csv[1]) };
  }
  // Try campaign cartographer style: X = -1170, Y = 113, Z = -3800
  const match = input.match(/X\s*=\s*(-?\d+).*Z\s*=\s*(-?\d+)/i);
  if (match) {
    return { x: Number(match[1]), z: Number(match[2]) };
  }
  return null;
}

async function goToCoords() {
  const input = window.prompt('Enter coordinates (X,Z) or "X = 123, Y = 0, Z = 456"', '0,0');
  if (!input) return;
  const parsed = parseCoordsInput(input);
  if (!parsed) {
    window.alert('Invalid format. Use "X,Z" (e.g., 2050,6900) or "X = 2050, Y = 0, Z = 6900".');
    return;
  }
  const { x, z } = parsed;
  // Map coordinate Y corresponds to inverted game Z for display; use negative to move correctly
  mapStore.flyTo([x, -z]);
}

async function copyLink() {
  const { center, zoom } = mapStore.getCenterZoom();
  const x = Math.round(center[0]);
  const yGame = Math.round(-center[1]); // invert back to game Z for URL
  const url = new URL(window.location.href);
  url.searchParams.set('x', String(x));
  url.searchParams.set('y', String(yGame));
  url.searchParams.set('zoom', String(Math.round(zoom)));
  const text = url.toString();
  try {
    await navigator.clipboard.writeText(text);
    window.alert('Link copied to clipboard');
  } catch {
    // Fallback
    window.prompt('Copy this link:', text);
  }
}
</script>

<style scoped>
.tools-bar {
  position: absolute;
  top: 12px;
  right: 12px;
  display: flex;
  gap: 8px;
  background: rgba(30, 41, 59, 0.6);
  padding: 6px;
  border-radius: 8px;
  backdrop-filter: blur(4px);
  z-index: 1000;
}
.tool-btn {
  font-size: 16px;
  line-height: 1;
  padding: 6px 8px;
  border: none;
  border-radius: 6px;
  color: #fff;
  background: rgba(255,255,255,0.08);
  cursor: pointer;
}
.tool-btn:hover {
  background: rgba(255,255,255,0.18);
}
.tool-btn:disabled {
  opacity: 0.5;
  cursor: default;
}
</style>

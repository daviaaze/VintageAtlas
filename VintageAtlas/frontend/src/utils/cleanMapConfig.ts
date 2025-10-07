/**
 * Clean Map Configuration - Zero Custom Logic
 * Backend provides everything, frontend just uses it
 */
import TileGrid from 'ol/tilegrid/TileGrid';
import { fetchMapConfig } from '@/services/api/mapConfig';

let config: any = null;

/**
 * Initialize from server
 */
export async function initMapConfig() {
  config = await fetchMapConfig();
  console.log('[CleanMapConfig] Loaded:', config);
}

/**
 * Create tile grid - directly from server config
 */
export function createTileGrid(): TileGrid {
  if (!config) throw new Error('Config not initialized');
  
  return new TileGrid({
    extent: config.worldExtent,
    origin: config.worldOrigin,
    resolutions: config.tileResolutions,
    tileSize: [config.tileSize, config.tileSize]
  });
}

/**
 * Get tile URL - backend tells us exactly what to do
 * For now: simple absolute coordinates
 * Future: backend can provide custom template/function
 */
export function getTileUrl(z: number, x: number, y: number): string {
  if (!config) return '';
  
  // Simple absolute mode: tiles stored as /tiles/{z}/{x}_{y}.png
  // Where x,y are the tile grid coordinates directly
  return `/tiles/${z}/${x}_${y}.png`;
}

/**
 * Get view resolutions - use tile resolutions for simplicity
 */
export function getViewResolutions(): number[] {
  if (!config) throw new Error('Config not initialized');
  // Use tile resolutions directly - view and tiles must match
  return config.tileResolutions;
}

/**
 * Get view center
 */
export function getViewCenter(): [number, number] {
  if (!config) throw new Error('Config not initialized');
  return config.defaultCenter;
}

/**
 * Get view zoom
 */
export function getViewZoom(): number {
  if (!config) throw new Error('Config not initialized');
  return config.defaultZoom;
}

/**
 * Format coordinates for display
 */
export function formatCoords(coord: [number, number]): string {
  const x = Math.round(coord[0]);
  const y = Math.round(coord[1]);
  return `${x}, ${y}`;
}

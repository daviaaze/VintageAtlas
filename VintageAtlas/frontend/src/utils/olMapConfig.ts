/**
 * Clean OpenLayers Map Configuration
 * Based on OPENLAYERS_SPECIFICATION.md
 */
import TileGrid from 'ol/tilegrid/TileGrid';
import { fetchMapConfig } from '@/services/api/mapConfig';

// Server configuration cache
let serverConfig: any = null;

/**
 * Initialize configuration from server
 */
export async function initOLConfig() {
  serverConfig = await fetchMapConfig();
}

/**
 * Create tile grid for world tiles - WebCartographer approach
 * Uses fixed extent and origin exactly like WebCartographer
 */
export function createTileGrid(): TileGrid {
  if (!serverConfig) throw new Error('Config not initialized');
  
  const extent = serverConfig.worldExtent;
  const origin = serverConfig.worldOrigin;
  const resolutions = serverConfig.tileResolutions;
  const tileSize = serverConfig.tileSize;

  console.log('[createTileGrid] Creating WebCartographer-style tile grid with:', {
    extent,
    origin,
    resolutions,
    tileSize,
    note: 'Using fixed coordinates like WebCartographer'
  });
  
  // WebCartographer-style tile grid: fixed extent and origin
  const grid = new TileGrid({
    extent,     // [-512000, -512000, 512000, 512000]
    origin,     // [-512000, 512000]  
    resolutions, // [512, 256, 128, 64, 32, 16, 8, 4, 2, 1]
    tileSize: [tileSize, tileSize] // [256, 256]
  });
  
  return grid;
}

/**
 * Get view resolutions (Spec lines 72-83)
 * View has more levels than tiles for smooth zooming
 */
export function getViewResolutions(): number[] {
  if (!serverConfig) throw new Error('Config not initialized');
  return serverConfig.viewResolutions;
}

/**
 * Get initial view center (spawn point or default)
 * 
 * CLEAN ARCHITECTURE: Backend provides tile-space coordinates directly.
 * No transformations needed!
 */
export function getViewCenter(): [number, number] {
  if (!serverConfig) throw new Error('Config not initialized');
  
  const center = serverConfig.defaultCenter;
  
  console.log('[getViewCenter] Using backend world block coordinates:', {
    center,
    extent: serverConfig.worldExtent,
    origin: serverConfig.worldOrigin
  });
  
  return [center[0], center[1]];
}

/**
 * Get initial zoom level
 */
export function getViewZoom(): number {
  if (!serverConfig) throw new Error('Config not initialized');
  return serverConfig.defaultZoom;
}

/**
 * Generate tile URL from OpenLayers grid coordinates.
 * 
 * CLEAN ARCHITECTURE: Backend handles all coordinate transformations!
 * - OpenLayers provides 0-based grid coordinates (x, y) at zoom level z
 * - Frontend passes these directly to backend
 * - Backend transforms grid coordinates to storage tile numbers
 * - No frontend coordinate logic needed!
 */
export function getTileUrl(z: number, x: number, y: number): string {
  // Simple passthrough: backend does the heavy lifting
  return `/tiles/${z}/${x}_${y}.png`;
}

/**
 * Format coordinates for display
 * 
 * WebCartographer-style coordinate display: Game world coordinates
 * Convert from OpenLayers coordinates back to game coordinates for display
 */
export function formatCoords(coord: [number, number]): string {
  const x = Math.round(coord[0]);
  const z = Math.round(-coord[1]); // Negate Y to show as positive Z like in game
  return `${x}, ${z}`;
}

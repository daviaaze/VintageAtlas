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
  console.log('[OL Config] Loaded from server:', serverConfig);
}

/**
 * Create tile grid for world tiles (Spec lines 32-62)
 */
export function createTileGrid(): TileGrid {
  if (!serverConfig) throw new Error('Config not initialized');
  
  const extent = serverConfig.worldExtent;
  const origin = serverConfig.worldOrigin;
  const resolutions = serverConfig.tileResolutions;
  const tileSize = serverConfig.tileSize;
  
  console.log('[createTileGrid] Creating with:', {
    extent,
    origin,
    resolutions,
    tileSize,
    numResolutions: resolutions.length
  });
  
  // Let OpenLayers use default bottom-left origin from extent
  // This matches our tile storage coordinate system naturally
  const grid = new TileGrid({
    extent,
    resolutions,
    tileSize: [tileSize, tileSize]
  });
  
  console.log('[createTileGrid] Created grid:', {
    minZoom: 0,
    maxZoom: resolutions.length - 1,
    resolution0: grid.getResolution(0),
    tileSize: grid.getTileSize(0)
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
 * CLEAN ARCHITECTURE: Shows tile coordinates directly
 */
export function formatCoords(coord: [number, number]): string {
  const x = Math.round(coord[0]);
  const y = Math.round(coord[1]);
  return `Tile: ${x}, ${y}`;
}

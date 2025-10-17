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
  
  const extent = serverConfig.worldExtent as [number, number, number, number];
  const origin = serverConfig.worldOrigin as [number, number];
  const olExtent: [number, number, number, number] = [extent[0], extent[1], extent[2], extent[3]];
  const olOrigin: [number, number] = [origin[0], origin[1]];
  const resolutions = serverConfig.tileResolutions;
  const tileSize = serverConfig.tileSize;

  console.log('[createTileGrid] Creating WebCartographer-style tile grid with:', {
    extent: olExtent,
    origin: olOrigin,
    resolutions,
    tileSize,
  });

  const grid = new TileGrid({
    extent: extent,
    origin: olOrigin,
    resolutions,
    tileSize: [tileSize, tileSize]
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

  console.log('[getViewCenter] Using default center:', center);
  
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
 * Get the view extent in OpenLayers coordinates
 * Backend provides extent as [minX, minZ, maxX, maxZ]
 * OL internal Y axis is inverted vs game Z, so flip Z to Y: [minX, -maxZ, maxX, -minZ]
 */
export function getViewExtent(): [number, number, number, number] {
  if (!serverConfig) throw new Error('Config not initialized');
  const e = serverConfig.worldExtent as [number, number, number, number];
  return [e[0], e[1], e[2], e[3]];
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
  const z = Math.round(-coord[1]);
  return `${x}, ${z}`;
}

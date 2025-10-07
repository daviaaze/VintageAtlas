/**
 * Simplified WebCartographer-style map configuration
 * Uses spawn-centered coordinates with simple tile grid
 */

import TileGrid from 'ol/tilegrid/TileGrid';
import { fetchMapConfig, type MapConfigData } from '../services/api/mapConfig';

// Configuration loaded from server
let serverConfig: MapConfigData | null = null;

/**
 * Initialize map configuration from server
 * WebCartographer style: Keep it simple, fetch once
 */
export async function initMapConfig(): Promise<void> {
  if (serverConfig) return; // Already loaded
  
  try {
    serverConfig = await fetchMapConfig();
    console.log('[SimpleMapConfig] ✅ Server config loaded:', {
      tileSize: serverConfig.tileSize,
      baseZoom: serverConfig.baseZoomLevel,
      spawnPosition: serverConfig.spawnPosition,
      worldExtent: serverConfig.worldExtent
    });
  } catch (error) {
    console.error('[SimpleMapConfig] ❌ Failed to load config:', error);
    throw error;
  }
}

/**
 * Get tile grid for the map
 * WebCartographer uses fixed extent and origin - we use server-provided values
 */
export function createTileGrid(): TileGrid {
  if (!serverConfig) throw new Error('Config not loaded');
  
  return new TileGrid({
    extent: serverConfig.worldExtent,
    origin: serverConfig.worldOrigin,
    resolutions: serverConfig.tileResolutions,
    tileSize: [serverConfig.tileSize, serverConfig.tileSize]
  });
}

/**
 * Get tile URL from OpenLayers grid coordinates.
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
 * Get view resolutions
 * WebCartographer has more view resolutions than tile resolutions for smooth zooming
 */
export function getViewResolutions(): number[] {
  if (!serverConfig) throw new Error('Config not loaded');
  return serverConfig.viewResolutions;
}

/**
 * Get tile resolutions (must match tile grid)
 */
export function getTileResolutions(): number[] {
  if (!serverConfig) throw new Error('Config not loaded');
  return serverConfig.tileResolutions;
}

/**
 * Get default map center
 * WebCartographer uses [0, 0] for spawn
 */
export function getDefaultCenter(): [number, number] {
  if (!serverConfig) throw new Error('Config not loaded');
  return [serverConfig.defaultCenter[0], serverConfig.defaultCenter[1]];
}

/**
 * Get default zoom level
 */
export function getDefaultZoom(): number {
  if (!serverConfig) throw new Error('Config not loaded');
  return serverConfig.defaultZoom;
}

/**
 * Get zoom constraints
 */
export function getMinZoom(): number {
  if (!serverConfig) throw new Error('Config not loaded');
  return serverConfig.minZoom;
}

export function getMaxZoom(): number {
  if (!serverConfig) throw new Error('Config not loaded');
  return serverConfig.maxZoom;
}

/**
 * Get world extent
 */
export function getWorldExtent(): number[] {
  if (!serverConfig) throw new Error('Config not loaded');
  return serverConfig.worldExtent;
}

/**
 * Format coordinates for display
 * Shows tile coordinates directly from the map
 */
export function formatCoordinates(x: number, y: number): string {
  if (!serverConfig) return 'Loading...';
  
  // Display tile coordinates as-is (these match the backend tile grid)
  const tileX = Math.round(x);
  const tileY = Math.round(y);
  
  return `Tile: ${tileX}, ${tileY}`;
}

/**
 * Get spawn position
 */
export function getSpawnPosition(): [number, number] {
  if (!serverConfig) throw new Error('Config not loaded');
  return [serverConfig.spawnPosition[0], serverConfig.spawnPosition[1]];
}

/**
 * Get raw config for debugging
 */
export function getRawConfig(): MapConfigData | null {
  return serverConfig;
}

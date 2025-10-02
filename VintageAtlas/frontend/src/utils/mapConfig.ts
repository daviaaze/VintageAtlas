import TileGrid from 'ol/tilegrid/TileGrid';
import { fetchMapConfig, type MapConfigData } from '../services/api/mapConfig';

// Dynamic configuration - will be loaded from API
let dynamicConfig: MapConfigData | null = null;

/**
 * Initialize map configuration from API
 * This should be called before creating the map
 */
export async function initializeMapConfig(): Promise<void> {
  try {
    dynamicConfig = await fetchMapConfig();
    console.log('Map configuration loaded from server:', dynamicConfig);
  } catch (error) {
    console.error('Failed to load map config, using fallback:', error);
  }
}

/**
 * Get configuration value with fallback to defaults
 */
function getConfig<K extends keyof MapConfigData>(
  key: K,
  fallback: MapConfigData[K]
): MapConfigData[K] {
  return dynamicConfig?.[key] ?? fallback;
}

/**
 * Configuration for the Vintage Story world map
 * These values are now fetched from the API but have sensible defaults
 */
export const worldExtent = (): number[] => 
  getConfig('worldExtent', [-512000, -512000, 512000, 512000]);

export const worldOrigin = (): number[] => 
  getConfig('worldOrigin', [-512000, 512000]);

// Tile grid resolutions - must match the actual tile zoom levels (1-9)
export const tileResolutions = (): number[] => 
  getConfig('tileResolutions', [512, 256, 128, 64, 32, 16, 8, 4, 2, 1]);

// View resolutions - can have more detail for smooth zooming
export const worldResolutions = (): number[] => 
  getConfig('viewResolutions', [256, 128, 64, 32, 16, 8, 4, 2, 1, 0.5, 0.25, 0.125]);

/**
 * Tile grid for the world map
 * This is a function now since it depends on dynamic config
 */
export const createWorldTileGrid = (): TileGrid => {
  return new TileGrid({
    extent: worldExtent(),
    origin: worldOrigin(),
    resolutions: tileResolutions(),
    tileSize: [getConfig('tileSize', 256), getConfig('tileSize', 256)]
  });
};

/**
 * Default map center - fetched from server or based on actual tile coverage
 */
export const defaultCenter = (): [number, number] => {
  const center = getConfig('defaultCenter', [0, -5000]);
  return [center[0], center[1]];
};

/**
 * Default map zoom level
 */
export const defaultZoom = (): number => 
  getConfig('defaultZoom', 7);

/**
 * Map zoom constraints
 */
export const minZoom = (): number => 
  getConfig('minZoom', 0);

export const maxZoom = (): number => 
  getConfig('maxZoom', 9);

/**
 * Get the raw dynamic config (useful for debugging)
 */
export const getDynamicConfig = (): MapConfigData | null => dynamicConfig;

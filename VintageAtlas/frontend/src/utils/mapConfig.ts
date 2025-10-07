import TileGrid from 'ol/tilegrid/TileGrid';
import { fetchMapConfig, type MapConfigData } from '../services/api/mapConfig';

// Dynamic configuration - will be loaded from API
let dynamicConfig: MapConfigData | null = null;

/**
 * Initialize map configuration from API
 * This should be called before creating the map
 * @throws Error if config cannot be loaded
 */
export async function initializeMapConfig(): Promise<void> {
  try {
    dynamicConfig = await fetchMapConfig();
    console.log('✅ [MapConfig] Configuration loaded from server:', dynamicConfig);
    console.log('   World extent (blocks):', dynamicConfig?.worldExtent);
    console.log('   World origin (blocks):', dynamicConfig?.worldOrigin);
  } catch (error) {
    console.error('❌ [MapConfig] Failed to load map config from server:', error);
    throw new Error(`Cannot initialize map without server configuration: ${error}`);
  }
}

/**
 * Get configuration value - throws if not available
 * NO FALLBACKS - server must provide all required config
 */
function getConfig<K extends keyof MapConfigData>(key: K): MapConfigData[K] {
  if (!dynamicConfig) {
    throw new Error(`❌ Map config not initialized! Attempted to access: ${String(key)}`);
  }
  
  const value = dynamicConfig[key];
  if (value === undefined || value === null) {
    throw new Error(`❌ Required config key '${String(key)}' is missing from server configuration. Server sent: ${JSON.stringify(Object.keys(dynamicConfig))}`);
  }
  
  return value;
}

/**
 * Check if config is loaded
 */
export function isConfigLoaded(): boolean {
  return dynamicConfig !== null;
}


/**
 * Get spawn position
 */
export const getSpawnPosition = (): [number, number] => {
  const spawn = getConfig('spawnPosition');
  return [spawn[0], spawn[1]];
};


/**
 * Configuration for the Vintage Story world map
 * These values are now fetched from the API (already transformed by backend)
 */
export const worldExtent = (): number[] => {
  const extent = getConfig('worldExtent');
  
  // Validate extent structure
  if (!extent || extent.length !== 4 || !extent.every(n => isFinite(n))) {
    throw new Error(`❌ Invalid extent from server: ${JSON.stringify(extent)}. Must be [minX, minZ, maxX, maxZ] with finite numbers.`);
  }
  
  console.log('   World extent validated:', extent);
  return extent;
};

export const worldOrigin = (): number[] => {
  const origin = getConfig('worldOrigin');
  
  // Validate origin structure
  if (!origin || origin.length !== 2 || !origin.every(n => isFinite(n))) {
    throw new Error(`❌ Invalid origin from server: ${JSON.stringify(origin)}. Must be [x, z] with finite numbers.`);
  }
  
  console.log('   World origin validated:', origin);
  return origin;
};

// Tile grid resolutions - must match the actual tile zoom levels (1-9)
export const tileResolutions = (): number[] => 
  getConfig('tileResolutions');

// View resolutions - can have more detail for smooth zooming
export const worldResolutions = (): number[] => 
  getConfig('viewResolutions');

/**
 * Tile grid for the world map
 * This is a function now since it depends on dynamic config
 */
export const createWorldTileGrid = (): TileGrid => {
  const tileSize = getConfig('tileSize');
  return new TileGrid({
    extent: worldExtent(),
    origin: worldOrigin(),
    resolutions: tileResolutions(),
    tileSize: [tileSize, tileSize]
  });
};

/**
 * Default map center - fetched from server or based on actual tile coverage
 */
export const defaultCenter = (): [number, number] => {
  const center = getConfig('defaultCenter');
  return [center[0], center[1]];
};

/**
 * Default map zoom level
 */
export const defaultZoom = (): number => 
  getConfig('defaultZoom');

/**
 * Map zoom constraints
 */
export const minZoom = (): number => 
  getConfig('minZoom');

export const maxZoom = (): number => 
  getConfig('maxZoom');

/**
 * Format coordinates for display to user
 * @param x X coordinate from map (tile grid coordinate)
 * @param z Z coordinate from map (tile grid coordinate)
 * @returns Formatted string suitable for display
 *
 * CLEAN ARCHITECTURE: Backend provides tile-space coordinates directly.
 * These match the tile storage coordinates exactly.
 */
export const formatCoordinates = (x: number, z: number): string => {
  // Handle invalid coordinates
  if (!isFinite(x) || !isFinite(z)) {
    return 'Loading...';
  }

  // Display tile coordinates (matching backend tile grid)
  return `Tile: ${Math.round(x)}, ${Math.round(z)}`;
};

/**
 * Get coordinate system info for display
 */
export const getCoordinateSystemInfo = (): string => {
  return 'Showing tile coordinates (matches backend tile storage)';
};

/**
 * Get the raw dynamic config (useful for debugging)
 */
export const getDynamicConfig = (): MapConfigData | null => dynamicConfig;

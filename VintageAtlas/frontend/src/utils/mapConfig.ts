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
    console.log('   Tile offset:', dynamicConfig?.tileOffset);
    console.log('   World extent:', dynamicConfig?.worldExtent);
    console.log('   Absolute positions:', dynamicConfig?.absolutePositions);
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
 * Get if using absolute position coordinates
 */
export const isAbsolutePositions = (): boolean => 
  getConfig('absolutePositions');

/**
 * Get spawn position
 */
export const getSpawnPosition = (): [number, number] => {
  const spawn = getConfig('spawnPosition');
  return [spawn[0], spawn[1]];
};

/**
 * Get tile coordinate offset for spawn-relative mode
 * This offset must be added to display tile coords to get absolute tile coords
 */
export const getTileOffset = (): [number, number] => {
  const offset = getConfig('tileOffset');
  if (!offset || offset.length < 2) {
    throw new Error('❌ Invalid tileOffset from server: must be [x, z] array');
  }
  return [offset[0], offset[1]];
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
 * @param x X coordinate from map (already transformed by backend)
 * @param z Z coordinate from map (already transformed and flipped by backend)
 * @returns Formatted string suitable for display
 *
 * COORDINATE SYSTEM:
 * - Backend sends worldExtent already transformed based on AbsolutePositions setting
 * - In absolute mode: coordinates are actual world coordinates
 * - In relative mode: coordinates are spawn-relative with Z-axis flipped (North is negative)
 */
export const formatCoordinates = (x: number, z: number): string => {
  // Handle invalid coordinates
  if (!isFinite(x) || !isFinite(z)) {
    return 'Loading...';
  }

  if (isAbsolutePositions()) {
    // Show absolute world coordinates
    return `${Math.round(x)}, ${Math.round(z)}`;
  } else {
    // Show spawn-relative coordinates with directional indicators
    // Backend already transformed: spawn is at (0, 0) and Z is flipped
    const ew = x >= 0 ? 'E' : 'W';
    const ns = z <= 0 ? 'N' : 'S'; // Z is negative = North, positive = South
    return `${Math.abs(Math.round(x))}${ew}, ${Math.abs(Math.round(z))}${ns} from spawn`;
  }
};

/**
 * Get coordinate system info for display
 */
export const getCoordinateSystemInfo = (): string => {
  if (isAbsolutePositions()) {
    return 'Using absolute world coordinates';
  } else {
    return 'Using spawn-relative coordinates (spawn at 0, 0)';
  }
};

/**
 * Get the raw dynamic config (useful for debugging)
 */
export const getDynamicConfig = (): MapConfigData | null => dynamicConfig;

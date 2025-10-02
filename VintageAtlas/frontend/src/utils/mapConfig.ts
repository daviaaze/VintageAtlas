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
 * Get if using absolute position coordinates
 */
export const isAbsolutePositions = (): boolean => 
  getConfig('absolutePositions', false);

/**
 * Get spawn position
 */
export const getSpawnPosition = (): [number, number] => {
  const spawn = getConfig('spawnPosition', [0, 0]);
  return [spawn[0], spawn[1]];
};

/**
 * Transform world extent based on coordinate mode
 * If using relative coordinates, transforms absolute extent to spawn-relative
 */
function transformWorldExtent(extent: number[]): number[] {
  // Validate extent
  if (!extent || extent.length !== 4 || !extent.every(n => isFinite(n))) {
    console.warn('Invalid extent, using fallback');
    return [-512000, -512000, 512000, 512000];
  }
  
  if (isAbsolutePositions()) {
    // Use absolute coordinates as-is
    return extent;
  } else {
    // Transform to spawn-relative coordinates
    const [spawnX, spawnZ] = getSpawnPosition();
    
    // Validate spawn position
    if (!isFinite(spawnX) || !isFinite(spawnZ)) {
      console.warn('Invalid spawn position, using extent as-is');
      return extent;
    }
    
    return [
      extent[0] - spawnX,       // minX relative
      -(extent[1] - spawnZ),    // minZ relative (Z-axis flipped)
      extent[2] - spawnX,       // maxX relative
      -(extent[3] - spawnZ)     // maxZ relative (Z-axis flipped)
    ];
  }
}

/**
 * Transform world origin based on coordinate mode
 */
function transformWorldOrigin(origin: number[]): number[] {
  // Validate origin
  if (!origin || origin.length !== 2 || !origin.every(n => isFinite(n))) {
    console.warn('Invalid origin, using fallback');
    return [-512000, 512000];
  }
  
  if (isAbsolutePositions()) {
    // Use absolute coordinates as-is
    return origin;
  } else {
    // Transform to spawn-relative coordinates
    const [spawnX, spawnZ] = getSpawnPosition();
    
    // Validate spawn position
    if (!isFinite(spawnX) || !isFinite(spawnZ)) {
      console.warn('Invalid spawn position, using origin as-is');
      return origin;
    }
    
    return [
      origin[0] - spawnX,       // X relative
      -(origin[1] - spawnZ)     // Z relative (Z-axis flipped)
    ];
  }
}

/**
 * Configuration for the Vintage Story world map
 * These values are now fetched from the API but have sensible defaults
 */
export const worldExtent = (): number[] => {
  const extent = getConfig('worldExtent', [-512000, -512000, 512000, 512000]);
  return transformWorldExtent(extent);
};

export const worldOrigin = (): number[] => {
  const origin = getConfig('worldOrigin', [-512000, 512000]);
  return transformWorldOrigin(origin);
};

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
 * Format coordinates for display to user
 * @param x X coordinate
 * @param z Z coordinate (note: already includes flip if in relative mode)
 * @returns Formatted string suitable for display
 */
export const formatCoordinates = (x: number, z: number): string => {
  // Handle invalid coordinates
  if (!isFinite(x) || !isFinite(z)) {
    return 'Loading...';
  }
  
  if (isAbsolutePositions()) {
    // Show absolute coordinates
    return `${Math.round(x)}, ${Math.round(z)}`;
  } else {
    // Show relative coordinates with directional indicators
    const ew = x >= 0 ? 'E' : 'W';
    const ns = z <= 0 ? 'N' : 'S'; // Note: Z is flipped in relative mode
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

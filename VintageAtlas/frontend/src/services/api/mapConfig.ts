/**
 * API service for fetching dynamic map configuration from the server
 */

export interface MapConfigData {
  worldExtent: number[];
  worldOrigin: number[];
  defaultCenter: number[];
  defaultZoom: number;
  minZoom: number;
  maxZoom: number;
  baseZoomLevel: number;
  tileSize: number;
  tileResolutions: number[];
  viewResolutions: number[];
  spawnPosition: number[];
  mapSizeX: number;
  mapSizeZ: number;
  mapSizeY: number;
  tileStats?: {
    totalTiles: number;
    totalSizeBytes: number;
    zoomLevels: Record<number, {
      tileCount: number;
      totalSizeBytes: number;
    }>;
  };
  serverName?: string;
  worldName?: string;
}

export interface WorldExtentData {
  minX: number;
  minZ: number;
  maxX: number;
  maxZ: number;
}

let cachedConfig: MapConfigData | null = null;
let configPromise: Promise<MapConfigData> | null = null;

/**
 * Fetch map configuration from the server
 * Results are cached for the session
 */
export async function fetchMapConfig(): Promise<MapConfigData> {
  // Return cached config if available
  if (cachedConfig) {
    return cachedConfig;
  }

  // If a request is already in progress, wait for it
  if (configPromise) {
    return configPromise;
  }

  // Make the request
  configPromise = (async () => {
    try {
      const response = await fetch('/api/map-config');
      
      if (!response.ok) {
        throw new Error(`Failed to fetch map config: ${response.statusText}`);
      }

      const config = await response.json();
      cachedConfig = config;
      return config;
    } catch (error) {
      console.error('Error fetching map config:', error);
      // Return fallback config
      return getFallbackConfig();
    } finally {
      configPromise = null;
    }
  })();

  return configPromise;
}

/**
 * Fetch world extent separately (lighter endpoint)
 */
export async function fetchWorldExtent(): Promise<WorldExtentData> {
  try {
    const response = await fetch('/api/map-extent');
    
    if (!response.ok) {
      throw new Error(`Failed to fetch world extent: ${response.statusText}`);
    }

    return await response.json();
  } catch (error) {
    console.error('Error fetching world extent:', error);
    return {
      minX: -512000,
      minZ: -512000,
      maxX: 512000,
      maxZ: 512000
    };
  }
}

/**
 * Invalidate cached config (e.g., after server restart)
 */
export function invalidateMapConfig(): void {
  cachedConfig = null;
  configPromise = null;
}

/**
 * Get fallback configuration if API is unavailable
 */
function getFallbackConfig(): MapConfigData {
  return {
    worldExtent: [-512000, -512000, 512000, 512000],
    worldOrigin: [-512000, 512000],
    defaultCenter: [0, -5000],
    defaultZoom: 7,
    minZoom: 0,
    maxZoom: 9,
    baseZoomLevel: 9,
    tileSize: 256,
    tileResolutions: [512, 256, 128, 64, 32, 16, 8, 4, 2, 1],
    viewResolutions: [256, 128, 64, 32, 16, 8, 4, 2, 1, 0.5, 0.25, 0.125],
    spawnPosition: [0, 0],
    mapSizeX: 1024000,
    mapSizeZ: 1024000,
    mapSizeY: 256
  };
}


/**
 * API service for fetching dynamic map configuration from the server
 */

export interface MapConfigData {
  // All coordinates in world BLOCK coordinate space
  // Backend provides block coordinates for OpenLayers TileGrid
  // Frontend's getTileUrl() converts grid coords to storage tile numbers
  worldExtent: number[]; // [minX, minZ, maxX, maxZ] in world blocks
  worldOrigin: number[]; // [x, z] origin in world blocks  
  defaultCenter: number[]; // [x, z] center in world blocks
  defaultZoom: number;
  minZoom: number;
  maxZoom: number;
  baseZoomLevel: number;
  tileSize: number; // Tile size in pixels
  tileResolutions: number[]; // Blocks per pixel at each zoom
  viewResolutions: number[]; // View resolutions for smooth zooming
  originTilesPerZoom?: number[][]; // Absolute tile origin per zoom [x,y]
  spawnPosition: number[]; // [x, z] in world block coordinates
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
 * @throws Error if config cannot be fetched
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
        throw new Error(`❌ HTTP ${response.status}: ${response.statusText}`);
      }

      const config = await response.json();
      
      // Validate required fields
      const requiredFields: (keyof MapConfigData)[] = [
        'worldExtent', 'worldOrigin', 'defaultCenter', 'defaultZoom',
        'minZoom', 'maxZoom', 'tileSize', 'tileResolutions', 'viewResolutions',
        'spawnPosition', 'originTilesPerZoom'
      ];
      
      const missingFields = requiredFields.filter(field => 
        config[field] === undefined || config[field] === null
      );
      
      if (missingFields.length > 0) {
        throw new Error(`❌ Server config is missing required fields: ${missingFields.join(', ')}`);
      }
      
      cachedConfig = config;
      return config;
    } catch (error) {
      console.error('❌ [MapConfigAPI] Failed to fetch config:', error);
      throw new Error(`Cannot fetch map configuration from server: ${error instanceof Error ? error.message : String(error)}`);
    } finally {
      configPromise = null;
    }
  })();

  return configPromise;
}

/**
 * Invalidate cached config (e.g., after server restart)
 */
export function invalidateMapConfig(): void {
  cachedConfig = null;
  configPromise = null;
}

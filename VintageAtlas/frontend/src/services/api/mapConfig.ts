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
  absolutePositions: boolean;
  tileOffset?: number[]; // Tile coordinate offset for spawn-relative mode
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
    console.log('✅ [MapConfigAPI] Using cached config');
    return cachedConfig;
  }

  // If a request is already in progress, wait for it
  if (configPromise) {
    console.log('⏳ [MapConfigAPI] Waiting for ongoing request...');
    return configPromise;
  }

  console.log('📡 [MapConfigAPI] Fetching from /api/map-config...');
  
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
        'spawnPosition', 'absolutePositions'
      ];
      
      const missingFields = requiredFields.filter(field => 
        config[field] === undefined || config[field] === null
      );
      
      if (missingFields.length > 0) {
        throw new Error(`❌ Server config is missing required fields: ${missingFields.join(', ')}`);
      }
      
      console.log('✅ [MapConfigAPI] Config validated and cached');
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
 * Fetch world extent separately (lighter endpoint)
 * @throws Error if extent cannot be fetched
 */
export async function fetchWorldExtent(): Promise<WorldExtentData> {
  try {
    console.log('📡 [MapConfigAPI] Fetching from /api/map-extent...');
    const response = await fetch('/api/map-extent');
    
    if (!response.ok) {
      throw new Error(`❌ HTTP ${response.status}: ${response.statusText}`);
    }

    const extent = await response.json();
    console.log('✅ [MapConfigAPI] World extent fetched:', extent);
    return extent;
  } catch (error) {
    console.error('❌ [MapConfigAPI] Failed to fetch world extent:', error);
    throw new Error(`Cannot fetch world extent from server: ${error instanceof Error ? error.message : String(error)}`);
  }
}

/**
 * Invalidate cached config (e.g., after server restart)
 */
export function invalidateMapConfig(): void {
  cachedConfig = null;
  configPromise = null;
}

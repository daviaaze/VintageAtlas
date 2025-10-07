/**
 * Configuration options for VintageAtlas
 */
export interface Config {
  // Map Export Settings
  mode?: ImageMode;
  outputDirectory?: string;
  extractWorldMap?: boolean;
  fixWhiteLines?: boolean;
  extractStructures?: boolean;
  exportHeightmap?: boolean;
  exportSigns?: boolean;
  exportUntaggedSigns?: boolean;
  exportCustomTaggedSigns?: boolean;
  tileSize?: number;
  baseZoomLevel?: number;
  createZoomLevels?: boolean;
  threadCount?: number;
  
  // Web Server Settings
  liveServerEnabled?: boolean;
  liveServerPort?: number;
  liveServerEndpoint?: string;
  maxConcurrentRequests?: number;
  
  // Historical Tracking
  historicalTrackingEnabled?: boolean;
  trackerIntervalMinutes?: number;
  trackOnlyPlayerActivity?: boolean;
  maxHistoricalEntries?: number;

  // Development Mode (new settings)
  developmentMode?: boolean;
  frontendDevServerUrl?: string;
  
  // Redis Settings (new settings)
  cachingEnabled?: boolean;
  cacheProvider?: string;
  redis?: RedisConfig;
  cacheDurations?: Record<string, number>;
}

/**
 * Redis configuration
 */
export interface RedisConfig {
  connectionString?: string;
  instanceName?: string;
  connectTimeout?: number;
  syncTimeout?: number;
}

/**
 * Image modes for map rendering
 */
export enum ImageMode {
  MedievalStyleWithHillShading = 'MedievalStyleWithHillShading',
  MedievalStyle = 'MedievalStyle',
  HeightColored = 'HeightColored',
  Default = 'Default'
}

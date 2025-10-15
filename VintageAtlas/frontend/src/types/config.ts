/**
 * Configuration options for VintageAtlas
 */
export interface Config {
  // Map Export Settings
  mode?: ImageMode;
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

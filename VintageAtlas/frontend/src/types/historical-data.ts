/**
 * Historical data snapshot
 */
export interface HistoricalSnapshot {
  id: string;
  timestamp: string; // ISO date string
  playerCount: number;
  players: string[]; // Player names or IDs
  changes?: MapChanges;
}

/**
 * Changes to the map
 */
export interface MapChanges {
  addedChunks?: string[];
  modifiedChunks?: string[];
  addedMarkers?: Marker[];
  modifiedMarkers?: Marker[];
  removedMarkers?: string[];
}

/**
 * Map marker
 */
export interface Marker {
  id: string;
  type: MarkerType;
  position: {
    x: number;
    y: number;
    z: number;
  };
  title?: string;
  description?: string;
  icon?: string;
  color?: string;
}

/**
 * Types of markers
 */
export enum MarkerType {
  TRADER = 'trader',
  TRANSLOCATOR = 'translocator',
  SIGN = 'sign',
  SIGN_POST = 'signpost',
  CUSTOM = 'custom'
}

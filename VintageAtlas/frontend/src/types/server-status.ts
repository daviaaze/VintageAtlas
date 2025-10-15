/**
 * Server status information
 */
export interface ServerStatus {
  date: {
    year: number
    month: number
    day: number
    hour: number
    minute: number
  },
  spawnTemperature: number
  spawnRainfall: number
  players: Player[]
  animals: any[]
}


/**
 * Player information
 */
export interface Player {
  id: string;
  name: string;
  position?: {
    x: number;
    y: number;
    z: number;
  };
  online: boolean;
  lastSeen?: string; // ISO date string
}

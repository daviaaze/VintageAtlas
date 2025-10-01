/**
 * Server status information
 */
export interface ServerStatus {
  serverName: string;
  gameVersion: string;
  modVersion: string;
  currentPlayers: number;
  maxPlayers: number;
  uptime: number;
  tps?: number;
  memoryUsage?: number;
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

/**
 * Server performance metrics
 */
export interface ServerMetrics {
  tps: number;
  memoryUsage: number;
  cpuUsage?: number;
  diskUsage?: number;
}

/**
 * Server status information from /api/status endpoint
 */
export interface ServerStatus {
  calendar: {
    totalDays: number;
    year: number;
    month: number;
    day: number;
    dayOfYear: number;
    season: string; // "Spring", "Summer", "Fall", "Winter"
    seasonProgress: number; // 0.0 to 1.0
    totalHours: number;
    hourOfDay: number; // 0-24 with decimals
    minute: number;
    daysPerMonth: number;
    hoursPerDay: number;
    speedOfTime: number;
  };
  server: {
    playersOnline: number;
    serverName: string;
  };
}

/**
 * Player information
 */
export interface Player {
  name: string;
  uid: string;
  x: number;
  y: number;
  yaw: number;
  pitch: number;
}

/**
 * Players response from /api/players endpoint
 */
export interface PlayersResponse {
  players: Player[];
  count: number;
}

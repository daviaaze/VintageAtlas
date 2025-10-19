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
  temperature: {
    seasonModifier: number; // °C modifier for current season
    timeOfDayModifier: number; // °C modifier for current time
    totalModifier: number; // Combined modifier to add to base temperature
  };
  server: {
    playersOnline: number;
    serverName: string;
  };
}

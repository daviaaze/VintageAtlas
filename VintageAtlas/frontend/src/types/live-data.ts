/**
 * Types for live data from the server
 */

export interface LiveData {
  players: PlayerData[];
  animals: AnimalData[];
  spawnPoint: Coordinates;
  spawnTemperature?: number;
  spawnRainfall?: number;
  date?: GameDate;
  weather?: Weather;
}

export interface Coordinates {
  x: number;
  y: number;
  z: number;
}

export interface Health {
  current: number;
  max: number;
}

export interface Hunger {
  current: number;
  max: number;
}

export interface Wind {
  percent?: number;
  windSpeed?: number;
  direction?: string;
}

export interface PlayerData {
  name: string;
  uid?: string;
  coordinates: Coordinates;
  health: Health;
  hunger: Hunger;
  temperature?: number;
  bodyTemp?: number;
}

export interface AnimalData {
  type: string;
  name?: string;
  coordinates: Coordinates;
  health?: Health;
  temperature?: number;
  rainfall?: number;
  wind?: Wind;
}

export interface GameDate {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
}

export interface Weather {
  temperature: number;
  rainfall?: number;
  windSpeed?: number;
}

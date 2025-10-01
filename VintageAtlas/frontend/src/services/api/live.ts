import { apiClient } from './client';
import type { LiveData } from '@/types/live-data';

/**
 * Get live data from the server
 * This fetches all data including players, animals, and server status in a single request
 */
export async function getLiveData(): Promise<LiveData> {
  return apiClient.get('/status');
}

// Split endpoints (lighter payloads)
export async function getPlayers() {
  return apiClient.get('/live/players').then((x: any) => x.players ?? []);
}

export async function getAnimals() {
  return apiClient.get('/live/animals').then((x: any) => x.animals ?? []);
}

export async function getWeather() {
  return apiClient.get('/live/weather').then((x: any) => x.weather);
}

export async function getDate() {
  return apiClient.get('/live/date').then((x: any) => x.date);
}

export async function getSpawn() {
  return apiClient.get('/live/spawn');
}

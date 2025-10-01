import { apiClient } from './client';

/**
 * Get the current server status
 */
export async function getServerStatus() {
  return apiClient.get('/status');
}

/**
 * Get online players
 * @deprecated Use getLiveData() from live.ts instead which returns all data including players
 */
export async function getOnlinePlayers() {
  // Get the full status and extract just the players
  return apiClient.get('/status').then((response: any) => response.players || []);
}

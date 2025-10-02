import { apiClient } from './client';
import type { LiveData } from '@/types/live-data';

/**
 * Get live data from the server
 * This fetches all data including players, animals, and server status in a single request
 */
export async function getLiveData(): Promise<LiveData> {
  return apiClient.get('/status');
}

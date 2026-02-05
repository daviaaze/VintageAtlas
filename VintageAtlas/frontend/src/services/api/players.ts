import { apiClient } from './client';
import type { PlayersResponse } from '@/types/server-status';

/**
 * Get all online player positions
 */
export async function getPlayers(): Promise<PlayersResponse> {
  const response = await apiClient.get('/players');
  return response as any as PlayersResponse;
}


import { apiClient } from './client';

/**
 * Get the current server status
 */
export async function getServerStatus() {
  const response = await apiClient.get('/status');

  return response as any
}

import { apiClient } from './client';

/**
 * Get configuration settings
 */
export async function getConfig() {
  return apiClient.get('/config');
}

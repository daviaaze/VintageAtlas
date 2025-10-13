import { defineStore } from 'pinia';
import { ref, computed, onUnmounted } from 'vue';
import type { LiveData, Coordinates } from '@/types/live-data';
import { getLiveData } from '@/services/api/live';

/**
 * Store for live data from the server
 */
export const useLiveStore = defineStore('live', () => {
  // State
  const data = ref<LiveData | null>(null);
  const loading = ref(false);
  const error = ref<Error | null>(null);
  const lastUpdated = ref<Date | null>(null);
  const connectionStatus = ref<'ok' | 'error' | 'warning' | 'reconnecting' | 'loading'>('loading');
  const connectionMessage = ref<string | null>(null);
  const retryCount = ref(0);
  const maxRetries = 3;
  let pollingInterval: number | undefined;
  let retryTimeout: number | undefined;
  
  // Computed
  const players = computed(() => data.value?.players || []);
  const animals = computed(() => data.value?.animals || []);
  const spawnPoint = computed(() => data.value?.spawnPoint || { x: 512000.0, y: 116.0, z: 512000.0 });
  const spawnTemperature = computed(() => data.value?.spawnTemperature);
  const spawnRainfall = computed(() => data.value?.spawnRainfall);
  const gameDate = computed(() => data.value?.date);
  const weather = computed(() => data.value?.weather);
  
  // Helper function to convert world coordinates to map coordinates
  function worldToMapCoords(coords: Coordinates): [number, number] {
    // Convert from game coordinates to map coordinates
    // No need to offset by spawnPoint as our map is already aligned with the game world
    return [coords.x, -coords.z];
  }
  
  // Actions
  async function fetchLiveData() {
    if (connectionStatus.value === 'reconnecting') return;
    
    loading.value = true;
    connectionStatus.value = 'loading';
    connectionMessage.value = 'Fetching live data...';
    
    try {
      const result = await getLiveData();
      data.value = result;
      lastUpdated.value = new Date();
      connectionStatus.value = 'ok';
      connectionMessage.value = null;
      retryCount.value = 0;
    } catch (e) {
      error.value = e as Error;
      console.error('Failed to fetch live data:', e);
      
      // Handle different error types
      const errorMessage = (e as Error).message || 'Unknown error';
      if (errorMessage.includes('503')) {
        connectionStatus.value = 'warning';
        connectionMessage.value = 'Server starting up - Retrying...';
      } else if (errorMessage.includes('500')) {
        connectionStatus.value = 'error';
        connectionMessage.value = 'Server error - Please check logs';
      } else if (errorMessage.includes('NetworkError') || errorMessage.includes('Failed to fetch')) {
        connectionStatus.value = 'reconnecting';
        connectionMessage.value = 'Cannot reach server - Retrying...';
      } else {
        connectionStatus.value = 'error';
        connectionMessage.value = `Connection error: ${errorMessage}`;
      }
      
      // Retry with exponential backoff
      if (retryCount.value < maxRetries) {
        retryCount.value++;
        const backoffTime = Math.min(1000 * Math.pow(2, retryCount.value), 30000); // Max 30s
        
        connectionStatus.value = 'reconnecting';
        connectionMessage.value = `Retrying in ${Math.round(backoffTime/1000)}s... (${retryCount.value}/${maxRetries})`;
        
        if (retryTimeout) clearTimeout(retryTimeout);
        retryTimeout = window.setTimeout(() => {
          fetchLiveData();
        }, backoffTime);
      } else {
        // Max retries reached, wait for next interval
        connectionStatus.value = 'error';
        connectionMessage.value = 'Connection failed - Will retry in 15s';
        retryCount.value = 0; // Reset for next interval
      }
    } finally {
      loading.value = false;
    }
  }
  
  function startPolling(intervalMs = 15000) {
    if (pollingInterval) clearInterval(pollingInterval);
    fetchLiveData(); // Fetch immediately
    pollingInterval = window.setInterval(() => {
      // Only refresh if we're not in the middle of retrying
      if (connectionStatus.value !== 'reconnecting') {
        fetchLiveData();
      }
    }, intervalMs);
  }
  
  function stopPolling() {
    if (pollingInterval) {
      clearInterval(pollingInterval);
      pollingInterval = undefined;
    }
    if (retryTimeout) {
      clearTimeout(retryTimeout);
      retryTimeout = undefined;
    }
  }
  
  // Clean up on unmount
  onUnmounted(() => {
    stopPolling();
  });
  
  return {
    // State
    data,
    loading,
    error,
    lastUpdated,
    connectionStatus,
    connectionMessage,
    
    // Computed
    players,
    animals,
    spawnPoint,
    spawnTemperature,
    spawnRainfall,
    gameDate,
    weather,
    
    // Actions
    fetchLiveData,
    startPolling,
    stopPolling,
    worldToMapCoords
  };
});

import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import type { ServerStatus, Player } from '@/types/server-status';
import { getServerStatus, getOnlinePlayers } from '@/services/api/status';

/**
 * Store for server status and player information
 */
export const useServerStore = defineStore('server', () => {
  // State
  const status = ref<ServerStatus | null>(null);
  const players = ref<Player[]>([]);
  const loading = ref(false);
  const error = ref<Error | null>(null);
  const lastUpdated = ref<Date | null>(null);

  // Getters
  const isOnline = computed(() => !!status.value);
  const onlinePlayerCount = computed(() => status.value?.currentPlayers || 0);
  const onlinePlayers = computed(() => players.value.filter(p => p.online));

  // Actions
  async function fetchStatus() {
    loading.value = true;
    error.value = null;
    
    try {
      status.value = await getServerStatus();
      lastUpdated.value = new Date();
    } catch (err) {
      error.value = err as Error;
      status.value = null;
    } finally {
      loading.value = false;
    }
  }

  async function fetchPlayers() {
    loading.value = true;
    error.value = null;
    
    try {
      players.value = await getOnlinePlayers();
      lastUpdated.value = new Date();
    } catch (err) {
      error.value = err as Error;
    } finally {
      loading.value = false;
    }
  }

  // Initialize data polling
  let statusInterval: number | null = null;
  
  function startPolling(interval = 30000) {
    // Clear any existing interval
    if (statusInterval) {
      clearInterval(statusInterval);
    }
    
    // Initial fetch
    fetchStatus();
    fetchPlayers();
    
    // Set up interval for polling
    statusInterval = window.setInterval(() => {
      fetchStatus();
      fetchPlayers();
    }, interval);
  }
  
  function stopPolling() {
    if (statusInterval) {
      clearInterval(statusInterval);
      statusInterval = null;
    }
  }

  return {
    // State
    status,
    players,
    loading,
    error,
    lastUpdated,
    
    // Getters
    isOnline,
    onlinePlayerCount,
    onlinePlayers,
    
    // Actions
    fetchStatus,
    fetchPlayers,
    startPolling,
    stopPolling
  };
});

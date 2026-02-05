import { defineStore } from 'pinia';
import { ref, computed, watch } from 'vue';
import type { ServerStatus, Player } from '@/types/server-status';
import { getServerStatus } from '@/services/api/status';
import { getPlayers } from '@/services/api/players';
import { useWebSocket } from '@/composables/useWebSocket';

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
  const onlinePlayerCount = computed(() => players.value.length);
  const onlinePlayers = computed(() => players.value);

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
    try {
      const response = await getPlayers();
      players.value = response.players;
    } catch (err) {
      console.error('Failed to fetch players:', err);
      // Don't clear players on error, keep last known positions
    }
  }

  // WebSocket integration
  const { isConnected, connect, subscribe } = useWebSocket();

  // Initialize data polling
  let statusInterval: number | null = null;
  let playersInterval: number | null = null;
  let unsubscribePlayers: (() => void) | undefined;

  function startPolling(statusIntervalMs = 30000, playersIntervalMs = 2000) {
    // Clear any existing intervals
    stopPolling();

    // Connect WebSocket
    connect();

    // Subscribe to player updates
    if (unsubscribePlayers) unsubscribePlayers();
    unsubscribePlayers = subscribe('players', (data: Player[]) => {
      players.value = data;
    });

    // Initial fetch
    fetchStatus();

    // Set up intervals for polling status (always polled for now)
    statusInterval = window.setInterval(() => {
      fetchStatus();
    }, statusIntervalMs);

    // Player polling logic (fallback)
    const startPlayerPolling = () => {
      if (playersInterval) clearInterval(playersInterval);
      fetchPlayers();
      playersInterval = window.setInterval(fetchPlayers, playersIntervalMs);
    };

    const stopPlayerPolling = () => {
      if (playersInterval) {
        clearInterval(playersInterval);
        playersInterval = null;
      }
    };

    // Watch connection state to toggle polling
    watch(isConnected, (connected) => {
      if (connected) {
        console.log('[ServerStore] WebSocket connected, stopping player polling');
        stopPlayerPolling();
      } else {
        console.log('[ServerStore] WebSocket disconnected, starting player polling');
        startPlayerPolling();
      }
    }, { immediate: true });
  }

  function stopPolling() {
    if (statusInterval) {
      clearInterval(statusInterval);
      statusInterval = null;
    }
    if (playersInterval) {
      clearInterval(playersInterval);
      playersInterval = null;
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

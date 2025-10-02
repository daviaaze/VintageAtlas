<template>
  <div class="admin-dashboard">
    <h1>Admin Dashboard</h1>
    
    <div class="dashboard-grid">
      <div class="dashboard-card server-status">
        <h2>Server Status</h2>
        <div v-if="serverStore.loading">Loading...</div>
        <div v-else-if="serverStore.error">Error loading server status</div>
        <div v-else-if="serverStore.status" class="status-info">
          <div class="status-item">
            <span class="label">Name:</span>
            <span class="value">{{ serverStore.status.serverName }}</span>
          </div>
          <div class="status-item">
            <span class="label">Version:</span>
            <span class="value">{{ serverStore.status.gameVersion }}</span>
          </div>
          <div class="status-item">
            <span class="label">Mod Version:</span>
            <span class="value">{{ serverStore.status.modVersion }}</span>
          </div>
          <div class="status-item">
            <span class="label">Players:</span>
            <span class="value">{{ serverStore.status.currentPlayers }}/{{ serverStore.status.maxPlayers }}</span>
          </div>
          <div class="status-item">
            <span class="label">Uptime:</span>
            <span class="value">{{ formatUptime(serverStore.status.uptime) }}</span>
          </div>
          <div class="status-item" v-if="serverStore.status.tps">
            <span class="label">TPS:</span>
            <span class="value">{{ serverStore.status.tps.toFixed(2) }}</span>
          </div>
          <div class="status-item" v-if="serverStore.status.memoryUsage">
            <span class="label">Memory:</span>
            <span class="value">{{ formatMemory(serverStore.status.memoryUsage) }}</span>
          </div>
        </div>
        <div v-else class="offline">
          Server is offline
        </div>
        
        <button @click="refreshServerStatus" class="refresh-button">
          Refresh
        </button>
      </div>
      
      <div class="dashboard-card active-players">
        <h2>Active Players</h2>
        <div v-if="serverStore.loading">Loading...</div>
        <div v-else-if="serverStore.onlinePlayers.length === 0">
          No players online
        </div>
        <ul v-else class="player-list">
          <li v-for="player in serverStore.onlinePlayers" :key="player.id">
            <span class="player-name">{{ player.name }}</span>
            <span class="player-pos" v-if="player.position">
              {{ player.position.x.toFixed(0) }}, {{ player.position.y.toFixed(0) }}, {{ player.position.z.toFixed(0) }}
            </span>
          </li>
        </ul>
      </div>
      
      <div class="dashboard-card export-controls">
        <h2>Map Export</h2>
        <button class="export-button">
          Trigger Manual Export
        </button>
        <div class="last-export">
          Last Export: N/A
        </div>
      </div>
      
      <div class="dashboard-card historical-data">
        <h2>Historical Data</h2>
        <div class="data-stats">
          <div class="stat-item">
            <span class="label">Snapshots:</span>
            <span class="value">{{ historicalStore.snapshots.length }}</span>
          </div>
          <div class="stat-item">
            <span class="label">Disk Usage:</span>
            <span class="value">Unknown</span>
          </div>
        </div>
        <button class="purge-button">
          Purge Old Data
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue';
import { useServerStore } from '@/stores/server';
import { useHistoricalStore } from '@/stores/historical';

const serverStore = useServerStore();
const historicalStore = useHistoricalStore();

// Format uptime in human readable format
function formatUptime(seconds: number): string {
  const days = Math.floor(seconds / 86400);
  const hours = Math.floor((seconds % 86400) / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  
  if (days > 0) {
    return `${days}d ${hours}h ${minutes}m`;
  } else if (hours > 0) {
    return `${hours}h ${minutes}m`;
  } else {
    return `${minutes}m`;
  }
}

// Format memory in human readable format
function formatMemory(bytes: number): string {
  const mb = bytes / (1024 * 1024);
  const gb = mb / 1024;
  
  if (gb >= 1) {
    return `${gb.toFixed(2)} GB`;
  } else {
    return `${mb.toFixed(0)} MB`;
  }
}

// Refresh server status
function refreshServerStatus() {
  serverStore.fetchStatus();
  serverStore.fetchPlayers();
}

// Fetch data on component mount
onMounted(() => {
  refreshServerStatus();
  historicalStore.fetchHistoricalData();
});
</script>

<style scoped>
.admin-dashboard {
  padding: 1rem;
}

.dashboard-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(350px, 1fr));
  gap: 1rem;
  margin-top: 1rem;
}

.dashboard-card {
  background-color: #f8f9fa;
  border-radius: 8px;
  padding: 1rem;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

.status-info {
  margin-bottom: 1rem;
}

.status-item, .stat-item {
  display: flex;
  justify-content: space-between;
  margin-bottom: 0.5rem;
}

.label {
  font-weight: bold;
}

.player-list {
  list-style: none;
  padding: 0;
}

.player-list li {
  padding: 0.5rem;
  border-bottom: 1px solid #ddd;
  display: flex;
  justify-content: space-between;
}

.player-list li:last-child {
  border-bottom: none;
}

.player-name {
  font-weight: bold;
}

.player-pos {
  font-family: monospace;
  color: #666;
}

button {
  background-color: #007bff;
  color: white;
  border: none;
  padding: 0.5rem 1rem;
  border-radius: 4px;
  cursor: pointer;
  width: 100%;
}

button:hover {
  background-color: #0069d9;
}

.export-button {
  background-color: #28a745;
}

.export-button:hover {
  background-color: #218838;
}

.purge-button {
  background-color: #dc3545;
}

.purge-button:hover {
  background-color: #c82333;
}

.last-export {
  text-align: center;
  margin-top: 0.5rem;
  font-style: italic;
  color: #666;
}

.data-stats {
  margin-bottom: 1rem;
}

.offline {
  color: #dc3545;
  text-align: center;
  font-weight: bold;
  margin-bottom: 1rem;
}
</style>

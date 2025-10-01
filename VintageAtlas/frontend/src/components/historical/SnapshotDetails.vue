<template>
  <div class="snapshot-details">
    <div v-if="!snapshot" class="no-selection">
      <div class="placeholder-icon">
        <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="12" r="10"></circle>
          <line x1="12" y1="8" x2="12" y2="12"></line>
          <line x1="12" y1="16" x2="12" y2="16"></line>
        </svg>
      </div>
      <p>Select a snapshot from the timeline to view details</p>
    </div>
    
    <div v-else class="snapshot-content">
      <div class="snapshot-header">
        <h3>Snapshot Details</h3>
        <div class="timestamp">{{ formatDate(snapshot.timestamp) }}</div>
      </div>
      
      <div class="stats-grid">
        <div class="stat-card">
          <div class="stat-label">Player Count</div>
          <div class="stat-value">{{ snapshot.playerCount }}</div>
        </div>
        
        <div class="stat-card">
          <div class="stat-label">Changes</div>
          <div class="stat-value">{{ getChangeCount(snapshot) }}</div>
        </div>
      </div>
      
      <div class="section">
        <h4>
          <span>Players</span>
          <span class="badge">{{ snapshot.players?.length || 0 }}</span>
        </h4>
        <div v-if="snapshot.players?.length" class="player-list">
          <div v-for="player in snapshot.players" :key="player" class="player-item">
            <div class="player-icon">
              <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
                <circle cx="12" cy="7" r="4"></circle>
              </svg>
            </div>
            <div class="player-name">{{ player }}</div>
          </div>
        </div>
        <div v-else class="empty-list">No players online</div>
      </div>
      
      <div v-if="snapshot.changes" class="section">
        <h4>Map Changes</h4>
        
        <div v-if="snapshot.changes.addedChunks?.length" class="change-group">
          <div class="change-title">
            <div class="change-icon added">+</div>
            <span>Added Chunks</span>
            <span class="badge">{{ snapshot.changes.addedChunks.length }}</span>
          </div>
          <div class="change-list">
            <div v-for="chunk in snapshot.changes.addedChunks.slice(0, 5)" :key="chunk" class="change-item">
              {{ chunk }}
            </div>
            <div v-if="snapshot.changes.addedChunks.length > 5" class="more-indicator">
              +{{ snapshot.changes.addedChunks.length - 5 }} more
            </div>
          </div>
        </div>
        
        <div v-if="snapshot.changes.modifiedChunks?.length" class="change-group">
          <div class="change-title">
            <div class="change-icon modified">~</div>
            <span>Modified Chunks</span>
            <span class="badge">{{ snapshot.changes.modifiedChunks.length }}</span>
          </div>
          <div class="change-list">
            <div v-for="chunk in snapshot.changes.modifiedChunks.slice(0, 5)" :key="chunk" class="change-item">
              {{ chunk }}
            </div>
            <div v-if="snapshot.changes.modifiedChunks.length > 5" class="more-indicator">
              +{{ snapshot.changes.modifiedChunks.length - 5 }} more
            </div>
          </div>
        </div>
        
        <div v-if="snapshot.changes.addedMarkers?.length" class="change-group">
          <div class="change-title">
            <div class="change-icon added">+</div>
            <span>Added Markers</span>
            <span class="badge">{{ snapshot.changes.addedMarkers.length }}</span>
          </div>
          <div class="change-list">
            <div v-for="marker in snapshot.changes.addedMarkers.slice(0, 5)" :key="marker.id" class="change-item">
              <span class="marker-type">{{ marker.type }}</span>
              <span class="marker-title">{{ marker.title || marker.id }}</span>
            </div>
            <div v-if="snapshot.changes.addedMarkers.length > 5" class="more-indicator">
              +{{ snapshot.changes.addedMarkers.length - 5 }} more
            </div>
          </div>
        </div>
        
        <div v-if="!hasChanges(snapshot)" class="empty-list">No changes in this snapshot</div>
      </div>
      
      <div class="actions">
        <button class="btn btn-primary" @click="$emit('view-map', snapshot)">
          View on Map
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { HistoricalSnapshot } from '@/types/historical-data';

const props = defineProps<{
  snapshot: HistoricalSnapshot | null;
}>();

const emit = defineEmits<{
  (e: 'view-map', snapshot: HistoricalSnapshot): void;
}>();

function formatDate(dateString: string) {
  const date = new Date(dateString);
  return date.toLocaleString();
}

function getChangeCount(snapshot: HistoricalSnapshot): number {
  if (!snapshot.changes) return 0;
  
  let count = 0;
  if (snapshot.changes.addedChunks) count += snapshot.changes.addedChunks.length;
  if (snapshot.changes.modifiedChunks) count += snapshot.changes.modifiedChunks.length;
  if (snapshot.changes.addedMarkers) count += snapshot.changes.addedMarkers.length;
  if (snapshot.changes.modifiedMarkers) count += snapshot.changes.modifiedMarkers.length;
  if (snapshot.changes.removedMarkers) count += snapshot.changes.removedMarkers.length;
  
  return count;
}

function hasChanges(snapshot: HistoricalSnapshot): boolean {
  return getChangeCount(snapshot) > 0;
}
</script>

<style scoped>
.snapshot-details {
  background-color: var(--color-background-soft);
  border-radius: 8px;
  height: 100%;
  overflow-y: auto;
}

.no-selection {
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: var(--color-text-light);
  padding: 2rem;
  text-align: center;
}

.placeholder-icon {
  margin-bottom: 1rem;
  color: var(--color-border-hover);
}

.snapshot-content {
  padding: 1.5rem;
}

.snapshot-header {
  margin-bottom: 1.5rem;
}

.snapshot-header h3 {
  margin: 0;
  color: var(--color-heading);
  font-size: 1.5rem;
}

.timestamp {
  color: var(--color-text-light);
  font-size: 0.9rem;
  margin-top: 0.25rem;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.stat-card {
  background-color: var(--color-background);
  border-radius: 8px;
  padding: 1rem;
  box-shadow: var(--shadow-elevation-xsmall);
  border: 1px solid var(--color-border);
}

.stat-label {
  font-size: 0.9rem;
  color: var(--color-text-light);
  margin-bottom: 0.5rem;
}

.stat-value {
  font-size: 1.5rem;
  font-weight: 600;
  color: var(--color-heading);
}

.section {
  margin-bottom: 1.5rem;
  background-color: var(--color-background);
  border-radius: 8px;
  padding: 1rem;
  box-shadow: var(--shadow-elevation-xsmall);
  border: 1px solid var(--color-border);
}

.section h4 {
  margin: 0 0 1rem;
  color: var(--color-heading);
  font-size: 1.1rem;
  display: flex;
  align-items: center;
}

.badge {
  background-color: var(--color-primary);
  color: white;
  border-radius: 20px;
  padding: 0.15rem 0.5rem;
  font-size: 0.75rem;
  margin-left: 0.5rem;
}

.player-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.player-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.5rem;
  border-radius: 4px;
  background-color: var(--color-background-soft);
}

.player-icon {
  color: var(--color-text-light);
}

.empty-list {
  color: var(--color-text-light);
  font-style: italic;
  padding: 0.5rem;
  text-align: center;
}

.change-group {
  margin-bottom: 1rem;
}

.change-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.change-icon {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: bold;
  font-size: 0.9rem;
}

.change-icon.added {
  background-color: var(--color-success);
  color: white;
}

.change-icon.modified {
  background-color: var(--color-warning);
  color: white;
}

.change-icon.removed {
  background-color: var(--color-danger);
  color: white;
}

.change-list {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  margin-left: 1.5rem;
}

.change-item {
  font-family: monospace;
  font-size: 0.9rem;
  color: var(--color-text);
}

.marker-type {
  display: inline-block;
  background-color: var(--color-background-mute);
  color: var(--color-text-light);
  padding: 0.1rem 0.3rem;
  border-radius: 4px;
  font-size: 0.8rem;
  margin-right: 0.5rem;
}

.more-indicator {
  color: var(--color-text-light);
  font-size: 0.9rem;
  margin-top: 0.25rem;
}

.actions {
  margin-top: 2rem;
  display: flex;
  justify-content: flex-end;
}

.btn {
  padding: 0.75rem 1.25rem;
  border-radius: 6px;
  font-weight: 600;
  cursor: pointer;
  transition: background-color 0.2s;
  border: none;
}

.btn-primary {
  background-color: var(--color-primary);
  color: white;
}

.btn-primary:hover {
  background-color: var(--color-primary-dark);
}
</style>

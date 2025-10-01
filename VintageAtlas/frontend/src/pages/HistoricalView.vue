<template>
  <div class="historical-view">
    <div class="page-header">
      <h1>Historical Data</h1>
      <div class="header-actions">
        <div class="date-range">
          <div class="date-input">
            <label for="start-date">From</label>
            <input 
              type="date" 
              id="start-date" 
              v-model="startDate"
              :max="endDate || undefined"
            />
          </div>
          <div class="date-input">
            <label for="end-date">To</label>
            <input 
              type="date" 
              id="end-date" 
              v-model="endDate"
              :min="startDate || undefined"
            />
          </div>
          <button class="btn btn-primary" @click="applyDateFilter">Apply</button>
          <button class="btn btn-secondary" @click="clearFilters">Clear</button>
        </div>
        <button class="btn btn-refresh" @click="refreshData" :disabled="historicalStore.loading">
          <svg v-if="!historicalStore.loading" xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M21.5 2v6h-6M21.34 15.57a10 10 0 1 1-.57-8.38"></path>
          </svg>
          <div v-else class="btn-spinner"></div>
        </button>
      </div>
    </div>

    <div v-if="historicalStore.error" class="error-message">
      <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
        <circle cx="12" cy="12" r="10"></circle>
        <line x1="12" y1="8" x2="12" y2="12"></line>
        <line x1="12" y1="16" x2="12" y2="16"></line>
      </svg>
      <span>{{ historicalStore.error.message }}</span>
      <button class="btn-close" @click="dismissError">×</button>
    </div>

    <div class="timeline-container">
      <TimelineChart 
        :snapshots="historicalStore.filteredSnapshots"
        :selectedTimestamp="selectedTimestamp"
        :loading="historicalStore.loading"
        @select="onSelectTimestamp"
      />
    </div>

    <div class="content-grid">
      <div class="snapshot-list">
        <h2>Snapshots</h2>
        <div v-if="historicalStore.loading" class="loading-indicator">
          <div class="spinner"></div>
          <span>Loading snapshots...</span>
        </div>
        <div v-else-if="!historicalStore.filteredSnapshots.length" class="empty-state">
          No snapshots available for the selected time period.
        </div>
        <div v-else class="snapshot-items">
          <div 
            v-for="snapshot in historicalStore.filteredSnapshots" 
            :key="snapshot.id"
            class="snapshot-item"
            :class="{ active: selectedTimestamp === snapshot.timestamp }"
            @click="onSelectTimestamp(snapshot.timestamp)"
          >
            <div class="snapshot-time">
              {{ formatDate(snapshot.timestamp) }}
            </div>
            <div class="snapshot-info">
              <div class="info-item">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path>
                  <circle cx="12" cy="7" r="4"></circle>
                </svg>
                <span>{{ snapshot.playerCount }}</span>
              </div>
              <div v-if="snapshot.changes" class="info-item">
                <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M21 14l-3-3h-7a1 1 0 0 1-1-1V4"></path>
                  <path d="M3 10l3 3h7a1 1 0 0 1 1 1v6"></path>
                </svg>
                <span>{{ getChangeCount(snapshot) }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="snapshot-details-container">
        <SnapshotDetails 
          :snapshot="selectedSnapshotData"
          @view-map="viewOnMap"
        />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useHistoricalStore } from '@/stores/historical';
import TimelineChart from '@/components/historical/TimelineChart.vue';
import SnapshotDetails from '@/components/historical/SnapshotDetails.vue';
import type { HistoricalSnapshot } from '@/types/historical-data';

const router = useRouter();
const historicalStore = useHistoricalStore();

// Local state
const selectedTimestamp = ref<string | null>(null);
const startDate = ref<string | null>(null);
const endDate = ref<string | null>(null);

// Computed
const selectedSnapshotData = computed(() => {
  if (!selectedTimestamp.value) return null;
  return historicalStore.filteredSnapshots.find(s => s.timestamp === selectedTimestamp.value) || null;
});

// Methods
function onSelectTimestamp(timestamp: string) {
  selectedTimestamp.value = timestamp;
}

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

function applyDateFilter() {
  if (startDate.value && endDate.value) {
    historicalStore.fetchDateRange(startDate.value, endDate.value);
  }
}

function clearFilters() {
  startDate.value = null;
  endDate.value = null;
  historicalStore.clearFilters();
  historicalStore.fetchHistoricalData();
}

function refreshData() {
  if (startDate.value && endDate.value) {
    historicalStore.fetchDateRange(startDate.value, endDate.value);
  } else {
    historicalStore.fetchHistoricalData();
  }
}

function dismissError() {
  historicalStore.error = null;
}

function viewOnMap(snapshot: HistoricalSnapshot) {
  // Navigate to map view with this snapshot's data
  router.push({
    name: 'Map',
    query: { snapshot: snapshot.id }
  });
}

// Lifecycle
onMounted(() => {
  historicalStore.fetchHistoricalData();
});
</script>

<style scoped>
.historical-view {
  padding: 2rem;
  max-width: 1600px;
  margin: 0 auto;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
}

.page-header h1 {
  margin: 0;
  color: var(--color-heading);
}

.header-actions {
  display: flex;
  gap: 1rem;
  align-items: center;
}

.date-range {
  display: flex;
  gap: 0.75rem;
  align-items: center;
}

.date-input {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.date-input label {
  font-size: 0.8rem;
  color: var(--color-text-light);
}

.date-input input {
  padding: 0.5rem;
  border-radius: 4px;
  border: 1px solid var(--color-border);
  background-color: var(--color-background);
  color: var(--color-text);
}

.btn {
  padding: 0.5rem 1rem;
  border-radius: 6px;
  font-weight: 500;
  cursor: pointer;
  transition: background-color 0.2s;
  border: none;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
}

.btn-primary {
  background-color: var(--color-primary);
  color: white;
}

.btn-primary:hover {
  background-color: var(--color-primary-dark);
}

.btn-secondary {
  background-color: var(--color-background-mute);
  color: var(--color-text);
}

.btn-secondary:hover {
  background-color: var(--color-border);
}

.btn-refresh {
  background-color: var(--color-background);
  color: var(--color-text);
  border: 1px solid var(--color-border);
  padding: 0.5rem;
  border-radius: 6px;
  height: 38px;
  width: 38px;
}

.btn-refresh:hover {
  background-color: var(--color-background-mute);
}

.btn-refresh:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-spinner {
  width: 16px;
  height: 16px;
  border: 2px solid rgba(var(--color-text-rgb), 0.1);
  border-left-color: var(--color-text);
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

.error-message {
  background-color: var(--color-danger);
  color: white;
  padding: 0.75rem 1rem;
  border-radius: 6px;
  margin-bottom: 1.5rem;
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.btn-close {
  background: none;
  border: none;
  color: white;
  font-size: 1.5rem;
  cursor: pointer;
  margin-left: auto;
  padding: 0;
  line-height: 1;
}

.timeline-container {
  margin-bottom: 2rem;
}

.content-grid {
  display: grid;
  grid-template-columns: 300px 1fr;
  gap: 2rem;
  height: calc(100vh - 300px);
  min-height: 500px;
}

.snapshot-list {
  background-color: var(--color-background-soft);
  border-radius: 8px;
  padding: 1.5rem;
  height: 100%;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.snapshot-list h2 {
  margin: 0 0 1rem;
  color: var(--color-heading);
  font-size: 1.25rem;
}

.snapshot-items {
  overflow-y: auto;
  flex-grow: 1;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.snapshot-item {
  background-color: var(--color-background);
  border-radius: 6px;
  padding: 0.75rem 1rem;
  cursor: pointer;
  transition: all 0.2s;
  border: 1px solid var(--color-border);
}

.snapshot-item:hover {
  background-color: var(--color-background-mute);
}

.snapshot-item.active {
  background-color: var(--color-primary);
  color: white;
  border-color: var(--color-primary);
}

.snapshot-time {
  font-size: 0.9rem;
  margin-bottom: 0.5rem;
}

.snapshot-info {
  display: flex;
  gap: 1rem;
}

.info-item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
}

.snapshot-details-container {
  height: 100%;
}

.loading-indicator, .empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: var(--color-text-light);
  text-align: center;
  padding: 2rem;
}

.loading-indicator {
  gap: 1rem;
}

.spinner {
  width: 40px;
  height: 40px;
  border: 4px solid rgba(var(--color-primary-rgb), 0.1);
  border-left-color: var(--color-primary);
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* Responsive adjustments */
@media (max-width: 1024px) {
  .content-grid {
    grid-template-columns: 1fr;
    grid-template-rows: auto 1fr;
  }
  
  .snapshot-list {
    height: 300px;
  }
}

@media (max-width: 768px) {
  .historical-view {
    padding: 1rem;
  }
  
  .page-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }
  
  .header-actions {
    width: 100%;
    flex-direction: column;
    align-items: stretch;
  }
  
  .date-range {
    flex-wrap: wrap;
  }
  
  .date-input {
    flex: 1;
    min-width: 120px;
  }
}
</style>
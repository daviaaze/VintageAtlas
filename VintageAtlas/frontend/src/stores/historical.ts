import { defineStore } from 'pinia';
import { ref, computed } from 'vue';
import type { HistoricalSnapshot } from '@/types/historical-data';
import { getHistoricalData, getHistoricalRange } from '@/services/api/historical';

/**
 * Store for historical data
 */
export const useHistoricalStore = defineStore('historical', () => {
  // State
  const snapshots = ref<HistoricalSnapshot[]>([]);
  const selectedSnapshot = ref<HistoricalSnapshot | null>(null);
  const loading = ref(false);
  const error = ref<Error | null>(null);
  
  // Filter state
  const dateRange = ref<{start: string | null; end: string | null}>({
    start: null,
    end: null
  });
  
  // Getters
  const sortedSnapshots = computed(() => {
    return [...snapshots.value].sort((a, b) => {
      return new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime();
    });
  });
  
  const filteredSnapshots = computed(() => {
    if (!dateRange.value.start && !dateRange.value.end) {
      return sortedSnapshots.value;
    }
    
    return sortedSnapshots.value.filter(snapshot => {
      const snapshotDate = new Date(snapshot.timestamp).getTime();
      
      const startOk = !dateRange.value.start || 
        snapshotDate >= new Date(dateRange.value.start).getTime();
        
      const endOk = !dateRange.value.end || 
        snapshotDate <= new Date(dateRange.value.end).getTime();
        
      return startOk && endOk;
    });
  });
  
  // Actions
  async function fetchHistoricalData() {
    loading.value = true;
    error.value = null;
    
    try {
      snapshots.value = await getHistoricalData();
    } catch (err) {
      error.value = err as Error;
    } finally {
      loading.value = false;
    }
  }
  
  async function fetchDateRange(start: string, end: string) {
    loading.value = true;
    error.value = null;
    dateRange.value = { start, end };
    
    try {
      snapshots.value = await getHistoricalRange(start, end);
    } catch (err) {
      error.value = err as Error;
    } finally {
      loading.value = false;
    }
  }
  
  function selectSnapshot(snapshot: HistoricalSnapshot | null) {
    selectedSnapshot.value = snapshot;
  }
  
  function clearFilters() {
    dateRange.value = { start: null, end: null };
  }
  
  return {
    // State
    snapshots,
    selectedSnapshot,
    loading,
    error,
    dateRange,
    
    // Getters
    sortedSnapshots,
    filteredSnapshots,
    
    // Actions
    fetchHistoricalData,
    fetchDateRange,
    selectSnapshot,
    clearFilters
  };
});

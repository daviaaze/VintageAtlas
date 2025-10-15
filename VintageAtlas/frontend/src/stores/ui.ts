import { defineStore } from 'pinia';
import { ref } from 'vue';

/**
 * Store for UI state
 */
export const useUiStore = defineStore('ui', () => {
  // State
  const sidebarOpen = ref(true);
  
  // Actions
  function toggleSidebar() {
    sidebarOpen.value = !sidebarOpen.value;
  }
  
  return {
    // State
    sidebarOpen,
    
    // Actions
    toggleSidebar,
  };
});


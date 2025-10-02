import { defineStore } from 'pinia';
import { ref } from 'vue';

export type ThemeMode = 'light' | 'dark' | 'system';

/**
 * Store for UI state
 */
export const useUiStore = defineStore('ui', () => {
  // State
  const sidebarOpen = ref(true);
  const currentTheme = ref<ThemeMode>(
    (localStorage.getItem('theme') as ThemeMode) || 'system'
  );
  const mobileMenuOpen = ref(false);
  
  // Actions
  function toggleSidebar() {
    sidebarOpen.value = !sidebarOpen.value;
  }
  
  function setTheme(theme: ThemeMode) {
    currentTheme.value = theme;
    localStorage.setItem('theme', theme);
    
    applyTheme(theme);
  }
  
  function applyTheme(theme: ThemeMode) {
    // Determine if we should use dark mode
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    const isDark = 
      theme === 'dark' || 
      (theme === 'system' && prefersDark);
    
    // Apply theme to document
    if (isDark) {
      document.documentElement.classList.add('dark');
    } else {
      document.documentElement.classList.remove('dark');
    }
  }
  
  function toggleMobileMenu() {
    mobileMenuOpen.value = !mobileMenuOpen.value;
  }
  
  function closeMobileMenu() {
    mobileMenuOpen.value = false;
  }
  
  // Initialize theme on store creation
  applyTheme(currentTheme.value);
  
  // Watch for system preference changes if using system theme
  if (typeof window !== 'undefined') {
    window.matchMedia('(prefers-color-scheme: dark)')
      .addEventListener('change', () => {
        if (currentTheme.value === 'system') {
          applyTheme('system');
        }
      });
  }
  
  return {
    // State
    sidebarOpen,
    currentTheme,
    mobileMenuOpen,
    
    // Actions
    toggleSidebar,
    setTheme,
    applyTheme,
    toggleMobileMenu,
    closeMobileMenu
  };
});


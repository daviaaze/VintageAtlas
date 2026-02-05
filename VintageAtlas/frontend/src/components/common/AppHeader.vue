<template>
  <header class="bg-white/80 dark:bg-gray-900/80 backdrop-blur-md border-b border-gray-200 dark:border-gray-800 text-gray-800 dark:text-white py-0 px-6 h-[64px] flex items-center z-20 sticky top-0 transition-colors duration-300">
    <div class="flex items-center justify-between w-full max-w-7xl mx-auto">
      <div class="flex items-center gap-4">
        <div class="flex items-center gap-3 group cursor-pointer">
          <div class="p-2 rounded-lg bg-teal-500/10 group-hover:bg-teal-500/20 transition-colors">
            <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="text-teal-600 dark:text-teal-400">
              <polygon points="3 6 9 3 15 6 21 3 21 18 15 21 9 18 3 21"></polygon>
              <line x1="9" y1="3" x2="9" y2="18"></line>
              <line x1="15" y1="6" x2="15" y2="21"></line>
            </svg>
          </div>
          <h1 class="text-xl m-0 font-bold tracking-tight bg-gradient-to-r from-teal-600 to-blue-600 dark:from-teal-400 dark:to-blue-400 bg-clip-text text-transparent">
            VintageAtlas
          </h1>
        </div>
      </div>
      
      <div class="flex items-center gap-3">
        <div
          v-if="serverStore.status"
          class="flex items-center gap-2 text-sm font-medium bg-teal-50 dark:bg-teal-900/20 text-teal-700 dark:text-teal-300 py-1.5 px-3 rounded-full border border-teal-100 dark:border-teal-800 transition-all"
          title="Server Online"
        >
          <span class="relative flex h-2.5 w-2.5">
            <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-teal-400 opacity-75"></span>
            <span class="relative inline-flex rounded-full h-2.5 w-2.5 bg-teal-500"></span>
          </span>
          <span>Online</span>
        </div>
        <div 
          v-else
          class="flex items-center gap-2 text-sm font-medium bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-400 py-1.5 px-3 rounded-full border border-gray-200 dark:border-gray-700 transition-all"
        >
          <span class="h-2.5 w-2.5 rounded-full bg-gray-400"></span>
          <span>Offline</span>
        </div>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { useUiStore } from '@/stores/ui';
import { useServerStore } from '@/stores/server';

const uiStore = useUiStore();
const serverStore = useServerStore();

function toggleSidebar() {
  uiStore.toggleSidebar();
}
</script>

<style scoped>
/* All styles are now handled with Tailwind CSS utility classes */
@keyframes ping {
  0% {
    transform: translate(-50%, -50%) scale(1);
    opacity: 1;
  }
  100% {
    transform: translate(-50%, -50%) scale(2);
    opacity: 0;
  }
}

.animate-ping {
  animation: ping 1.5s ease-out infinite;
}
</style>
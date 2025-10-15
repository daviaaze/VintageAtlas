<template>
  <header class="bg-gradient-to-r from-[#2c3e50] to-[#3a506b] dark:from-gray-800 dark:to-gray-900 text-white py-0 px-4 shadow-md h-[60px] flex items-center z-10">
    <div class="flex items-center justify-between w-full max-w-7xl mx-auto">
      <div class="flex items-center gap-4">
        <button 
          class="bg-white/10 border-0 text-white cursor-pointer p-1 flex items-center justify-center transition-all hover:bg-white/20 hover:scale-105 w-9 h-9 rounded"
          @click="toggleSidebar"
          title="Toggle sidebar"
        >
          <svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
            <line x1="3" y1="12" x2="21" y2="12"></line>
            <line x1="3" y1="6" x2="21" y2="6"></line>
            <line x1="3" y1="18" x2="21" y2="18"></line>
          </svg>
        </button>
        <div class="flex items-center gap-3">
          <svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="text-teal-400">
            <polygon points="3 6 9 3 15 6 21 3 21 18 15 21 9 18 3 21"></polygon>
            <line x1="9" y1="3" x2="9" y2="18"></line>
            <line x1="15" y1="6" x2="15" y2="21"></line>
          </svg>
          <h1 class="text-xl m-0 font-semibold tracking-wider bg-gradient-to-r from-white to-gray-200 bg-clip-text text-transparent">
            VintageAtlas
          </h1>
        </div>
      </div>
      
      <div class="flex items-center gap-3">
        <div 
          v-if="serverStore.status"
          class="flex items-center gap-2 text-sm bg-black/20 py-2 px-3 rounded-full transition-all"
        >
          <span 
            class="inline-block w-2 h-2 rounded-full bg-[#10b981] relative after:content-[''] after:absolute after:w-3 after:h-3 after:rounded-full after:bg-[#10b981]/30 after:top-1/2 after:left-1/2 after:-translate-x-1/2 after:-translate-y-1/2 after:animate-ping" 
            ></span> 
            <!-- TODO fix max player count -->
            title="Server Online"
          <span class="font-semibold">{{ serverStore.status.players.length }} / 0</span>
        </div>
        <div 
          v-else
          class="flex items-center gap-2 text-sm bg-black/20 py-2 px-3 rounded-full transition-all opacity-70"
        >
          <span 
            class="inline-block w-2 h-2 rounded-full bg-[#dc3545]" 
            title="Server Offline"
          ></span>
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
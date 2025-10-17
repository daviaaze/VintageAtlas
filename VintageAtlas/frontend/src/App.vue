<script setup lang="ts">
import AppHeader from '@/components/common/AppHeader.vue';
import AppSidebar from '@/components/common/AppSidebar.vue';
import { useUiStore } from '@/stores/ui';

// Initialize UI store
const uiStore = useUiStore();

</script>

<template>
  <div class="app-container" :class="{ 'sidebar-open': uiStore.sidebarOpen }">
    <AppHeader />
    <div class="main-layout">
      <AppSidebar v-if="uiStore.sidebarOpen" />
      <main class="main-content">
        <router-view />
      </main>
    </div>
  </div>
</template>

<style scoped>
.app-container {
  @apply flex flex-col h-screen w-full overflow-hidden bg-gray-50 dark:bg-gray-900;
}

.main-layout {
  @apply flex flex-1 overflow-hidden;
  height: calc(100vh - 60px);
}

.main-content {
  @apply flex-1 overflow-hidden flex flex-col bg-white dark:bg-gray-800 shadow;
}
</style>
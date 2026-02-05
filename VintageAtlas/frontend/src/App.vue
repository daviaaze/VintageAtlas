<script setup lang="ts">
import AppHeader from '@/components/common/AppHeader.vue';

</script>

<template>
  <div class="app-container">
    <AppHeader />
    <div class="main-layout">
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
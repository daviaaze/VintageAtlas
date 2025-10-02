<script setup lang="ts">
import { onMounted } from 'vue';
import AppHeader from '@/components/common/AppHeader.vue';
import AppSidebar from '@/components/common/AppSidebar.vue';
import { useUiStore } from '@/stores/ui';

// Initialize UI store
const uiStore = useUiStore();

// Apply theme on mount
onMounted(() => {
  uiStore.applyTheme(uiStore.currentTheme);
});
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

<style>
/* Global CSS reset and base styles */
* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

html, body {
  height: 100%;
  font-family: 'Segoe UI', 'Inter', system-ui, -apple-system, sans-serif;
}

body {
  background-color: #f8f9fa;
  color: #212529;
}

/* Dark mode */
html.dark body {
  background-color: #121212;
  color: #e0e0e0;
}

#app {
  height: 100vh;
  width: 100%;
  overflow: hidden;
}

.app-container {
  display: flex;
  flex-direction: column;
  height: 100vh;
  width: 100%;
  overflow: hidden;
}

.main-layout {
  display: flex;
  flex: 1;
  height: calc(100vh - 60px);
  overflow: hidden;
}

.main-content {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

/* Responsive adjustments */
@media (max-width: 768px) {
  .main-layout {
    flex-direction: column;
  }
}

/* Button styles */
button {
  cursor: pointer;
  border: none;
  border-radius: 4px;
  padding: 8px 16px;
  font-weight: 500;
  transition: background-color 0.2s, color 0.2s;
}

/* Primary button */
.btn-primary {
  background-color: #0d6efd;
  color: white;
}

.btn-primary:hover {
  background-color: #0b5ed7;
}

/* Link styles */
a {
  color: #0d6efd;
  text-decoration: none;
}

a:hover {
  text-decoration: underline;
}

/* Section styles */
.section {
  padding: 16px;
  margin-bottom: 16px;
  border-radius: 8px;
  background-color: #fff;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

/* Dark mode section */
html.dark .section {
  background-color: #1e1e1e;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
}

/* Form elements */
input[type="text"],
input[type="number"],
input[type="email"],
input[type="password"],
select,
textarea {
  padding: 8px 12px;
  border: 1px solid #ced4da;
  border-radius: 4px;
  font-size: 14px;
  transition: border-color 0.2s, box-shadow 0.2s;
  background-color: #fff;
  color: #212529;
}

input[type="text"]:focus,
input[type="number"]:focus,
input[type="email"]:focus,
input[type="password"]:focus,
select:focus,
textarea:focus {
  border-color: #86b7fe;
  outline: none;
  box-shadow: 0 0 0 0.25rem rgba(13, 110, 253, 0.25);
}

html.dark input[type="text"],
html.dark input[type="number"],
html.dark input[type="email"],
html.dark input[type="password"],
html.dark select,
html.dark textarea {
  background-color: #2c2c2c;
  border-color: #444;
  color: #e0e0e0;
}

html.dark input[type="text"]:focus,
html.dark input[type="number"]:focus,
html.dark input[type="email"]:focus,
html.dark input[type="password"]:focus,
html.dark select:focus,
html.dark textarea:focus {
  border-color: #90caf9;
  box-shadow: 0 0 0 0.25rem rgba(144, 202, 249, 0.25);
}

/* Loading spinner */
.spinner {
  border: 3px solid rgba(13, 110, 253, 0.1);
  border-radius: 50%;
  border-top: 3px solid #0d6efd;
  width: 24px;
  height: 24px;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

html.dark .spinner {
  border-color: rgba(144, 202, 249, 0.1);
  border-top-color: #90caf9;
}
</style>
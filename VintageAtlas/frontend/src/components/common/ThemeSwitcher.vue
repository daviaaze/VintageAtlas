<template>
  <div class="theme-switcher">
    <div class="theme-options">
      <button 
        class="theme-option" 
        :class="{ active: currentTheme === 'default' }" 
        @click="changeTheme('default')"
        title="Default Theme"
      >
        <span class="theme-color default"></span>
      </button>
      <button 
        class="theme-option" 
        :class="{ active: currentTheme === 'classicblue' }" 
        @click="changeTheme('classicblue')"
        title="Classic Blue Theme"
      >
        <span class="theme-color classicblue"></span>
      </button>
      <button 
        class="theme-option" 
        :class="{ active: currentTheme === 'charcoalgray' }" 
        @click="changeTheme('charcoalgray')"
        title="Charcoal Gray Theme"
      >
        <span class="theme-color charcoalgray"></span>
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';

const currentTheme = ref('default');

function changeTheme(theme: string) {
  currentTheme.value = theme;
  
  // Update the CSS link
  const themeLink = document.getElementById('theme-css') as HTMLLinkElement;
  if (themeLink) {
    themeLink.href = `/css/${theme}.css`;
  }
  
  // Save the theme preference
  localStorage.setItem('vintageAtlas-theme', theme);
}

onMounted(() => {
  // Load saved theme preference
  const savedTheme = localStorage.getItem('vintageAtlas-theme');
  if (savedTheme) {
    changeTheme(savedTheme);
  }
});
</script>

<style scoped>
.theme-switcher {
  display: flex;
  align-items: center;
  margin-left: 10px;
}

.theme-options {
  display: flex;
  gap: 5px;
}

.theme-option {
  width: 24px;
  height: 24px;
  border-radius: 50%;
  border: 2px solid transparent;
  padding: 2px;
  background: transparent;
  cursor: pointer;
  transition: all 0.2s ease;
}

.theme-option.active {
  border-color: #fff;
  transform: scale(1.1);
}

.theme-color {
  display: block;
  width: 100%;
  height: 100%;
  border-radius: 50%;
}

.theme-color.default {
  background: linear-gradient(135deg, #3a506b, #1c2331);
}

.theme-color.classicblue {
  background: linear-gradient(135deg, #1e3a8a, #0f172a);
}

.theme-color.charcoalgray {
  background: linear-gradient(135deg, #374151, #111827);
}
</style>

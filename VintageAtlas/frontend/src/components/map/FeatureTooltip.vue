<template>
  <div 
    v-if="visible && content" 
    class="feature-tooltip"
    :style="{ left: position.x + 'px', top: position.y + 'px' }"
  >
    <div class="tooltip-content">
      <div class="tooltip-title">{{ content.name || content.title || 'Unknown' }}</div>
      <div v-if="content.type" class="tooltip-type">{{ content.type }}</div>
      <div v-if="content.wares" class="tooltip-wares">{{ content.wares }}</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';

interface TooltipContent {
  name?: string;
  title?: string;
  type?: string;
  wares?: string;
  [key: string]: any;
}

interface TooltipPosition {
  x: number;
  y: number;
}

const visible = ref(false);
const content = ref<TooltipContent | null>(null);
const position = ref<TooltipPosition>({ x: 0, y: 0 });

function show(tooltipContent: TooltipContent, x: number, y: number) {
  content.value = tooltipContent;
  position.value = { x: x + 15, y: y - 10 }; // Offset from cursor
  visible.value = true;
}

function hide() {
  visible.value = false;
  // Keep content for a brief moment to avoid flashing
  setTimeout(() => {
    if (!visible.value) {
      content.value = null;
    }
  }, 100);
}

defineExpose({ show, hide });
</script>

<style scoped>
.feature-tooltip {
  position: fixed;
  z-index: 10000;
  pointer-events: none;
  transition: opacity 0.2s;
}

.tooltip-content {
  background: rgba(30, 41, 59, 0.98);
  color: #fff;
  padding: 8px 12px;
  border-radius: 6px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
  backdrop-filter: blur(8px);
  font-size: 13px;
  line-height: 1.4;
  max-width: 250px;
  border: 1px solid rgba(255, 255, 255, 0.1);
}

.tooltip-title {
  font-weight: 600;
  margin-bottom: 2px;
  color: #fff;
}

.tooltip-type {
  font-size: 11px;
  color: rgba(255, 255, 255, 0.7);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.tooltip-wares {
  font-size: 12px;
  color: #10b981;
  margin-top: 4px;
}
</style>


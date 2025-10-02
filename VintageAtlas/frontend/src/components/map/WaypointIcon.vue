<template>
  <div class="waypoint-icon" :class="{ 'with-label': showLabel }">
    <img :src="iconPath" :alt="iconName" class="icon" />
    <span v-if="showLabel" class="label">{{ label || iconName }}</span>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { getWaypointIcon, getWaypointIconByType } from '@/utils/waypointIcons';

const props = defineProps<{
  icon?: string;
  type?: string;
  label?: string;
  showLabel?: boolean;
}>();

const iconPath = computed(() => {
  if (props.icon) {
    return getWaypointIcon(props.icon).path;
  } else if (props.type) {
    return getWaypointIconByType(props.type).path;
  } else {
    // Default icon
    return getWaypointIcon('star1').path;
  }
});

const iconName = computed(() => {
  if (props.icon) {
    return getWaypointIcon(props.icon).name;
  } else if (props.type) {
    return getWaypointIconByType(props.type).name;
  } else {
    return 'Waypoint';
  }
});
</script>

<style scoped>
.waypoint-icon {
  display: inline-flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
}

.icon {
  width: 24px;
  height: 24px;
  object-fit: contain;
}

.with-label {
  gap: 4px;
}

.label {
  font-size: 12px;
  font-weight: 500;
  white-space: nowrap;
  background-color: rgba(0, 0, 0, 0.6);
  color: white;
  padding: 2px 6px;
  border-radius: 4px;
}
</style>

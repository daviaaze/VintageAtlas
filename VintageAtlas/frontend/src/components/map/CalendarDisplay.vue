<template>
  <div class="calendar-display" v-if="serverStore.status?.calendar">
    <div class="calendar-content">
      <!-- Season Icon -->
      <div class="season-icon" :title="seasonInfo.name">
        <span class="icon">{{ seasonInfo.icon }}</span>
      </div>
      
      <!-- Date and Time Info -->
      <div class="info">
        <div class="date-line">
          <span class="season-name" :class="seasonInfo.class">{{ calendar.season }}</span>
          <span class="separator">•</span>
          <span class="date">Year {{ calendar.year }}, Day {{ calendar.day }}</span>
        </div>
        <div class="time-line">
          <span class="time">{{ formattedTime }}</span>
        </div>
      </div>
      
      <!-- Season Progress Bar -->
      <div class="season-progress" :title="`${Math.round(calendar.seasonProgress * 100)}% through ${calendar.season}`">
        <div 
          class="progress-fill" 
          :class="seasonInfo.class"
          :style="{ width: `${calendar.seasonProgress * 100}%` }"
        ></div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useServerStore } from '@/stores/server';

const serverStore = useServerStore();

const calendar = computed(() => serverStore.status?.calendar);

const formattedTime = computed(() => {
  if (!calendar.value) return '--:--';
  
  const hour = Math.floor(calendar.value.hourOfDay);
  const minute = calendar.value.minute;
  
  // Format as HH:MM
  const hourStr = hour.toString().padStart(2, '0');
  const minuteStr = minute.toString().padStart(2, '0');
  
  return `${hourStr}:${minuteStr}`;
});

const seasonInfo = computed(() => {
  const season = calendar.value?.season || 'Spring';
  
  const seasons = {
    Spring: {
      name: 'Spring',
      icon: '🌸',
      class: 'spring'
    },
    Summer: {
      name: 'Summer',
      icon: '☀️',
      class: 'summer'
    },
    Fall: {
      name: 'Fall',
      icon: '🍂',
      class: 'fall'
    },
    Winter: {
      name: 'Winter',
      icon: '❄️',
      class: 'winter'
    }
  };
  
  return seasons[season as keyof typeof seasons] || seasons.Spring;
});
</script>

<style scoped>
.calendar-display {
  position: absolute;
  top: 16px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 20;
  pointer-events: none;
}

.calendar-content {
  display: flex;
  align-items: center;
  gap: 12px;
  background: rgba(30, 41, 59, 0.95);
  backdrop-filter: blur(8px);
  padding: 12px 20px;
  border-radius: 12px;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.4);
  border: 2px solid rgba(255, 255, 255, 0.1);
  pointer-events: auto;
  min-width: 320px;
}

.season-icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 48px;
  height: 48px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  flex-shrink: 0;
}

.season-icon .icon {
  font-size: 24px;
  filter: drop-shadow(0 2px 4px rgba(0, 0, 0, 0.3));
}

.info {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}

.date-line {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  color: rgba(255, 255, 255, 0.9);
}

.season-name {
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.season-name.spring {
  color: #86efac;
}

.season-name.summer {
  color: #fbbf24;
}

.season-name.fall {
  color: #fb923c;
}

.season-name.winter {
  color: #93c5fd;
}

.separator {
  color: rgba(255, 255, 255, 0.4);
  font-weight: 300;
}

.date {
  color: rgba(255, 255, 255, 0.8);
  font-weight: 500;
}

.time-line {
  display: flex;
  align-items: center;
}

.time {
  font-family: 'Courier New', monospace;
  font-size: 16px;
  font-weight: 700;
  color: #fff;
  letter-spacing: 1px;
}

.season-progress {
  width: 4px;
  height: 48px;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 2px;
  overflow: hidden;
  position: relative;
  flex-shrink: 0;
}

.progress-fill {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  transition: height 0.3s ease;
  border-radius: 2px;
}

.progress-fill.spring {
  background: linear-gradient(to top, #86efac, #22c55e);
}

.progress-fill.summer {
  background: linear-gradient(to top, #fbbf24, #f59e0b);
}

.progress-fill.fall {
  background: linear-gradient(to top, #fb923c, #f97316);
}

.progress-fill.winter {
  background: linear-gradient(to top, #93c5fd, #3b82f6);
}

/* Responsive adjustments */
@media (max-width: 640px) {
  .calendar-content {
    min-width: 280px;
    padding: 10px 16px;
    gap: 10px;
  }
  
  .season-icon {
    width: 40px;
    height: 40px;
  }
  
  .season-icon .icon {
    font-size: 20px;
  }
  
  .date-line {
    font-size: 12px;
  }
  
  .time {
    font-size: 14px;
  }
  
  .season-progress {
    height: 40px;
  }
}
</style>


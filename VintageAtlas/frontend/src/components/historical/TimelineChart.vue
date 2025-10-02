<template>
  <div class="timeline-chart">
    <div v-if="loading" class="chart-loading">
      <div class="spinner"></div>
      <div>Loading historical data...</div>
    </div>
    <div v-else-if="!chartData.datasets[0].data.length" class="chart-empty">
      <div>No historical data available</div>
    </div>
    <canvas ref="chartRef"></canvas>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue';
import Chart from 'chart.js/auto';
import 'chartjs-adapter-date-fns';
import { format } from 'date-fns';
import type { HistoricalSnapshot } from '@/types/historical-data';

const props = defineProps<{
  snapshots: HistoricalSnapshot[];
  selectedTimestamp: string | null;
  loading: boolean;
}>();

const emit = defineEmits<{
  (e: 'select', timestamp: string): void;
}>();

const chartRef = ref<HTMLCanvasElement | null>(null);
let chart: Chart | null = null;

// Format the data for Chart.js
const chartData = computed(() => {
  const data = props.snapshots.map(snapshot => ({
    x: new Date(snapshot.timestamp),
    y: snapshot.playerCount
  }));

  return {
    datasets: [
      {
        label: 'Player Count',
        data,
        borderColor: '#42b883',
        backgroundColor: 'rgba(66, 184, 131, 0.2)',
        pointBackgroundColor: (context: any) => {
          // Highlight the selected point
          if (!props.selectedTimestamp) return '#42b883';
          
          const timestamp = context.raw.x.toISOString();
          return timestamp === props.selectedTimestamp ? '#ff5252' : '#42b883';
        },
        pointRadius: (context: any) => {
          if (!props.selectedTimestamp) return 4;
          
          const timestamp = context.raw.x.toISOString();
          return timestamp === props.selectedTimestamp ? 8 : 4;
        },
        pointHoverRadius: 8,
        tension: 0.3
      }
    ]
  };
});

function initChart() {
  if (!chartRef.value) return;
  
  const ctx = chartRef.value.getContext('2d');
  if (!ctx) return;
  
  chart = new Chart(ctx, {
    type: 'line',
    data: chartData.value,
    options: {
      responsive: true,
      maintainAspectRatio: false,
      scales: {
        x: {
          type: 'time',
          time: {
            unit: 'day',
            tooltipFormat: 'MMM d, yyyy HH:mm'
          },
          title: {
            display: true,
            text: 'Date'
          }
        },
        y: {
          beginAtZero: true,
          title: {
            display: true,
            text: 'Player Count'
          }
        }
      },
      plugins: {
        tooltip: {
          callbacks: {
            title: (items) => {
              if (!items.length) return '';
              const date = new Date(items[0].parsed.x);
              return date.toLocaleString();
            }
          }
        },
        legend: {
          display: false
        }
      },
      onClick: (event, elements) => {
        if (!elements.length) return;
        
        const index = elements[0].index;
        const snapshot = props.snapshots[index];
        emit('select', snapshot.timestamp);
      }
    }
  });
}

// Update chart when data changes
watch(() => props.snapshots, () => {
  if (chart) {
    chart.data = chartData.value;
    chart.update();
  }
}, { deep: true });

// Update chart when selected timestamp changes
watch(() => props.selectedTimestamp, () => {
  if (chart) {
    chart.data = chartData.value;
    chart.update();
  }
});

onMounted(() => {
  initChart();
});
</script>

<style scoped>
.timeline-chart {
  position: relative;
  width: 100%;
  height: 300px;
  background-color: var(--color-background-soft);
  border-radius: 8px;
  padding: 16px;
  margin-bottom: 24px;
}

.chart-loading, .chart-empty {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  background-color: rgba(var(--color-background-rgb), 0.8);
  border-radius: 8px;
  z-index: 1;
}

.spinner {
  width: 40px;
  height: 40px;
  border: 4px solid rgba(var(--color-primary-rgb), 0.1);
  border-left-color: var(--color-primary);
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin-bottom: 16px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>

<template>
  <div class="search-container">
    <div class="relative w-full">
      <input
        type="text"
        v-model="searchQuery"
        @input="handleSearch"
        @focus="showResults = true"
        @blur="handleBlur"
        placeholder="Search for traders, signs, locations..."
        class="w-full py-2 pl-10 pr-4 rounded-lg bg-white/90 dark:bg-gray-800/90 border border-gray-300 dark:border-gray-600 text-gray-700 dark:text-gray-200 placeholder-gray-500 dark:placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-primary focus:border-primary"
      />
      <div class="absolute inset-y-0 left-0 flex items-center pl-3 pointer-events-none">
        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-gray-500 dark:text-gray-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
        </svg>
      </div>
      <button 
        v-if="searchQuery" 
        @click="clearSearch" 
        class="absolute inset-y-0 right-0 flex items-center pr-3"
      >
        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5 text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-200" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
    </div>
    
    <div 
      v-if="showResults && searchResults.length > 0" 
      class="search-results mt-2 bg-white dark:bg-gray-800 rounded-lg shadow-lg border border-gray-200 dark:border-gray-700 max-h-80 overflow-y-auto"
    >
      <div class="p-2 text-sm text-gray-500 dark:text-gray-400 border-b border-gray-200 dark:border-gray-700">
        {{ searchResults.length }} results found
      </div>
      <div class="divide-y divide-gray-200 dark:divide-gray-700">
        <div 
          v-for="result in searchResults" 
          :key="result.id"
          @click="selectResult(result)"
          class="p-3 hover:bg-gray-100 dark:hover:bg-gray-700 cursor-pointer"
        >
          <div class="flex items-center">
            <div class="flex-shrink-0">
              <div 
                class="w-8 h-8 rounded-full flex items-center justify-center"
                :class="{
                  'bg-green-100 dark:bg-green-900 text-green-600 dark:text-green-400': result.type === 'trader',
                  'bg-blue-100 dark:bg-blue-900 text-blue-600 dark:text-blue-400': result.type === 'translocator',
                  'bg-yellow-100 dark:bg-yellow-900 text-yellow-600 dark:text-yellow-400': result.type === 'sign',
                  'bg-red-100 dark:bg-red-900 text-red-600 dark:text-red-400': result.type === 'player',
                }"
              >
                <svg v-if="result.type === 'trader'" xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z" />
                </svg>
                <svg v-else-if="result.type === 'translocator'" xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
                </svg>
                <svg v-else-if="result.type === 'sign'" xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                <svg v-else-if="result.type === 'player'" xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
                </svg>
              </div>
            </div>
            <div class="ml-4">
              <h4 class="text-sm font-medium text-gray-900 dark:text-gray-100">{{ result.name }}</h4>
              <p class="text-xs text-gray-500 dark:text-gray-400">
                <span class="capitalize">{{ result.type }}</span>
                <span v-if="result.coordinates"> • X: {{ result.coordinates[0] }}, Z: {{ result.coordinates[1] }}</span>
                <span v-if="result.text" class="block mt-1 italic">{{ truncateText(result.text) }}</span>
              </p>
            </div>
          </div>
        </div>
      </div>
    </div>
    
    <div 
      v-else-if="showResults && searchQuery && !isLoading" 
      class="search-results mt-2 bg-white dark:bg-gray-800 rounded-lg shadow-lg border border-gray-200 dark:border-gray-700 p-4 text-center"
    >
      <p class="text-gray-500 dark:text-gray-400">No results found</p>
    </div>
    
    <div 
      v-if="isLoading" 
      class="search-results mt-2 bg-white dark:bg-gray-800 rounded-lg shadow-lg border border-gray-200 dark:border-gray-700 p-4 text-center"
    >
      <div class="flex justify-center items-center">
        <div class="w-5 h-5 border-2 border-t-primary border-r-primary border-b-gray-300 border-l-gray-300 rounded-full animate-spin"></div>
        <span class="ml-2 text-gray-500 dark:text-gray-400">Searching...</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
import { useMapStore } from '@/stores/map';
import { debounce } from 'lodash';

const props = defineProps<{
  minSearchLength?: number;
}>();

const mapStore = useMapStore();
const searchQuery = ref('');
const searchResults = ref<any[]>([]);
const showResults = ref(false);
const isLoading = ref(false);

// Debounced search function
const handleSearch = debounce(async () => {
  if (!searchQuery.value || searchQuery.value.length < (props.minSearchLength || 2)) {
    searchResults.value = [];
    return;
  }
  
  isLoading.value = true;
  
  try {
    // Search in all map features
    const results = await searchMapFeatures(searchQuery.value);
    searchResults.value = results;
  } catch (error) {
    console.error('Error searching features:', error);
  } finally {
    isLoading.value = false;
  }
}, 300);

// Search in all map features (traders, translocators, signs, players)
async function searchMapFeatures(query: string) {
  const lowerQuery = query.toLowerCase();
  const results: any[] = [];
  
  // Get the map instance
  const map = mapStore.map;
  if (!map) return [];
  
  // Search in all vector layers
  map.getLayers().forEach(layerGroup => {
    if (layerGroup instanceof ol.layer.Group) {
      layerGroup.getLayers().forEach(layer => {
        if (layer instanceof ol.layer.Vector) {
          const source = layer.getSource();
          if (!source) return;
          
          // Get all features from the layer
          const features = source.getFeatures();
          features.forEach(feature => {
            const properties = feature.getProperties();
            const name = properties.name || '';
            const text = properties.text || properties.wares || '';
            const type = properties.type || (properties.wares ? 'trader' : 'unknown');
            
            // Check if feature matches search query
            if (
              name.toLowerCase().includes(lowerQuery) || 
              text.toLowerCase().includes(lowerQuery)
            ) {
              // Get coordinates
              const geometry = feature.getGeometry();
              let coordinates;
              
              if (geometry && geometry.getType() === 'Point') {
                coordinates = ol.proj.toLonLat(geometry.getCoordinates());
              }
              
              results.push({
                id: properties.id || `${type}-${results.length}`,
                name: name || 'Unnamed',
                text,
                type,
                coordinates,
                feature
              });
            }
          });
        }
      });
    }
  });
  
  return results;
}

function selectResult(result: any) {
  // Center the map on the selected feature
  if (result.coordinates) {
    mapStore.setCenter(result.coordinates);
    mapStore.setZoom(5); // Zoom in to see the feature
  }
  
  // Select the feature
  if (result.feature) {
    mapStore.selectFeature({
      id: result.id,
      name: result.name,
      type: result.type,
      text: result.text,
    });
  }
  
  // Clear search
  showResults.value = false;
}

function clearSearch() {
  searchQuery.value = '';
  searchResults.value = [];
}

function handleBlur() {
  // Delay hiding results to allow for click events
  setTimeout(() => {
    showResults.value = false;
  }, 200);
}

function truncateText(text: string, maxLength = 60) {
  if (text.length <= maxLength) return text;
  return text.substring(0, maxLength) + '...';
}

// Clear search when component is mounted
onMounted(() => {
  clearSearch();
});
</script>

<style scoped>
.search-container {
  position: relative;
  width: 100%;
  z-index: 20;
}

.search-results {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  z-index: 30;
}
</style>

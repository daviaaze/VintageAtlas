import { defineStore } from 'pinia';
import { ref, computed, shallowRef } from 'vue';
import type { Map, View } from 'ol';
import type { GeoJsonFeature } from '@/types/geojson';

/**
 * Store for map state
 */
export const useMapStore = defineStore('map', () => {
  // Use shallowRef for complex objects like OL Map
  const map = shallowRef<Map | null>(null);
  const mapView = shallowRef<View | null>(null);
  const loading = ref(false);
  const error = ref<Error | null>(null);
  
  // Layer visibility state
  const layerVisibility = ref({
    terrain: true,
    biomes: false,
    traders: true,
    translocators: true,
    landmarks: true,
    signs: true,
    exploredChunks: false,
    chunks: false,
    chunkVersions: false,
    players: true,
    animals: true,
    custom: true
  });
  
  // Sub-layer visibility for filtering by feature type (spec lines 379-399)
  // Controls opacity: 1 = visible, 0 = hidden
  const subLayerVisibility = ref({
    traders: {
      'Artisan trader': true,
      'Building materials trader': true,
      'Clothing trader': true,
      'Commodities trader': true,
      'Agriculture trader': true,
      'Furniture trader': true,
      'Luxuries trader': true,
      'Survival goods trader': true,
      'Treasure hunter trader': true,
      'unknown': true
    },
    translocators: {
      'Translocator': true,
      'Named Translocator': true,
      'Spawn Translocator': true,
      'Teleporter': true
    },
    landmarks: {
      'Server': true,
      'Base': true,
      'Misc': true
    }
  });
  
  // Label size for landmarks (spec line 326)
  const labelSize = ref(10);
  
  // Map markers/features
  const features = ref<GeoJsonFeature[]>([]);
  
  // Selected feature
  const selectedFeature = ref<GeoJsonFeature | null>(null);
  
  // Map position
  const center = ref<[number, number]>([0, 0]);
  const zoom = ref(3);
  
  // Computed properties
  const layers = computed(() => {
    if (!map.value) return {};
    
    const result: Record<string, any> = {};
    map.value.getLayers().forEach(layer => {
      const name = layer.get('name');
      if (name) {
        result[name] = layer;
      }
    });
    return result;
  });

  // Actions
  function setMap(newMap: Map) {
    map.value = newMap;
    mapView.value = newMap.getView();
    
    // Get initial values
    const initialCenter = mapView.value.getCenter();
    if (initialCenter) {
      center.value = [initialCenter[0], initialCenter[1]];
    }
    zoom.value = mapView.value.getZoom() || zoom.value;
    
    // Listen for view changes
    mapView.value.on('change:center', () => {
      const newCenter = mapView.value?.getCenter();
      if (newCenter) {
        center.value = [newCenter[0], newCenter[1]];
      }
    });
    
    mapView.value.on('change:resolution', () => {
      zoom.value = mapView.value?.getZoom() || zoom.value;
    });
  }
  
  function toggleLayer(layerName: keyof typeof layerVisibility.value) {
    layerVisibility.value[layerName] = !layerVisibility.value[layerName];
  }
  
  function setLayerVisibility(layerName: keyof typeof layerVisibility.value, visible: boolean) {
    layerVisibility.value[layerName] = visible;
  }
  
  function toggleSubLayer(
    layerName: keyof typeof subLayerVisibility.value,
    subLayerName: string
  ) {
    const layer = subLayerVisibility.value[layerName] as Record<string, boolean>;
    if (layer[subLayerName] !== undefined) {
      layer[subLayerName] = !layer[subLayerName];
      // Force refresh of layer styling
      refreshLayers();
    }
  }
  
  function setSubLayerVisibility(
    layerName: keyof typeof subLayerVisibility.value,
    subLayerName: string,
    visible: boolean
  ) {
    const layer = subLayerVisibility.value[layerName] as Record<string, boolean>;
    if (layer[subLayerName] !== undefined) {
      layer[subLayerName] = visible;
      // Force refresh of layer styling
      refreshLayers();
    }
  }
  
  function setLabelSize(size: number) {
    // Clamp between 8-144px (spec line 326)
    labelSize.value = Math.max(8, Math.min(144, size));
    // Force refresh of layers to update label styling
    refreshLayers();
  }
  
  function selectFeature(feature: GeoJsonFeature | null) {
    selectedFeature.value = feature;
  }
  
  function setCenter(newCenter: [number, number]) {
    center.value = newCenter;
    mapView.value?.setCenter(newCenter);
  }
  
  function setZoom(newZoom: number) {
    zoom.value = newZoom;
    mapView.value?.setZoom(newZoom);
  }
  
  // Fly to a location with animation
  function flyTo(newCenter: [number, number], newZoom?: number, duration = 500) {
    mapView.value?.animate({
      center: newCenter,
      zoom: newZoom !== undefined ? newZoom : zoom.value,
      duration
    });
  }
  
  // Zoom in with animation
  function zoomIn() {
    if (!mapView.value) return;
    const currentZoom = mapView.value.getZoom() || 0;
    mapView.value.animate({
      zoom: currentZoom + 1,
      duration: 250
    });
  }
  
  // Zoom out with animation
  function zoomOut() {
    if (!mapView.value) return;
    const currentZoom = mapView.value.getZoom() || 0;
    mapView.value.animate({
      zoom: currentZoom - 1,
      duration: 250
    });
  }
  
  // Reset view to default position
  function resetView() {
    // Import the default values from mapConfig
    import('@/utils/mapConfig').then(({ defaultCenter, defaultZoom }) => {
      setCenter(defaultCenter());
      setZoom(defaultZoom());
    }).catch(() => {
      // Fallback if import fails
      setCenter([0, 0]);
      setZoom(6);
    });
  }
  
  // Refresh all vector sources to update styles
  function refreshLayers() {
    if (!map.value) return;
    
    map.value.getLayers().forEach((layer: any) => {
      const source = layer.getSource?.();
      if (source && typeof source.changed === 'function') {
        source.changed();
      }
    });
  }
  
  return {
    // State
    map,
    mapView,
    loading,
    error,
    layerVisibility,
    subLayerVisibility,
    labelSize,
    features,
    selectedFeature,
    center,
    zoom,
    layers,
    
    // Actions
    setMap,
    toggleLayer,
    setLayerVisibility,
    toggleSubLayer,
    setSubLayerVisibility,
    setLabelSize,
    selectFeature,
    setCenter,
    setZoom,
    flyTo,
    zoomIn,
    zoomOut,
    resetView,
    refreshLayers
  };
});

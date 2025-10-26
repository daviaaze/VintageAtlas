/**
 * Composable for creating climate heatmap layers using OpenLayers
 * Replaces raster PNG tiles with vector GeoJSON heatmaps
 */
import { ref } from 'vue';
import VectorSource from 'ol/source/Vector';
import GeoJSON from 'ol/format/GeoJSON';
import type Map from 'ol/Map';
import { Heatmap as HeatmapLayer } from 'ol/layer';

export interface ClimateGeoJsonFeature {
  type: 'Feature';
  properties: {
    value: number; // Real temperature/rainfall value
    realValue: number; // Real temperature/rainfall value
  };
  geometry: {
    type: 'Point';
    coordinates: [number, number];
  };
}

export interface ClimateGeoJsonCollection {
  type: 'FeatureCollection';
  name: string;
  features: ClimateGeoJsonFeature[];
}

export function useClimateHeatmap() {
  const isLoadingTemperature = ref(false);
  const isLoadingRainfall = ref(false);
  const temperatureLayer = ref<HeatmapLayer | null>(null);
  const rainfallLayer = ref<HeatmapLayer | null>(null);

  /**
   * Fetch climate GeoJSON data from API
   */
  async function fetchClimateData(type: 'temperature' | 'rainfall'): Promise<ClimateGeoJsonCollection | null> {
    try {
      const response = await fetch(`/api/geojson/climate/${type}`, {
        headers: {
          'Accept': 'application/geo+json, application/json'
        }
      });

      if (!response.ok) {
        throw new Error(`Failed to fetch ${type} data: ${response.statusText}`);
      }

      return await response.json();
    } catch (error) {
      console.error(`Error fetching ${type} GeoJSON:`, error);
      return null;
    }
  }

  /**
   * Create a heatmap layer for temperature data
   * Temperature gradient: blue (cold) → yellow → red (hot)
   */
  function createTemperatureHeatmapLayer(data: ClimateGeoJsonCollection, projection: string): HeatmapLayer {
    const vectorSource = new VectorSource({
      features: new GeoJSON().readFeatures(data, {
        dataProjection: projection,
        featureProjection: projection
      })
    });

    const heatmapLayer = new HeatmapLayer({
      source: vectorSource,
      // @ts-ignore - OpenLayers types are sometimes incomplete
      blur: 25, // Blur radius in pixels (increased for better coverage)
      radius: 15, // Feature radius in pixels (increased for better coverage)
      weight: (feature) => {
        // Temperature range: -50°C to 40°C (Vintage Story standard)
        // Normalize to 0-1 range for heatmap weight
        const temp = feature.get('value') || 0;
        const normalized = (temp + 50) / 90; // Maps [-50, 40] to [0, 1]
        return Math.max(0, Math.min(1, normalized)); // Clamp to [0, 1]
      },
      // Temperature gradient: cold to hot
      gradient: [
        '#00f', // 0.0 - Deep blue (coldest: -50°C)
        '#0af', // 0.2 - Light blue
        '#0ff', // 0.3 - Cyan
        '#0f0', // 0.4 - Green
        '#8f0', // 0.5 - Yellow-green
        '#ff0', // 0.6 - Yellow
        '#fa0', // 0.7 - Orange
        '#f00', // 0.8 - Red
        '#a00'  // 1.0 - Dark red (hottest: 40°C)
      ],
      opacity: 0.6
    });

    heatmapLayer.set('name', 'temperature');
    heatmapLayer.setVisible(false); // Start hidden
    
    return heatmapLayer;
  }

  /**
   * Create a heatmap layer for rainfall data
   * Rainfall gradient: brown (arid) → yellow → green → blue (wet)
   */
  function createRainfallHeatmapLayer(data: ClimateGeoJsonCollection, projection: string): HeatmapLayer {
    const vectorSource = new VectorSource({
      features: new GeoJSON().readFeatures(data, {
        dataProjection: projection,
        featureProjection: projection
      })
    });

    const heatmapLayer = new HeatmapLayer({
      source: vectorSource,
      // @ts-ignore - OpenLayers types are sometimes incomplete
      blur: 25, // Blur radius in pixels (increased for better coverage)
      radius: 15, // Feature radius in pixels (increased for better coverage)
      weight: (feature) => {
        // Rainfall range: 0.0 to 1.0 (already normalized)
        const rainfall = feature.get('value') || 0;
        return Math.max(0, Math.min(1, rainfall)); // Clamp to [0, 1]
      },
      // Rainfall gradient: dry to wet
      gradient: [
        '#8b4513', // 0.0 - Brown (arid desert)
        '#daa520', // 0.2 - Goldenrod
        '#ffff00', // 0.3 - Yellow
        '#9acd32', // 0.4 - Yellow-green
        '#00ff00', // 0.5 - Green (moderate)
        '#00fa9a', // 0.6 - Medium spring green
        '#20b2aa', // 0.7 - Light sea green
        '#1e90ff', // 0.8 - Dodger blue
        '#0000ff'  // 1.0 - Blue (very wet/rainforest)
      ],
      opacity: 0.6
    });

    heatmapLayer.set('name', 'rain');
    heatmapLayer.setVisible(false); // Start hidden
    
    return heatmapLayer;
  }

  /**
   * Load and add temperature heatmap layer to the map
   */
  async function loadTemperatureHeatmap(map: Map, projection: string): Promise<void> {
    if (temperatureLayer.value) {
      console.log('Temperature heatmap already loaded');
      return;
    }

    isLoadingTemperature.value = true;
    
    try {
      const data = await fetchClimateData('temperature');
      if (!data) {
        throw new Error('Failed to fetch temperature data');
      }

      console.log(`Loaded ${data.features.length} temperature data points`);
      
      const layer = createTemperatureHeatmapLayer(data, projection);
      temperatureLayer.value = layer;
      map.addLayer(layer);
      
      console.log('Temperature heatmap layer added to map');
    } catch (error) {
      console.error('Failed to load temperature heatmap:', error);
    } finally {
      isLoadingTemperature.value = false;
    }
  }

  /**
   * Load and add rainfall heatmap layer to the map
   */
  async function loadRainfallHeatmap(map: Map, projection: string): Promise<void> {
    if (rainfallLayer.value) {
      console.log('Rainfall heatmap already loaded');
      return;
    }

    isLoadingRainfall.value = true;
    
    try {
      const data = await fetchClimateData('rainfall');
      if (!data) {
        throw new Error('Failed to fetch rainfall data');
      }

      console.log(`Loaded ${data.features.length} rainfall data points`);
      
      const layer = createRainfallHeatmapLayer(data, projection);
      rainfallLayer.value = layer;
      map.addLayer(layer);
      
      console.log('Rainfall heatmap layer added to map');
    } catch (error) {
      console.error('Failed to load rainfall heatmap:', error);
    } finally {
      isLoadingRainfall.value = false;
    }
  }

  /**
   * Toggle temperature heatmap visibility
   */
  function toggleTemperatureHeatmap(visible: boolean): void {
    if (temperatureLayer.value) {
      temperatureLayer.value.setVisible(visible);
    }
  }

  /**
   * Toggle rainfall heatmap visibility
   */
  function toggleRainfallHeatmap(visible: boolean): void {
    if (rainfallLayer.value) {
      rainfallLayer.value.setVisible(visible);
    }
  }

  return {
    isLoadingTemperature,
    isLoadingRainfall,
    temperatureLayer,
    rainfallLayer,
    loadTemperatureHeatmap,
    loadRainfallHeatmap,
    toggleTemperatureHeatmap,
    toggleRainfallHeatmap
  };
}


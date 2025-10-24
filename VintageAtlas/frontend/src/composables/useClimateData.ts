/**
 * Composable for sampling climate data from map layers
 */
import { ref, onMounted, onUnmounted } from 'vue';
import WebGLTileLayer from 'ol/layer/WebGLTile';
import type Map from 'ol/Map';
import type { Pixel } from 'ol/pixel';
import type { ServerStatus } from '@/types/server-status';

export interface ClimateData {
  temperature: number | null;
  rainfall: number | null;
  temperatureCelsius: number | null; // Base temperature at sea level
  rainfallMm: number | null;
  currentTemperature: number | null; // Real-time temperature (base + modifiers)
}

/**
 * Convert raw 0-255 values to real-world climate data
 * 
 * Based on Vintage Story's Climate API:
 * - Vintagestory.API.Common.Climate.DescaleTemperature()
 * - Temperature scale: float [-50, 40] to int [0, 255]
 * - TemperatureScaleConversion = 255f / (40f - (-50f)) = 255f / 90f
 * 
 * Temperature (from API):
 * - Unscaled [0, 255] → Real temperature [-50°C, 40°C] at sea level
 * - 0 = -50°C (polar)
 * - 128 = -6.6°C (cool)
 * - 255 = 40°C (hot desert)
 * 
 * At sea level average:
 * - Equator: ~30°C
 * - Poles: ~-20°C
 * 
 * Elevation adjustment: -1.5°C per 10 blocks above sea level
 * (not applied here as we don't have Y coordinate in map tiles)
 * 
 * Rainfall:
 * - [0, 255] → normalized [0.0, 1.0] range
 * - Used for biome determination and spawn conditions
 * - Higher values = more rainfall/humidity
 * 
 * References:
 * - https://apidocs.vintagestory.at/api/Vintagestory.API.Common.Climate.html
 * - https://wiki.vintagestory.at/index.php/Temperature
 */
function convertClimateValues(tempRaw: number, rainRaw: number): ClimateData {
  // Temperature conversion based on Climate.DescaleTemperature formula
  // Formula: temp = (unscaledTemp / 255.0) * 90 - 50
  // Range: [-50°C, 40°C] at sea level
  const temperatureCelsius = (tempRaw / 255.0) * 90 - 50;
  
  // Rainfall conversion to normalized value (0.0 to 1.0)
  // This is how it's used in spawn conditions and biome determination
  const rainfallNormalized = rainRaw / 255.0;
  
  // Convert to approximate mm for display (rough estimate)
  // Vintage Story doesn't directly use mm, but for reference:
  // - 0.0 = arid desert
  // - 0.5 = moderate
  // - 1.0 = rainforest/very wet
  // Approximate mm/year for context: 0-2000mm
  const rainfallMm = rainfallNormalized * 2000;
  
  return {
    temperature: tempRaw,
    rainfall: rainRaw,
    temperatureCelsius: Math.round(temperatureCelsius * 10) / 10, // 1 decimal place
    rainfallMm: Math.round(rainfallMm),
    currentTemperature: null // Will be calculated later using server status
  };
}

export function useClimateData() {
  const climateData = ref<ClimateData>({
    temperature: null,
    rainfall: null,
    temperatureCelsius: null,
    rainfallMm: null,
    currentTemperature: null
  });

  const serverStatus = ref<ServerStatus | null>(null);
  let statusFetchInterval: number | null = null;

  // Fetch server status on mount and poll every 5 seconds
  onMounted(() => {
    fetchServerStatus();
    statusFetchInterval = window.setInterval(fetchServerStatus, 5000);
  });

  onUnmounted(() => {
    if (statusFetchInterval) {
      clearInterval(statusFetchInterval);
    }
  });

  /**
   * Fetch current server status (calendar, season, time)
   */
  async function fetchServerStatus() {
    try {
      const response = await fetch('/api/status');
      if (response.ok) {
        serverStatus.value = await response.json();
      }
    } catch (e) {
      console.debug('Failed to fetch server status:', e);
    }
  }

  /**
   * Sample climate data at a pixel location using WebGLTileLayer's getData()
   * This is much faster than Canvas-based sampling
   * @param map OpenLayers map instance
   * @param pixel [x, y] pixel coordinates on the screen
   */
  function sampleClimateAtPixel(map: Map, pixel: Pixel): ClimateData {
    let tempLayer: WebGLTileLayer | undefined;
    let rainLayer: WebGLTileLayer | undefined;

    // Find temperature and rain layers
    map.getLayers().forEach((layer) => {
      const name = layer.get('name');
      if (name === 'temperature' && layer instanceof WebGLTileLayer) {
        tempLayer = layer as WebGLTileLayer;
      } else if (name === 'rain' && layer instanceof WebGLTileLayer) {
        rainLayer = layer as WebGLTileLayer;
      }
    });

    let tempValue = 0;
    let rainValue = 0;

    // Sample temperature data using WebGL's fast getData() method
    if (tempLayer) {
      try {
        const data = tempLayer.getData(pixel);
        // getData returns Uint8Array or similar array-like structure
        if (data && 'length' in data && data.length >= 3) {
          // Since we stored as grayscale, all RGB channels should be the same
          // Use the red channel (or any channel, they're identical)
          tempValue = (data as Uint8Array | Uint8ClampedArray)[0]; // 0-255
        }
      } catch (e) {
        // Tile not loaded yet or out of bounds
        console.debug('Temperature data not available at this location');
      }
    }

    // Sample rainfall data using WebGL's fast getData() method
    if (rainLayer) {
      try {
        const data = rainLayer.getData(pixel);
        // getData returns Uint8Array or similar array-like structure
        if (data && 'length' in data && data.length >= 3) {
          // Since we stored as grayscale, all RGB channels should be the same
          rainValue = (data as Uint8Array | Uint8ClampedArray)[0]; // 0-255
        }
      } catch (e) {
        // Tile not loaded yet or out of bounds
        console.debug('Rain data not available at this location');
      }
    }

    const converted = convertClimateValues(tempValue, rainValue);
    
    // Calculate current temperature if we have server status
    if (serverStatus.value && converted.temperatureCelsius !== null) {
      converted.currentTemperature = Math.round(
        (converted.temperatureCelsius + serverStatus.value.temperature.totalModifier) * 10
      ) / 10;
    }
    
    climateData.value = converted;
    return converted;
  }

  /**
   * Format climate data for display
   */
  function formatClimateData(data: ClimateData): { temperatureCelsius: string; rainfall: string } | null {
    if (data.temperatureCelsius === null || data.rainfall === null) {
      return null;
    }

    const tempRange = getInGameTemperatureRange(data.temperatureCelsius)

    const rainfallDesc = getRainfallDescriptor(data.rainfall / 255);
    return {
      temperatureCelsius: `${tempRange.min}°C - ${tempRange.max}°C`,
      rainfall: rainfallDesc,
    };
  }

  /**
   * Convert rainfall normalized value to in-game descriptor
   * Based on Vintage Story's rainfall terminology
   */
  function getRainfallDescriptor(rainNormalized: number): string {
    if (rainNormalized >= 0.95) return 'Almost all the time';
    if (rainNormalized >= 0.75) return 'Very often';
    if (rainNormalized >= 0.50) return 'Often';
    if (rainNormalized >= 0.30) return 'Sometimes';
    if (rainNormalized >= 0.10) return 'Rare';
    return 'Almost never';
  }

  /**
   * Get a hint about the biome type based on climate values
   * Based on Vintage Story's biome determination logic
   */
  function getBiomeHint(tempC: number, rainNormalized: number): string {
    // Very cold
    if (tempC < -10) {
      return rainNormalized > 0.3 ? '❄️ Tundra' : '🏔️ Polar';
    }
    
    // Cold
    if (tempC < 5) {
      return rainNormalized > 0.5 ? '🌲 Taiga' : '🏔️ Alpine';
    }
    
    // Temperate
    if (tempC < 20) {
      if (rainNormalized > 0.6) return '🌳 Forest';
      if (rainNormalized > 0.3) return '🌾 Plains';
      return '🏜️ Steppe';
    }
    
    // Warm
    if (tempC < 30) {
      if (rainNormalized > 0.6) return '🌴 Tropical';
      if (rainNormalized > 0.3) return '🌿 Savanna';
      return '🏜️ Desert';
    }
    
    // Hot
    if (rainNormalized > 0.6) return '🌴 Rainforest';
    return '🏜️ Hot Desert';
  }

  /**
   * Calculate expected in-game temperature range from base temperature
   * Helps users understand why in-game temperature differs from map
   */
  function getInGameTemperatureRange(baseTemp: number): { min: number; max: number } {
    // In-game modifiers (approximate):
    // - Season: ±10-15°C (coldest winter to hottest summer)
    // - Time of day: ±10-15°C (coldest 4am to hottest 4pm)
    // Combined range is roughly base ±25-30°C
    
    return {
      min: Math.round(baseTemp - 25),  // Cold winter night
      max: Math.round(baseTemp + 30)   // Hot summer afternoon
    };
  }

  return {
    climateData,
    serverStatus,
    sampleClimateAtPixel,
    formatClimateData,
    getRainfallDescriptor,
    getInGameTemperatureRange
  };
}


/**
 * API service for fetching GeoJSON data dynamically from the server
 */

export interface GeoJsonFeatureCollection {
  type: 'FeatureCollection';
  features: Array<{
    type: string;
    properties: Record<string, any>;
    geometry: {
      type: string;
      coordinates: any;
    };
  }>;
  crs?: {
    type: string;
    properties: {
      name: string;
    };
  };
  name?: string;
}

/**
 * Fetch signs/landmarks GeoJSON from API
 */
export async function fetchSignsGeoJson(): Promise<GeoJsonFeatureCollection> {
  return fetchGeoJson('/api/geojson/signs');
}

/**
 * Fetch signposts GeoJSON from API
 */
export async function fetchSignPostsGeoJson(): Promise<GeoJsonFeatureCollection> {
  return fetchGeoJson('/api/geojson/signposts');
}

/**
 * Fetch traders GeoJSON from API
 */
export async function fetchTradersGeoJson(): Promise<GeoJsonFeatureCollection> {
  return fetchGeoJson('/api/geojson/traders');
}

/**
 * Fetch translocators GeoJSON from API
 */
export async function fetchTranslocatorsGeoJson(): Promise<GeoJsonFeatureCollection> {
  return fetchGeoJson('/api/geojson/translocators');
}

/**
 * Generic GeoJSON fetcher with caching support
 */
async function fetchGeoJson(url: string): Promise<GeoJsonFeatureCollection> {
  try {
    const response = await fetch(url, {
      headers: {
        'Accept': 'application/geo+json, application/json'
      }
    });

    if (!response.ok) {
      throw new Error(`Failed to fetch GeoJSON from ${url}: ${response.statusText}`);
    }

    const data = await response.json();
    return data;
  } catch (error) {
    console.error(`Error fetching GeoJSON from ${url}:`, error);
    // Return empty feature collection as fallback
    return {
      type: 'FeatureCollection',
      features: []
    };
  }
}

/**
 * Fetch all GeoJSON layers at once
 */
export async function fetchAllGeoJson(): Promise<{
  signs: GeoJsonFeatureCollection;
  signposts: GeoJsonFeatureCollection;
  traders: GeoJsonFeatureCollection;
  translocators: GeoJsonFeatureCollection;
}> {
  const [signs, signposts, traders, translocators] = await Promise.all([
    fetchSignsGeoJson(),
    fetchSignPostsGeoJson(),
    fetchTradersGeoJson(),
    fetchTranslocatorsGeoJson()
  ]);

  return {
    signs,
    signposts,
    traders,
    translocators
  };
}


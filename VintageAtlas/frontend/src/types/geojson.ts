/**
 * GeoJSON types for map features
 */

/**
 * GeoJSON Feature
 */
export interface GeoJsonFeature<P = any, G = any> {
  type: 'Feature';
  geometry: G;
  properties: P;
  id?: string | number;
}

/**
 * GeoJSON FeatureCollection
 */
export interface GeoJsonFeatureCollection<P = any, G = any> {
  type: 'FeatureCollection';
  features: GeoJsonFeature<P, G>[];
}

/**
 * GeoJSON Point Geometry
 */
export interface GeoJsonPointGeometry {
  type: 'Point';
  coordinates: [number, number]; // [longitude, latitude]
}

/**
 * GeoJSON LineString Geometry
 */
export interface GeoJsonLineStringGeometry {
  type: 'LineString';
  coordinates: Array<[number, number]>; // Array of [longitude, latitude] pairs
}

/**
 * GeoJSON Polygon Geometry
 */
export interface GeoJsonPolygonGeometry {
  type: 'Polygon';
  coordinates: Array<Array<[number, number]>>; // Array of arrays of [longitude, latitude] pairs
}

/**
 * GeoJSON Properties for traders
 */
export interface TraderProperties {
  name: string;
  type: string;
  description?: string;
  icon?: string;
}

/**
 * GeoJSON Properties for translocators
 */
export interface TranslocatorProperties {
  name: string;
  destination?: string;
  active: boolean;
  icon?: string;
}

/**
 * GeoJSON Properties for signs
 */
export interface SignProperties {
  text: string;
  tag?: string;
  color?: string;
  icon?: string;
}

/**
 * GeoJSON Properties for chunks
 */
export interface ChunkProperties {
  chunkId: string;
  version: number;
  lastModified: string; // ISO date string
}

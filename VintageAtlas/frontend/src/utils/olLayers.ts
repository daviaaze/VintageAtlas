/**
 * Clean OpenLayers Layer Factory
 * Based on OPENLAYERS_SPECIFICATION.md
 */
import TileLayer from 'ol/layer/Tile';
import WebGLTileLayer from 'ol/layer/WebGLTile';
import VectorLayer from 'ol/layer/Vector';
import VectorSource from 'ol/source/Vector';
import XYZ from 'ol/source/XYZ';
import DataTile from 'ol/source/DataTile';
import GeoJSON from 'ol/format/GeoJSON';
import { createTileGrid, getTileUrl } from './olMapConfig';
import {
  exploredChunksStyle,
  tradersStyle,
  translocatorsStyle,
  landmarksStyle
} from './olStyles';

/**
 * Create world tile layer (Spec lines 89-108)
 */
export function createWorldLayer(): TileLayer<XYZ> {
  const tileGrid = createTileGrid(256);
  
  const source = new XYZ({
    tileGrid: tileGrid,
    wrapX: false,
    interpolate: false,
    tileUrlFunction: ([z, x, y]) => getTileUrl(z, x, y)
  });
  
  source.on('tileloaderror', (evt) => {
    console.error('[Tile Load Error]', evt);
  });
  
  return new TileLayer({
    source: source,
    properties: { name: 'world' }
  });
}

/**
 * Create rain layer with data sampling capability
 * Uses WebGLTileLayer for efficient pixel data access via getData()
 */
export function createRainLayer(): WebGLTileLayer {
  const tileGrid = createTileGrid(512, 1);

  const source = new DataTile({
    tileGrid: tileGrid,
    wrapX: false,
    interpolate: true, // Enable for smoother rendering
    loader: async (_z, x, y) => {
      const url = `/rain-tiles/${x}_${y}.png`;
      const response = await fetch(url);
      if (!response.ok) {
        throw new Error(`Failed to load rain tile: ${url}`);
      }
      const blob = await response.blob();
      const imageBitmap = await createImageBitmap(blob);
      
      // Extract pixel data from the image
      const canvas = document.createElement('canvas');
      canvas.width = imageBitmap.width;
      canvas.height = imageBitmap.height;
      const ctx = canvas.getContext('2d')!;
      ctx.drawImage(imageBitmap, 0, 0);
      const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
      
      return imageData.data;
    }
  });
  
  return new WebGLTileLayer({
    source: source,
    visible: false,
    opacity: 0.6,
    style: {
      color: [
        'array',
        0,    // Red
        0,    // Green
        ['/', ['band', 1], 255], // Blue (rainfall value normalized)
        ['/', ['band', 1], 255]  // Alpha (rainfall value normalized)
      ]
    },
    properties: { name: 'rain' }
  });
}

/**
 * Create temperature layer with data sampling capability
 * Uses WebGLTileLayer for efficient pixel data access via getData()
 */
export function createTemperatureLayer(): WebGLTileLayer {
  const tileGrid = createTileGrid(512, 1);
  
  const source = new DataTile({
    tileGrid: tileGrid,
    wrapX: false,
    interpolate: true, // Enable for smoother rendering
    loader: async (_z, x, y) => {
      const url = `/temperature-tiles/${x}_${y}.png`;
      const response = await fetch(url);
      if (!response.ok) {
        throw new Error(`Failed to load temperature tile: ${url}`);
      }
      const blob = await response.blob();
      const imageBitmap = await createImageBitmap(blob);
      
      // Extract pixel data from the image
      const canvas = document.createElement('canvas');
      canvas.width = imageBitmap.width;
      canvas.height = imageBitmap.height;
      const ctx = canvas.getContext('2d')!;
      ctx.drawImage(imageBitmap, 0, 0);
      const imageData = ctx.getImageData(0, 0, canvas.width, canvas.height);
      
      return imageData.data;
    }
  });
  
  return new WebGLTileLayer({
    source: source,
    visible: false,
    opacity: 0.6,
    style: {
      color: [
        'array',
        ['/', ['band', 1], 255], // Red (temperature value normalized)
        0,    // Green
        0,    // Blue
        ['/', ['band', 1], 255]  // Alpha (temperature value normalized)
      ]
    },
    properties: { name: 'temperature' }
  });
}

/**
 * Create explored chunks layer (Spec lines 111-142)
 */
export function createExploredChunksLayer(): VectorLayer<VectorSource> {
  return new VectorLayer({
    source: new VectorSource({
      url: '/api/geojson/chunk',
      format: new GeoJSON({
        dataProjection: 'EPSG:3857',
        featureProjection: 'EPSG:3857'
      })
    }),
    style: exploredChunksStyle,
    opacity: 0.5,
    visible: false,
    properties: { name: 'explored-chunks' }
  });
}

/**
 * Create traders layer (Spec lines 145-188)
 */
export function createTradersLayer(): VectorLayer<VectorSource> {
  return new VectorLayer({
    source: new VectorSource({
      url: '/api/geojson/traders',
      format: new GeoJSON({
        dataProjection: 'EPSG:3857',
        featureProjection: 'EPSG:3857'
      })
    }),
    style: tradersStyle,
    properties: { name: 'traders' }
  });
}

/**
 * Create translocators layer (Spec lines 191-257)
 */
export function createTranslocatorsLayer(): VectorLayer<VectorSource> {
  return new VectorLayer({
    source: new VectorSource({
      url: '/api/geojson/translocators',
      format: new GeoJSON({
        dataProjection: 'EPSG:3857',
        featureProjection: 'EPSG:3857'
      })
    }),
    style: translocatorsStyle,
    minZoom: 2,
    properties: { name: 'translocators' }
  });
}

/**
 * Create landmarks layer (Spec lines 260-329)
 */
export function createLandmarksLayer(mapInstance: any): VectorLayer<VectorSource> {
  return new VectorLayer({
    source: new VectorSource({
      url: '/api/geojson/landmarks',
      format: new GeoJSON({
        dataProjection: 'EPSG:3857',
        featureProjection: 'EPSG:3857'
      })
    }),
    style: (feature) => {
      const zoom = mapInstance?.getView()?.getZoom() || 6;
      return landmarksStyle(feature, zoom);
    },
    minZoom: 2,
    properties: { name: 'landmarks' }
  });
}
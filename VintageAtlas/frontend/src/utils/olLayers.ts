/**
 * Clean OpenLayers Layer Factory
 * Based on OPENLAYERS_SPECIFICATION.md
 */
import TileLayer from 'ol/layer/Tile';
import VectorLayer from 'ol/layer/Vector';
import VectorSource from 'ol/source/Vector';
import XYZ from 'ol/source/XYZ';
import GeoJSON from 'ol/format/GeoJSON';
import { createTileGrid, getTileUrl } from './olMapConfig';
import {
  exploredChunksStyle,
  tradersStyle,
  translocatorsStyle,
  landmarksStyle
} from './olStyles';
import TileGrid from 'ol/tilegrid/TileGrid';

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

export function createRainLayer(): TileLayer<XYZ> {
  const tileGrid = createTileGrid(512, 1);

  const source = new XYZ({
    tileGrid: tileGrid,
    wrapX: false,
    interpolate: false,
    tileUrlFunction: ([_, x, y]) => `/rain-tiles/${x}_${y}.png`
  });
  return new TileLayer({
    source: source,
    visible: true,
    properties: { name: 'rain' }
  });
}

export function createTemperatureLayer(): TileLayer<XYZ> {
  const tileGrid = createTileGrid(512, 1);
  const source = new XYZ({
    tileGrid: tileGrid,
    wrapX: false,
    interpolate: false,
    tileUrlFunction: ([_, x, y]) => `/temperature-tiles/${x}_${y}.png`
  });
  return new TileLayer({
    source: source,
    visible: true,
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
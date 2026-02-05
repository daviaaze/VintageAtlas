/**
 * Clean OpenLayers Layer Factory
 * Based on OPENLAYERS_SPECIFICATION.md
 */
import TileLayer from 'ol/layer/Tile';
import VectorLayer from 'ol/layer/Vector';
import VectorSource from 'ol/source/Vector';
import XYZ from 'ol/source/XYZ';
import GeoJSON from 'ol/format/GeoJSON';
import { Feature } from 'ol';
import { Point } from 'ol/geom';
import { createTileGrid, getTileUrl } from './olMapConfig';
import {
  exploredChunksStyle,
  tradersStyle,
  createDynamicTradersStyleFn,
  translocatorsStyle,
  landmarksStyle,
  playerStyle,
  spawnStyle,
  waypointStyle
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
    properties: { name: 'terrain' }
  });
}

export function createTemperatureLayer(): TileLayer<XYZ> {
  const tileGrid = createTileGrid(256);
  const source = new XYZ({
    tileGrid: tileGrid,
    wrapX: false,
    interpolate: false,
    tileUrlFunction: ([z, x, y]) => `/tiles/temperature/${z}/${x}_${y}.png`
  });
  source.on('tileloaderror', (evt) => {
    console.error('[Tile Load Error] Temperature', evt);
  });
  return new TileLayer({
    source: source,
    properties: { name: 'temperature' },
    visible: false
  });
}

export function createRainfallLayer(): TileLayer<XYZ> {
  const tileGrid = createTileGrid(256);
  const source = new XYZ({
    tileGrid: tileGrid,
    wrapX: false,
    interpolate: false,
    tileUrlFunction: ([z, x, y]) => `/tiles/rainfall/${z}/${x}_${y}.png`
  });
  source.on('tileloaderror', (evt) => {
    console.error('[Tile Load Error] Rainfall', evt);
  });
  return new TileLayer({
    source: source,
    properties: { name: 'rainfall' },
    visible: false
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
 * Pass a getter function to dynamically read sublayer visibility
 */
export function createTradersLayer(getVisibility?: (category: string) => boolean): VectorLayer<VectorSource> {
  const styleFunction = getVisibility
    ? createDynamicTradersStyleFn(getVisibility)
    : tradersStyle;

  return new VectorLayer({
    source: new VectorSource({
      url: '/api/geojson/traders',
      format: new GeoJSON({
        dataProjection: 'EPSG:3857',
        featureProjection: 'EPSG:3857'
      })
    }),
    style: styleFunction,
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

/**
 * Create players layer - shows online player positions
 */
export function createPlayersLayer(): VectorLayer<VectorSource> {
  return new VectorLayer({
    source: new VectorSource({
      features: []
    }),
    style: playerStyle,
    zIndex: 100,
    properties: { name: 'players' }
  });
}

/**
 * Create spawn point layer - shows the world spawn
 */
export function createSpawnLayer(spawnX: number, spawnY: number): VectorLayer<VectorSource> {
  const spawnFeature = new Feature({
    geometry: new Point([spawnX, spawnY]),
    name: 'Spawn'
  });

  return new VectorLayer({
    source: new VectorSource({
      features: [spawnFeature]
    }),
    style: spawnStyle,
    zIndex: 50,
    properties: { name: 'spawn' }
  });
}

// Waypoints layer
export function createWaypointsLayer(): VectorLayer<VectorSource> {
  return new VectorLayer({
    source: new VectorSource({
      features: []
    }),
    style: waypointStyle,
    properties: { name: 'waypoints' },
    visible: false
  });
}
/**
 * Factory for creating optimized OpenLayers layers
 * Uses VectorImage layers for better performance
 */
import VectorImageLayer from 'ol/layer/VectorImage';
import VectorLayer from 'ol/layer/Vector';
import VectorSource from 'ol/source/Vector';
import Cluster from 'ol/source/Cluster';
import GeoJSON from 'ol/format/GeoJSON';
import { Style, Fill, Stroke, Text, Circle, Icon } from 'ol/style';
import type { StyleLike } from 'ol/style/Style';

export interface LayerOptions {
  name: string;
  url: string;
  style: StyleLike;
  visible?: boolean;
  zIndex?: number;
  minZoom?: number;
  maxZoom?: number;
  useImage?: boolean; // Use VectorImageLayer for better performance
  cluster?: boolean; // Use clustering for many features
  clusterDistance?: number;
}

/**
 * Create an optimized vector layer
 */
export function createVectorLayer(options: LayerOptions) {
  const {
    name,
    url,
    style,
    visible = true,
    zIndex = 0,
    minZoom,
    maxZoom,
    useImage = true, // Default to VectorImage for performance
    cluster = false,
    clusterDistance = 40
  } = options;

  const source = new VectorSource({
    url,
    format: new GeoJSON(),
    // Load features only when in viewport
    strategy: (extent, resolution) => [extent],
  });

  // If clustering is enabled
  const layerSource = cluster
    ? new Cluster({
        distance: clusterDistance,
        source: source,
        minDistance: 20
      })
    : source;

  // Create layer with appropriate type
  const LayerClass = useImage ? VectorImageLayer : VectorLayer;
  
  const layer = new LayerClass({
    source: layerSource,
    style: cluster ? createClusterStyle(style) : style,
    visible,
    zIndex,
    minZoom,
    maxZoom,
    properties: { name },
    // Only for VectorImageLayer
    ...(useImage && {
      imageRatio: 2, // Higher quality when zooming
      renderBuffer: 250
    })
  });

  return layer;
}

/**
 * Create a cluster style wrapper
 */
function createClusterStyle(baseStyle: StyleLike) {
  return (feature: any) => {
    const features = feature.get('features');
    const size = features?.length || 1;

    // If single feature, use base style
    if (size === 1) {
      return typeof baseStyle === 'function'
        ? baseStyle(features[0])
        : baseStyle;
    }

    // Cluster style
    return new Style({
      image: new Circle({
        radius: Math.min(15 + size * 0.5, 40),
        fill: new Fill({
          color: 'rgba(66, 133, 244, 0.8)'
        }),
        stroke: new Stroke({
          color: '#fff',
          width: 2
        })
      }),
      text: new Text({
        text: size.toString(),
        fill: new Fill({
          color: '#fff'
        }),
        font: 'bold 14px sans-serif'
      })
    });
  };
}

/**
 * Create optimized trader layer
 */
export function createTraderLayer(visible = true) {
  return createVectorLayer({
    name: 'traders',
    url: '/api/geojson/traders',
    visible,
    zIndex: 100,
    useImage: true,
    style: (feature: any) => {
      const properties = feature.getProperties();
      return new Style({
        image: new Icon({
          src: '/assets/icons/waypoints/trader.svg',
          scale: 0.8,
          anchor: [0.5, 0.5]
        }),
        text: new Text({
          text: properties.name || '',
          offsetY: -20,
          font: '12px sans-serif',
          fill: new Fill({ color: '#333' }),
          stroke: new Stroke({
            color: '#fff',
            width: 3
          })
        })
      });
    }
  });
}

/**
 * Create optimized translocator layer
 */
export function createTranslocatorLayer(visible = true) {
  return createVectorLayer({
    name: 'translocators',
    url: '/api/geojson/translocators',
    visible,
    zIndex: 100,
    useImage: true,
    style: (feature: any) => {
      const properties = feature.getProperties();
      return new Style({
        image: new Icon({
          src: '/assets/icons/waypoints/spiral.svg',
          scale: 0.8,
          anchor: [0.5, 0.5]
        }),
        text: properties.name ? new Text({
          text: properties.name,
          offsetY: -20,
          font: '12px sans-serif',
          fill: new Fill({ color: '#333' }),
          stroke: new Stroke({
            color: '#fff',
            width: 3
          })
        }) : undefined
      });
    }
  });
}

/**
 * Create optimized signs layer
 */
export function createSignsLayer(visible = true) {
  return createVectorLayer({
    name: 'signs',
    url: '/api/geojson/signposts',
    visible,
    zIndex: 100,
    useImage: true,
    style: (feature: any) => {
      const properties = feature.getProperties();
      return new Style({
        image: new Icon({
          src: '/assets/icons/waypoints/star1.svg',
          scale: 0.7,
          anchor: [0.5, 0.5]
        }),
        text: properties.name ? new Text({
          text: properties.name,
          offsetY: -20,
          font: '12px sans-serif',
          fill: new Fill({ color: '#333' }),
          stroke: new Stroke({
            color: '#fff',
            width: 3
          })
        }) : undefined
      });
    }
  });
}

/**
 * Create optimized chunk layer
 */
export function createChunkLayer(visible = false) {
  return createVectorLayer({
    name: 'chunks',
    url: '/api/geojson/chunks',  // TODO: Implement chunks API endpoint
    visible,
    zIndex: 10,
    useImage: true,
    style: new Style({
      stroke: new Stroke({
        color: 'rgba(100, 149, 237, 0.6)',
        width: 2
      }),
      fill: new Fill({
        color: 'rgba(100, 149, 237, 0.1)'
      })
    })
  });
}


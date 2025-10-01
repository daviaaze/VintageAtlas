/**
 * Factory for creating OpenLayers layers matching WebCartographer specification
 * Uses VectorImage layers for better performance
 */
import VectorImageLayer from 'ol/layer/VectorImage';
import VectorLayer from 'ol/layer/Vector';
import VectorSource from 'ol/source/Vector';
import Cluster from 'ol/source/Cluster';
import GeoJSON from 'ol/format/GeoJSON';
import { Style, Fill, Stroke, Text, Circle, Icon } from 'ol/style';
import { MultiPoint, LineString } from 'ol/geom';
import type { StyleLike } from 'ol/style/Style';
import type { Feature } from 'ol';
import {
  getTraderColor,
  getTranslocatorColor,
  getLandmarkColor,
  getLandmarkIcon,
  rgbToString,
  HIGHLIGHT_COLORS,
  brightenColor
} from './layerColors';

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
  // Removed projection - WebCartographer uses OpenLayers defaults (EPSG:3857)
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
    format: new GeoJSON(),  // Use defaults - both data and display are EPSG:3857
    // No strategy = fetch once and cache (perfect for static data like traders/signs)
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
  return (feature: any, resolution: number) => {
    const features = feature.get('features');
    const size = features?.length || 1;

    // If single feature, use base style
      if (size === 1) {
        return typeof baseStyle === 'function'
          ? baseStyle(features[0], resolution)
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
 * Create trader layer matching spec (lines 145-166)
 * - Dynamic color based on trader wares type
 * - Min zoom level 3
 * - SVG icon dynamically colored
 */
export function createTraderLayer(
  visible = true,
  subLayerVisibility?: Record<string, boolean>
) {
  return createVectorLayer({
    name: 'traders',
    url: '/api/geojson/traders',
    visible,
    zIndex: 100,
    minZoom: 3, // Spec line 150
    useImage: true,
    style: (feature: any) => {
      const properties = feature.getProperties();
      const wares = properties.wares || 'unknown';
      const color = getTraderColor(wares);
      
      // Get opacity from sub-layer visibility (spec line 156)
      const opacity = subLayerVisibility?.[wares] !== false ? 1 : 0;
      
      return new Style({
        image: new Icon({
          src: '/assets/icons/waypoints/trader.svg',
          color: color, // Dynamic color (spec line 159)
          opacity: opacity, // Visibility control (spec line 160)
          anchor: [0.5, 0.5]
        })
      });
    }
  });
}

/**
 * Create translocator layer matching spec (lines 191-241)
 * - Dual style: line connecting endpoints + icons at both ends
 * - Dynamic color based on tag and label
 * - Min zoom level 2
 */
export function createTranslocatorLayer(
  visible = true, 
  subLayerVisibility?: Record<string, boolean>
) {
  return createVectorLayer({
    name: 'translocators',
    url: '/api/geojson/translocators',
    visible,
    zIndex: 100,
    minZoom: 2, // Spec line 196
    useImage: true,
    style: (feature: any) => {
      const properties = feature.getProperties();
      const tag = properties.tag;
      const label = properties.label;
      const color = getTranslocatorColor(tag, label);
      
      // Determine translocator type for visibility control
      let typeKey = 'Translocator';
      if (tag === 'SPAWN') {
        typeKey = 'Spawn Translocator';
      } else if (tag === 'TP') {
        typeKey = 'Teleporter';
      } else if (label && label.length > 0) {
        typeKey = 'Named Translocator';
      }
      
      // Get opacity from sub-layer visibility (spec line 204)
      const opacity = subLayerVisibility?.[typeKey] !== false ? 1 : 0;
      
      // Spec lines 218-239: Dual style with line and endpoint icons
      return [
        // Line connecting endpoints (spec lines 220-225)
        new Style({
          stroke: new Stroke({
            color: rgbToString(color, opacity),
            width: 2,
          })
        }),
        // Icons at both endpoints (spec lines 227-238)
        new Style({
          image: new Icon({
            color: color,
            opacity: opacity,
            src: '/assets/icons/waypoints/spiral.svg',
            anchor: [0.5, 0.5]
          }),
          geometry: function (feature) {
            // Get LineString coordinates and create MultiPoint for both ends
            const geom = feature.getGeometry();
            if (geom && geom instanceof LineString) {
              const coordinates = geom.getCoordinates();
              return new MultiPoint(coordinates);
            }
            return geom;
          }
        })
      ];
    }
  });
}

/**
 * Create landmarks layer matching spec (lines 260-306)
 * - Separate from signs, includes Base/Misc/Server types
 * - Dynamic icons and colors based on type
 * - Min zoom level 2
 * - Misc landmarks hidden below zoom 9
 * - Text labels with configurable size
 */
export function createLandmarksLayer(
  visible = true,
  subLayerVisibility?: Record<string, boolean>,
  labelSize: number = 10,
  mapInstance?: any // To access zoom level for Misc visibility
) {
  return createVectorLayer({
    name: 'landmarks',
    url: '/api/geojson/landmarks',
    visible,
    zIndex: 100,
    minZoom: 2, // Spec line 265
    useImage: true,
    style: (feature: any) => {
      const properties = feature.getProperties();
      const type = properties.type || 'Misc';
      const label = properties.label || properties.name || '';
      const color = getLandmarkColor(type);
      const iconSrc = getLandmarkIcon(type);
      
      // Get opacity from sub-layer visibility
      const isOn = subLayerVisibility?.[type] !== false ? 1 : 0;
      
      // Hide 'Misc' landmarks below zoom 9 (spec lines 272-279)
      const currentZoom = mapInstance?.getView()?.getZoom() || 6;
      if (type === 'Misc' && currentZoom < 9) {
        return new Style({
          image: new Icon({
            opacity: 0,
            src: iconSrc
          })
        });
      }
      
      // Spec lines 281-299: Icon and text styling
      let image = undefined, text = undefined;
      
      if (isOn) {
        image = new Icon({
          color: type === 'Server' ? undefined : color, // Server uses PNG, no color
          opacity: isOn,
          src: iconSrc,
          anchor: [0.5, 0.5]
        });
        
        // Text label (spec lines 290-298)
        text = new Text({
          font: `bold ${labelSize}px "arial narrow", "sans serif"`,
          text: label,
          textAlign: 'left',
          textBaseline: 'bottom',
          offsetX: 10,
          fill: new Fill({ color: [0, 0, 0] }),
          stroke: new Stroke({ color: [255, 255, 255], width: 3 })
        });
      }
      
      // Server landmarks have high z-index (spec line 302)
      return new Style({
        zIndex: type === "Server" ? 1000 : undefined,
        image,
        text
      });
    }
  });
}

/**
 * Create signs layer (signposts)
 * This is separate from landmarks
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
 * Create explored chunks layer matching spec (lines 111-129)
 * Shows chunk generation version/history with feature-defined colors
 */
export function createExploredChunksLayer(visible = false) {
  return createVectorLayer({
    name: 'explored-chunks',
    url: '/api/geojson/chunk',
    visible,
    zIndex: 10,
    useImage: true,
    style: (feature: any) => {
      // Color from GeoJSON feature properties (spec line 124)
      const color = feature.get('color') || 'rgba(100, 149, 237, 0.5)';
      
      return new Style({
        fill: new Fill({ color: color }),
        stroke: new Stroke({
          color: '#000000', // Black 1px border (spec line 125)
          width: 1
        })
      });
    }
  });
}

/**
 * Create chunk layer (simple grid overlay)
 */
export function createChunkLayer(visible = false) {
  return createVectorLayer({
    name: 'chunks',
    url: '/api/geojson/chunks',
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

/**
 * Create chunk version layer
 * Shows colored regions for chunks grouped by game version (exploration history)
 */
export function createChunkVersionLayer(visible = false) {
  return createVectorLayer({
    name: 'chunk-versions',
    url: '/api/geojson/chunk-versions',
    visible,
    zIndex: 15, // Above base map, below entities
    useImage: true,
    style: (feature: any) => {
      // Get hex color from GeoJSON feature properties (e.g., "#FF6A00")
      const hexColor = feature.get('color') || '#FF6A00';
      
      // Convert hex to rgba with opacity
      // Remove # if present
      const hex = hexColor.replace('#', '');
      const r = parseInt(hex.substring(0, 2), 16);
      const g = parseInt(hex.substring(2, 4), 16);
      const b = parseInt(hex.substring(4, 6), 16);
      
      // Create colors with different opacities
      const fillColor = `rgba(${r}, ${g}, ${b}, 0.4)`;   // 40% opacity for fill
      const strokeColor = `rgba(${r}, ${g}, ${b}, 0.8)`; // 80% opacity for stroke
      
      return new Style({
        fill: new Fill({
          color: fillColor
        }),
        stroke: new Stroke({
          color: strokeColor,
          width: 2
        })
      });
    }
  });
}

/**
 * Create highlight style for translocators (spec lines 424-445)
 */
export function createTranslocatorHighlightStyle(): Style[] {
  const color = HIGHLIGHT_COLORS.translocator;
  return [
    new Style({
      stroke: new Stroke({
        color: color.stroke,
        width: 3,
      }),
    }),
    new Style({
      image: new Icon({
        color: color.icon,
        opacity: 1,
        src: '/assets/icons/waypoints/spiral.svg',
        anchor: [0.5, 0.5]
      }),
      geometry: function (feature) {
        const geom = feature.getGeometry();
        if (geom && geom instanceof LineString) {
          const coordinates = geom.getCoordinates();
          return new MultiPoint(coordinates);
        }
        return geom;
      }
    })
  ];
}

/**
 * Create highlight style for traders (spec lines 447-459)
 * Brightens the trader color by 1.5x (clamped between 64-255)
 */
export function createTraderHighlightStyle(feature: Feature): Style {
  const wares = feature.get('wares') || 'unknown';
  const baseColor = getTraderColor(wares);
  const highlightColor = brightenColor(baseColor);
  
  return new Style({
    image: new Icon({
      color: highlightColor,
      src: '/assets/icons/waypoints/trader.svg',
      anchor: [0.5, 0.5]
    })
  });
}


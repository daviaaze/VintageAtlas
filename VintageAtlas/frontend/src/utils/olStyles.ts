/**
 * Clean OpenLayers Styles
 * Based on OPENLAYERS_SPECIFICATION.md
 */
import { Style, Fill, Stroke, Text, Icon } from 'ol/style';
import { MultiPoint, LineString } from 'ol/geom';
import type { Feature } from 'ol';

// Trader colors by wares (Spec lines 175-187)
const TRADER_COLORS: Record<string, [number, number, number]> = {
  'Artisan trader': [0, 240, 240],
  'Building materials trader': [255, 0, 0],
  'Clothing trader': [0, 128, 0],
  'Commodities trader': [128, 128, 128],
  'Agriculture trader': [200, 192, 128],
  'Furniture trader': [255, 128, 0],
  'Luxuries trader': [0, 0, 255],
  'Survival goods trader': [255, 255, 0],
  'Treasure hunter trader': [160, 0, 160],
  'unknown': [48, 48, 48]
};

// Translocator colors (Spec lines 250-256)
const TRANSLOCATOR_COLORS: Record<string, [number, number, number]> = {
  'standard': [192, 0, 192],
  'named': [71, 45, 255],
  'spawn': [0, 192, 192],
  'teleporter': [229, 57, 53]
};

// Landmark colors (Spec lines 318-322)
const LANDMARK_COLORS: Record<string, [number, number, number]> = {
  'Base': [192, 192, 192],
  'Misc': [224, 224, 224],
  'Server': [255, 255, 255]
};

// Landmark icons
const LANDMARK_ICONS: Record<string, string> = {
  'Base': '/assets/icons/waypoints/home.svg',
  'Misc': '/assets/icons/waypoints/star1.svg',
  'Server': '/assets/icons/temporal_gear.png'
};

/**
 * Explored Chunks style (Spec lines 122-127)
 */
export function exploredChunksStyle(feature: any): Style {
  const color = feature.get('color') || 'rgba(100, 149, 237, 0.5)';
  
  return new Style({
    fill: new Fill({ color }),
    stroke: new Stroke({ color: '#000000', width: 1 })
  });
}

/**
 * Traders style (Spec lines 155-163)
 */
export function tradersStyle(feature: any): Style {
  const wares = feature.get('wares') || 'unknown';
  const color = TRADER_COLORS[wares] || TRADER_COLORS['unknown'];
  
  return new Style({
    image: new Icon({
      src: '/assets/icons/waypoints/trader.svg',
      color: color,
      anchor: [0.5, 0.5]
    })
  });
}

/**
 * Translocators style (Spec lines 218-239)
 * Dual style: line + endpoint icons
 */
export function translocatorsStyle(feature: any): Style[] {
  const tag = feature.get('tag');
  const label = feature.get('label');
  
  // Determine color based on type
  let color: [number, number, number];
  if (tag === 'SPAWN') {
    color = TRANSLOCATOR_COLORS['spawn'];
  } else if (tag === 'TP') {
    color = TRANSLOCATOR_COLORS['teleporter'];
  } else if (label && label.length > 0) {
    color = TRANSLOCATOR_COLORS['named'];
  } else {
    color = TRANSLOCATOR_COLORS['standard'];
  }
  
  return [
    // Line connecting endpoints
    new Style({
      stroke: new Stroke({
        color: `rgb(${color[0]}, ${color[1]}, ${color[2]})`,
        width: 2
      })
    }),
    // Icons at both endpoints
    new Style({
      image: new Icon({
        src: '/assets/icons/waypoints/spiral.svg',
        color: color,
        anchor: [0.5, 0.5]
      }),
      geometry: (feature) => {
        const geom = feature.getGeometry();
        if (geom instanceof LineString) {
          return new MultiPoint(geom.getCoordinates());
        }
        return geom;
      }
    })
  ];
}

/**
 * Landmarks style (Spec lines 281-304)
 */
export function landmarksStyle(feature: any, currentZoom: number): Style {
  const type = feature.get('type') || 'Misc';
  const label = feature.get('label') || feature.get('name') || '';
  const color = LANDMARK_COLORS[type] || LANDMARK_COLORS['Misc'];
  const iconSrc = LANDMARK_ICONS[type] || LANDMARK_ICONS['Misc'];
  
  // Hide Misc below zoom 9 (Spec line 272)
  if (type === 'Misc' && currentZoom < 9) {
    return new Style({
      image: new Icon({
        src: iconSrc,
        opacity: 0
      })
    });
  }
  
  return new Style({
    zIndex: type === 'Server' ? 1000 : undefined,
    image: new Icon({
      src: iconSrc,
      color: type === 'Server' ? undefined : color, // Server uses PNG
      anchor: [0.5, 0.5]
    }),
    text: new Text({
      font: 'bold 10px "arial narrow", "sans serif"',
      text: label,
      textAlign: 'left',
      textBaseline: 'bottom',
      offsetX: 10,
      fill: new Fill({ color: '#000' }),
      stroke: new Stroke({ color: '#fff', width: 3 })
    })
  });
}

/**
 * Highlight style for translocators (Spec lines 424-445)
 */
export function highlightTranslocatorStyle(): Style[] {
  return [
    new Style({
      stroke: new Stroke({
        color: '#ddaaff',
        width: 3
      })
    }),
    new Style({
      image: new Icon({
        src: '/assets/icons/waypoints/spiral.svg',
        color: [255, 192, 255],
        anchor: [0.5, 0.5]
      }),
      geometry: (feature) => {
        const geom = feature.getGeometry();
        if (geom instanceof LineString) {
          return new MultiPoint(geom.getCoordinates());
        }
        return geom;
      }
    })
  ];
}

/**
 * Highlight style for traders (Spec lines 447-459)
 */
export function highlightTraderStyle(feature: any): Style {
  const wares = feature.get('wares') || 'unknown';
  const baseColor = TRADER_COLORS[wares] || TRADER_COLORS['unknown'];
  
  // Brighten by 1.5x, clamp 64-255
  const color: [number, number, number] = [
    Math.min(Math.max(baseColor[0] * 1.5, 64), 255),
    Math.min(Math.max(baseColor[1] * 1.5, 64), 255),
    Math.min(Math.max(baseColor[2] * 1.5, 64), 255)
  ];
  
  return new Style({
    image: new Icon({
      src: '/assets/icons/waypoints/trader.svg',
      color: color,
      anchor: [0.5, 0.5]
    })
  });
}

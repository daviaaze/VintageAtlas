/**
 * Clean OpenLayers Styles
 * Based on OPENLAYERS_SPECIFICATION.md
 */
import { Style, Fill, Stroke, Text, Icon } from 'ol/style';
import { MultiPoint, LineString } from 'ol/geom';

// Trader colors by wares (Spec lines 175-187)
export const TRADER_COLORS: Record<string, [number, number, number]> = {
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
 * Factory function to create traders style that reads from store
 * This avoids recreating the style function on every change
 */
export function createDynamicTradersStyleFn(getVisibility: (category: string) => boolean) {
  return (feature: any): Style => {
    const wares = feature.get('wares') || 'unknown';
    const color = TRADER_COLORS[wares] || TRADER_COLORS['unknown'];
    const opacity = getVisibility(wares) ? 1 : 0;

    return new Style({
      image: new Icon({
        src: '/assets/icons/waypoints/trader.svg',
        color: color,
        opacity: opacity,
        anchor: [0.5, 0.5]
      })
    });
  };
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

/**
 * Player style - shows players with a person icon and visual yaw compass indicator
 */
export function playerStyle(feature: any): Style[] {
  const name = feature.get('name') || 'Player';
  const yawRadians = feature.get('yaw') || 0;

  // Vintage Story: 0=South, π/2=East, π=North, 3π/2=West (clockwise)
  // Math rotation: 0=East, π/2=North, π=West, 3π/2=South (counter-clockwise)
  // Negate and rotate -π/2 to align: South->down, North->up, East->right, West->left
  const correctedRotation = -(yawRadians - Math.PI);

  return [
    // Player circle with integrated black triangle direction indicator
    new Style({
      image: new Icon({
        src: 'data:image/svg+xml;base64,' + btoa(`
          <svg width="24" height="24" viewBox="0 0 24 24" xmlns="http://www.w3.org/2000/svg">
            <circle cx="12" cy="12" r="10" fill="rgba(0,0,0,0.7)" stroke="#fff" stroke-width="2"/>
            <polygon points="12,2 18,14 12,10 6,14" fill="#fff"/>
          </svg>
        `),
        anchor: [0.5, 0.5],
        scale: 0.8,
        rotation: correctedRotation
      })
    }),

    // Player name
    new Style({
      text: new Text({
        font: 'bold 11px "arial narrow", "sans serif"',
        text: name,
        textAlign: 'center',
        textBaseline: 'top',
        offsetY: 8,
        fill: new Fill({ color: '#000' }),
        stroke: new Stroke({ color: '#fff', width: 3 })
      })
    })
  ];
}

/**
 * Spawn point style - shows the spawn location with a distinct icon
 */
export function spawnStyle(): Style {
  return new Style({
    image: new Icon({
      src: '/assets/icons/waypoints/home.svg',
      color: [255, 200, 0],
      anchor: [0.5, 0.5],
      scale: 1.5
    }),
    text: new Text({
      font: 'bold 12px "arial narrow", "sans serif"',
      text: 'Spawn',
      textAlign: 'center',
      textBaseline: 'top',
      offsetY: 15,
      fill: new Fill({ color: '#000' }),
      stroke: new Stroke({ color: '#fff', width: 3 })
    })
  });
}

/**
 * Waypoint style - shows waypoints with their specific color and icon
 */
export function waypointStyle(feature: any): Style {
  const title = feature.get('title') || 'Waypoint';
  const colorHex = feature.get('color') || '#ffffff';
  const icon = feature.get('icon') || 'circle';

  return new Style({
    image: new Icon({
      src: `/assets/icons/waypoints/${icon}.svg`,
      color: colorHex,
      anchor: [0.5, 0.5],
      scale: 1.0
    }),
    text: new Text({
      font: 'bold 11px "arial narrow", "sans serif"',
      text: title,
      textAlign: 'center',
      textBaseline: 'top',
      offsetY: 10,
      fill: new Fill({ color: '#000' }),
      stroke: new Stroke({ color: '#fff', width: 3 })
    })
  });
}

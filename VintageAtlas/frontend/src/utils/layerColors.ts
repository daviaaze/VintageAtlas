/**
 * Layer color configuration matching OpenLayers specification
 * Colors are RGB arrays that can be used with OpenLayers Icon color property
 */

/**
 * Trader colors by wares type (RGB)
 * From spec lines 175-187
 */
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

/**
 * Translocator colors by type (RGB)
 * From spec lines 250-256
 */
export const TRANSLOCATOR_COLORS: Record<string, [number, number, number]> = {
  'Translocator': [192, 0, 192],           // Standard unnamed
  'Named Translocator': [71, 45, 255],     // Has label
  'Spawn Translocator': [0, 192, 192],     // tag='SPAWN'
  'Teleporter': [229, 57, 53]              // tag='TP'
};

/**
 * Landmark colors by type (RGB)
 * From spec lines 318-322
 */
export const LANDMARK_COLORS: Record<string, [number, number, number]> = {
  'Base': [192, 192, 192],
  'Misc': [224, 224, 224],
  'Server': [255, 255, 255]  // Server uses PNG, but provide fallback
};

/**
 * Landmark icons by type
 * From spec lines 318-322
 */
export const LANDMARK_ICONS: Record<string, string> = {
  'Base': '/assets/icons/waypoints/home.svg',
  'Misc': '/assets/icons/waypoints/star1.svg',
  'Server': '/assets/icons/temporal_gear.png'
};

/**
 * Highlight colors for hover states
 * From spec lines 422-461
 */
export const HIGHLIGHT_COLORS = {
  translocator: {
    stroke: '#ddaaff',
    icon: [255, 192, 255] as [number, number, number]
  },
  trader: {
    // Brightens by 1.5x, clamped between 64-255
    multiplier: 1.5,
    min: 64,
    max: 255
  }
};

/**
 * Convert RGB array to CSS color string with optional opacity
 */
export function rgbToString(rgb: [number, number, number], opacity: number = 1): string {
  if (opacity === 1) {
    return `rgb(${rgb[0]}, ${rgb[1]}, ${rgb[2]})`;
  }
  return `rgba(${rgb[0]}, ${rgb[1]}, ${rgb[2]}, ${opacity})`;
}

/**
 * Brighten color for hover effect (spec line 452-454)
 */
export function brightenColor(rgb: [number, number, number], multiplier: number = 1.5): [number, number, number] {
  return [
    Math.min(Math.max(rgb[0] * multiplier, 64), 255),
    Math.min(Math.max(rgb[1] * multiplier, 64), 255),
    Math.min(Math.max(rgb[2] * multiplier, 64), 255)
  ];
}

/**
 * Get trader color by wares type
 */
export function getTraderColor(wares: string): [number, number, number] {
  return TRADER_COLORS[wares] || TRADER_COLORS['unknown'];
}

/**
 * Get translocator color by type (determined by tag and label)
 */
export function getTranslocatorColor(tag?: string, label?: string): [number, number, number] {
  if (tag === 'SPAWN') {
    return TRANSLOCATOR_COLORS['Spawn Translocator'];
  } else if (tag === 'TP') {
    return TRANSLOCATOR_COLORS['Teleporter'];
  } else if (label && label.length > 0) {
    return TRANSLOCATOR_COLORS['Named Translocator'];
  }
  return TRANSLOCATOR_COLORS['Translocator'];
}

/**
 * Get landmark color by type
 */
export function getLandmarkColor(type: string): [number, number, number] {
  return LANDMARK_COLORS[type] || LANDMARK_COLORS['Misc'];
}

/**
 * Get landmark icon by type
 */
export function getLandmarkIcon(type: string): string {
  return LANDMARK_ICONS[type] || LANDMARK_ICONS['Misc'];
}

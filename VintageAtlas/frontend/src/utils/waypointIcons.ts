/**
 * Waypoint icons utility
 * Provides access to the waypoint icons from the old HTML implementation
 */

// Available waypoint icons
export const waypointIcons = [
  { id: 'bee', name: 'Bee', path: '/assets/icons/waypoints/bee.svg' },
  { id: 'cave', name: 'Cave', path: '/assets/icons/waypoints/cave.svg' },
  { id: 'home', name: 'Home', path: '/assets/icons/waypoints/home.svg' },
  { id: 'ladder', name: 'Ladder', path: '/assets/icons/waypoints/ladder.svg' },
  { id: 'pick', name: 'Pick', path: '/assets/icons/waypoints/pick.svg' },
  { id: 'rocks', name: 'Rocks', path: '/assets/icons/waypoints/rocks.svg' },
  { id: 'ruins', name: 'Ruins', path: '/assets/icons/waypoints/ruins.svg' },
  { id: 'spiral', name: 'Spiral', path: '/assets/icons/waypoints/spiral.svg' },
  { id: 'star1', name: 'Star 1', path: '/assets/icons/waypoints/star1.svg' },
  { id: 'star2', name: 'Star 2', path: '/assets/icons/waypoints/star2.svg' },
  { id: 'trader', name: 'Trader', path: '/assets/icons/waypoints/trader.svg' },
  { id: 'vessel', name: 'Vessel', path: '/assets/icons/waypoints/vessel.svg' },
];

// Default icon
export const defaultIcon = waypointIcons[0];

/**
 * Get a waypoint icon by ID
 * @param id Icon ID
 * @returns Icon object or default icon if not found
 */
export function getWaypointIcon(id: string) {
  return waypointIcons.find(icon => icon.id === id) || defaultIcon;
}

/**
 * Get a waypoint icon by name
 * @param name Icon name
 * @returns Icon object or default icon if not found
 */
export function getWaypointIconByName(name: string) {
  return waypointIcons.find(icon => icon.name.toLowerCase() === name.toLowerCase()) || defaultIcon;
}

/**
 * Get a waypoint icon by type
 * This maps entity types to appropriate icons
 * @param type Entity type
 * @returns Icon object
 */
export function getWaypointIconByType(type: string) {
  const typeMap: Record<string, string> = {
    trader: 'trader',
    sign: 'star1',
    signpost: 'star2',
    translocator: 'spiral',
    player: 'home',
    animal: 'bee',
    spawn: 'star1',
    chunk: 'rocks',
    default: 'star1'
  };
  
  const iconId = typeMap[type.toLowerCase()] || typeMap.default;
  return getWaypointIcon(iconId);
}

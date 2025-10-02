/**
 * Example of how to integrate IndexedDB tile caching with OpenLayers
 * This shows the modifications needed to MapContainer.vue
 */

import TileLayer from 'ol/layer/Tile';
import XYZ from 'ol/source/XYZ';
import { createWorldTileGrid } from './mapConfig';
import { tileCache } from './tileCache';

/**
 * Create a terrain layer with IndexedDB caching
 */
export function createCachedTerrainLayer(visible: boolean = true): TileLayer<XYZ> {
  return new TileLayer({
    source: new XYZ({
      tileGrid: createWorldTileGrid(),
      wrapX: false,
      
      // Custom tile URL function with caching
      tileUrlFunction: async (tileCoord) => {
        if (!tileCoord) return '';
        
        const z = tileCoord[0] + 1; // Adjust zoom
        const x = tileCoord[1];
        const y = tileCoord[2];
        const adjustedY = -y - 1;

        try {
          // Try to get from IndexedDB cache
          const cached = await tileCache.get(z, x, adjustedY);
          
          if (cached) {
            // Return object URL for cached blob
            return URL.createObjectURL(cached);
          }

          // Not in cache, construct server URL
          return `/tiles/${z}/${x}_${adjustedY}.png`;
        } catch (error) {
          console.error('Error accessing tile cache:', error);
          // Fallback to direct server URL
          return `/tiles/${z}/${x}_${adjustedY}.png`;
        }
      },
      
      // Cache tiles when loaded
      tileLoadFunction: async (imageTile: any, src: string) => {
        // If it's already a blob URL (from cache), use it directly
        if (src.startsWith('blob:')) {
          imageTile.getImage().src = src;
          return;
        }

        try {
          // Fetch from server
          const response = await fetch(src);
          
          if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
          }

          const blob = await response.blob();
          const etag = response.headers.get('etag') || undefined;

          // Extract coordinates from URL: /tiles/9/123_-456.png
          const match = src.match(/\/tiles\/(\d+)\/(-?\d+)_(-?\d+)\.png/);
          if (match) {
            const [, z, x, y] = match.map(Number);
            // Store in IndexedDB
            await tileCache.put(z, x, y, blob, etag);
          }

          // Create object URL and set on image
          const objectUrl = URL.createObjectURL(blob);
          imageTile.getImage().src = objectUrl;
          
          // Clean up object URL after image loads
          imageTile.getImage().onload = () => {
            URL.revokeObjectURL(objectUrl);
          };
        } catch (error) {
          console.error('Failed to load tile:', src, error);
          // OpenLayers will handle the error state
        }
      },
    }),
    visible,
  });
}

/**
 * Monitor cache performance
 */
export async function logCacheStats(): Promise<void> {
  const stats = await tileCache.getStats();
  console.log('[TileCache] Statistics:', {
    tiles: stats.count,
    size: `${(stats.sizeBytes / 1024 / 1024).toFixed(2)} MB`,
  });
}

/**
 * Clear cache when map data is regenerated
 */
export async function clearTileCache(): Promise<void> {
  await tileCache.clear();
  console.log('[TileCache] Cache cleared');
}


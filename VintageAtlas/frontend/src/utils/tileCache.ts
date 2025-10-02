/**
 * Client-side tile caching using IndexedDB
 * Dramatically reduces server requests for frequently viewed tiles
 */

interface CachedTile {
  zoom: number;
  x: number;
  y: number;
  data: Blob;
  timestamp: number;
  etag?: string;
}

class TileCache {
  private dbName = 'vintage-atlas-tiles';
  private storeName = 'tiles';
  private version = 1;
  private db: IDBDatabase | null = null;
  
  // Cache tiles for 7 days
  private maxAge = 7 * 24 * 60 * 60 * 1000;
  
  // Maximum cache size (50MB)
  private maxCacheSize = 50 * 1024 * 1024;

  /**
   * Initialize the IndexedDB database
   */
  async init(): Promise<void> {
    return new Promise((resolve, reject) => {
      const request = indexedDB.open(this.dbName, this.version);

      request.onerror = () => reject(request.error);
      request.onsuccess = () => {
        this.db = request.result;
        resolve();
      };

      request.onupgradeneeded = (event) => {
        const db = (event.target as IDBOpenDBRequest).result;
        
        // Create object store if it doesn't exist
        if (!db.objectStoreNames.contains(this.storeName)) {
          const store = db.createObjectStore(this.storeName, { 
            keyPath: ['zoom', 'x', 'y'] 
          });
          
          // Index for timestamp-based cleanup
          store.createIndex('timestamp', 'timestamp', { unique: false });
        }
      };
    });
  }

  /**
   * Get a tile from cache
   */
  async get(zoom: number, x: number, y: number): Promise<Blob | null> {
    if (!this.db) await this.init();

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([this.storeName], 'readonly');
      const store = transaction.objectStore(this.storeName);
      const request = store.get([zoom, x, y]);

      request.onsuccess = () => {
        const tile = request.result as CachedTile | undefined;
        
        if (!tile) {
          resolve(null);
          return;
        }

        // Check if tile is expired
        const age = Date.now() - tile.timestamp;
        if (age > this.maxAge) {
          // Remove expired tile
          this.remove(zoom, x, y);
          resolve(null);
          return;
        }

        resolve(tile.data);
      };

      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Store a tile in cache
   */
  async put(
    zoom: number, 
    x: number, 
    y: number, 
    data: Blob, 
    etag?: string
  ): Promise<void> {
    if (!this.db) await this.init();

    // Check cache size and cleanup if needed
    await this.cleanupIfNeeded();

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([this.storeName], 'readwrite');
      const store = transaction.objectStore(this.storeName);
      
      const tile: CachedTile = {
        zoom,
        x,
        y,
        data,
        timestamp: Date.now(),
        etag
      };

      const request = store.put(tile);

      request.onsuccess = () => resolve();
      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Remove a specific tile from cache
   */
  async remove(zoom: number, x: number, y: number): Promise<void> {
    if (!this.db) await this.init();

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([this.storeName], 'readwrite');
      const store = transaction.objectStore(this.storeName);
      const request = store.delete([zoom, x, y]);

      request.onsuccess = () => resolve();
      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Get cache statistics
   */
  async getStats(): Promise<{ count: number; sizeBytes: number }> {
    if (!this.db) await this.init();

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([this.storeName], 'readonly');
      const store = transaction.objectStore(this.storeName);
      const countRequest = store.count();
      const getAllRequest = store.getAll();

      let count = 0;
      let sizeBytes = 0;

      countRequest.onsuccess = () => {
        count = countRequest.result;
      };

      getAllRequest.onsuccess = () => {
        const tiles = getAllRequest.result as CachedTile[];
        sizeBytes = tiles.reduce((sum, tile) => sum + tile.data.size, 0);
        resolve({ count, sizeBytes });
      };

      countRequest.onerror = getAllRequest.onerror = () => {
        reject(countRequest.error || getAllRequest.error);
      };
    });
  }

  /**
   * Clean up old tiles if cache is too large
   */
  private async cleanupIfNeeded(): Promise<void> {
    const stats = await this.getStats();
    
    if (stats.sizeBytes < this.maxCacheSize) {
      return;
    }

    console.log(`[TileCache] Cache size ${stats.sizeBytes} exceeds limit, cleaning up...`);

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([this.storeName], 'readwrite');
      const store = transaction.objectStore(this.storeName);
      const index = store.index('timestamp');
      
      // Get all tiles sorted by timestamp (oldest first)
      const request = index.openCursor();
      let deletedSize = 0;
      const targetDelete = stats.sizeBytes - (this.maxCacheSize * 0.8); // Delete 20% extra

      request.onsuccess = (event) => {
        const cursor = (event.target as IDBRequest).result;
        
        if (cursor && deletedSize < targetDelete) {
          const tile = cursor.value as CachedTile;
          deletedSize += tile.data.size;
          cursor.delete();
          cursor.continue();
        } else {
          console.log(`[TileCache] Deleted ${deletedSize} bytes`);
          resolve();
        }
      };

      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Clear entire cache
   */
  async clear(): Promise<void> {
    if (!this.db) await this.init();

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([this.storeName], 'readwrite');
      const store = transaction.objectStore(this.storeName);
      const request = store.clear();

      request.onsuccess = () => {
        console.log('[TileCache] Cache cleared');
        resolve();
      };
      request.onerror = () => reject(request.error);
    });
  }
}

// Singleton instance
export const tileCache = new TileCache();

/**
 * Custom XYZ loader that uses IndexedDB cache
 */
export async function cachedTileLoader(
  tileCoord: [number, number, number],
  baseUrl: string
): Promise<string> {
  const [z, x, y] = tileCoord;
  const zoom = z + 1; // Adjust for your coordinate system
  const adjustedY = -y - 1;

  try {
    // Try to get from cache
    const cached = await tileCache.get(zoom, x, adjustedY);
    
    if (cached) {
      // Return object URL for cached blob
      return URL.createObjectURL(cached);
    }

    // Not in cache, fetch from server
    const url = `${baseUrl}/${zoom}/${x}_${adjustedY}.png`;
    const response = await fetch(url);
    
    if (!response.ok) {
      throw new Error(`Failed to load tile: ${response.status}`);
    }

    const blob = await response.blob();
    const etag = response.headers.get('etag') || undefined;

    // Store in cache for future use
    await tileCache.put(zoom, x, adjustedY, blob, etag);

    // Return object URL
    return URL.createObjectURL(blob);
  } catch (error) {
    console.error(`[TileCache] Failed to load tile ${zoom}/${x}/${adjustedY}:`, error);
    throw error;
  }
}

/**
 * Preload tiles for a specific area
 */
export async function preloadArea(
  zoom: number,
  minX: number,
  maxX: number,
  minY: number,
  maxY: number
): Promise<void> {
  const promises: Promise<void>[] = [];
  
  for (let x = minX; x <= maxX; x++) {
    for (let y = minY; y <= maxY; y++) {
      promises.push(
        (async () => {
          try {
            const url = `/tiles/${zoom}/${x}_${y}.png`;
            const response = await fetch(url);
            const blob = await response.blob();
            await tileCache.put(zoom, x, y, blob);
          } catch (error) {
            console.warn(`Failed to preload tile ${zoom}/${x}/${y}:`, error);
          }
        })()
      );
    }
  }

  await Promise.all(promises);
  console.log(`[TileCache] Preloaded ${promises.length} tiles for zoom ${zoom}`);
}


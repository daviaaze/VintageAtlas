# VintageAtlas Improvement Requirements

> **Document Version:** 1.0  
> **Date:** October 6, 2025  
> **Status:** Active Planning

## Executive Summary

This document outlines the requirements for critical improvements to the VintageAtlas mod, focusing on automatic tile regeneration control, tile generation efficiency, real-time entity updates via WebSockets, entity caching optimization, movement tracking enhancements, and historical tracker fixes.

---

## 1. Disable Automatic Regeneration

### Current State

**Status:** ✅ **ALREADY DISABLED**

The automatic tile regeneration is already disabled in the codebase:

```csharp
// VintageAtlasModSystem.cs:186-198
// ═══════════════════════════════════════════════════════════════
// BACKGROUND TILE SERVICE DISABLED FOR TESTING
// This prevents automatic tile generation on chunk updates
// Tiles are ONLY generated during /atlas export
// ═══════════════════════════════════════════════════════════════
```

### Implementation Details

- **Location:** `VintageAtlas/VintageAtlasModSystem.cs`
- **Lines:** 136-137 (ChunkChangeTracker), 191-198 (BackgroundTileService)
- **Components Disabled:**
  - `ChunkChangeTracker` - Not initialized
  - `BackgroundTileService` - Not initialized

### Configuration Option Required

**Requirement:** Add a configuration option to allow users to enable/disable automatic tile regeneration.

#### Implementation

1. **Add to `ModConfig.cs`:**
```csharp
/// <summary>
/// Enable automatic tile regeneration when chunks are modified
/// Default: false
/// Warning: Can impact server performance
/// </summary>
public bool EnableAutomaticTileRegeneration { get; set; } = false;

/// <summary>
/// Interval in milliseconds for checking chunk changes
/// Default: 30000 (30 seconds)
/// </summary>
public int TileRegenerationCheckIntervalMs { get; set; } = 30000;

/// <summary>
/// Maximum number of tiles to regenerate per batch
/// Default: 100
/// </summary>
public int MaxTilesPerBatch { get; set; } = 100;
```

2. **Modify `VintageAtlasModSystem.cs`:**
```csharp
// Line ~136: Conditionally initialize chunk tracker
if (_config.EnableAutomaticTileRegeneration)
{
    _chunkChangeTracker = new ChunkChangeTracker(_sapi);
}

// Line ~191: Conditionally initialize background service
if (_config.EnableAutomaticTileRegeneration && _chunkChangeTracker != null)
{
    _backgroundTileService = new BackgroundTileService(
        _sapi, 
        _config, 
        _tileState, 
        _tileGenerator, 
        _chunkChangeTracker
    );
    
    _sapi.Server.AddServerThread("tile_service", _backgroundTileService);
    _backgroundTileService.Start();
    
    _sapi.Logger.Notification("[VintageAtlas] Background tile generation enabled");
}
else
{
    _sapi.Logger.Notification("[VintageAtlas] Background tile generation disabled - tiles only generated via /atlas export");
}
```

### Testing Requirements

- ✅ Verify tiles are NOT auto-regenerated with default config
- ✅ Verify tiles ARE auto-regenerated when enabled
- ✅ Test performance impact with various batch sizes
- ✅ Verify configuration persists across server restarts

---

## 2. Improve Tile Generation

### Current State

**Issues Identified:**

1. **On-demand tile generation is disabled** (UnifiedTileGenerator.cs:368-387)
2. **Memory cache may be too aggressive** (5-minute TTL, 100 tile limit)
3. **No progressive tile loading** for map viewer
4. **Tile generation is single-threaded** during on-demand requests
5. **No tile priority system** for visible vs. background tiles

### Requirements

#### 2.1 Enable Smart On-Demand Tile Generation

**Priority:** HIGH

**Goal:** Generate missing tiles on-demand while maintaining performance.

**Implementation:**

1. **Uncomment and enhance on-demand generation:**

```csharp
// UnifiedTileGenerator.cs:368-387
// Re-enable on-demand generation with rate limiting

private readonly SemaphoreSlim _onDemandSemaphore = new(5); // Max 5 concurrent generations
private readonly ConcurrentDictionary<string, Task<byte[]?>> _pendingGenerations = new();

private async Task<byte[]?> GenerateTileOnDemandAsync(int zoom, int tileX, int tileZ)
{
    var tileKey = $"{zoom}_{tileX}_{tileZ}";
    
    // Check if already being generated
    if (_pendingGenerations.TryGetValue(tileKey, out var existing))
    {
        return await existing;
    }
    
    // Rate limit concurrent generations
    await _onDemandSemaphore.WaitAsync();
    
    try
    {
        var task = Task.Run(async () =>
        {
            try
            {
                byte[]? newTileData = null;

                if (zoom == _config.BaseZoomLevel)
                {
                    // Base zoom: generate from loaded chunks
                    var loadedChunksSource = new LoadedChunksDataSource(_sapi, _config);
                    newTileData = await RenderTileAsync(zoom, tileX, tileZ, loadedChunksSource);
                }
                else if (zoom < _config.BaseZoomLevel)
                {
                    // Lower zoom: downsample from higher zoom
                    newTileData = await _downsampler.GenerateTileByDownsamplingAsync(zoom, tileX, tileZ);
                }

                // Cache the generated tile
                if (newTileData != null)
                {
                    await _storage.SaveTileAsync(zoom, tileX, tileZ, newTileData);
                    var lastModified = DateTime.UtcNow;
                    var etag = GenerateETag(newTileData, lastModified);
                    CacheInMemory(tileKey, newTileData, etag, lastModified);
                }
                
                return newTileData;
            }
            finally
            {
                _pendingGenerations.TryRemove(tileKey, out _);
            }
        });
        
        _pendingGenerations.TryAdd(tileKey, task);
        return await task;
    }
    finally
    {
        _onDemandSemaphore.Release();
    }
}
```

2. **Add configuration options:**

```csharp
// ModConfig.cs
/// <summary>
/// Enable on-demand tile generation for missing tiles
/// Default: true
/// </summary>
public bool EnableOnDemandTileGeneration { get; set; } = true;

/// <summary>
/// Maximum concurrent on-demand tile generations
/// Default: 5
/// </summary>
public int MaxConcurrentOnDemandTiles { get; set; } = 5;
```

#### 2.2 Optimize Memory Cache

**Priority:** MEDIUM

**Current Issues:**
- Fixed 100 tile limit may be too low
- 5-minute TTL may be too aggressive
- No LRU eviction policy

**Requirements:**

1. **Implement configurable cache settings:**

```csharp
// ModConfig.cs
/// <summary>
/// Maximum number of tiles to keep in memory cache
/// Default: 500
/// </summary>
public int TileCacheSize { get; set; } = 500;

/// <summary>
/// Time-to-live for cached tiles in seconds
/// Default: 600 (10 minutes)
/// </summary>
public int TileCacheTtlSeconds { get; set; } = 600;
```

2. **Implement LRU cache with statistics:**

```csharp
// Add cache statistics endpoint
public class TileCacheStats
{
    public int CachedTiles { get; set; }
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }
    public double HitRate => CacheHits + CacheMisses > 0 
        ? (double)CacheHits / (CacheHits + CacheMisses) 
        : 0;
    public long MemoryUsageBytes { get; set; }
}
```

#### 2.3 Implement Tile Priority System

**Priority:** HIGH

**Goal:** Prioritize visible tiles over background/zoom tiles.

**Requirements:**

1. **Add tile request prioritization:**

```csharp
public enum TilePriority
{
    Background = 0,  // Pre-generated tiles
    Viewport = 10,   // Currently visible
    Adjacent = 5     // Adjacent to viewport
}

// Queue tiles with priority
public Task<TileResult> GetTileAsync(int zoom, int tileX, int tileZ, TilePriority priority = TilePriority.Viewport)
{
    // Implementation...
}
```

2. **Priority queue for on-demand generation:**

```csharp
private readonly PriorityQueue<TileRequest, int> _tileQueue = new();

private class TileRequest
{
    public int Zoom { get; set; }
    public int TileX { get; set; }
    public int TileZ { get; set; }
    public TilePriority Priority { get; set; }
    public TaskCompletionSource<byte[]?> CompletionSource { get; set; }
}
```

#### 2.4 Add Progressive Tile Loading

**Priority:** MEDIUM

**Goal:** Load low-resolution tiles first, then progressively load higher resolution.

**Frontend Requirements:**

1. **Implement progressive layer loading:**

```typescript
// layerFactory.ts
export function createProgressiveTerrainLayer(): LayerGroup {
    const layers = [];
    
    // Start with low-res tiles (zoom 0-3)
    const lowResLayer = createTerrainLayer(0, 3);
    layers.push(lowResLayer);
    
    // Then mid-res (zoom 4-6)
    setTimeout(() => {
        const midResLayer = createTerrainLayer(4, 6);
        layers.push(midResLayer);
    }, 500);
    
    // Finally high-res (zoom 7-9)
    setTimeout(() => {
        const highResLayer = createTerrainLayer(7, 9);
        layers.push(highResLayer);
    }, 1000);
    
    return new LayerGroup({ layers });
}
```

### Testing Requirements

- ✅ Test on-demand generation with 10+ concurrent requests
- ✅ Measure cache hit rate over 30-minute session
- ✅ Test priority system with viewport changes
- ✅ Verify progressive loading reduces initial load time
- ✅ Test memory usage with 500+ cached tiles

---

## 3. Fix Entity Display and Real-Time Data via WebSockets

### Current State

**Issues:**

1. **Using HTTP polling instead of WebSockets** (15-second intervals)
2. **No real-time updates** for player/entity positions
3. **Inefficient bandwidth usage** (full data refresh every poll)
4. **High latency** for position updates (up to 15 seconds)
5. **No connection state management** for long-lived connections

**Current Implementation:**

```typescript
// stores/live.ts:97-106
function startPolling(intervalMs = 15000) {
    if (pollingInterval) clearInterval(pollingInterval);
    fetchLiveData(); // Fetch immediately
    pollingInterval = window.setInterval(() => {
        if (connectionStatus.value !== 'reconnecting') {
            fetchLiveData();
        }
    }, intervalMs);
}
```

### Requirements

#### 3.1 Implement WebSocket Backend

**Priority:** HIGH

**Implementation:**

1. **Add WebSocket server to `WebServer.cs`:**

```csharp
using System.Net.WebSockets;
using System.Collections.Concurrent;

public class WebSocketManager
{
    private readonly ConcurrentDictionary<string, WebSocket> _connections = new();
    private readonly ICoreServerAPI _sapi;
    private readonly IDataCollector _dataCollector;
    
    public async Task HandleWebSocketAsync(HttpListenerContext context)
    {
        if (!context.Request.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }
        
        var wsContext = await context.AcceptWebSocketAsync(null);
        var ws = wsContext.WebSocket;
        var connectionId = Guid.NewGuid().ToString();
        
        _connections.TryAdd(connectionId, ws);
        
        try
        {
            await HandleConnectionAsync(connectionId, ws);
        }
        finally
        {
            _connections.TryRemove(connectionId, out _);
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
        }
    }
    
    private async Task HandleConnectionAsync(string connectionId, WebSocket ws)
    {
        var buffer = new byte[1024];
        
        while (ws.State == WebSocketState.Open)
        {
            // Receive messages (subscriptions, pings, etc.)
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            
            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }
            
            // Handle subscription messages
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            await HandleMessageAsync(connectionId, ws, message);
        }
    }
    
    private async Task HandleMessageAsync(string connectionId, WebSocket ws, string message)
    {
        // Parse subscription requests
        // {"type": "subscribe", "channel": "players"}
        // {"type": "subscribe", "channel": "entities"}
        // {"type": "ping"}
    }
    
    public async Task BroadcastLiveDataAsync(ServerStatusData data)
    {
        var json = JsonConvert.SerializeObject(new
        {
            type = "live_update",
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            data
        });
        
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);
        
        var disconnected = new List<string>();
        
        foreach (var (connectionId, ws) in _connections)
        {
            try
            {
                if (ws.State == WebSocketState.Open)
                {
                    await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else
                {
                    disconnected.Add(connectionId);
                }
            }
            catch
            {
                disconnected.Add(connectionId);
            }
        }
        
        // Clean up disconnected clients
        foreach (var id in disconnected)
        {
            _connections.TryRemove(id, out _);
        }
    }
}
```

2. **Integrate with main thread updates:**

```csharp
// VintageAtlasModSystem.cs:149-159
_sapi.Event.RegisterGameTickListener(dt => 
{
    // Update data cache (called on main thread - THREAD SAFE)
    _dataCollector.UpdateCache(dt);
    
    // Broadcast via WebSocket
    var data = _dataCollector.CollectData();
    _webSocketManager?.BroadcastLiveDataAsync(data).Wait();
    
    // Update historical tracker if enabled
    if (_config.EnableHistoricalTracking && _historicalTracker != null)
    {
        _historicalTracker.OnGameTick(dt);
    }
}, 1000); // Call every second (1000ms)
```

3. **Add WebSocket endpoint to router:**

```csharp
// RequestRouter.cs
if (context.Request.Url?.AbsolutePath == "/ws/live" && context.Request.IsWebSocketRequest)
{
    await _webSocketManager.HandleWebSocketAsync(context);
    return;
}
```

#### 3.2 Implement WebSocket Frontend

**Priority:** HIGH

**Implementation:**

1. **Create WebSocket service:**

```typescript
// src/services/websocket/liveWebSocket.ts
import type { LiveData } from '@/types/live-data';

export type WebSocketStatus = 'connecting' | 'connected' | 'disconnected' | 'error';

export interface WebSocketMessage {
  type: 'live_update' | 'error' | 'pong';
  timestamp?: number;
  data?: LiveData;
  error?: string;
}

export class LiveWebSocket {
  private ws: WebSocket | null = null;
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;
  private reconnectDelay = 1000;
  private pingInterval: number | undefined;
  private listeners: Map<string, Set<(data: any) => void>> = new Map();
  private statusListeners: Set<(status: WebSocketStatus) => void> = new Set();
  
  constructor(private url: string) {}
  
  connect(): void {
    if (this.ws?.readyState === WebSocket.OPEN) {
      return;
    }
    
    this.updateStatus('connecting');
    
    try {
      this.ws = new WebSocket(this.url);
      
      this.ws.onopen = () => {
        console.log('[WebSocket] Connected');
        this.updateStatus('connected');
        this.reconnectAttempts = 0;
        this.startPing();
        
        // Subscribe to channels
        this.send({ type: 'subscribe', channel: 'players' });
        this.send({ type: 'subscribe', channel: 'entities' });
      };
      
      this.ws.onmessage = (event) => {
        try {
          const message: WebSocketMessage = JSON.parse(event.data);
          this.handleMessage(message);
        } catch (error) {
          console.error('[WebSocket] Failed to parse message:', error);
        }
      };
      
      this.ws.onerror = (error) => {
        console.error('[WebSocket] Error:', error);
        this.updateStatus('error');
      };
      
      this.ws.onclose = () => {
        console.log('[WebSocket] Disconnected');
        this.updateStatus('disconnected');
        this.stopPing();
        this.attemptReconnect();
      };
    } catch (error) {
      console.error('[WebSocket] Connection failed:', error);
      this.updateStatus('error');
      this.attemptReconnect();
    }
  }
  
  disconnect(): void {
    this.stopPing();
    if (this.ws) {
      this.ws.close();
      this.ws = null;
    }
  }
  
  private attemptReconnect(): void {
    if (this.reconnectAttempts >= this.maxReconnectAttempts) {
      console.error('[WebSocket] Max reconnect attempts reached');
      return;
    }
    
    this.reconnectAttempts++;
    const delay = this.reconnectDelay * Math.pow(2, this.reconnectAttempts - 1);
    
    console.log(`[WebSocket] Reconnecting in ${delay}ms (attempt ${this.reconnectAttempts}/${this.maxReconnectAttempts})`);
    
    setTimeout(() => {
      this.connect();
    }, delay);
  }
  
  private startPing(): void {
    this.pingInterval = window.setInterval(() => {
      if (this.ws?.readyState === WebSocket.OPEN) {
        this.send({ type: 'ping' });
      }
    }, 30000); // Ping every 30 seconds
  }
  
  private stopPing(): void {
    if (this.pingInterval) {
      clearInterval(this.pingInterval);
      this.pingInterval = undefined;
    }
  }
  
  private send(data: any): void {
    if (this.ws?.readyState === WebSocket.OPEN) {
      this.ws.send(JSON.stringify(data));
    }
  }
  
  private handleMessage(message: WebSocketMessage): void {
    switch (message.type) {
      case 'live_update':
        this.emit('live_update', message.data);
        break;
      case 'error':
        console.error('[WebSocket] Server error:', message.error);
        break;
      case 'pong':
        // Heartbeat response
        break;
    }
  }
  
  private emit(event: string, data: any): void {
    const listeners = this.listeners.get(event);
    if (listeners) {
      listeners.forEach(listener => listener(data));
    }
  }
  
  on(event: string, listener: (data: any) => void): void {
    if (!this.listeners.has(event)) {
      this.listeners.set(event, new Set());
    }
    this.listeners.get(event)!.add(listener);
  }
  
  off(event: string, listener: (data: any) => void): void {
    const listeners = this.listeners.get(event);
    if (listeners) {
      listeners.delete(listener);
    }
  }
  
  onStatusChange(listener: (status: WebSocketStatus) => void): void {
    this.statusListeners.add(listener);
  }
  
  offStatusChange(listener: (status: WebSocketStatus) => void): void {
    this.statusListeners.delete(listener);
  }
  
  private updateStatus(status: WebSocketStatus): void {
    this.statusListeners.forEach(listener => listener(status));
  }
}
```

2. **Update live store to use WebSocket:**

```typescript
// src/stores/live.ts
import { LiveWebSocket } from '@/services/websocket/liveWebSocket';

export const useLiveStore = defineStore('live', () => {
  // ... existing state ...
  
  const ws = ref<LiveWebSocket | null>(null);
  
  function connectWebSocket() {
    const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsUrl = `${protocol}//${window.location.host}/ws/live`;
    
    ws.value = new LiveWebSocket(wsUrl);
    
    ws.value.on('live_update', (newData: LiveData) => {
      data.value = newData;
      lastUpdated.value = new Date();
      connectionStatus.value = 'ok';
      connectionMessage.value = null;
    });
    
    ws.value.onStatusChange((status) => {
      switch (status) {
        case 'connected':
          connectionStatus.value = 'ok';
          connectionMessage.value = null;
          break;
        case 'connecting':
          connectionStatus.value = 'loading';
          connectionMessage.value = 'Connecting...';
          break;
        case 'disconnected':
          connectionStatus.value = 'warning';
          connectionMessage.value = 'Disconnected - Reconnecting...';
          break;
        case 'error':
          connectionStatus.value = 'error';
          connectionMessage.value = 'Connection error';
          break;
      }
    });
    
    ws.value.connect();
  }
  
  function disconnectWebSocket() {
    if (ws.value) {
      ws.value.disconnect();
      ws.value = null;
    }
  }
  
  // Replace startPolling with connectWebSocket
  return {
    // ... existing returns ...
    connectWebSocket,
    disconnectWebSocket
  };
});
```

#### 3.3 Add Differential Updates

**Priority:** MEDIUM

**Goal:** Only send changed entity data, not full state.

**Implementation:**

```csharp
// DataCollector.cs
private ServerStatusData? _lastBroadcastData;

public ServerStatusData GetDifferentialUpdate()
{
    lock (_cacheLock)
    {
        if (!_dataReady || _cachedData == null)
        {
            return _cachedData ?? new ServerStatusData();
        }
        
        // First time or full refresh
        if (_lastBroadcastData == null)
        {
            _lastBroadcastData = _cachedData;
            return _cachedData;
        }
        
        // Calculate differential
        var diff = new ServerStatusData
        {
            SpawnPoint = _cachedData.SpawnPoint,
            Date = _cachedData.Date,
            Weather = _cachedData.Weather,
            Players = GetChangedPlayers(_lastBroadcastData.Players, _cachedData.Players),
            Animals = GetChangedAnimals(_lastBroadcastData.Animals, _cachedData.Animals)
        };
        
        _lastBroadcastData = _cachedData;
        return diff;
    }
}

private List<PlayerData> GetChangedPlayers(List<PlayerData> old, List<PlayerData> current)
{
    // Return only players whose position or stats changed
    var changed = new List<PlayerData>();
    
    foreach (var player in current)
    {
        var oldPlayer = old.FirstOrDefault(p => p.Uid == player.Uid);
        if (oldPlayer == null || HasPlayerChanged(oldPlayer, player))
        {
            changed.Add(player);
        }
    }
    
    return changed;
}

private bool HasPlayerChanged(PlayerData old, PlayerData current)
{
    // Check if position changed significantly (> 0.5 blocks)
    var distance = Math.Sqrt(
        Math.Pow(old.Position.X - current.Position.X, 2) +
        Math.Pow(old.Position.Y - current.Position.Y, 2) +
        Math.Pow(old.Position.Z - current.Position.Z, 2)
    );
    
    return distance > 0.5 || 
           Math.Abs(old.Health - current.Health) > 0.1 ||
           Math.Abs(old.Hunger - current.Hunger) > 1;
}
```

### Configuration Options

```csharp
// ModConfig.cs
/// <summary>
/// Enable WebSocket support for real-time updates
/// Default: true
/// </summary>
public bool EnableWebSocket { get; set; } = true;

/// <summary>
/// Use differential updates (only send changed data)
/// Default: true
/// </summary>
public bool UseDifferentialUpdates { get; set; } = true;

/// <summary>
/// Minimum position change in blocks to trigger update
/// Default: 0.5
/// </summary>
public double MinimumPositionChangeBlocks { get; set; } = 0.5;
```

### Testing Requirements

- ✅ Test WebSocket connection establishment
- ✅ Test automatic reconnection after network failure
- ✅ Test differential updates reduce bandwidth by >50%
- ✅ Test with 10+ concurrent WebSocket connections
- ✅ Verify updates arrive within 1-2 seconds
- ✅ Test graceful fallback to HTTP polling if WebSocket fails

---

## 4. Improve Entity Loading and Caching

### Current State

**Issues:**

1. **Fixed 3-second cache** may be too aggressive or too slow
2. **No spatial indexing** for entity lookups
3. **Scans only around players** (64-block radius) - may miss entities
4. **No entity type filtering** in cache
5. **Animals cache limited to 200 entities**
6. **No cache statistics** or monitoring

**Current Implementation:**

```csharp
// DataCollector.cs:18-22
private List<AnimalData>? _animalsCache;
private DateTime _animalsCacheUntil = DateTime.MinValue;
private const int AnimalsCacheSeconds = 3;
private const int AnimalsMax = 200;
private const int AnimalTrackingRadius = 64;
```

### Requirements

#### 4.1 Configurable Entity Caching

**Priority:** HIGH

**Implementation:**

1. **Add configuration options:**

```csharp
// ModConfig.cs
/// <summary>
/// Entity cache duration in seconds
/// Default: 3
/// </summary>
public int EntityCacheSeconds { get; set; } = 3;

/// <summary>
/// Maximum number of entities to track
/// Default: 500
/// </summary>
public int MaxTrackedEntities { get; set; } = 500;

/// <summary>
/// Radius around players to scan for entities (in blocks)
/// Default: 64
/// </summary>
public int EntityTrackingRadius { get; set; } = 64;

/// <summary>
/// Track all loaded entities regardless of player proximity
/// Default: false (can impact performance)
/// </summary>
public bool TrackAllLoadedEntities { get; set; } = false;

/// <summary>
/// Entity types to track (comma-separated)
/// Default: "EntityDrifter,EntityWolf,EntityBoar,EntityBear"
/// Empty = track all
/// </summary>
public string TrackedEntityTypes { get; set; } = "";
```

#### 4.2 Implement Entity Spatial Index

**Priority:** MEDIUM

**Goal:** Fast entity lookups by location without scanning all entities.

**Implementation:**

```csharp
// Create new file: Tracking/EntitySpatialIndex.cs
public class EntitySpatialIndex
{
    private const int CellSize = 128; // 128 blocks per cell
    private readonly Dictionary<Vec2i, HashSet<long>> _spatialGrid = new();
    private readonly Dictionary<long, Vec2i> _entityLocations = new();
    
    public void UpdateEntity(long entityId, Vec3d position)
    {
        var cell = GetCell(position);
        
        // Remove from old cell
        if (_entityLocations.TryGetValue(entityId, out var oldCell))
        {
            if (_spatialGrid.TryGetValue(oldCell, out var oldCellEntities))
            {
                oldCellEntities.Remove(entityId);
            }
        }
        
        // Add to new cell
        if (!_spatialGrid.ContainsKey(cell))
        {
            _spatialGrid[cell] = new HashSet<long>();
        }
        _spatialGrid[cell].Add(entityId);
        _entityLocations[entityId] = cell;
    }
    
    public void RemoveEntity(long entityId)
    {
        if (_entityLocations.TryGetValue(entityId, out var cell))
        {
            if (_spatialGrid.TryGetValue(cell, out var cellEntities))
            {
                cellEntities.Remove(entityId);
            }
            _entityLocations.Remove(entityId);
        }
    }
    
    public HashSet<long> GetEntitiesNear(Vec3d position, double radius)
    {
        var result = new HashSet<long>();
        var centerCell = GetCell(position);
        var cellRadius = (int)Math.Ceiling(radius / CellSize);
        
        for (int x = -cellRadius; x <= cellRadius; x++)
        {
            for (int z = -cellRadius; z <= cellRadius; z++)
            {
                var cell = new Vec2i(centerCell.X + x, centerCell.Y + z);
                if (_spatialGrid.TryGetValue(cell, out var entities))
                {
                    result.UnionWith(entities);
                }
            }
        }
        
        return result;
    }
    
    private Vec2i GetCell(Vec3d position)
    {
        return new Vec2i(
            (int)Math.Floor(position.X / CellSize),
            (int)Math.Floor(position.Z / CellSize)
        );
    }
}
```

2. **Integrate into DataCollector:**

```csharp
// DataCollector.cs
private readonly EntitySpatialIndex _spatialIndex = new();

private List<AnimalData> GetAnimalsData()
{
    // Use cached data if still valid
    if (_animalsCache != null && DateTime.UtcNow < _animalsCacheUntil)
    {
        return _animalsCache;
    }

    var animals = new List<AnimalData>();

    try
    {
        var players = sapi.World.AllOnlinePlayers;
        
        if (players == null || players.Length == 0)
        {
            _animalsCache = animals;
            _animalsCacheUntil = DateTime.UtcNow.AddSeconds(_config.EntityCacheSeconds);
            return animals;
        }

        var seenEntities = new HashSet<long>();
        
        if (_config.TrackAllLoadedEntities)
        {
            // Track all loaded entities (slower but comprehensive)
            foreach (var entity in sapi.World.LoadedEntities.Values)
            {
                if (ShouldTrackEntity(entity) && animals.Count < _config.MaxTrackedEntities)
                {
                    animals.Add(CreateAnimalData(entity));
                }
            }
        }
        else
        {
            // Track only entities near players (faster)
            foreach (var player in players)
            {
                if (player?.Entity?.Pos == null) continue;
                
                var nearbyIds = _spatialIndex.GetEntitiesNear(
                    player.Entity.Pos.XYZ,
                    _config.EntityTrackingRadius
                );
                
                foreach (var entityId in nearbyIds)
                {
                    if (seenEntities.Contains(entityId)) continue;
                    if (animals.Count >= _config.MaxTrackedEntities) break;
                    
                    if (sapi.World.LoadedEntities.TryGetValue(entityId, out var entity))
                    {
                        if (ShouldTrackEntity(entity))
                        {
                            animals.Add(CreateAnimalData(entity));
                            seenEntities.Add(entityId);
                        }
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        sapi.Logger.Warning($"[VintageAtlas] Error collecting animals: {ex.Message}");
    }

    _animalsCache = animals;
    _animalsCacheUntil = DateTime.UtcNow.AddSeconds(_config.EntityCacheSeconds);
    return animals;
}

private bool ShouldTrackEntity(Entity entity)
{
    if (entity is not EntityAgent) return false;
    if (entity is EntityPlayer) return false;
    if (!entity.Alive) return false;
    
    // Check entity type filter
    if (!string.IsNullOrEmpty(_config.TrackedEntityTypes))
    {
        var allowedTypes = _config.TrackedEntityTypes
            .Split(',')
            .Select(t => t.Trim())
            .ToHashSet();
            
        var entityType = entity.GetType().Name;
        if (!allowedTypes.Contains(entityType))
        {
            return false;
        }
    }
    
    return true;
}
```

#### 4.3 Add Cache Statistics

**Priority:** LOW

**Implementation:**

```csharp
// Models/CacheStatistics.cs
public class EntityCacheStatistics
{
    public int CachedEntities { get; set; }
    public int TotalEntitiesScanned { get; set; }
    public double AverageUpdateTimeMs { get; set; }
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }
    public double HitRate => CacheHits + CacheMisses > 0 
        ? (double)CacheHits / (CacheHits + CacheMisses) 
        : 0;
    public DateTime LastUpdate { get; set; }
}

// Add to DataCollector.cs
private long _cacheHits;
private long _cacheMisses;
private readonly List<double> _updateTimes = new();

public EntityCacheStatistics GetCacheStatistics()
{
    return new EntityCacheStatistics
    {
        CachedEntities = _animalsCache?.Count ?? 0,
        CacheHits = _cacheHits,
        CacheMisses = _cacheMisses,
        AverageUpdateTimeMs = _updateTimes.Count > 0 ? _updateTimes.Average() : 0,
        LastUpdate = _animalsCacheUntil
    };
}
```

### Testing Requirements

- ✅ Test with 500+ entities
- ✅ Verify spatial index improves query time by >50%
- ✅ Test entity type filtering
- ✅ Test cache hit rate > 80% over 5 minutes
- ✅ Verify no entities missed within tracking radius

---

## 5. Find Better Way to Track Entity Movement

### Current State

**Issues:**

1. **No dedicated entity movement tracker** - only player movement tracked
2. **Historical tracker only records player positions** every 15 seconds
3. **No entity trail/path visualization**
4. **No entity speed/velocity tracking**
5. **No entity density heatmaps**

**Current Implementation:**

- Players: Recorded every 15 seconds in HistoricalTracker
- Entities: Only current position in DataCollector, no history

### Requirements

#### 5.1 Implement Entity Movement Tracker

**Priority:** HIGH

**Goal:** Track entity movements for visualization and analysis.

**Implementation:**

```csharp
// Create new file: Tracking/EntityMovementTracker.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VintageAtlas.Tracking
{
    public class EntityMovementTracker : IDisposable
    {
        private readonly ICoreServerAPI _sapi;
        private readonly ModConfig _config;
        private SqliteConnection? _db;
        
        // Track entity positions in memory
        private readonly ConcurrentDictionary<long, EntityPositionHistory> _entityPositions = new();
        
        // Configurable settings
        private const int SnapshotIntervalMs = 5000; // 5 seconds
        private const int MaxHistoryPoints = 1000; // Per entity
        private const double MinMovementDistance = 1.0; // Only record if moved > 1 block
        
        private long _lastSnapshot;
        
        public EntityMovementTracker(ICoreServerAPI sapi, ModConfig config)
        {
            _sapi = sapi;
            _config = config;
        }
        
        public void Initialize()
        {
            try
            {
                var dataPath = Path.Combine(_sapi.DataBasePath, "ModData", "VintageAtlas");
                Directory.CreateDirectory(dataPath);
                
                var dbPath = Path.Combine(dataPath, "entity_movement.db");
                _db = new SqliteConnection($"Data Source={dbPath}");
                _db.Open();
                
                CreateSchema();
                
                _sapi.Logger.Notification("[VintageAtlas] Entity movement tracker initialized");
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to initialize entity movement tracker: {ex.Message}");
            }
        }
        
        private void CreateSchema()
        {
            if (_db == null) return;
            
            var cmd = _db.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS entity_positions (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp INTEGER NOT NULL,
                    entity_id INTEGER NOT NULL,
                    entity_type TEXT NOT NULL,
                    x REAL NOT NULL,
                    y REAL NOT NULL,
                    z REAL NOT NULL,
                    velocity_x REAL,
                    velocity_z REAL,
                    heading REAL
                );
                
                CREATE INDEX IF NOT EXISTS idx_entity_positions_entity_id 
                    ON entity_positions(entity_id);
                CREATE INDEX IF NOT EXISTS idx_entity_positions_timestamp 
                    ON entity_positions(timestamp);
                CREATE INDEX IF NOT EXISTS idx_entity_positions_type 
                    ON entity_positions(entity_type);
                    
                -- Aggregate table for heatmaps
                CREATE TABLE IF NOT EXISTS entity_density (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    timestamp INTEGER NOT NULL,
                    entity_type TEXT NOT NULL,
                    grid_x INTEGER NOT NULL,
                    grid_z INTEGER NOT NULL,
                    count INTEGER NOT NULL,
                    avg_velocity REAL
                );
                
                CREATE INDEX IF NOT EXISTS idx_entity_density_grid 
                    ON entity_density(grid_x, grid_z);
                CREATE INDEX IF NOT EXISTS idx_entity_density_type 
                    ON entity_density(entity_type);
            ";
            cmd.ExecuteNonQuery();
        }
        
        public void OnGameTick(float dt)
        {
            if (_db == null) return;
            
            var now = _sapi.World.ElapsedMilliseconds;
            
            if (now - _lastSnapshot < SnapshotIntervalMs)
            {
                return;
            }
            
            _lastSnapshot = now;
            
            try
            {
                RecordEntityPositions();
                CleanupOldData();
            }
            catch (Exception ex)
            {
                _sapi.Logger.Warning($"[VintageAtlas] Error in entity movement tracker: {ex.Message}");
            }
        }
        
        private void RecordEntityPositions()
        {
            if (_db == null) return;
            
            var timestamp = _sapi.World.ElapsedMilliseconds;
            var recorded = 0;
            
            using var transaction = _db.BeginTransaction();
            try
            {
                var cmd = _db.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO entity_positions 
                    (timestamp, entity_id, entity_type, x, y, z, velocity_x, velocity_z, heading)
                    VALUES (@ts, @id, @type, @x, @y, @z, @vx, @vz, @heading)
                ";
                
                foreach (var entity in _sapi.World.LoadedEntities.Values)
                {
                    if (entity is not EntityAgent agent) continue;
                    if (entity is EntityPlayer) continue; // Players tracked separately
                    if (!entity.Alive) continue;
                    
                    var entityId = entity.EntityId;
                    var pos = entity.ServerPos ?? entity.Pos;
                    
                    // Check if entity moved significantly
                    if (_entityPositions.TryGetValue(entityId, out var history))
                    {
                        var lastPos = history.LastPosition;
                        var distance = pos.DistanceTo(lastPos);
                        
                        if (distance < MinMovementDistance)
                        {
                            continue; // Skip if hasn't moved enough
                        }
                    }
                    
                    // Calculate velocity
                    var velocity = entity.ServerPos?.Motion ?? new Vec3d(0, 0, 0);
                    
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@ts", timestamp);
                    cmd.Parameters.AddWithValue("@id", entityId);
                    cmd.Parameters.AddWithValue("@type", entity.Code?.ToString() ?? "unknown");
                    cmd.Parameters.AddWithValue("@x", pos.X);
                    cmd.Parameters.AddWithValue("@y", pos.Y);
                    cmd.Parameters.AddWithValue("@z", pos.Z);
                    cmd.Parameters.AddWithValue("@vx", velocity.X);
                    cmd.Parameters.AddWithValue("@vz", velocity.Z);
                    cmd.Parameters.AddWithValue("@heading", Math.Atan2(velocity.Z, velocity.X));
                    
                    cmd.ExecuteNonQuery();
                    recorded++;
                    
                    // Update in-memory history
                    if (!_entityPositions.ContainsKey(entityId))
                    {
                        _entityPositions[entityId] = new EntityPositionHistory();
                    }
                    _entityPositions[entityId].AddPosition(pos, timestamp);
                }
                
                transaction.Commit();
                
                if (recorded > 0)
                {
                    _sapi.Logger.Debug($"[VintageAtlas] Recorded {recorded} entity positions");
                }
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _sapi.Logger.Warning($"[VintageAtlas] Failed to record entity positions: {ex.Message}");
            }
        }
        
        private void CleanupOldData()
        {
            if (_db == null) return;
            
            try
            {
                // Keep only last 24 hours of data
                var cutoff = _sapi.World.ElapsedMilliseconds - (24 * 60 * 60 * 1000);
                
                var cmd = _db.CreateCommand();
                cmd.CommandText = "DELETE FROM entity_positions WHERE timestamp < @cutoff";
                cmd.Parameters.AddWithValue("@cutoff", cutoff);
                var deleted = cmd.ExecuteNonQuery();
                
                if (deleted > 0)
                {
                    _sapi.Logger.Debug($"[VintageAtlas] Cleaned up {deleted} old entity position records");
                }
            }
            catch (Exception ex)
            {
                _sapi.Logger.Warning($"[VintageAtlas] Failed to cleanup old entity positions: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get entity movement path for a specific entity
        /// </summary>
        public List<EntityMovementPoint> GetEntityPath(long entityId, long fromTimestamp, long toTimestamp)
        {
            if (_db == null) return new List<EntityMovementPoint>();
            
            try
            {
                var cmd = _db.CreateCommand();
                cmd.CommandText = @"
                    SELECT timestamp, x, y, z, velocity_x, velocity_z, heading
                    FROM entity_positions
                    WHERE entity_id = @id AND timestamp BETWEEN @from AND @to
                    ORDER BY timestamp ASC
                    LIMIT 10000
                ";
                
                cmd.Parameters.AddWithValue("@id", entityId);
                cmd.Parameters.AddWithValue("@from", fromTimestamp);
                cmd.Parameters.AddWithValue("@to", toTimestamp);
                
                var path = new List<EntityMovementPoint>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    path.Add(new EntityMovementPoint
                    {
                        Timestamp = reader.GetInt64(0),
                        X = reader.GetDouble(1),
                        Y = reader.GetDouble(2),
                        Z = reader.GetDouble(3),
                        VelocityX = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                        VelocityZ = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                        Heading = reader.IsDBNull(6) ? 0 : reader.GetDouble(6)
                    });
                }
                
                return path;
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to get entity path: {ex.Message}");
                return new List<EntityMovementPoint>();
            }
        }
        
        /// <summary>
        /// Get entity density heatmap for a specific entity type
        /// </summary>
        public List<EntityDensityPoint> GetEntityDensityHeatmap(string entityType, int gridSize = 32)
        {
            if (_db == null) return new List<EntityDensityPoint>();
            
            try
            {
                var cmd = _db.CreateCommand();
                cmd.CommandText = @"
                    SELECT 
                        CAST(x / @gridSize AS INTEGER) * @gridSize as grid_x,
                        CAST(z / @gridSize AS INTEGER) * @gridSize as grid_z,
                        COUNT(*) as count,
                        AVG(velocity_x * velocity_x + velocity_z * velocity_z) as avg_velocity_squared
                    FROM entity_positions
                    WHERE entity_type = @type
                    GROUP BY grid_x, grid_z
                    HAVING count > 5
                    ORDER BY count DESC
                    LIMIT 10000
                ";
                
                cmd.Parameters.AddWithValue("@type", entityType);
                cmd.Parameters.AddWithValue("@gridSize", gridSize);
                
                var heatmap = new List<EntityDensityPoint>();
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    heatmap.Add(new EntityDensityPoint
                    {
                        GridX = reader.GetInt32(0),
                        GridZ = reader.GetInt32(1),
                        Count = reader.GetInt32(2),
                        AvgVelocity = Math.Sqrt(reader.GetDouble(3))
                    });
                }
                
                return heatmap;
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to get entity density heatmap: {ex.Message}");
                return new List<EntityDensityPoint>();
            }
        }
        
        public void Dispose()
        {
            _db?.Close();
            _db?.Dispose();
        }
    }
    
    public class EntityPositionHistory
    {
        private readonly Queue<(Vec3d pos, long timestamp)> _positions = new();
        
        public Vec3d LastPosition => _positions.Count > 0 ? _positions.Last().pos : Vec3d.Zero;
        
        public void AddPosition(Vec3d pos, long timestamp)
        {
            _positions.Enqueue((pos, timestamp));
            
            while (_positions.Count > 100) // Keep last 100 positions in memory
            {
                _positions.Dequeue();
            }
        }
    }
    
    public class EntityMovementPoint
    {
        public long Timestamp { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double VelocityX { get; set; }
        public double VelocityZ { get; set; }
        public double Heading { get; set; }
    }
    
    public class EntityDensityPoint
    {
        public int GridX { get; set; }
        public int GridZ { get; set; }
        public int Count { get; set; }
        public double AvgVelocity { get; set; }
    }
}
```

#### 5.2 Add Movement Visualization API

**Priority:** MEDIUM

**Implementation:**

```csharp
// Create new file: Web/API/EntityMovementController.cs
public class EntityMovementController
{
    private readonly ICoreServerAPI _sapi;
    private readonly EntityMovementTracker _tracker;
    
    public EntityMovementController(ICoreServerAPI sapi, EntityMovementTracker tracker)
    {
        _sapi = sapi;
        _tracker = tracker;
    }
    
    public async Task HandleRequest(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "";
        
        if (path.EndsWith("/api/entity-movement/path"))
        {
            await HandleGetEntityPath(context);
        }
        else if (path.EndsWith("/api/entity-movement/density"))
        {
            await HandleGetEntityDensity(context);
        }
        else
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
        }
    }
    
    private async Task HandleGetEntityPath(HttpListenerContext context)
    {
        var query = context.Request.QueryString;
        
        if (!long.TryParse(query["entityId"], out var entityId))
        {
            context.Response.StatusCode = 400;
            await WriteJsonAsync(context.Response, new { error = "Invalid entityId" });
            return;
        }
        
        var hoursBack = int.TryParse(query["hours"], out var h) ? h : 1;
        var toTime = _sapi.World.ElapsedMilliseconds;
        var fromTime = toTime - (hoursBack * 3600000);
        
        var path = _tracker.GetEntityPath(entityId, fromTime, toTime);
        
        var geoJson = new
        {
            type = "Feature",
            properties = new { entityId, hoursBack },
            geometry = new
            {
                type = "LineString",
                coordinates = path.Select(p => new[] { p.X, p.Z }).ToArray()
            }
        };
        
        await WriteJsonAsync(context.Response, geoJson);
    }
    
    private async Task HandleGetEntityDensity(HttpListenerContext context)
    {
        var query = context.Request.QueryString;
        var entityType = query["type"] ?? "game:drifter-normal";
        var gridSize = int.TryParse(query["gridSize"], out var gs) ? gs : 32;
        
        var heatmap = _tracker.GetEntityDensityHeatmap(entityType, gridSize);
        
        var geoJson = new
        {
            type = "FeatureCollection",
            features = heatmap.Select(h => new
            {
                type = "Feature",
                properties = new { count = h.Count, avgVelocity = h.AvgVelocity },
                geometry = new
                {
                    type = "Point",
                    coordinates = new[] { h.GridX + gridSize / 2.0, h.GridZ + gridSize / 2.0 }
                }
            }).ToArray()
        };
        
        await WriteJsonAsync(context.Response, geoJson);
    }
    
    private async Task WriteJsonAsync(HttpListenerResponse response, object data)
    {
        response.ContentType = "application/json";
        var json = JsonConvert.SerializeObject(data);
        var bytes = Encoding.UTF8.GetBytes(json);
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        response.Close();
    }
}
```

#### 5.3 Configuration Options

```csharp
// ModConfig.cs
/// <summary>
/// Enable entity movement tracking
/// Default: true
/// </summary>
public bool EnableEntityMovementTracking { get; set; } = true;

/// <summary>
/// Interval in milliseconds for recording entity movements
/// Default: 5000 (5 seconds)
/// </summary>
public int EntityMovementSnapshotIntervalMs { get; set; } = 5000;

/// <summary>
/// Minimum distance an entity must move to be recorded (in blocks)
/// Default: 1.0
/// </summary>
public double MinEntityMovementDistance { get; set; } = 1.0;

/// <summary>
/// Track entity movements (separate from player movements)
/// Default: true
/// </summary>
public bool TrackEntityMovements { get; set; } = true;
```

### Testing Requirements

- ✅ Test entity tracking with 100+ entities
- ✅ Verify paths are accurate over 1-hour period
- ✅ Test heatmap generation with various grid sizes
- ✅ Verify database doesn't exceed 100MB over 24 hours
- ✅ Test cleanup removes old data correctly

---

## 6. Fix Player Historical Tracker

### Current State

**Issues Identified:**

1. **Interval may be too slow** (15 seconds) for smooth path visualization
2. **No error handling** for database connection failures
3. **Cleanup may be too aggressive** (10,000 positions per player limit)
4. **No configurable intervals** in ModConfig
5. **Recording may fail silently** if player data is incomplete
6. **No path simplification** (stores every point, even if player is stationary)

**Current Implementation:**

```csharp
// HistoricalTracker.cs:25-28
private const int PlayerSnapshotIntervalMs = 15000; // 15 seconds
private const int CensusSnapshotIntervalMs = 60000; // 1 minute
private const int StatsSnapshotIntervalMs = 30000;  // 30 seconds
private const int MaxPositionsPerPlayer = 10000; // Limit history per player
```

### Requirements

#### 6.1 Make Intervals Configurable

**Priority:** HIGH

**Implementation:**

1. **Add to ModConfig.cs:**

```csharp
/// <summary>
/// Interval in milliseconds for recording player positions
/// Default: 15000 (15 seconds)
/// Lower values = smoother paths but more database writes
/// </summary>
public int PlayerSnapshotIntervalMs { get; set; } = 15000;

/// <summary>
/// Interval in milliseconds for entity census snapshots
/// Default: 60000 (1 minute)
/// </summary>
public int CensusSnapshotIntervalMs { get; set; } = 60000;

/// <summary>
/// Interval in milliseconds for server stats snapshots
/// Default: 30000 (30 seconds)
/// </summary>
public int StatsSnapshotIntervalMs { get; set; } = 30000;

/// <summary>
/// Maximum number of position records to keep per player
/// Default: 10000
/// Older records are automatically cleaned up
/// </summary>
public int MaxPositionsPerPlayer { get; set; } = 10000;

/// <summary>
/// Only record player position if moved more than this distance (in blocks)
/// Default: 0.5
/// Prevents recording when player is stationary
/// </summary>
public double MinPlayerMovementDistance { get; set; } = 0.5;
```

2. **Update HistoricalTracker.cs:**

```csharp
public class HistoricalTracker : IHistoricalTracker, IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private SqliteConnection? _metricsDb;
    private long _lastPlayerSnapshot;
    private long _lastCensusSnapshot;
    private long _lastStatsSnapshot;
    
    // Track last positions to avoid recording stationary players
    private readonly Dictionary<string, Vec3d> _lastPlayerPositions = new();
    
    public HistoricalTracker(ICoreServerAPI sapi, ModConfig config)
    {
        _sapi = sapi;
        _config = config;
    }
    
    public void OnGameTick(float dt)
    {
        if (_metricsDb == null) return;

        var now = _sapi.World.ElapsedMilliseconds;

        try
        {
            // Player position snapshots
            if (now - _lastPlayerSnapshot > _config.PlayerSnapshotIntervalMs)
            {
                RecordPlayerPositions();
                _lastPlayerSnapshot = now;
                CleanupOldPlayerPositions();
            }

            // Entity census
            if (now - _lastCensusSnapshot > _config.CensusSnapshotIntervalMs)
            {
                RecordEntityCensus();
                _lastCensusSnapshot = now;
            }

            // Server stats
            if (now - _lastStatsSnapshot > _config.StatsSnapshotIntervalMs)
            {
                RecordServerStats();
                _lastStatsSnapshot = now;
            }
        }
        catch (Exception ex)
        {
            _sapi.Logger.Warning($"[VintageAtlas] Error in historical tracker tick: {ex.Message}");
        }
    }
    
    private void RecordPlayerPositions()
    {
        if (_metricsDb == null) return;

        var timestamp = _sapi.World.ElapsedMilliseconds;
        var recorded = 0;

        using var transaction = _metricsDb.BeginTransaction();
        try
        {
            var cmd = _metricsDb.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = @"
                INSERT INTO player_positions 
                (timestamp, player_uid, player_name, x, y, z, health, max_health, hunger, max_hunger, temperature, body_temp)
                VALUES (@ts, @uid, @name, @x, @y, @z, @health, @maxHealth, @hunger, @maxHunger, @temp, @bodyTemp)
            ";

            foreach (var player in _sapi.World.AllOnlinePlayers)
            {
                if (player?.Entity == null) continue;

                try
                {
                    var pos = player.Entity.ServerPos ?? player.Entity.Pos;
                    
                    // Check if player has moved significantly
                    if (_lastPlayerPositions.TryGetValue(player.PlayerUID, out var lastPos))
                    {
                        var distance = pos.DistanceTo(lastPos);
                        if (distance < _config.MinPlayerMovementDistance)
                        {
                            continue; // Skip if player hasn't moved
                        }
                    }
                    
                    var attrs = player.Entity.WatchedAttributes as TreeAttribute;
                    
                    var healthTree = attrs?.GetTreeAttribute("health");
                    var hungerTree = attrs?.GetTreeAttribute("hunger");
                    var bodyTempTree = attrs?.GetTreeAttribute("bodyTemp");
                    
                    var blockPos = pos.AsBlockPos;
                    var climate = _sapi.World.BlockAccessor?.GetClimateAt(blockPos);

                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@ts", timestamp);
                    cmd.Parameters.AddWithValue("@uid", player.PlayerUID);
                    cmd.Parameters.AddWithValue("@name", player.PlayerName);
                    cmd.Parameters.AddWithValue("@x", pos.X);
                    cmd.Parameters.AddWithValue("@y", pos.Y);
                    cmd.Parameters.AddWithValue("@z", pos.Z);
                    cmd.Parameters.AddWithValue("@health", (object?)healthTree?.GetFloat("currenthealth") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@maxHealth", (object?)healthTree?.GetFloat("maxhealth", 20f) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@hunger", (object?)hungerTree?.GetFloat("currentsaturation") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@maxHunger", (object?)hungerTree?.GetFloat("maxsaturation", 1500f) ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@temp", (object?)climate?.Temperature ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@bodyTemp", (object?)bodyTempTree?.GetFloat("bodytemp") ?? DBNull.Value);

                    cmd.ExecuteNonQuery();
                    recorded++;
                    
                    // Update last position
                    _lastPlayerPositions[player.PlayerUID] = pos.XYZ;
                }
                catch (Exception ex)
                {
                    _sapi.Logger.Debug($"[VintageAtlas] Failed to record position for {player.PlayerName}: {ex.Message}");
                }
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _sapi.Logger.Warning($"[VintageAtlas] Failed to record player positions: {ex.Message}");
        }
    }
    
    private void CleanupOldPlayerPositions()
    {
        if (_metricsDb == null) return;

        try
        {
            var cmd = _metricsDb.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM player_positions 
                WHERE id IN (
                    SELECT id FROM player_positions 
                    WHERE player_uid = @uid 
                    ORDER BY timestamp DESC 
                    LIMIT -1 OFFSET @maxPositions
                )
            ";

            foreach (var player in _sapi.World.AllPlayers)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@uid", player.PlayerUID);
                cmd.Parameters.AddWithValue("@maxPositions", _config.MaxPositionsPerPlayer);
                cmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            _sapi.Logger.Debug($"[VintageAtlas] Failed to cleanup old player positions: {ex.Message}");
        }
    }
}
```

#### 6.2 Add Database Health Checks

**Priority:** MEDIUM

**Implementation:**

```csharp
// HistoricalTracker.cs
public bool IsHealthy()
{
    if (_metricsDb == null) return false;
    
    try
    {
        var cmd = _metricsDb.CreateCommand();
        cmd.CommandText = "SELECT 1";
        cmd.ExecuteScalar();
        return true;
    }
    catch
    {
        return false;
    }
}

public Dictionary<string, object> GetDatabaseStats()
{
    if (_metricsDb == null) return new Dictionary<string, object>();
    
    try
    {
        var stats = new Dictionary<string, object>();
        
        var cmd = _metricsDb.CreateCommand();
        
        // Count player positions
        cmd.CommandText = "SELECT COUNT(*) FROM player_positions";
        stats["playerPositions"] = Convert.ToInt64(cmd.ExecuteScalar());
        
        // Count entity census
        cmd.CommandText = "SELECT COUNT(*) FROM entity_census";
        stats["censusRecords"] = Convert.ToInt64(cmd.ExecuteScalar());
        
        // Count server stats
        cmd.CommandText = "SELECT COUNT(*) FROM server_stats";
        stats["statsRecords"] = Convert.ToInt64(cmd.ExecuteScalar());
        
        // Get database file size
        cmd.CommandText = "SELECT page_count * page_size as size FROM pragma_page_count(), pragma_page_size()";
        stats["databaseSizeBytes"] = Convert.ToInt64(cmd.ExecuteScalar());
        
        // Get oldest record
        cmd.CommandText = "SELECT MIN(timestamp) FROM player_positions";
        var oldestTimestamp = cmd.ExecuteScalar();
        if (oldestTimestamp != null && oldestTimestamp != DBNull.Value)
        {
            stats["oldestRecordAge"] = _sapi.World.ElapsedMilliseconds - Convert.ToInt64(oldestTimestamp);
        }
        
        return stats;
    }
    catch (Exception ex)
    {
        _sapi.Logger.Warning($"[VintageAtlas] Failed to get database stats: {ex.Message}");
        return new Dictionary<string, object> { ["error"] = ex.Message };
    }
}
```

#### 6.3 Add Path Simplification

**Priority:** LOW (performance optimization)

**Goal:** Reduce database size by simplifying paths using Douglas-Peucker algorithm.

**Implementation:**

```csharp
// Utilities/PathSimplifier.cs
public static class PathSimplifier
{
    /// <summary>
    /// Simplify a path using Douglas-Peucker algorithm
    /// </summary>
    public static List<PlayerPathPoint> Simplify(List<PlayerPathPoint> points, double epsilon = 1.0)
    {
        if (points.Count < 3)
        {
            return points;
        }
        
        // Find the point with maximum distance
        var dmax = 0.0;
        var index = 0;
        var end = points.Count - 1;
        
        for (var i = 1; i < end; i++)
        {
            var d = PerpendicularDistance(points[i], points[0], points[end]);
            if (d > dmax)
            {
                index = i;
                dmax = d;
            }
        }
        
        // If max distance is greater than epsilon, recursively simplify
        List<PlayerPathPoint> result;
        if (dmax > epsilon)
        {
            // Recursive call
            var leftPoints = points.Take(index + 1).ToList();
            var rightPoints = points.Skip(index).ToList();
            
            var leftSimplified = Simplify(leftPoints, epsilon);
            var rightSimplified = Simplify(rightPoints, epsilon);
            
            // Combine results
            result = leftSimplified.Take(leftSimplified.Count - 1)
                .Concat(rightSimplified)
                .ToList();
        }
        else
        {
            result = new List<PlayerPathPoint> { points[0], points[end] };
        }
        
        return result;
    }
    
    private static double PerpendicularDistance(PlayerPathPoint point, PlayerPathPoint lineStart, PlayerPathPoint lineEnd)
    {
        var dx = lineEnd.X - lineStart.X;
        var dz = lineEnd.Z - lineStart.Z;
        
        var mag = Math.Sqrt(dx * dx + dz * dz);
        if (mag < 0.00001)
        {
            return Math.Sqrt(
                Math.Pow(point.X - lineStart.X, 2) + 
                Math.Pow(point.Z - lineStart.Z, 2)
            );
        }
        
        var u = ((point.X - lineStart.X) * dx + (point.Z - lineStart.Z) * dz) / (mag * mag);
        
        double closestX, closestZ;
        if (u < 0)
        {
            closestX = lineStart.X;
            closestZ = lineStart.Z;
        }
        else if (u > 1)
        {
            closestX = lineEnd.X;
            closestZ = lineEnd.Z;
        }
        else
        {
            closestX = lineStart.X + u * dx;
            closestZ = lineStart.Z + u * dz;
        }
        
        return Math.Sqrt(
            Math.Pow(point.X - closestX, 2) + 
            Math.Pow(point.Z - closestZ, 2)
        );
    }
}
```

### Testing Requirements

- ✅ Test with various snapshot intervals (5s, 15s, 30s)
- ✅ Verify stationary players are not recorded
- ✅ Test database health checks
- ✅ Verify cleanup maintains max positions per player
- ✅ Test path simplification reduces points by 50%+
- ✅ Verify database stats are accurate

---

## Summary

### Priority Matrix

| Task | Priority | Effort | Impact |
|------|----------|--------|--------|
| 1. Disable Auto Regen (Config) | HIGH | LOW | MEDIUM |
| 2. Enable On-Demand Tiles | HIGH | MEDIUM | HIGH |
| 2. Optimize Tile Cache | MEDIUM | LOW | MEDIUM |
| 2. Tile Priority System | HIGH | MEDIUM | HIGH |
| 2. Progressive Loading | MEDIUM | MEDIUM | MEDIUM |
| 3. WebSocket Backend | HIGH | HIGH | HIGH |
| 3. WebSocket Frontend | HIGH | MEDIUM | HIGH |
| 3. Differential Updates | MEDIUM | MEDIUM | MEDIUM |
| 4. Configurable Entity Cache | HIGH | LOW | MEDIUM |
| 4. Spatial Index | MEDIUM | HIGH | MEDIUM |
| 4. Cache Statistics | LOW | LOW | LOW |
| 5. Entity Movement Tracker | HIGH | HIGH | HIGH |
| 5. Movement Visualization | MEDIUM | MEDIUM | MEDIUM |
| 6. Configurable Historical Intervals | HIGH | LOW | MEDIUM |
| 6. Database Health Checks | MEDIUM | LOW | LOW |
| 6. Path Simplification | LOW | MEDIUM | LOW |

### Recommended Implementation Order

1. **Phase 1: Core Improvements (1-2 weeks)**
   - Configurable auto-regen (Task 1)
   - Configurable historical intervals (Task 6.1)
   - Configurable entity cache (Task 4.1)
   - Enable on-demand tiles (Task 2.1)

2. **Phase 2: Real-Time Updates (2-3 weeks)**
   - WebSocket backend (Task 3.1)
   - WebSocket frontend (Task 3.2)
   - Differential updates (Task 3.3)

3. **Phase 3: Movement Tracking (2-3 weeks)**
   - Entity movement tracker (Task 5.1)
   - Movement visualization API (Task 5.2)

4. **Phase 4: Performance & Polish (1-2 weeks)**
   - Tile priority system (Task 2.3)
   - Spatial index (Task 4.2)
   - Database health checks (Task 6.2)
   - Progressive loading (Task 2.4)

5. **Phase 5: Optimization (1 week)**
   - Optimize tile cache (Task 2.2)
   - Cache statistics (Task 4.3)
   - Path simplification (Task 6.3)

### Configuration File Example

```json
{
  "EnableAutomaticTileRegeneration": false,
  "TileRegenerationCheckIntervalMs": 30000,
  "MaxTilesPerBatch": 100,
  
  "EnableOnDemandTileGeneration": true,
  "MaxConcurrentOnDemandTiles": 5,
  "TileCacheSize": 500,
  "TileCacheTtlSeconds": 600,
  
  "EnableWebSocket": true,
  "UseDifferentialUpdates": true,
  "MinimumPositionChangeBlocks": 0.5,
  
  "EntityCacheSeconds": 3,
  "MaxTrackedEntities": 500,
  "EntityTrackingRadius": 64,
  "TrackAllLoadedEntities": false,
  "TrackedEntityTypes": "",
  
  "EnableEntityMovementTracking": true,
  "EntityMovementSnapshotIntervalMs": 5000,
  "MinEntityMovementDistance": 1.0,
  
  "PlayerSnapshotIntervalMs": 15000,
  "CensusSnapshotIntervalMs": 60000,
  "StatsSnapshotIntervalMs": 30000,
  "MaxPositionsPerPlayer": 10000,
  "MinPlayerMovementDistance": 0.5
}
```

---

## Testing Strategy

### Unit Tests

- Configuration validation
- Path simplification algorithm
- Spatial index operations
- Cache eviction policies

### Integration Tests

- WebSocket connection lifecycle
- Tile generation pipeline
- Entity tracking accuracy
- Historical data integrity

### Performance Tests

- Tile generation throughput
- WebSocket message rates
- Entity cache hit rates
- Database query performance

### Load Tests

- 100+ concurrent WebSocket connections
- 500+ cached tiles
- 1000+ tracked entities
- 24 hours of historical data

---

## Documentation Requirements

1. **User Guide**
   - Configuration options explained
   - Performance tuning guide
   - Troubleshooting common issues

2. **Developer Guide**
   - WebSocket protocol specification
   - API endpoints documentation
   - Database schema documentation

3. **Migration Guide**
   - Upgrading from HTTP polling to WebSocket
   - Database migration scripts
   - Breaking changes

---

## Conclusion

This document provides a comprehensive roadmap for improving VintageAtlas across all requested areas. The improvements are designed to be backwards-compatible and configurable, allowing users to choose the features they need while maintaining performance and stability.

The phased approach ensures that critical improvements (WebSockets, entity movement tracking) are implemented first, while optimizations and polish are saved for later phases.

All changes should be thoroughly tested before deployment, with particular attention paid to threading safety, database integrity, and network stability.

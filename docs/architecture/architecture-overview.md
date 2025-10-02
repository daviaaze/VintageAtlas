# VintageAtlas Architecture Overview

**Last Updated:** 2025-10-02  
**Version:** 1.0.0

## Introduction

VintageAtlas is a comprehensive mapping and server monitoring mod for Vintage Story featuring dynamic map generation, real-time player tracking, and historical data analysis. This document provides an overview of the system architecture and design decisions.

## System Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Vintage Story Server                      │
│                      (Game Engine)                           │
└────────────────┬────────────────────────────────────────────┘
                 │
                 │ Vintage Story API
                 ▼
┌─────────────────────────────────────────────────────────────┐
│              VintageAtlas Mod System                         │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  Core System (VintageAtlasModSystem)                 │   │
│  │  - Configuration Management                          │   │
│  │  - Component Lifecycle                               │   │
│  │  - Event Coordination                                │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌─────────────────┐   │
│  │   Export     │  │   Tracking   │  │      Web        │   │
│  │              │  │              │  │                 │   │
│  │ Map          │  │ Chunk        │  │ HTTP Server     │   │
│  │ Generation   │  │ Changes      │  │ API Routes      │   │
│  │              │  │              │  │ Static Files    │   │
│  │ Tile         │  │ Historical   │  │                 │   │
│  │ Processing   │  │ Data         │  │ Controllers     │   │
│  │              │  │              │  │                 │   │
│  │ GeoJSON      │  │ Background   │  │ Authentication  │   │
│  │ Export       │  │ Services     │  │                 │   │
│  └──────────────┘  └──────────────┘  └─────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                 │
                 │ HTTP/REST
                 ▼
┌─────────────────────────────────────────────────────────────┐
│                  Web Frontend (Browser)                      │
│  ┌──────────────────────────────────────────────────────┐   │
│  │          Vue 3 + TypeScript + Vite                   │   │
│  │                                                       │   │
│  │  ┌───────────┐  ┌────────────┐  ┌────────────────┐  │   │
│  │  │ OpenLayers│  │   API      │  │   Components   │  │   │
│  │  │    Map    │  │  Client    │  │                │  │   │
│  │  │  Renderer │  │            │  │  Map Container │  │   │
│  │  │           │  │  Services  │  │  Player Layer  │  │   │
│  │  │  Layers   │  │            │  │  Sidebar       │  │   │
│  │  │  Controls │  │  Cache     │  │  Header        │  │   │
│  │  └───────────┘  └────────────┘  └────────────────┘  │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## Core Components

### 1. VintageAtlasModSystem (Entry Point)

**Location:** `VintageAtlasModSystem.cs`

The main mod system that initializes and coordinates all components.

**Responsibilities:**

- Load and validate configuration
- Initialize subsystems (Export, Tracking, Web)
- Register game event handlers
- Manage mod lifecycle (Start, Stop, Dispose)
- Coordinate periodic tasks

**Key Methods:**

```csharp
Start(ICoreAPI)              // Client and server initialization
StartServerSide(ICoreServerAPI)  // Server-only initialization
Dispose()                    // Cleanup and shutdown
OnGameTick(float)            // Periodic update handler
```

### 2. Export System

**Location:** `Export/` directory

Handles map tile generation and GeoJSON export.

#### MapExporter

Orchestrates the full map export process:

- Prepare chunk data
- Generate base tiles
- Create zoom levels
- Export structures as GeoJSON
- Save configuration

#### DynamicTileGenerator

Generates tiles on-demand:

- Query chunk data from savegame
- Render terrain colors
- Apply blur/smoothing
- Cache to disk with ETags
- Generate placeholders for missing data

#### Extractor

Core terrain rendering engine:

- Multi-threaded chunk processing
- Block color mapping
- Height-based shading
- Various render modes (medieval, flat, etc.)

#### PyramidTileDownsampler

Generates lower zoom levels by downsampling:

- Read 4 tiles from higher zoom
- Downsample to single tile
- Apply smoothing
- Cache result

### 3. Tracking System

**Location:** `Tracking/` directory

Monitors world changes and collects historical data.

#### ChunkChangeTracker

Real-time world change detection:

- Listens to Vintage Story events:
  - `BreakBlock` - Block breaking
  - `DidPlaceBlock` - Block placement
  - `CanPlaceOrBreakBlock` - Catch-all
  - `ChunkColumnLoaded` - New terrain
  - `OnTrySpawnEntity` - Entity spawns
- Tracks modified chunks in memory
- Thread-safe concurrent collections
- Query methods for changed data

#### HistoricalTracker

Stores historical data:

- Player positions over time
- Movement paths
- Death events
- Entity census
- SQLite database storage

#### BackgroundTileService

Background tile regeneration:

- Implements `IAsyncServerSystem`
- Checks for modified chunks
- Queues tile regeneration
- Runs on separate thread
- Integrates with server lifecycle

#### TileGenerationState

Tracks tile generation progress:

- Persists state across restarts
- Prevents duplicate work
- Tracks completion status

### 4. Web System

**Location:** `Web/` directory

HTTP server and API implementation.

#### WebServer

HTTP server implementation:

- Lightweight HTTP listener
- Request routing
- CORS support
- Request throttling
- Static file serving

#### RequestRouter

Routes requests to controllers:

- Pattern matching
- Query string parsing
- Route registration
- Controller dispatch

#### Controllers

**MapConfigController** - Dynamic map configuration:

```
GET /api/map-config  - Full configuration
GET /api/map-extent  - World boundaries
```

**GeoJsonController** - Structure data:

```
GET /api/geojson/signs
GET /api/geojson/signposts
GET /api/geojson/traders
GET /api/geojson/translocators
```

**TileController** - Map tiles:

```
GET /tiles/{zoom}/{x}_{z}.png
```

**StatusController** - Server status:

```
GET /api/status  - Full server status
GET /api/health  - Health check
```

### 5. Frontend System

**Location:** `frontend/` directory

Vue 3 + TypeScript web interface.

#### Core Components

**MapContainer.vue** - Main map display:

- OpenLayers map integration
- Layer management
- Coordinate transformation
- User interactions

**PlayerLayer.vue** - Real-time player positions:

- Periodic API polling
- Marker rendering
- Player info display

**AppSidebar.vue** - Navigation and controls:

- Layer toggles
- Settings panel
- Search functionality

#### Services

**API Client** - HTTP communication:

- Centralized request handling
- Error handling
- Response caching

**Map Services** - Map configuration:

- Fetch dynamic config
- Cache configuration
- Fallback values

## Directory Structure

```
VintageAtlas/
├── Core/                       # Core configuration and interfaces
│   ├── IAtlasConfig.cs        # Configuration interface
│   └── ConfigValidator.cs      # Config validation
│
├── Models/                     # Data models
│   ├── PlayerStatus.cs
│   ├── ServerStatus.cs
│   └── ...
│
├── Export/                     # Map generation
│   ├── MapExporter.cs         # Full export orchestration
│   ├── DynamicTileGenerator.cs # On-demand generation
│   ├── Extractor.cs           # Terrain rendering
│   ├── BlurTool.cs            # Image processing
│   ├── PyramidTileDownsampler.cs # Zoom level generation
│   └── SavegameDataLoader.cs  # Savegame access
│
├── Storage/                    # Persistent storage
│   └── MBTilesStorage.cs      # SQLite tile storage (optional)
│
├── Tracking/                   # Change tracking and history
│   ├── ChunkChangeTracker.cs  # Real-time change detection
│   ├── HistoricalTracker.cs   # Historical data
│   ├── DataCollector.cs       # Statistics
│   ├── BackgroundTileService.cs # Background generation
│   └── TileGenerationState.cs  # Generation state
│
├── Web/                        # HTTP server
│   ├── Server/
│   │   ├── WebServer.cs       # HTTP listener
│   │   └── RequestRouter.cs   # Request routing
│   │
│   └── API/                    # REST controllers
│       ├── MapConfigController.cs
│       ├── GeoJsonController.cs
│       ├── TileController.cs
│       └── StatusController.cs
│
├── Commands/                   # Chat commands
│   └── AtlasCommands.cs       # /atlas commands
│
├── frontend/                   # Vue 3 frontend
│   ├── src/
│   │   ├── components/        # Vue components
│   │   ├── services/          # API clients
│   │   ├── utils/             # Utilities
│   │   └── App.vue            # Main app
│   │
│   ├── vite.config.ts
│   └── package.json
│
├── html/                       # Static assets
│   └── index.html
│
├── VintageAtlasModSystem.cs   # Main entry point
├── modinfo.json               # Mod metadata
└── VintageAtlas.csproj        # Project file
```

## Data Flow

### Map Tile Generation Flow

```
1. User Action (or scheduled task)
   ↓
2. Trigger Export Command (/atlas export)
   ↓
3. MapExporter.ExportMap()
   ↓
4. SavegameDataLoader.LoadChunks()
   ↓
5. Extractor.GenerateTiles() (parallel processing)
   ↓
6. BlurTool.ProcessTile() (smoothing)
   ↓
7. Save to disk: ModData/VintageAtlas/data/world/{zoom}/{x}_{z}.png
   ↓
8. PyramidTileDownsampler.GenerateZoomLevels()
   ↓
9. Export complete
```

### Real-Time Update Flow

```
1. Player places/breaks block
   ↓
2. Vintage Story fires event (DidPlaceBlock/BreakBlock)
   ↓
3. ChunkChangeTracker.OnBlockPlaced/OnBlockBreaking()
   ↓
4. Add to modified chunks list (thread-safe)
   ↓
5. Background: OnGameTick() (every 30s)
   ↓
6. Query modified chunks
   ↓
7. DynamicTileGenerator.RegenerateTilesForChunksAsync()
   ↓
8. Calculate affected tiles
   ↓
9. Regenerate tiles (parallel)
   ↓
10. Clear tracked changes
```

### API Request Flow

```
1. Browser: GET /api/map-config
   ↓
2. WebServer receives request
   ↓
3. RequestRouter matches route
   ↓
4. MapConfigController.HandleRequest()
   ↓
5. Check cache (5 minute TTL)
   ↓
6. If miss: Calculate configuration
   ├── Scan tile directory
   ├── Query world data
   ├── Calculate extent
   └── Get spawn position
   ↓
7. Generate JSON response
   ↓
8. Cache result
   ↓
9. Send response with ETag
   ↓
10. Browser receives and caches
```

## Threading Model

### Main Thread (Game Thread)

- **Used for:** All game state access
- **Components:** Event handlers, world queries
- **Critical:** Never block this thread!

### HTTP Server Thread Pool

- **Used for:** HTTP requests
- **Components:** WebServer, Controllers
- **Note:** Async for I/O-bound operations

### Background Worker Threads

- **Used for:** CPU-intensive tasks
- **Components:** Map generation, tile processing
- **Note:** Use `Task.Run()` for isolation

### Event-to-Main-Thread Pattern

```csharp
// Background thread
Task.Run(() => {
    // CPU-intensive work here
    var result = ProcessData();
    
    // Switch to main thread for game state access
    _sapi.Event.EnqueueMainThreadTask(() => {
        // Safe to access game state here
        UpdateGameState(result);
    }, "VintageAtlas-Update");
});
```

## Caching Strategy

### Multi-Level Caching

```
Browser Request
    ↓
┌──────────────────────┐
│  Browser Cache       │  (60 minutes via Cache-Control)
│  - Tiles: 1 hour     │
│  - API: per response │
└────────┬─────────────┘
         │ [miss]
         ▼
┌──────────────────────┐
│  Server Memory       │  (in-process cache)
│  - Config: 5 min     │
│  - GeoJSON: 30-60s   │
│  - Tile Meta: session│
└────────┬─────────────┘
         │ [miss]
         ▼
┌──────────────────────┐
│  Disk Cache          │  (persistent files)
│  - Tiles: PNG files  │
│  - Until regenerated │
└────────┬─────────────┘
         │ [miss]
         ▼
┌──────────────────────┐
│  Dynamic Generation  │  (compute from world data)
│  - Query chunks      │
│  - Render terrain    │
│  - Save to disk      │
└──────────────────────┘
```

### Cache Invalidation

**Event-Driven:**

- Chunk changes → Mark tiles for regeneration
- Entity spawns → Invalidate GeoJSON

**Time-Based:**

- Config cache: 5 minutes
- GeoJSON cache: 30-60 seconds
- Browser cache: 1 hour

**Manual:**

- `/atlas export` → Full regeneration
- API config update → Clear relevant caches

## Performance Considerations

### Map Export Performance

- **Large worlds:** 15-20 minutes for 35GB savegames
- **Parallelism:** Uses all CPU cores
- **Memory:** Peak usage ~4-8GB during export
- **Disk I/O:** Intensive, use SSD for best performance

### Incremental Updates

- **Per-chunk regeneration:** 2-5 seconds for 10 chunks
- **Background processing:** Doesn't block gameplay
- **Automatic:** No manual intervention needed

### API Response Times

- **Cached responses:** < 1ms (memory)
- **Disk reads:** 1-5ms (tiles)
- **Dynamic generation:** 50-200ms (GeoJSON)
- **Full tile generation:** 100-500ms

### Optimization Strategies

1. **Multi-level caching** reduces repeated work
2. **ETag support** eliminates unnecessary transfers
3. **Parallel processing** utilizes all CPU cores
4. **Background services** offload work from main thread
5. **Smart invalidation** only updates what changed

## Configuration

### Main Configuration File

**Location:** `ModConfig/WebCartographerConfig.json`

**Key Settings:**

```json
{
  "Mode": 4,                      // Render mode
  "OutputDirectory": "...",       // Where data is stored
  "EnableLiveServer": true,       // HTTP server
  "LiveServerPort": 42421,        // Port (default: game+1)
  "AutoExportMap": true,          // Periodic exports
  "MapExportIntervalMs": 300000,  // Export interval (5 min)
  "EnableHistoricalTracking": true,
  "AbsolutePositions": false,     // Coordinate system
  "TileSize": 256,                // Tile dimensions
  "BaseZoomLevel": 9,             // Max zoom
  "MaxConcurrentRequests": 50,    // Server throttling
  "EnableCORS": true              // Cross-origin requests
}
```

## Security Considerations

### Network Security

- Default: Listen on localhost only
- Production: Use reverse proxy (nginx)
- CORS: Configurable for integrations

### Access Control

- Chat commands require `controlserver` privilege
- API: No authentication (relies on network security)
- Config updates: Logged for auditing

### File Access

- Serve only from designated output directory
- Path validation prevents directory traversal
- No write access via web interface (except config API)

## Error Handling

### Graceful Degradation

- Missing tiles → Generate placeholder
- API errors → Return cached data or defaults
- Component failures → Log and continue

### Logging Levels

- **Error:** Critical failures
- **Warning:** Non-critical issues
- **Notification:** Important events
- **Debug:** Detailed troubleshooting
- **VerboseDebug:** Trace-level details

### Recovery Mechanisms

- Automatic cache invalidation on errors
- Background service restart on failure
- Configuration reload on validation errors

## Future Architecture Considerations

### Potential Enhancements

1. **WebSocket support** - Real-time push updates
2. **Distributed caching** - Redis/Memcached
3. **API versioning** - `/api/v1/` namespace
4. **Plugin system** - Custom data sources
5. **Multi-server support** - Clustering
6. **GraphQL API** - Flexible queries
7. **Tile streaming** - Progressive loading

### Scalability

- Current: Single server, <100 concurrent users
- Potential: Load balancing, CDN integration
- Database: Consider PostgreSQL for historical data

## Related Documentation

- [API Integration Guide](api-integration.md) - Vintage Story API usage
- [Coordinate Systems](coordinate-systems.md) - Coordinate transformations
- [Tile Generation](../implementation/tile-generation.md) - Tile system details
- [REST API Reference](../api/rest-api.md) - API endpoints

---

**Maintained by:** daviaaze  
**Based on:** Original WebCartographer by Th3Dilli

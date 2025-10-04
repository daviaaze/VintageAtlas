# VintageAtlas - System Architecture Diagrams

## 1. Overall System Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         Vintage Story Server                             │
│                                                                           │
│  ┌─────────────────────────────────────────────────────────────────┐    │
│  │              VintageAtlasModSystem (Main Entry)                  │    │
│  │  - Initializes all components                                    │    │
│  │  - Manages lifecycle (Start → Running → Shutdown)                │    │
│  │  - Configuration loading and validation                          │    │
│  └────┬──────────────┬──────────────┬─────────────┬─────────────┬──┘    │
│       │              │              │             │             │        │
│       ▼              ▼              ▼             ▼             ▼        │
│  ┌────────┐    ┌─────────┐    ┌─────────┐  ┌──────────┐  ┌────────┐   │
│  │ Export │    │Tracking │    │ Storage │  │   Web    │  │Commands│   │
│  │ System │    │ System  │    │ System  │  │  Server  │  │ System │   │
│  └────┬───┘    └────┬────┘    └────┬────┘  └────┬─────┘  └────────┘   │
│       │             │              │             │                       │
└───────┼─────────────┼──────────────┼─────────────┼───────────────────────┘
        │             │              │             │
        ▼             ▼              ▼             ▼
   ┌────────────────────────────────────────────────────┐
   │          MBTiles SQLite Database                    │
   │  - Stores map tiles                                 │
   │  - WAL mode for concurrent access                   │
   │  - Historical data                                  │
   │  - Generation state                                 │
   └───────────────────┬────────────────────────────────┘
                       │
                       │ HTTP/REST API
                       │
   ┌───────────────────▼────────────────────────────────┐
   │          Web Browser (Vue 3 Frontend)               │
   │  ┌────────────────────────────────────────┐        │
   │  │  OpenLayers Map View                   │        │
   │  │  - Displays tiles from server          │        │
   │  │  - Shows players, entities, markers    │        │
   │  │  - Interactive controls                │        │
   │  └────────────────────────────────────────┘        │
   └─────────────────────────────────────────────────────┘
```

## 2. Export System - Hybrid Tile Generation

```
┌──────────────────────────────────────────────────────────────────┐
│                    Full Map Export Flow                           │
└──────────────────────────────────────────────────────────────────┘

  User: /atlas export
       │
       ▼
┌─────────────────┐
│  MapExporter    │  Orchestrates the export process
└────────┬────────┘
         │
         ▼
┌─────────────────────┐
│  Extractor          │  Reads entire savegame database
│  - Opens SQLite     │  - Accesses ALL chunks (not just loaded)
│  - Iterates chunks  │  - Generates high-quality tiles
│  - Applies colors   │  - Multi-threaded processing
│  - Hill shading     │
└────────┬────────────┘
         │ Saves PNG files to filesystem
         │ {OutputDirectory}/data/world/{zoom}/{x}_{z}.png
         ▼
┌─────────────────────┐
│  PyramidTile        │  Generates zoom levels
│  Downsampler        │  - Starts from highest zoom (most detail)
│  - Combines 4 tiles │  - Downsamples to lower zooms
│  - Creates parent   │  - Creates tile pyramid
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│  TileImporter       │  Imports PNG → Database
│  - Reads PNG files  │  - Stores in MBTiles format
│  - Imports to DB    │  - Makes available to web server
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│  MBTilesStorage     │  Persistent storage
│  (SQLite Database)  │  - All tiles stored here
│  - tiles.mbtiles    │  - Served to web interface
└─────────────────────┘


┌──────────────────────────────────────────────────────────────────┐
│                  Live Dynamic Tile Generation                     │
└──────────────────────────────────────────────────────────────────┘

  Browser: GET /tiles/9/512/512.png
       │
       ▼
┌─────────────────────┐
│  TileController     │  HTTP endpoint handler
└────────┬────────────┘
         │
         ▼
┌─────────────────────┐
│ DynamicTile         │  Checks if tile exists
│ Generator           │  - First checks database
│                     │  - If missing, generates dynamically
└────────┬────────────┘
         │
         ├─ Exists? → Return from DB (fast)
         │
         └─ Missing? ↓
              ▼
       ┌─────────────────────┐
       │ ChunkDataExtractor  │  Extracts from loaded chunks
       │ - Only loaded chunks│  - Can't access unloaded areas
       │ - Fast generation   │  - Uses current game state
       └─────────┬───────────┘
                 │
                 ▼
       ┌─────────────────────┐
       │ BlockColorCache     │  Applies colors
       │ - Block → Color map │
       │ - Hill shading      │
       └─────────┬───────────┘
                 │
                 ▼
       ┌─────────────────────┐
       │ Render PNG tile     │
       └─────────┬───────────┘
                 │
                 ▼
       ┌─────────────────────┐
       │ Store in Database   │  Cache for future requests
       │ Return to browser   │
       └─────────────────────┘
```

## 3. Web Server Request Flow

```
┌──────────────────────────────────────────────────────────────────┐
│                    HTTP Request Flow                              │
└──────────────────────────────────────────────────────────────────┘

Browser Request
    │
    ▼
┌─────────────────┐
│  WebServer.cs   │  HttpListener
│  Port: 42422    │  - Accepts connections
│  (configurable) │  - Rate limiting
└────────┬────────┘  - CORS headers
         │
         ▼
┌─────────────────────┐
│  RequestRouter      │  Routes to handlers
└────────┬────────────┘
         │
         ├─ /api/* ────────────────────────┐
         │                                  │
         ├─ /tiles/* ──────────────┐       │
         │                          │       │
         └─ /* (static files) ─┐   │       │
                               │   │       │
         ┌─────────────────────┘   │       │
         │                         │       │
         ▼                         ▼       ▼
┌─────────────────┐  ┌──────────────┐  ┌────────────────┐
│StaticFileServer │  │TileController│  │ API Controllers│
│ - Serves HTML   │  │- Tile serving│  │ - Status       │
│ - CSS, JS, etc  │  │- ETags       │  │ - Config       │
│ - MIME types    │  │- Caching     │  │ - Historical   │
└─────────────────┘  └──────┬───────┘  │ - GeoJSON      │
                            │          │ - Map Config   │
                            ▼          └────┬───────────┘
                   ┌─────────────────┐      │
                   │ DynamicTile     │      │
                   │ Generator       │      ▼
                   └─────────────────┘ ┌────────────────┐
                                       │ DataCollector  │
                                       │ Historical     │
                                       │ Tracker        │
                                       └────────────────┘
```

## 4. Frontend Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                    Vue 3 Frontend Structure                       │
└──────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│  App.vue (Root Component)                                        │
│  ├─ AppHeader (navigation, theme switcher)                       │
│  ├─ AppSidebar (layer controls, settings)                        │
│  └─ RouterView (page content)                                    │
└────────┬────────────────────────────────────────────────────────┘
         │
         ├─ MapView ──────────────────────────┐
         │                                     │
         ├─ HistoricalView                    │
         │                                     │
         ├─ AdminDashboard                    │
         │                                     │
         └─ SettingsView                      │
                                              │
         ┌────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────────┐
│  MapView.vue                                                     │
│  ┌────────────────────────────────────────────────────────┐    │
│  │  MapContainer (OpenLayers)                             │    │
│  │  ┌──────────────────────────────────────────────┐     │    │
│  │  │  Base Layers                                 │     │    │
│  │  │  - Terrain tiles from /tiles/{z}/{x}/{y}    │     │    │
│  │  │  - Cached in IndexedDB                       │     │    │
│  │  └──────────────────────────────────────────────┘     │    │
│  │  ┌──────────────────────────────────────────────┐     │    │
│  │  │  Vector Layers (GeoJSON)                     │     │    │
│  │  │  - PlayerLayer (real-time positions)         │     │    │
│  │  │  - AnimalLayer (nearby entities)             │     │    │
│  │  │  - Signs, Traders, Translocators             │     │    │
│  │  │  - SpawnMarker                               │     │    │
│  │  └──────────────────────────────────────────────┘     │    │
│  │  ┌──────────────────────────────────────────────┐     │    │
│  │  │  Controls                                    │     │    │
│  │  │  - Zoom, pan                                 │     │    │
│  │  │  - Layer switcher                            │     │    │
│  │  │  - Coordinate display                        │     │    │
│  │  │  - Screenshot                                │     │    │
│  │  └──────────────────────────────────────────────┘     │    │
│  └────────────────────────────────────────────────────────┘    │
│                                                                  │
│  LiveControls                                                    │
│  - Update interval                                               │
│  - Auto-follow player                                            │
└──────────────────────────────────────────────────────────────────┘


┌──────────────────────────────────────────────────────────────────┐
│                    State Management (Pinia)                       │
└──────────────────────────────────────────────────────────────────┘

┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│  mapStore       │  │  liveStore      │  │  serverStore    │
│  - map instance │  │  - players      │  │  - status       │
│  - layers       │  │  - animals      │  │  - uptime       │
│  - center/zoom  │  │  - spawn point  │  │  - performance  │
│  - features     │  │  - polling      │  │  - polling      │
└────────┬────────┘  └────────┬────────┘  └────────┬────────┘
         │                    │                     │
         │                    │                     │
         ▼                    ▼                     ▼
┌─────────────────────────────────────────────────────────────┐
│              API Services (Axios)                            │
│  ┌───────────┬────────────┬────────────┬──────────────┐    │
│  │ mapConfig │   status   │ historical │   geojson    │    │
│  └─────┬─────┴──────┬─────┴──────┬─────┴──────┬───────┘    │
└────────┼────────────┼────────────┼────────────┼────────────┘
         │            │            │            │
         ▼            ▼            ▼            ▼
┌──────────────────────────────────────────────────────────────┐
│                    Backend REST API                           │
│  GET /api/map-config                                          │
│  GET /api/status                                              │
│  GET /api/heatmap?player=UUID&hours=24                        │
│  GET /api/geojson/traders                                     │
│  GET /tiles/{z}/{x}/{y}.png                                   │
└──────────────────────────────────────────────────────────────┘
```

## 5. Data Flow - Live Player Tracking

```
Game Loop (Main Thread)
    │
    │ Every 1 second
    │
    ▼
┌─────────────────────┐
│  DataCollector      │
│  - Queries VS API   │
│  - Gets player data │
│  - Gets entity data │
│  - Gets weather     │
└─────────┬───────────┘
          │ Caches for 1 second
          │
          ▼
┌─────────────────────┐
│  StatusController   │  Waits for HTTP request
│  - Holds cached data│
└─────────┬───────────┘
          │
          │ Browser polls every 15 seconds
          │
          ▼
┌─────────────────────┐
│  Frontend           │
│  liveStore.ts       │
│  - Fetches /api/status
│  - Updates state    │
└─────────┬───────────┘
          │
          │ Reactive updates
          │
          ▼
┌─────────────────────┐
│  PlayerLayer.vue    │
│  - Renders markers  │
│  - Shows tooltips   │
│  - Updates positions│
└─────────────────────┘
```

## 6. Coordinate Systems

```
┌──────────────────────────────────────────────────────────────────┐
│                    Coordinate Transformations                     │
└──────────────────────────────────────────────────────────────────┘

Game World Coordinates (Vintage Story)
    X: 512000 (East/West)
    Y: 123    (Height)
    Z: 519000 (South/North)
          │
          │ Backend converts to relative
          │
          ▼
Map Coordinates (Spawn-Relative)
    X: -529 (relative to spawn)
    Z: 7009 (relative to spawn)
          │
          │ Frontend transforms for rendering
          │
          ▼
OpenLayers Coordinates (EPSG:3857 or custom)
    [X, Y] in map projection units
          │
          │ Tile system
          │
          ▼
Tile Coordinates
    Zoom: 9
    TileX: 511
    TileZ: 519
          │
          │ HTTP request
          │
          ▼
/tiles/9/511/519.png
```

## 7. Threading Model

```
┌──────────────────────────────────────────────────────────────────┐
│                    Thread Architecture                            │
└──────────────────────────────────────────────────────────────────┘

Main Game Thread (Vintage Story)
    │
    ├─► OnGameTick()
    │   - DataCollector updates
    │   - HistoricalTracker records
    │   - Lightweight work only
    │
    └─► Event Handlers
        - PlayerJoin, BlockPlaced, etc.
        - ChunkChangeTracker

Background Thread Pool (System.Threading.Tasks)
    │
    ├─► Map Export
    │   - Extractor.Run() (Task.Run)
    │   - Multi-threaded chunk processing
    │   - Can take 20+ minutes for large worlds
    │
    ├─► Tile Import
    │   - TileImporter.ImportExportedTilesAsync()
    │   - Async file I/O
    │
    └─► Dynamic Tile Generation
        - DynamicTileGenerator.GenerateTileAsync()
        - Per-request basis
        - Fast (10-50ms)

Async Server System Thread (Vintage Story API)
    │
    └─► BackgroundTileService
        - Monitors chunk changes
        - Queues tiles for regeneration
        - Non-blocking background work

HTTP Listener Thread Pool (System.Net)
    │
    └─► WebServer
        - HttpListener handles requests
        - Each request gets thread from pool
        - Max concurrent requests configurable
```

## 8. Storage Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                    Storage Systems                                │
└──────────────────────────────────────────────────────────────────┘

Filesystem (Temporary during export)
{OutputDirectory}/data/world/
    └─ {zoom}/
        └─ {x}_{z}.png
           │
           │ Imported by TileImporter
           │
           ▼

MBTiles Database (Primary storage)
{OutputDirectory}/data/tiles.mbtiles
    ├─ tiles table
    │  - zoom_level, tile_column, tile_row
    │  - tile_data (PNG blob)
    │
    ├─ metadata table
    │  - name, value (map info)
    │
    └─ WAL mode enabled
       - Concurrent read/write
       - Better performance

Historical Database
{OutputDirectory}/data/historical.db
    ├─ player_positions
    ├─ entity_census
    ├─ server_stats
    └─ death_events

Tile Generation State Database
{OutputDirectory}/data/tile_state.db
    ├─ tiles (generation status)
    ├─ queue (pending tiles)
    └─ chunks_to_tiles (mapping)

Configuration File
ModConfig/VintageAtlasConfig.json
    - User-editable settings
    - Validated on load
    - Hot-reload via API
```

---

## Legend

- ✅ Fully Implemented and Working
- 🟡 Implemented but Needs Testing
- ❌ Not Yet Implemented
- 📋 Planned for Future

---

These diagrams show the complete VintageAtlas architecture from multiple perspectives. The system is well-designed with clear separation of concerns and proper threading.


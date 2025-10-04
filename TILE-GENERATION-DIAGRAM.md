# VintageAtlas - Tile Generation Architecture Diagrams

## Current System (Complex & Inefficient)

```
┌────────────────────────────────────────────────────────────────────────────┐
│                          TILE GENERATION FLOW                               │
│                           (Current System)                                  │
└────────────────────────────────────────────────────────────────────────────┘

                            USER TRIGGERS
                         /atlas export command
                                 │
                                 ▼
                        ┌─────────────────┐
                        │  MapExporter    │
                        │  StartExport()  │
                        └────────┬────────┘
                                 │
                                 ▼
                    ┌────────────────────────┐
                    │   Extractor.Run()      │
                    │  (1,300 lines legacy)  │
                    └────────┬───────────────┘
                             │
              ┌──────────────┼──────────────┐
              │              │              │
              ▼              ▼              ▼
    ┌─────────────┐  ┌─────────────┐  ┌──────────────┐
    │ExtractWorld │  │CreateZoom   │  │Extract       │
    │Map()        │  │Levels()     │  │Structures()  │
    └──────┬──────┘  └──────┬──────┘  └──────┬───────┘
           │                │                │
           │ Reads          │ Downsample    │ GeoJSON
           ▼                ▼                ▼
    ┌──────────────────────────────────────────────┐
    │      Vintage Story Savegame Database         │
    │        (SQLite: chunks, entities)            │
    └──────────────┬───────────────────────────────┘
                   │
                   ▼
         ╔═══════════════════════╗
         ║  RENDERING ENGINE     ║
         ║  (SkiaSharp)          ║
         ║  - Block colors       ║
         ║  - Height mapping     ║
         ║  - Hill shading       ║
         ╚═══════════════════════╝
                   │
                   ▼
         ┌──────────────────────┐
         │  PNG Tile Generation │
         │  /data/world/        │
         │    7/1234_5678.png   │
         │    6/617_2839.png    │
         │    5/308_1419.png    │
         │    ...               │
         └──────────┬───────────┘
                    │
                    │ DISK WRITE (10-15 GB)
                    │
                    ▼
         ┌──────────────────────┐
         │  File System         │
         │  PNG files on disk   │
         └──────────┬───────────┘
                    │
                    │ DISK READ
                    │
                    ▼
         ┌──────────────────────┐
         │  TileImporter        │
         │  ImportExportedTiles │
         │  (130 lines bridge)  │
         └──────────┬───────────┘
                    │
                    │ For each PNG:
                    │  - Read bytes
                    │  - Parse coords
                    │  - Insert DB
                    │
                    ▼
         ╔══════════════════════╗
         ║  MBTiles Database    ║
         ║  (SQLite)            ║
         ║  - tiles table       ║
         ║  - metadata          ║
         ╚══════════════════════╝
                    │
                    │ Result:
                    │ - PNG files (kept)
                    │ - Database (used)
                    │ - DUPLICATION!
                    │
                    └────────────────────────────────┐
                                                     │
                         ┌───────────────────────────┘
                         │
                         │ WEB REQUEST
                         │ GET /tiles/7/1234/5678.png
                         │
                         ▼
              ┌─────────────────────────┐
              │  DynamicTileGenerator   │
              │  (504 lines, separate)  │
              └────────┬────────────────┘
                       │
                       ├─→ Check memory cache (ConcurrentDict)
                       │       └─→ HIT? Return
                       │
                       ├─→ Check MBTiles DB
                       │       └─→ HIT? Cache + Return
                       │
                       └─→ Generate from loaded chunks
                               │
                               ├─→ ChunkDataExtractor (main thread)
                               │       └─→ Read game state
                               │
                               ├─→ RenderTileFromSnapshot (background)
                               │       └─→ SkiaSharp render
                               │
                               └─→ Store in DB + Cache + Return


┌────────────────────────────────────────────────────────────────────────────┐
│                            PROBLEMS:                                        │
├────────────────────────────────────────────────────────────────────────────┤
│  ❌  Two separate rendering implementations (Extractor vs Dynamic)         │
│  ❌  Double I/O: Savegame → PNG → Disk → Read → Database                  │
│  ❌  Wasted disk space (10-15 GB PNG files kept after import)             │
│  ❌  Slow export (PNG encode + write + read + DB insert)                  │
│  ❌  Code duplication (~1,800 lines across 3 files)                       │
│  ❌  Maintenance burden (fix bugs in two places)                          │
│  ❌  No coordination between export and dynamic generation                │
└────────────────────────────────────────────────────────────────────────────┘
```

---

## Proposed System (Simplified & Efficient)

```
┌────────────────────────────────────────────────────────────────────────────┐
│                          TILE GENERATION FLOW                               │
│                         (Proposed Unified System)                           │
└────────────────────────────────────────────────────────────────────────────┘

                            USER TRIGGERS
                         /atlas export command
                                 │
                                 ▼
                        ┌─────────────────┐
                        │  MapExporter    │
                        │  (Simplified)   │
                        └────────┬────────┘
                                 │
                                 ▼
                        ╔═════════════════════════╗
                        ║  UnifiedTileGenerator   ║
                        ║  (New - 600 lines)      ║
                        ║                         ║
                        ║  Single Implementation  ║
                        ║  for ALL tile gen       ║
                        ╚═══════════╦═════════════╝
                                    │
                      ┌─────────────┼─────────────┐
                      │                           │
                      ▼                           ▼
              ┌───────────────┐          ┌───────────────┐
              │ FULL EXPORT   │          │ ON-DEMAND     │
              │ (All tiles)   │          │ (Single tile) │
              └───────┬───────┘          └───────┬───────┘
                      │                          │
                      ▼                          ▼
            ╔═══════════════════════╗  ╔═══════════════════════╗
            ║ IChunkDataSource      ║  ║ IChunkDataSource      ║
            ║ (Strategy Pattern)    ║  ║ (Strategy Pattern)    ║
            ╚═══════════════════════╝  ╚═══════════════════════╝
                      │                          │
              ┌───────┴────────┐         ┌──────┴────────┐
              ▼                ▼         ▼               ▼
    ┌──────────────┐  ┌──────────────┐  ┌─────────┐  ┌─────────┐
    │ Savegame DB  │  │ All chunks   │  │ Loaded  │  │ Game    │
    │ Direct access│  │ Parallel     │  │ Chunks  │  │ State   │
    └──────┬───────┘  └──────┬───────┘  └────┬────┘  └────┬────┘
           │                 │               │            │
           └─────────┬───────┘               └──────┬─────┘
                     │                              │
                     ▼                              ▼
           ┌──────────────────┐         ┌──────────────────┐
           │ Extract chunks   │         │ Extract chunks   │
           │ for tile area    │         │ for tile area    │
           └─────────┬────────┘         └─────────┬────────┘
                     │                            │
                     └───────────┬────────────────┘
                                 │
                                 ▼
                      ╔═══════════════════════╗
                      ║  UNIFIED RENDERER     ║
                      ║  (SkiaSharp)          ║
                      ║                       ║
                      ║  Single codebase:     ║
                      ║  - Block colors       ║
                      ║  - Height mapping     ║
                      ║  - Hill shading       ║
                      ║  - Water edges        ║
                      ╚═══════════╦═══════════╝
                                  │
                                  │ Render tile
                                  │
                                  ▼
                      ┌────────────────────┐
                      │  PNG byte[] data   │
                      │  (in memory only)  │
                      └──────────┬─────────┘
                                 │
                                 │ NO DISK WRITE!
                                 │
                                 ▼
                      ╔══════════════════════╗
                      ║  MBTiles Database    ║
                      ║  (SQLite)            ║
                      ║                      ║
                      ║  Direct storage:     ║
                      ║  - tiles table       ║
                      ║  - metadata          ║
                      ║  - ETags             ║
                      ╚══════════╦═══════════╝
                                 │
                                 │ Result:
                                 │ - ONLY database
                                 │ - No PNG files
                                 │ - 50% less disk
                                 │
                ┌────────────────┴────────────────┐
                │                                 │
                │ CACHE LAYERS                    │
                │                                 │
                ├─→ Memory Cache (100 tiles)      │
                │       └─→ ConcurrentDictionary  │
                │                                 │
                ├─→ MBTiles DB (all tiles)        │
                │       └─→ SQLite with WAL       │
                │                                 │
                └─→ Browser Cache (IndexedDB)     │
                        └─→ Frontend caching      │
                                 │
                                 │
                         ┌───────┴────────┐
                         │                │
                         ▼                ▼
              ┌─────────────────┐  ┌─────────────────┐
              │  WEB REQUEST    │  │  WEB REQUEST    │
              │  (first time)   │  │  (cached)       │
              └────────┬────────┘  └────────┬────────┘
                       │                    │
                       ▼                    ▼
              ┌─────────────────┐  ┌─────────────────┐
              │  Return tile    │  │  304 Not        │
              │  200 OK         │  │  Modified       │
              │  + ETag         │  │  (instant)      │
              └─────────────────┘  └─────────────────┘


┌────────────────────────────────────────────────────────────────────────────┐
│  SEPARATE CONCERN: GeoJSON Export                                          │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│                        ┌──────────────────┐                                │
│                        │ GeoJsonExporter  │                                │
│                        │ (Extracted)      │                                │
│                        └────────┬─────────┘                                │
│                                 │                                           │
│                    ┌────────────┼────────────┐                            │
│                    │            │            │                             │
│                    ▼            ▼            ▼                             │
│            ┌──────────┐  ┌──────────┐  ┌──────────┐                      │
│            │ Traders  │  │Translocat│  │  Signs   │                      │
│            │.geojson  │  │ors.geojson│  │.geojson  │                     │
│            └──────────┘  └──────────┘  └──────────┘                      │
│                                                                             │
│  Clean separation: tiles vs GeoJSON are independent concerns               │
└────────────────────────────────────────────────────────────────────────────┘


┌────────────────────────────────────────────────────────────────────────────┐
│                            BENEFITS:                                        │
├────────────────────────────────────────────────────────────────────────────┤
│  ✅  Single rendering implementation (bugs fixed once)                     │
│  ✅  Direct to database (no intermediate files)                           │
│  ✅  50% disk space savings (no PNG duplication)                          │
│  ✅  25-33% faster exports (eliminate double I/O)                         │
│  ✅  33% less code (3,000 → 2,000 lines)                                  │
│  ✅  Easier maintenance (one rendering path)                              │
│  ✅  Better caching (memory → DB → browser)                               │
│  ✅  Enables incremental export (regenerate changed tiles only)           │
│  ✅  Progress tracking (API endpoints)                                    │
└────────────────────────────────────────────────────────────────────────────┘
```

---

## Data Flow Comparison

### Current System (Inefficient)

```
┌──────────────────────────────────────────────────────────────────────┐
│  DATA FLOW: /atlas export                                            │
└──────────────────────────────────────────────────────────────────────┘

Savegame DB
    ↓ (SQL query)
  Chunks
    ↓ (Extractor.ExtractWorldMap)
  Bitmap (SkiaSharp)
    ↓ (PNG encode)
  PNG bytes
    ↓ (File.WriteAllBytes)
  Disk: /data/world/7/1234_5678.png
    ↓ (File.ReadAllBytes)
  PNG bytes
    ↓ (TileImporter)
  MBTiles DB
    ↓ (SQL INSERT)
  ✓ Stored

TIME: ~350ms per tile
DISK: Write + Read + Delete (optional)
```

### Proposed System (Efficient)

```
┌──────────────────────────────────────────────────────────────────────┐
│  DATA FLOW: /atlas export                                            │
└──────────────────────────────────────────────────────────────────────┘

Savegame DB
    ↓ (SQL query)
  Chunks
    ↓ (UnifiedTileGenerator.RenderTile)
  Bitmap (SkiaSharp)
    ↓ (PNG encode)
  PNG bytes (in memory)
    ↓ (MBTilesStorage.PutTileAsync)
  MBTiles DB
    ↓ (SQL INSERT)
  ✓ Stored

TIME: ~150ms per tile
DISK: Write only (57% faster!)
```

---

## Component Architecture

### Current System

```
┌─────────────────────────────────────────────────────────────────────┐
│                      Component Diagram (Current)                     │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────┐         ┌─────────────────┐
│  MapExporter    │─────────│  Extractor      │
│  (110 lines)    │         │  (1,300 lines)  │
└─────────────────┘         └────────┬────────┘
                                     │
                            ┌────────┼────────┐
                            │        │        │
                            ▼        ▼        ▼
                    ┌───────────┐ ┌──────┐ ┌──────┐
                    │SavegameDB │ │Color │ │Blur  │
                    │Loader     │ │Cache │ │Tool  │
                    └───────────┘ └──────┘ └──────┘

┌─────────────────┐
│ TileImporter    │ (Bridges PNG → DB)
│ (130 lines)     │
└─────────────────┘

┌─────────────────┐         ┌─────────────────┐
│ TileController  │─────────│ DynamicTile     │
│ (Web)           │         │ Generator       │
└─────────────────┘         │ (504 lines)     │
                            └────────┬────────┘
                                     │
                            ┌────────┼────────┐
                            │        │        │
                            ▼        ▼        ▼
                    ┌───────────┐ ┌──────┐ ┌──────┐
                    │ChunkData  │ │Color │ │Pyramid│
                    │Extractor  │ │Cache │ │Down  │
                    └───────────┘ └──────┘ │sampler│
                                            └──────┘

┌──────────────────────────────────────┐
│  MBTilesStorage (shared)             │
└──────────────────────────────────────┘

PROBLEMS:
- Two rendering paths (Extractor vs DynamicTileGenerator)
- Different coordinate handling
- Code duplication (color cache, downsampling)
- Bridge component needed (TileImporter)
```

### Proposed System

```
┌─────────────────────────────────────────────────────────────────────┐
│                      Component Diagram (Proposed)                    │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────┐         ╔═══════════════════════╗
│  MapExporter    │─────────║ UnifiedTileGenerator  ║
│  (Simplified)   │         ║ (600 lines)           ║
└─────────────────┘         ║                       ║
                            ║  - RenderTile()       ║
┌─────────────────┐         ║  - ExportFullMap()    ║
│ TileController  │─────────║  - GetTile()          ║
│ (Web)           │         ║  - InvalidateTile()   ║
└─────────────────┘         ╚═══════════╦═══════════╝
                                        │
                            ┌───────────┼───────────┐
                            │           │           │
                            ▼           ▼           ▼
                    ┌───────────┐ ┌──────────┐ ┌──────────┐
                    │IChunkData │ │BlockColor│ │Pyramid   │
                    │Source     │ │Cache     │ │Downsampler│
                    │(Interface)│ └──────────┘ └──────────┘
                    └─────┬─────┘
                          │
                ┌─────────┴─────────┐
                │                   │
                ▼                   ▼
        ┌───────────────┐   ┌───────────────┐
        │ SavegameData  │   │ LoadedChunks  │
        │ Source        │   │ DataSource    │
        │ (Export)      │   │ (On-demand)   │
        └───────────────┘   └───────────────┘

┌─────────────────┐
│ GeoJsonExporter │ (Extracted, separate concern)
│ (400 lines)     │
└─────────────────┘

┌──────────────────────────────────────┐
│  MBTilesStorage (shared)             │
└──────────────────────────────────────┘

BENEFITS:
- Single rendering path (UnifiedTileGenerator)
- Strategy pattern (IChunkDataSource)
- No duplication
- No bridge component needed
- Cleaner separation of concerns
```

---

## Timeline Visualization

```
┌────────────────────────────────────────────────────────────────────────┐
│                    Implementation Timeline                              │
└────────────────────────────────────────────────────────────────────────┘

Week 1: Foundation
├─ Create IChunkDataSource interface
├─ Implement SavegameDataSource
├─ Implement LoadedChunksDataSource
└─ UnifiedTileGenerator skeleton
        │
        ▼
Week 2: Rendering
├─ Port tile rendering logic
├─ Consolidate coordinate transforms
├─ Integrate BlockColorCache
└─ Add comprehensive logging
        │
        ▼
Week 3: Export Pipeline
├─ Full export flow
├─ Progress reporting
├─ MapExporter integration
└─ Test with various world sizes
        │
        ▼
Week 4: Validation
├─ Side-by-side comparison tests
├─ Performance benchmarks
├─ Memory profiling
└─ Coordinate validation
        │
        ▼
Week 5: Integration
├─ Switch MapExporter to new system
├─ Update documentation
├─ Remove old code (Extractor, TileImporter)
└─ Extract GeoJson to separate component
        │
        ▼
Week 6: Advanced Features
├─ Incremental export
├─ Export progress API
├─ Background generation queue
└─ Frontend integration
        │
        ▼
    PRODUCTION READY ✅
```

---

## Performance Comparison Chart

```
┌────────────────────────────────────────────────────────────────────────┐
│                       Export Performance                                │
│                     (10,000 tile export)                                │
└────────────────────────────────────────────────────────────────────────┘

Current System:
    Render  PNG Encode  Disk Write  Disk Read  DB Write
    ▓▓▓▓▓   ▓▓▓         ▓▓▓▓        ▓▓          ▓▓
    100ms   50ms        100ms       50ms        50ms
    └─────────────────────────────────────────────┘
                       350ms per tile
                       
    10,000 tiles × 350ms = 3,500s = 58 minutes


Proposed System:
    Render  PNG Encode  DB Write
    ▓▓▓▓▓   ▓▓▓         ▓▓
    100ms   50ms        50ms
    └─────────────────────────┘
             150ms per tile
             
    10,000 tiles × 150ms = 1,500s = 25 minutes
    
    
    IMPROVEMENT: 57% faster! ✅


┌────────────────────────────────────────────────────────────────────────┐
│                       Disk Space Usage                                  │
│                     (Large world example)                               │
└────────────────────────────────────────────────────────────────────────┘

Current System:
    PNG Files:           ████████████████████████ 12 GB
    MBTiles DB:          ████████████████████████ 12 GB
                         ─────────────────────────────
    Total:               ████████████████████████████████████████████████ 24 GB
    

Proposed System:
    PNG Files:                                     0 GB
    MBTiles DB:          ████████████████████████ 12 GB
                         ─────────────────────────────
    Total:               ████████████████████████ 12 GB
    
    
    IMPROVEMENT: 50% less disk space! ✅


┌────────────────────────────────────────────────────────────────────────┐
│                       Code Complexity                                   │
└────────────────────────────────────────────────────────────────────────┘

Current System:
    Extractor.cs:        ████████████████████████████████ 1,300 lines
    TileImporter.cs:     ███ 130 lines
    DynamicTileGen.cs:   ████████████████ 504 lines
    Supporting code:     ███████████████████ 800 lines
                         ───────────────────────────────────────────
    Total:               ████████████████████████████████████████████████████ 2,734 lines
    

Proposed System:
    UnifiedTileGen.cs:   ████████████████████ 600 lines
    GeoJsonExporter.cs:  █████████████ 400 lines
    Supporting code:     ███████████████████ 800 lines
                         ───────────────────────────────────────────
    Total:               ████████████████████████████████████ 1,800 lines
    
    
    IMPROVEMENT: 34% less code! ✅
```

---

**End of Diagrams** ✨

These diagrams complement the detailed analysis in `TILE-GENERATION-ANALYSIS.md`


# Architecture

VintageAtlas follows a modular architecture, separating the core mod logic, data extraction, and web presentation.

## High-Level Overview

```mermaid
graph TD
    VS[Vintage Story Server] -->|Mod API| Mod[VintageAtlas Mod]
    Mod -->|Initializes| Core[Core Components]
    Mod -->|Initializes| Web[Web Server]
    
    subgraph "Core Components"
        Exporter[Map Exporter]
        Storage[MBTiles Storage]
        ColorCache[Block Color Cache]
    end
    
    subgraph "Web Server"
        API[API Endpoints]
        Static[Static File Server]
        Socket[WebSocket (Planned)]
    end
    
    subgraph "Frontend (Vue.js)"
        Map[OpenLayers Map]
        UI[UI Components]
        Store[Pinia Store]
    end
    
    Web -->|Serves| Frontend
    Frontend -->|Requests Tiles/Data| API
    Exporter -->|Writes| Storage
    API -->|Reads| Storage
```

## Backend (C#)

The backend is structured using Clean Architecture principles:

- **Core**: Contains domain entities, interfaces, and business rules.
- **Application**: Contains use cases (e.g., `ExportMapUseCase`).
- **Infrastructure**: Implementations of interfaces (e.g., `VintageStory` API adapters, `MbTilesStorage`).
- **Web**: The HTTP server and API controllers.

### Key Components

- **VintageAtlasModSystem**: The entry point. Initializes the mod and handles lifecycle events.
- **ServerManager**: Manages the lifecycle of the web server.
- **MapExporter**: Handles the extraction of chunk data and rendering into tiles.
- **UnifiedTileGenerator**: Renders chunks into images.
- **MbTilesStorage**: Stores rendered tiles in a SQLite database (MBTiles format).

## Frontend (Vue.js)

The frontend is a Single Page Application (SPA) built with Vue 3 and Vite.

- **OpenLayers**: Used for the map interface. It consumes the tiles served by the backend.
- **Pinia**: Manages application state (e.g., current player positions, map settings).
- **Tailwind CSS**: Used for styling.

## Data Flow

1. **Map Generation**:
   - The `MapExporter` reads chunk data from the game.
   - `UnifiedTileGenerator` converts blocks to colors using `BlockColorCache`.
   - Tiles are saved to `tiles.mbtiles`.

2. **Map Serving**:
   - The user opens the web interface.
   - OpenLayers requests tiles (XYZ format).
   - `TileController` reads from `tiles.mbtiles` and serves the images.

3. **Live Data**:
   - The frontend polls (or uses WebSockets in the future) the API for player positions.
   - The `GeoJsonController` returns data in GeoJSON format.

# VintageAtlas

VintageAtlas is a Vintage Story mod that provides a real-time web-based map of your server. It runs a web server directly within the game server process, serving a modern Vue.js application that displays the world map, player positions, and other data.

## Key Features

- **Real-time Web Map**: View the server map in any web browser.
- **Dynamic Rendering**: Automatically generates map tiles from game chunks.
- **Player Tracking**: See player locations in real-time.
- **Waypoints**: View server and player waypoints.
- **Layer Support**: Toggle different layers (e.g., rainfall, temperature - *planned*).
- **Admin Dashboard**: Manage map settings and exports.

## Tech Stack

### Backend (Mod)
- **Language**: C# (.NET 7.0)
- **Framework**: Vintage Story Mod API
- **Web Server**: Custom `HttpListener` based implementation.
- **Data Storage**: SQLite (via MBTiles) for map tiles.
- **Architecture**: Clean Architecture (Core, Application, Infrastructure, Web).

### Frontend (Web App)
- **Framework**: Vue 3
- **Build Tool**: Vite
- **Language**: TypeScript
- **Styling**: Tailwind CSS
- **Map Library**: OpenLayers (`ol`)
- **State Management**: Pinia
- **Routing**: Vue Router

## Installation

1. Download the `VintageAtlas.zip` mod file.
2. Place it in your Vintage Story `Mods` folder.
3. Start the server.
4. Access the map at `http://localhost:8000` (default port).

## Configuration

Configuration is handled via `ModConfig` and can be adjusted in the `VintageAtlas.json` config file or via the Admin Dashboard.

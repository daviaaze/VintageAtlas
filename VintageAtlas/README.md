# VintageAtlas

**A comprehensive mapping and server monitoring solution for Vintage Story**

[![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)](https://github.com/daviaaze/VintageAtlas/releases)
[![Vintage Story](https://img.shields.io/badge/Vintage%20Story-1.20.1+-green.svg)](https://www.vintagestory.at)
[![License](https://img.shields.io/badge/license-see%20LICENSE-orange.svg)](LICENSE)

## Features

### 🗺️ Dynamic Map Generation
- **Multiple Rendering Modes**: Medieval style, hill shading, color variations
- **Automatic Zoom Levels**: Pre-generated tile pyramids for smooth zooming
- **Structure Export**: Traders, translocators, and custom signs
- **GeoJSON Support**: Export markers as standard GeoJSON for custom overlays
- **High Performance**: Multi-threaded export utilizing all CPU cores

### 🌐 Live Web Server
- **Real-time Player Tracking**: Monitor player positions, health, hunger, and temperature
- **Animal Tracking**: View nearby wildlife and entities with environmental data
- **Weather Information**: Current weather conditions at spawn
- **Admin Dashboard**: Control map exports and monitor server status
- **RESTful API**: Integrate with external tools and dashboards
- **No PHP Required**: Built-in C# HTTP server serves everything

### 📊 Historical Tracking
- **Player Activity Heatmaps**: Visualize where players spend time
- **Path Tracking**: See player movement history over time
- **Entity Census**: Track animal populations and spawns
- **Death Events**: Record and analyze player deaths with causes
- **Server Statistics**: Monitor performance and player activity

### ⚙️ Production Ready
- **Request Throttling**: Built-in DoS protection
- **Configurable Caching**: Optimized for performance
- **Runtime Configuration**: Toggle features via web API without restart
- **CORS Support**: Enable cross-origin requests for integrations
- **Nginx Compatible**: Works behind reverse proxies with sub-path support

## Installation

### Server Installation

1. Download the latest `VintageAtlas-v1.0.0.zip` from releases
2. Extract to your server's `Mods/` directory
3. Start the server to generate default configuration
4. Edit `ModConfig/VintageAtlasConfig.json` to customize settings
5. Restart the server

### Configuration

The mod will create `ModConfig/VintageAtlasConfig.json` with sensible defaults:

```json
{
  "Mode": 4,
  "OutputDirectory": "/path/to/output",
  "EnableLiveServer": true,
  "LiveServerPort": 42421,
  "AutoExportMap": true,
  "MapExportIntervalMs": 300000,
  "EnableHistoricalTracking": true,
  "ExtractWorldMap": true,
  "ExtractStructures": true,
  "CreateZoomLevels": true
}
```

**Key Settings:**
- `Mode`: Image rendering mode (4 = Medieval style with hill shading, recommended)
- `OutputDirectory`: Where map tiles and web files are saved
- `EnableLiveServer`: Enable the built-in web server
- `LiveServerPort`: HTTP server port (default: game port + 1)
- `AutoExportMap`: Automatically re-export map periodically
- `MapExportIntervalMs`: How often to auto-export (5 minutes default)

For full configuration options, see [Configuration Guide](docs/CONFIGURATION.md)

## Usage

### Commands

#### Map Export
```
/atlas export
/va export
```
Starts a manual map export. Requires `controlserver` privilege.

### Accessing the Web Interface

Once the server is running with `EnableLiveServer: true`:

1. **Local Access**: `http://localhost:<port>/`
2. **Network Access**: `http://<server-ip>:<port>/`
3. **Admin Dashboard**: `http://<server-ip>:<port>/adminDashboard.html`

### API Endpoints

- `GET /api/status` - Current server status, players, animals
- `GET /api/health` - Quick health check
- `GET /api/config` - Current runtime configuration
- `POST /api/config` - Update runtime configuration
- `POST /api/export` - Trigger manual export
- `GET /api/heatmap?player=UUID&hours=24` - Player activity heatmap
- `GET /api/player-path?player=UUID` - Player movement history
- `GET /api/census?entity=wolf&hours=24` - Entity census data
- `GET /api/stats` - Historical statistics

## Block Colors

For accurate map colors matching your texture pack and mods:

1. Install [WebCartographerColorExporter](https://mods.vintagestory.at/wcce) on your client
2. Join the server
3. Run command: `/exportcolors`
4. Colors will be sent to the server and a map export will start

You only need to do this once, or when you change texture packs/mods.

## Architecture

VintageAtlas uses a clean, modular architecture:

```
VintageAtlas/
├── Core/              # Configuration and interfaces
├── Models/            # Data models
├── Export/            # Map tile generation
├── Tracking/          # Historical data tracking
├── Web/
│   ├── Server/        # HTTP server
│   └── API/           # REST controllers
└── Commands/          # Chat commands
```

## Performance

Tested on: i7-8700K, 32 GiB RAM, 1TB SSD

**34.8 GB Savegame:**
- Total runtime: ~22 minutes
- Chunk preparation: ~1.5 minutes
- Image generation: ~15.5 minutes
- Structure export: ~5 minutes

## Deployment

### Standalone

The built-in web server serves everything you need. Just start your Vintage Story server and access the web interface.

### Behind Nginx

For production deployments, see [Deployment Guide](docs/DEPLOYMENT.md) for:
- Nginx reverse proxy configuration
- SSL/TLS setup
- Sub-path hosting
- Performance tuning

## Development

### Building from Source

**Requirements:**
- .NET 8.0 SDK
- Vintage Story 1.20.1+

**With Nix (Recommended):**
```bash
nix develop
cd VintageAtlas
dotnet build --configuration Release
```

**Without Nix:**
```bash
export VINTAGE_STORY=/path/to/vintagestory
cd VintageAtlas
dotnet build --configuration Release
```

Output: `bin/Release/Mods/vintageatlas/`

## Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## Credits

- **Original WebCartographer**: [Th3Dilli](https://gitlab.com/th3dilli_vintagestory/WebCartographer)
- **Webmap Base**: [vs-webmap by Drakker](https://bitbucket.org/vs-webmap/aurafury-webmap/)
- **Refactored for Production**: daviaaze

## License

See [LICENSE](../LICENSE) file for details.

## Links

- **GitHub**: [https://github.com/daviaaze/VintageAtlas](https://github.com/daviaaze/VintageAtlas)
- **Vintage Story Mods**: [https://mods.vintagestory.at](https://mods.vintagestory.at)
- **Original WebCartographer**: [GitLab](https://gitlab.com/th3dilli_vintagestory/WebCartographer)
- **Demo Map**: [Aura Fury Ancient Paths](https://map.ap.aurafury.org/?x=0&y=0&zoom=9)

## Support

- **Issues**: [GitHub Issues](https://github.com/daviaaze/VintageAtlas/issues)
- **Discussions**: [GitHub Discussions](https://github.com/daviaaze/VintageAtlas/discussions)
- **Wiki**: [Documentation](docs/)
- **Discord**: Vintage Story Official Discord


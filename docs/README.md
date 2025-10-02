# VintageAtlas Documentation

Welcome to the VintageAtlas documentation! This directory contains comprehensive documentation for developers, users, and contributors.

## 📚 Documentation Structure

### User Documentation
- [Main README](../README.md) - Project overview and quick start
- [User Guide](../VintageAtlas/README.md) - Detailed usage instructions
- [Changelog](../VintageAtlas/CHANGELOG.md) - Version history

### Developer Documentation

#### Architecture & Design
- [Architecture Overview](architecture/architecture-overview.md) - System design and component interaction
- [API Integration](architecture/api-integration.md) - Vintage Story API usage and best practices
- [Coordinate Systems](architecture/coordinate-systems.md) - Game, map, and tile coordinate transformations

#### API Documentation
- [RESTful API](api/rest-api.md) - Complete API endpoint reference
- [GeoJSON Format](api/geojson-format.md) - GeoJSON structure and usage
- [Map Configuration](api/map-config.md) - Dynamic map configuration API

#### Implementation Guides
- [Tile Generation System](implementation/tile-generation.md) - Map tile generation and caching
- [Background Services](implementation/background-services.md) - Async tile generation and tracking
- [Frontend Integration](implementation/frontend-integration.md) - Vue/OpenLayers integration

#### Developer Guides
- [Vintage Story Modding Constraints](guides/vintagestory-modding-constraints.md) - API constraints, threading, and best practices

#### Development
- [Setup Guide](development/setup.md) - Development environment setup
- [Building from Source](development/building.md) - Build instructions
- [Testing Guide](development/testing.md) - Testing procedures
- [Contributing](../CONTRIBUTING.md) - How to contribute

### Archive
- [archive/](archive/) - Historical documentation and implementation notes

## 🎯 Quick Navigation

### For New Users
Start here:
1. [Main README](../README.md) - Overview and installation
2. [User Guide](../VintageAtlas/README.md) - Configuration and usage
3. [API Reference](api/rest-api.md) - If integrating with other tools

### For Developers
Start here:
1. [Architecture Overview](architecture/architecture-overview.md) - Understand the system
2. [Setup Guide](development/setup.md) - Set up development environment
3. [Building from Source](development/building.md) - Build the mod
4. [Contributing](../CONTRIBUTING.md) - Contribution guidelines

### For Mod Developers
Start here:
1. [Vintage Story Modding Constraints](guides/vintagestory-modding-constraints.md) - Essential API constraints and patterns
2. [API Integration](architecture/api-integration.md) - How we use VS API
3. [Architecture Overview](architecture/architecture-overview.md) - Learn from our implementation

## 🔍 Finding Documentation

### By Topic

**Installation & Setup:**
- Installation instructions → [User Guide](../VintageAtlas/README.md#installation)
- Development setup → [Setup Guide](development/setup.md)
- Building from source → [Building Guide](development/building.md)

**Configuration:**
- Basic configuration → [User Guide](../VintageAtlas/README.md#configuration)
- Map configuration → [Map Config API](api/map-config.md)
- Advanced options → [Architecture Overview](architecture/architecture-overview.md)

**Usage:**
- Basic usage → [User Guide](../VintageAtlas/README.md#usage)
- Chat commands → [User Guide](../VintageAtlas/README.md#commands)
- Web interface → [User Guide](../VintageAtlas/README.md#accessing-the-web-interface)

**API:**
- API overview → [REST API](api/rest-api.md)
- GeoJSON data → [GeoJSON Format](api/geojson-format.md)
- Map configuration → [Map Config API](api/map-config.md)

**Technical Details:**
- System architecture → [Architecture Overview](architecture/architecture-overview.md)
- VS API constraints → [Modding Constraints](guides/vintagestory-modding-constraints.md)
- Coordinate systems → [Coordinate Systems](architecture/coordinate-systems.md)
- Tile generation → [Tile Generation](implementation/tile-generation.md)
- Background services → [Background Services](implementation/background-services.md)

**Development:**
- Setting up → [Setup Guide](development/setup.md)
- Building → [Building Guide](development/building.md)
- Testing → [Testing Guide](development/testing.md)
- Contributing → [Contributing](../CONTRIBUTING.md)

### By File Type

**Markdown Documentation:**
- User-facing: `../README.md`, `../VintageAtlas/README.md`
- Developer-facing: `architecture/`, `implementation/`, `development/`
- API reference: `api/`

**Code Documentation:**
- Inline comments in C# files
- TypeScript interfaces and JSDoc comments
- Component documentation in Vue SFCs

## 📝 Documentation Standards

When creating or updating documentation:

1. **Location:**
   - User docs: Root or VintageAtlas/ directory
   - Developer docs: docs/ subdirectories
   - Historical: docs/archive/

2. **Format:**
   - Use Markdown (.md)
   - Include table of contents for long documents
   - Use code blocks with language specification
   - Include diagrams where helpful

3. **Structure:**
   - Start with overview/summary
   - Organize with clear headings
   - Include examples
   - Link to related documents

4. **Maintenance:**
   - Update when code changes
   - Move outdated docs to archive/
   - Include date/version where relevant
   - Keep README.md index updated

## 🔗 External Resources

### Vintage Story
- [Official Wiki](https://wiki.vintagestory.at/)
- [API Documentation](https://apidocs.vintagestory.at/)
- [Modding Guide](https://wiki.vintagestory.at/Modding:Getting_Started)
- [Official Discord](https://discord.gg/vintagestory)

### OpenLayers
- [Official Documentation](https://openlayers.org/)
- [Examples](https://openlayers.org/en/latest/examples/)
- [API Reference](https://openlayers.org/en/latest/apidoc/)

### Development Tools
- [.NET Documentation](https://docs.microsoft.com/dotnet/)
- [Vue 3 Documentation](https://vuejs.org/)
- [TypeScript Documentation](https://www.typescriptlang.org/)
- [Nix Manual](https://nixos.org/manual/nix/stable/)

## 🆘 Getting Help

1. **Documentation Issues:**
   - Check this index for related docs
   - Search within relevant docs
   - Check archive for historical context

2. **Code Issues:**
   - Review architecture docs
   - Check API documentation
   - Look at code comments
   - Review test cases

3. **Still Stuck?**
   - [GitHub Issues](https://github.com/daviaaze/VintageAtlas/issues)
   - [GitHub Discussions](https://github.com/daviaaze/VintageAtlas/discussions)
   - Vintage Story Official Discord

## 📊 Documentation Status

| Category | Status | Last Updated |
|----------|--------|--------------|
| User Guide | ✅ Complete | 2025-10-02 |
| Architecture | ✅ Complete | 2025-10-02 |
| API Reference | ✅ Complete | 2025-10-02 |
| Implementation Guides | ✅ Complete | 2025-10-02 |
| Development Setup | ✅ Complete | 2025-10-02 |
| Testing Guide | ⚠️ Partial | 2025-10-02 |
| Deployment Guide | 📋 Planned | - |

## 🔄 Recent Updates

- **2025-10-02**: Consolidated and organized all documentation
- **2025-10-02**: Added comprehensive .cursorrules
- **2025-10-02**: Created structured docs/ directory

---

**Last Updated:** 2025-10-02  
**Version:** 1.0.0  
**Maintainer:** daviaaze


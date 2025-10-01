# Changelog

All notable changes to VintageAtlas will be documented in this file.

## [1.0.0] - 2025-10-01

### 🎉 Initial Release - Complete Refactoring

This is the first production release of **VintageAtlas** (formerly WebCartographer).

### Added

- **Clean Architecture**: Refactored into modular components (Core, Web, Export, Tracking)
- **Production-Ready Configuration**: Comprehensive validation and auto-fixes
- **Enhanced Web Server**: Separated concerns with proper routing and controllers
- **RESTful API**: Well-structured endpoints for all features
- **Improved Documentation**: Production-ready README and deployment guides
- **Better Error Handling**: Graceful failures with helpful error messages

### Changed

- **Renamed**: `WebCartographer` → `VintageAtlas`
- **Namespace**: `WebCartographer.*` → `VintageAtlas.*`
- **Config File**: `WebCartographerConfig.json` → `VintageAtlasConfig.json`
- **Mod ID**: `webcartographer` → `vintageatlas`
- **Commands**: `/webcartographer` or `/webc` → `/atlas` or `/va`

### Architecture Improvements

- Separated web server logic into `WebServer`, `RequestRouter`, and controllers
- Implemented dependency injection patterns with interfaces
- Created `IDataCollector`, `IHistoricalTracker`, and `IMapExporter` interfaces
- Organized code into logical namespaces:
  - `VintageAtlas.Core` - Configuration and interfaces
  - `VintageAtlas.Models` - Data models
  - `VintageAtlas.Export` - Map generation
  - `VintageAtlas.Tracking` - Historical data
  - `VintageAtlas.Web.Server` - HTTP infrastructure
  - `VintageAtlas.Web.API` - REST controllers
  - `VintageAtlas.Commands` - Chat commands

### Technical Details

- Built with .NET 8.0
- Compatible with Vintage Story 1.20.1+
- Server-side only mod
- Zero warnings, clean compilation

### Migration from WebCartographer

If migrating from WebCartographer:

1. Remove old `webcartographer` mod
2. Install `vintageatlas` mod
3. Rename `WebCartographerConfig.json` to `VintageAtlasConfig.json`
4. Update any scripts/integrations to use new API endpoints
5. Update command usage from `/webc` to `/va`

All data formats remain compatible - existing maps and historical data will work seamlessly.

---

## Previous Versions

For WebCartographer v0.8.0 and earlier changelog, see the [original repository](https://gitlab.com/th3dilli_vintagestory/WebCartographer).

---

**Note**: Version 1.0.0 represents a complete refactoring for production use. While functionally equivalent to WebCartographer 0.8.0, the internal architecture has been completely redesigned for maintainability, extensibility, and production deployment.

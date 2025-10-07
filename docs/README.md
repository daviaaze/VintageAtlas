# VintageAtlas Documentation

**Last Updated:** October 6, 2025

---

## 🚀 Quick Start

**New to VintageAtlas?** Start here: **[Quick Start Guide](QUICKSTART.md)**

5-minute guide covering:

- Development setup
- Common commands  
- Project structure
- Troubleshooting

---

## 📁 Documentation Structure

```
docs/
├── QUICKSTART.md          # ⭐ Start here for new developers
├── architecture/          # System design and technical specs
├── guides/               # Development guides and best practices
├── implementation/       # Active implementation work
├── planning/             # Long-term planning and roadmap
└── archive/              # Historical/outdated documentation
```

---

## 🏗️ Architecture

### [Architecture Overview](architecture/architecture-overview.md)

Complete system architecture, component structure, and data flow.

**Covers:**

- High-level system design
- Component responsibilities  
- Threading model
- Performance characteristics
- Caching strategy

### [API Integration](architecture/api-integration.md)

Vintage Story API usage patterns, event handling, and threading.

**Covers:**

- Event-driven architecture
- Block change detection
- Threading patterns and constraints
- World data access
- Common pitfalls and solutions

### [Coordinate Systems](architecture/coordinate-systems.md)

Game coordinates, map coordinates, tile coordinates, and transformations.

**Covers:**

- World vs tile vs display coordinate spaces
- Absolute vs spawn-relative positioning
- Backend transformation logic
- Frontend coordinate display

---

## 📖 Development Guides

### [Vintage Story Modding Constraints](guides/vintagestory-modding-constraints.md) ⚠️

**⚠️ CRITICAL READING for all developers**

Threading rules, chunk access patterns, data persistence, and common pitfalls.

**Key topics:**

- Main thread exclusivity for game state access
- Chunk serialization timing
- Performance considerations
- API patterns and best practices
- VintageAtlas-specific implications

### [Testing Guide](guides/testing-guide.md)

Unit testing, integration testing, mocking strategies, and test coverage.

**Covers:**

- Testing architecture for VS mods
- Example tests (ConfigValidator, MapColors, TileGeneration)
- Manual testing checklist
- Frontend testing (Vitest, Playwright)

---

## 🚧 Active Implementation

### [Improvement Requirements](implementation/improvement-requirements.md) ⭐

**NEW:** Comprehensive requirements document for critical improvements to VintageAtlas.

**Covers:**

- Disable automatic regeneration (with config)
- Improve tile generation (on-demand, caching, priority)
- Fix entity display and real-time data via WebSockets
- Improve entity loading and caching
- Better entity movement tracking
- Fix player historical tracker

**Status:** Planning Complete - Ready for Implementation  
**Timeline:** 5 phases over 8-10 weeks

### [Dynamic Tile Consolidation](implementation/dynamic-tile-consolidation.md)

Current work consolidating tile generation into `DynamicTileGenerator`.

**Status:** Phase 1 Complete ✅  
**Next:** Phase 2 - Block Color Mapping

### [Tile Generation System](implementation/tile-generation.md)

Comprehensive documentation of tile generation pipeline.

**Covers:**

- Full map export process
- Dynamic on-demand generation
- Terrain rendering modes
- Pyramid downsampling
- Caching strategy

---

## 📋 Planning

### [Frontend Modernization Plan](planning/FRONTEND-PLAN.md)

Strategic plan for modernizing frontend to Vue.js 3 + TypeScript.

**Key decisions:**

- Framework: Vue.js 3 + TypeScript
- Architecture: Hybrid (embedded SPA + dev proxy)
- Timeline: 15 weeks across 5 phases
- Optional Redis caching

**Status:** Planning Complete - Ready for Implementation

---

## 🎯 Quick Reference

### For New Developers

1. **[Quick Start Guide](QUICKSTART.md)** - Setup and common commands
2. **[Vintage Story Modding Constraints](guides/vintagestory-modding-constraints.md)** - Critical rules ⚠️
3. **[Architecture Overview](architecture/architecture-overview.md)** - System design

### For Backend Work

1. **[API Integration](architecture/api-integration.md)** - VS API patterns
2. **[Modding Constraints](guides/vintagestory-modding-constraints.md)** - Threading rules ⚠️
3. **[Coordinate Systems](architecture/coordinate-systems.md)** - Map coordinate handling
4. **[Testing Guide](guides/testing-guide.md)** - How to test

### For Frontend Work

1. **[Frontend Modernization Plan](planning/FRONTEND-PLAN.md)** - Roadmap and strategy
2. **[Architecture Overview](architecture/architecture-overview.md)** - Current system
3. Review `html/` directory for current implementation

### For Active Development

1. **[Improvement Requirements](implementation/improvement-requirements.md)** - Critical improvements roadmap ⭐
2. **[Dynamic Tile Consolidation](implementation/dynamic-tile-consolidation.md)** - Current work
3. **[Tile Generation System](implementation/tile-generation.md)** - Implementation details

---

## 📚 External Resources

- [Vintage Story API Docs](https://apidocs.vintagestory.at/) - Official API reference
- [VS Wiki Modding Guide](https://wiki.vintagestory.at/index.php/Modding) - Modding tutorials
- [OpenLayers Documentation](https://openlayers.org/) - Map library docs
- [Vue 3 Documentation](https://vuejs.org/) - Frontend framework (planned)

---

## 🔧 Common Commands

```bash
# Development
nix develop              # Enter dev environment
quick-test               # Build and start test server
build-vintageatlas       # Full release build

# In-game
/atlas export            # Full map export
/atlas export incremental # Update changed tiles
/atlas status            # Show mod status
/atlas config            # Show configuration
```

---

## 📝 Document Types

| Type | Purpose | When to Read |
|------|---------|--------------|
| **QUICKSTART.md** | Fast setup guide | First time setup |
| **architecture/** | System design | Understanding how it works |
| **guides/** | Best practices | Before writing code |
| **implementation/** | Active work | Contributing to current features |
| **planning/** | Future plans | Planning new features |
| **archive/** | Historical docs | Reference only (outdated) |

---

## 🤝 Contributing

When adding new documentation:

1. **Choose the right location:**
   - Architecture changes → `architecture/`
   - Development guides → `guides/`
   - Active implementation → `implementation/`
   - Future plans → `planning/`

2. **Update this README** to link to your new document

3. **Use clear titles** and follow existing formatting

4. **Add "Last Updated" dates** to long documents

5. **Move completed work** to `archive/` when superseded

---

**Project:** [VintageAtlas on GitHub](https://github.com/daviaaze/VintageAtlas)  
**Maintained by:** daviaaze

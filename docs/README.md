# VintageAtlas Documentation

**Last Updated:** October 5, 2025

## 📁 Documentation Structure

```
docs/
├── architecture/       # System design and technical architecture
├── guides/            # Development guides and best practices
├── implementation/    # Active implementation documentation
├── planning/          # Long-term planning and feature tracking
└── archive/           # Historical/outdated documentation
```

---

## 🏗 Architecture

### [Architecture Overview](architecture/architecture-overview.md)
Complete system architecture, component structure, and data flow diagrams.

### [API Integration](architecture/api-integration.md)
Vintage Story API usage patterns, event handling, and threading model.

### [Coordinate Systems](architecture/coordinate-systems.md)
Game coordinates, map coordinates, tile coordinates, and transformations between them.

---

## 📖 Development Guides

### [Vintage Story Modding Constraints](guides/vintagestory-modding-constraints.md)
**Critical reading for all developers.** Threading rules, chunk access patterns, data persistence, and common pitfalls.

### [Testing Guide](guides/testing-guide.md)
Unit testing, integration testing, mocking strategies, and test coverage goals.

---

## 🚧 Active Implementation

### [Dynamic Tile Consolidation](implementation/dynamic-tile-consolidation.md)
Current work consolidating tile generation into `DynamicTileGenerator` with full feature parity.

---

## 📋 Planning

### [Frontend Modernization Plan](planning/FRONTEND-MODERNIZATION-PLAN.md)
Strategic plan for modernizing frontend from vanilla JS to Vue 3 + TypeScript.

### [Feature Tracking Checklist](planning/FEATURE-TRACKING.md)
Detailed implementation checklist across 5 phases (Foundation, UI/UX, Advanced Features, Performance, Documentation).

---

## 🎯 Quick Reference

### For New Developers
1. Read **[Vintage Story Modding Constraints](guides/vintagestory-modding-constraints.md)** first
2. Review **[Architecture Overview](architecture/architecture-overview.md)**
3. Check **[Testing Guide](guides/testing-guide.md)** for contributing

### For Frontend Work
1. Review **[Frontend Modernization Plan](planning/FRONTEND-MODERNIZATION-PLAN.md)**
2. Track progress in **[Feature Tracking](planning/FEATURE-TRACKING.md)**

### For Backend Work
1. Understand **[API Integration](architecture/api-integration.md)**
2. Follow **[Modding Constraints](guides/vintagestory-modding-constraints.md)**
3. Check **[Coordinate Systems](architecture/coordinate-systems.md)** for map work

---

## 📚 External Resources

- [Vintage Story API Docs](https://apidocs.vintagestory.at/)
- [VS Wiki Modding Guide](https://wiki.vintagestory.at/index.php/Modding)
- [OpenLayers Documentation](https://openlayers.org/)
- [Vue 3 Documentation](https://vuejs.org/)

---

**Project:** [VintageAtlas on GitHub](https://github.com/daviaaze/VintageAtlas)  
**Maintained by:** daviaaze
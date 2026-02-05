# Roadmap to 1.0.0

This roadmap outlines the steps required to reach the production 1.0.0 release of VintageAtlas.

## Phase 1: Core Stability & Performance (Current Focus)

- [ ] **Optimize Map Export**: Ensure map generation does not cause server lag spikes.
  - [ ] Implement chunk processing throttling.
  - [ ] Optimize `BlockColorCache` lookup.
- [ ] **Robust Web Server**: Ensure the embedded web server handles concurrent requests gracefully.
- [ ] **Data Persistence**: Verify `tiles.mbtiles` integrity handling (corruption recovery).

## Phase 2: Feature Completion

- [ ] **Real-time Player Tracking**
  - [ ] Smooth player movement interpolation on frontend.
  - [ ] Player visibility toggles (admin/user permissions).
- [ ] **Waypoints & Markers**
  - [ ] Display server waypoints.
  - [ ] Allow users to add personal markers (stored in local storage or server).
- [ ] **Environmental Layers**
  - [x] **Temperature Layer**: Visualize chunk temperature.
  - [x] **Rainfall Layer**: Visualize rainfall data.
  - [ ] **Geology Layer**: (Optional) Show rock types.
- [ ] **WebSocket Support**
  - [ ] Real-time player updates.
  - [ ] Live map updates (future).

## Phase 3: Frontend Polish (UX/UI)

- [ ] **Responsive Design**: Ensure map works well on mobile devices.
- [ ] **Search Functionality**: Search for players or coordinates.
- [ ] **Settings Panel**: Allow users to configure refresh rates, layer opacity, etc.
- [ ] **Theme Support**: Light/Dark mode (already partially supported by Tailwind).

## Phase 4: Documentation & Release

- [ ] **User Guide**: How to install and use.
- [ ] **Admin Guide**: Configuration options and performance tuning.
- [ ] **Developer Guide**: How to contribute.
- [ ] **CI/CD**: Automated builds and releases (GitHub Actions).

## Future (Post 1.0.0)

- [ ] **3D View**: (Ambitious) Isometric or 3D rendering of chunks.
- [ ] **Multi-server Support**: Centralized map for network of servers.

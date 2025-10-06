# VintageAtlas Feature Tracking & Implementation Checklist

**Document Purpose:** Track progress on frontend modernization and feature additions  
**Last Updated:** October 2, 2025  
**Current Phase:** Planning

---

## Quick Status Overview

| Phase | Status | Start Date | End Date | Progress |
|-------|--------|------------|----------|----------|
| Phase 1: Foundation | 🔴 Not Started | TBD | TBD | 0% |
| Phase 2: Enhanced UI/UX | 🔴 Not Started | TBD | TBD | 0% |
| Phase 3: Advanced Features | 🔴 Not Started | TBD | TBD | 0% |
| Phase 4: Performance | 🔴 Not Started | TBD | TBD | 0% |
| Phase 5: Documentation | 🔴 Not Started | TBD | TBD | 0% |

**Legend:** 🔴 Not Started | 🟡 In Progress | 🟢 Completed | 🔵 Blocked

---

## Phase 1: Foundation (Weeks 1-3)

### 1.1 Project Setup & Infrastructure

#### Vue.js Project Initialization
- [ ] Install Node.js (v18+ recommended)
- [ ] Install pnpm or npm
- [ ] Create Vue.js project with Vite
  ```bash
  cd VintageAtlas
  npm create vite@latest frontend -- --template vue-ts
  cd frontend
  npm install
  ```
- [ ] Configure `vite.config.ts` with proxy
- [ ] Set up `tsconfig.json` for TypeScript
- [ ] Install core dependencies:
  - [ ] `vue@^3.3.0`
  - [ ] `vue-router@^4.2.0`
  - [ ] `pinia@^2.1.0`
  - [ ] `axios@^1.5.0`
  - [ ] `@tanstack/vue-query@^5.0.0`
- [ ] Install dev dependencies:
  - [ ] `@vitejs/plugin-vue`
  - [ ] `typescript@^5.0.0`
  - [ ] `eslint` + `@typescript-eslint`
  - [ ] `prettier`
  - [ ] `vitest`

**Deliverable:** Working Vue.js dev server with hot reload

#### Build Pipeline Integration
- [ ] Configure output directory to `../html/dist`
- [ ] Create build script in `package.json`
- [ ] Create `build-frontend.sh` script
- [ ] Update `.gitignore` for node_modules and dist
- [ ] Test production build
- [ ] Verify StaticFileServer can serve built files

**Deliverable:** Automated build process

#### Project Structure Setup
```
VintageAtlas/frontend/
├── src/
│   ├── assets/          # Images, fonts, icons
│   ├── components/      # Reusable Vue components
│   │   ├── common/      # Generic components
│   │   ├── map/         # Map-specific components
│   │   └── ui/          # UI components
│   ├── composables/     # Vue composition functions
│   ├── layouts/         # Page layouts
│   ├── pages/           # Route pages
│   ├── stores/          # Pinia stores
│   ├── types/           # TypeScript type definitions
│   ├── utils/           # Utility functions
│   ├── services/        # API services
│   ├── App.vue
│   └── main.ts
├── public/              # Static assets
├── index.html
├── vite.config.ts
├── tsconfig.json
└── package.json
```

- [ ] Create folder structure
- [ ] Set up path aliases in vite.config.ts
- [ ] Create basic layout components

### 1.2 TypeScript Type Definitions

#### C# Model Mapping
- [ ] Create TypeScript interfaces for C# models:
  - [ ] `ModConfig.cs` → `types/config.ts`
  - [ ] `ServerStatusData.cs` → `types/server-status.ts`
  - [ ] `HistoricalData.cs` → `types/historical-data.ts`
  - [ ] GeoJSON features → `types/geojson.ts`

**Example:**
```typescript
// types/server-status.ts
export interface ServerStatus {
  serverName: string;
  gameVersion: string;
  modVersion: string;
  currentPlayers: number;
  maxPlayers: number;
  uptime: number;
  // ... etc
}
```

- [ ] Create API response types
- [ ] Create error types
- [ ] Set up type guards for runtime validation

**Deliverable:** Complete type system

### 1.3 API Client Implementation

#### Base API Client
- [ ] Create `services/api/client.ts` with Axios instance
- [ ] Configure base URL (from config or env)
- [ ] Add request/response interceptors
- [ ] Add error handling
- [ ] Add loading state management

```typescript
// services/api/client.ts
import axios from 'axios';

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json'
  }
});

// Add interceptors
apiClient.interceptors.response.use(
  response => response.data,
  error => {
    // Handle errors
    return Promise.reject(error);
  }
);
```

#### API Service Modules
- [ ] `services/api/status.ts` - Status API endpoints
- [ ] `services/api/historical.ts` - Historical data endpoints
- [ ] `services/api/config.ts` - Configuration endpoints
- [ ] `services/api/map.ts` - Map tile endpoints (if needed)

**Deliverable:** Type-safe API client

### 1.4 State Management with Pinia

#### Store Setup
- [ ] Create Pinia instance in `main.ts`
- [ ] Create store structure:
  - [ ] `stores/map.ts` - Map state (layers, zoom, center)
  - [ ] `stores/server.ts` - Server status and info
  - [ ] `stores/historical.ts` - Historical data
  - [ ] `stores/ui.ts` - UI state (theme, sidebar, etc.)
  - [ ] `stores/settings.ts` - User settings

**Example Store:**
```typescript
// stores/server.ts
import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { ServerStatus } from '@/types/server-status';
import { getServerStatus } from '@/services/api/status';

export const useServerStore = defineStore('server', () => {
  const status = ref<ServerStatus | null>(null);
  const loading = ref(false);
  const error = ref<Error | null>(null);

  async function fetchStatus() {
    loading.value = true;
    error.value = null;
    try {
      status.value = await getServerStatus();
    } catch (e) {
      error.value = e as Error;
    } finally {
      loading.value = false;
    }
  }

  return { status, loading, error, fetchStatus };
});
```

- [ ] Implement stores with Composition API
- [ ] Add computed properties for derived state
- [ ] Add actions for state mutations

**Deliverable:** Complete state management system

### 1.5 Router Setup

#### Basic Routing
- [ ] Install and configure Vue Router
- [ ] Create route definitions:
  - [ ] `/` - Main map view
  - [ ] `/historical` - Historical data viewer
  - [ ] `/settings` - Settings panel
  - [ ] `/admin` - Admin dashboard (if applicable)
  - [ ] `/404` - Not found page

```typescript
// router/index.ts
import { createRouter, createWebHistory } from 'vue-router';

const routes = [
  {
    path: '/',
    name: 'Home',
    component: () => import('@/pages/MapView.vue')
  },
  {
    path: '/historical',
    name: 'Historical',
    component: () => import('@/pages/HistoricalView.vue')
  },
  // ... more routes
];

export const router = createRouter({
  history: createWebHistory(),
  routes
});
```

- [ ] Set up route guards (if needed)
- [ ] Configure navigation guards
- [ ] Test routing

**Deliverable:** Working routing system

### 1.6 Map Component Migration

#### OpenLayers Integration
- [ ] Install OpenLayers: `npm install ol`
- [ ] Install types: `npm install -D @types/ol`
- [ ] Create `components/map/MapContainer.vue`
- [ ] Set up basic map initialization
- [ ] Implement tile layer loading
- [ ] Add map controls (zoom, etc.)

```vue
<!-- components/map/MapContainer.vue -->
<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue';
import Map from 'ol/Map';
import View from 'ol/View';
import TileLayer from 'ol/layer/Tile';
import XYZ from 'ol/source/XYZ';

const mapRef = ref<HTMLDivElement>();
let map: Map | null = null;

onMounted(() => {
  if (!mapRef.value) return;
  
  map = new Map({
    target: mapRef.value,
    layers: [
      new TileLayer({
        source: new XYZ({
          url: '/tiles/{z}/{x}/{y}.png'
        })
      })
    ],
    view: new View({
      center: [0, 0],
      zoom: 2
    })
  });
});

onUnmounted(() => {
  map?.setTarget(undefined);
});
</script>

<template>
  <div ref="mapRef" class="map-container"></div>
</template>

<style scoped>
.map-container {
  width: 100%;
  height: 100%;
}
</style>
```

- [ ] Test map rendering
- [ ] Verify tile loading
- [ ] Add error handling for failed tiles

**Deliverable:** Working map component

### 1.7 Development Configuration

#### Environment Variables
- [ ] Create `.env.development` file
- [ ] Create `.env.production` file
- [ ] Define variables:
  ```env
  VITE_API_BASE_URL=http://localhost:42425/api
  VITE_MAP_TILES_URL=/tiles
  VITE_WEBSOCKET_URL=ws://localhost:42425/ws
  ```

#### Proxy Configuration
- [ ] Configure Vite proxy for development
```typescript
// vite.config.ts
export default defineConfig({
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:42425',
        changeOrigin: true
      },
      '/tiles': {
        target: 'http://localhost:42425',
        changeOrigin: true
      }
    }
  }
});
```

**Deliverable:** Working dev environment

---

## Phase 2: Enhanced UI/UX (Weeks 4-6)

### 2.1 UI Framework Integration

#### Option A: Tailwind CSS
- [ ] Install Tailwind: `npm install -D tailwindcss postcss autoprefixer`
- [ ] Initialize config: `npx tailwindcss init -p`
- [ ] Configure `tailwind.config.js`
- [ ] Create design tokens
- [ ] Set up dark mode support

#### Option B: PrimeVue
- [ ] Install PrimeVue: `npm install primevue`
- [ ] Install theme: `npm install @primevue/themes`
- [ ] Configure in `main.ts`
- [ ] Import components
- [ ] Customize theme

**Decision:** Choose ONE framework
- [ ] Decision made: _______________
- [ ] Framework installed and configured

### 2.2 Component Library

#### Common Components
- [ ] `Button.vue` - Reusable button component
- [ ] `Input.vue` - Form input component
- [ ] `Select.vue` - Dropdown select
- [ ] `Modal.vue` - Modal dialog
- [ ] `Tooltip.vue` - Tooltip component
- [ ] `Loading.vue` - Loading spinner
- [ ] `ErrorMessage.vue` - Error display
- [ ] `Card.vue` - Card container

#### Map Components
- [ ] `MapControls.vue` - Map control panel
- [ ] `LayerSwitcher.vue` - Layer toggle controls
- [ ] `ZoomControls.vue` - Zoom buttons
- [ ] `MapLegend.vue` - Map legend
- [ ] `Minimap.vue` - Overview minimap
- [ ] `CoordinateDisplay.vue` - Show coordinates
- [ ] `MarkerPopup.vue` - Popup for markers

**Deliverable:** Reusable component library

### 2.3 Layout System

#### Layouts
- [ ] `MainLayout.vue` - Main application layout
  - [ ] Header/navbar
  - [ ] Sidebar
  - [ ] Main content area
  - [ ] Footer
- [ ] `MapLayout.vue` - Full-screen map layout
- [ ] `AdminLayout.vue` - Admin dashboard layout

#### Responsive Design
- [ ] Mobile breakpoints (< 768px)
- [ ] Tablet breakpoints (768px - 1024px)
- [ ] Desktop breakpoints (> 1024px)
- [ ] Test on different screen sizes

**Deliverable:** Responsive layout system

### 2.4 Theme System

#### Dark/Light Theme
- [ ] Define color palettes
- [ ] Create theme toggle component
- [ ] Store theme preference in localStorage
- [ ] Apply theme classes/CSS variables
- [ ] Test theme switching

#### Custom Themes
- [ ] Allow custom color schemes
- [ ] Theme persistence
- [ ] Theme preview

**Deliverable:** Complete theming system

### 2.5 Settings Panel

#### Settings Page/Modal
- [ ] UI preferences (theme, language)
- [ ] Map preferences (default zoom, center)
- [ ] Data refresh intervals
- [ ] Layer visibility defaults
- [ ] Export/import settings

**Deliverable:** Functional settings interface

### 2.6 Historical Data Viewer

#### Timeline Component
- [ ] Timeline visualization
- [ ] Date range picker
- [ ] Playback controls (play/pause/speed)
- [ ] Snapshot selector

#### Data Visualization
- [ ] Charts for statistics (Chart.js or D3.js?)
  - [ ] Player count over time
  - [ ] Territory changes
  - [ ] Resource tracking
- [ ] Data export (CSV, JSON)

**Deliverable:** Historical data visualization

---

## Phase 3: Advanced Features (Weeks 7-10)

### 3.1 Real-Time Updates

#### Implementation Strategy
- [ ] **Decision:** WebSocket / SSE / Polling?
- [ ] Chosen: _______________

#### If WebSocket (SignalR)
- [ ] Install `@microsoft/signalr` on frontend
- [ ] Create SignalR hub on backend
- [ ] Implement connection management
- [ ] Handle reconnection logic
- [ ] Subscribe to real-time events

#### If Server-Sent Events (SSE)
- [ ] Create SSE endpoint in C#
- [ ] Implement EventSource in Vue
- [ ] Handle connection and errors
- [ ] Parse and dispatch events

#### If Enhanced Polling
- [ ] Implement smart polling (backoff)
- [ ] Use TanStack Query for automatic refetching
- [ ] Add visibility detection (pause when tab inactive)

**Deliverable:** Real-time data updates

### 3.2 Advanced Filtering & Search

#### Filter System
- [ ] Player name search
- [ ] Coordinate search
- [ ] Marker type filtering
- [ ] Date range filtering
- [ ] Custom query builder

#### Search Component
- [ ] Autocomplete search
- [ ] Recent searches
- [ ] Search suggestions
- [ ] Result highlighting

**Deliverable:** Comprehensive search/filter system

### 3.3 Multi-Layer Map Comparison

#### Side-by-Side View
- [ ] Split screen component
- [ ] Synchronized panning
- [ ] Synchronized zooming
- [ ] Independent layer control
- [ ] Comparison slider overlay

**Deliverable:** Map comparison feature

### 3.4 Player Analytics

#### Player Heatmap
- [ ] Activity heatmap layer
- [ ] Time-based filtering
- [ ] Intensity visualization
- [ ] Export heatmap data

#### Player Statistics Dashboard
- [ ] Total playtime
- [ ] Active regions
- [ ] Travel paths
- [ ] Resource gathering stats (if available)

**Deliverable:** Player analytics dashboard

### 3.5 Custom Marker Management

#### Marker System
- [ ] Add custom markers
- [ ] Edit marker properties
- [ ] Delete markers
- [ ] Marker categories/tags
- [ ] Import/export markers
- [ ] Share markers (collaborative)

**Deliverable:** Custom marker system

### 3.6 Mobile Optimization

#### Mobile UI
- [ ] Touch-friendly controls
- [ ] Mobile navigation
- [ ] Responsive map controls
- [ ] Gesture support (pinch zoom, pan)
- [ ] PWA support (optional)

**Deliverable:** Mobile-optimized interface

---

## Phase 4: Performance & Scaling (Weeks 11-13)

### 4.1 Redis Caching Layer

#### Backend Implementation

##### Cache Abstraction
- [ ] Create `VintageAtlas/Caching/` folder
- [ ] Implement `ICacheService` interface
- [ ] Implement `RedisCacheService`
- [ ] Implement `MemoryCacheService` (fallback)
- [ ] Implement `NullCacheService` (disabled)
- [ ] Create `CacheServiceFactory`

##### Configuration
- [ ] Add Redis settings to `ModConfig.cs`:
  ```csharp
  public bool CachingEnabled { get; set; } = false;
  public string CacheProvider { get; set; } = "memory"; // redis, memory, none
  public RedisConfig? Redis { get; set; }
  public Dictionary<string, int> CacheDurations { get; set; } = new();
  ```
- [ ] Add `RedisConfig` class
- [ ] Validate Redis configuration

##### Redis Service Implementation
- [ ] Install NuGet package: `StackExchange.Redis`
- [ ] Implement connection management
- [ ] Implement get/set/delete operations
- [ ] Handle serialization/deserialization
- [ ] Add connection health checks
- [ ] Implement graceful degradation

##### Integration Points
- [ ] Cache historical data queries
- [ ] Cache player statistics
- [ ] Cache aggregated map data
- [ ] Cache configuration data
- [ ] Implement cache invalidation strategies

**Deliverable:** Working Redis cache layer

#### Testing
- [ ] Test with Redis running
- [ ] Test with Redis unavailable (fallback to memory)
- [ ] Test cache hit/miss scenarios
- [ ] Performance benchmarks
- [ ] Load testing

### 4.2 Frontend Performance Optimization

#### Code Splitting
- [ ] Lazy load routes
- [ ] Dynamic component imports
- [ ] Analyze bundle size
- [ ] Split vendor chunks

#### Asset Optimization
- [ ] Image optimization (WebP, compression)
- [ ] Icon optimization (use SVG sprites)
- [ ] Font optimization (subset, preload)
- [ ] Remove unused CSS

#### Runtime Optimization
- [ ] Virtualize large lists
- [ ] Debounce/throttle expensive operations
- [ ] Memoize computed values
- [ ] Optimize map rendering

**Deliverable:** Optimized frontend bundle

### 4.3 API Optimization

#### Backend Improvements
- [ ] Add response compression (gzip/brotli)
- [ ] Implement request batching
- [ ] Add pagination for large datasets
- [ ] Optimize database queries (if applicable)
- [ ] Add request caching headers

#### Frontend Improvements
- [ ] Use TanStack Query for caching
- [ ] Implement request deduplication
- [ ] Add optimistic updates
- [ ] Prefetch likely data

**Deliverable:** Faster API responses

### 4.4 Monitoring & Metrics

#### Performance Monitoring
- [ ] Add performance API tracking
- [ ] Track Core Web Vitals (LCP, FID, CLS)
- [ ] Monitor bundle sizes
- [ ] Track API response times

#### Error Tracking
- [ ] Add error boundary components
- [ ] Log errors to console/service
- [ ] User-friendly error messages

#### Analytics (Optional)
- [ ] Page view tracking
- [ ] Feature usage analytics
- [ ] User flow analysis

**Deliverable:** Monitoring dashboard

---

## Phase 5: Documentation & Polish (Weeks 14-15)

### 5.1 User Documentation

#### Getting Started Guide
- [ ] Installation instructions
- [ ] First-time setup
- [ ] Basic usage walkthrough
- [ ] Screenshots/GIFs

#### Feature Documentation
- [ ] Map controls guide
- [ ] Historical data viewer guide
- [ ] Settings explanation
- [ ] Advanced features guide

#### Troubleshooting
- [ ] Common issues and solutions
- [ ] FAQ section
- [ ] Error messages explained

**Deliverable:** Complete user documentation

### 5.2 Developer Documentation

#### Setup Guide
- [ ] Development environment setup
- [ ] Building from source
- [ ] Running tests
- [ ] Debugging tips

#### Architecture Documentation
- [ ] System architecture diagram
- [ ] Component hierarchy
- [ ] Data flow documentation
- [ ] State management guide

#### API Documentation
- [ ] Endpoint documentation
- [ ] Request/response examples
- [ ] Authentication (if applicable)
- [ ] Rate limiting info

#### Contributing Guide
- [ ] Code style guidelines
- [ ] PR process
- [ ] Testing requirements
- [ ] Commit message format

**Deliverable:** Developer documentation

### 5.3 API Documentation (OpenAPI/Swagger)

- [ ] Generate OpenAPI spec from C# controllers
- [ ] Set up Swagger UI (optional)
- [ ] Document all endpoints
- [ ] Add example requests/responses
- [ ] Document error codes

**Deliverable:** Interactive API docs

### 5.4 Migration Guide

#### For Users
- [ ] Differences from old version
- [ ] New features overview
- [ ] Settings migration (if applicable)

#### For Developers
- [ ] Breaking changes
- [ ] API changes
- [ ] Configuration changes

**Deliverable:** Migration documentation

### 5.5 Testing & QA

#### Manual Testing Checklist
- [ ] All features work as expected
- [ ] No console errors
- [ ] Cross-browser testing (Chrome, Firefox, Safari, Edge)
- [ ] Mobile testing (iOS, Android)
- [ ] Performance testing
- [ ] Accessibility testing (keyboard navigation, screen readers)

#### Automated Testing
- [ ] Unit test coverage > 70%
- [ ] Integration tests for critical flows
- [ ] E2E tests for main user journeys

#### Bug Fixes
- [ ] Fix all critical bugs
- [ ] Fix all high-priority bugs
- [ ] Document known issues

**Deliverable:** Tested, production-ready application

### 5.6 Deployment Preparation

#### Build Optimization
- [ ] Final bundle size check
- [ ] Production build optimization
- [ ] Source map configuration
- [ ] Environment variable setup

#### Packaging
- [ ] Create mod package
- [ ] Include built frontend
- [ ] Include documentation
- [ ] Create release notes

#### Release Process
- [ ] Version bump
- [ ] Git tag
- [ ] GitHub release
- [ ] Mod portal upload (if applicable)

**Deliverable:** Production release

---

## Technical Debt & Future Enhancements

### Known Limitations
- [ ] List any temporary solutions that need improvement
- [ ] Document any deferred features
- [ ] Note any scalability concerns

### Future Feature Ideas
- [ ] Multi-language support (i18n)
- [ ] Plugin system for custom markers
- [ ] Integration with external services
- [ ] Advanced analytics and reporting
- [ ] Collaborative features (user accounts, shared maps)
- [ ] Map annotations and drawing tools
- [ ] 3D visualization (if feasible)

---

## Dependencies & Requirements

### Frontend
```json
{
  "dependencies": {
    "vue": "^3.3.0",
    "vue-router": "^4.2.0",
    "pinia": "^2.1.0",
    "axios": "^1.5.0",
    "@tanstack/vue-query": "^5.0.0",
    "ol": "^8.0.0"
  },
  "devDependencies": {
    "@vitejs/plugin-vue": "^4.4.0",
    "typescript": "^5.2.0",
    "vite": "^5.0.0",
    "vitest": "^1.0.0"
  }
}
```

### Backend (C#)
```xml
<ItemGroup>
  <PackageReference Include="StackExchange.Redis" Version="2.7.33" />
  <PackageReference Include="System.Memory.Data" Version="8.0.0" />
</ItemGroup>
```

### System Requirements
- Node.js 18+ (for development)
- .NET SDK (for mod compilation)
- Redis (optional, for caching)

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Breaking changes in VintageStory API | Medium | High | Version compatibility checks, thorough testing |
| Performance issues with large maps | Medium | Medium | Implement tile caching, lazy loading |
| Browser compatibility issues | Low | Medium | Cross-browser testing, polyfills |
| Redis connection failures | Medium | Low | Graceful fallback to memory cache |
| Build pipeline complexity | Medium | Medium | Comprehensive documentation, automation |

---

## Notes & Decisions

### Architecture Decisions
- **Date:** ____________
- **Decision:** Hybrid architecture (embedded SPA with dev mode proxy)
- **Rationale:** Best balance of user experience and developer experience

- **Date:** ____________
- **Decision:** Vue.js 3 with TypeScript
- **Rationale:** Better performance, smaller bundles, easier learning curve

- **Date:** ____________
- **Decision:** Keep OpenLayers for maps
- **Rationale:** Already integrated, powerful features, good Vue support

### Open Questions
1. Should we implement authentication for admin features?
2. What's the priority: mobile support or advanced analytics?
3. Should Redis be optional or recommended in documentation?

---

**Document Version:** 1.0  
**Last Updated:** October 2, 2025  
**Next Review:** _____________

# Testing Strategy & Roadmap

**Created:** October 6, 2025  
**Status:** Active Planning  
**Goal:** Achieve 70% backend coverage, 50% frontend coverage

---

## Current State

### Backend Testing (/VintageAtlas.Tests/)

**Existing Tests:**

- ✅ `ConfigValidatorTests.cs` - Configuration validation
- ✅ `MapColorsTests.cs` - Color mapping
- ✅ `TileGenerationTests.cs` - Tile generation basics
- ✅ 8 other test files

**Current Coverage:** ~30%  
**Target Coverage:** 70%

### Frontend Testing (frontend/src/)

**Status:** Infrastructure ready (Vitest configured)  
**Current Coverage:** 0%  
**Target Coverage:** 50%

---

## Testing Priorities

### Priority 1: Backend Critical Path 🔴

**What to test:**

1. **Tile Generation Pipeline**
   - UnifiedTileGenerator (all 5 render modes)
   - ChunkDataExtractor (main thread safety)
   - BlockColorCache (color lookups)
   - PyramidTileDownsampler (zoom levels)

2. **Data Collection**
   - ServerStatusQuery (player/animal tracking)
   - HistoricalDataTracker (database writes)
   - ChunkChangeTracker (change detection)

3. **API Controllers**
   - StatusController (server status)
   - MapConfigController (extent calculations)
   - HistoricalController (queries)
   - TileController (tile serving)

### Priority 2: Frontend Core Features 🟡

**What to test:**

1. **Stores (Pinia)**
   - mapStore - layer visibility, feature selection
   - serverStore - status polling
   - historicalStore - data fetching
   - liveStore - real-time updates

2. **API Services**
   - client.ts - axios configuration
   - status.ts - status endpoints
   - mapConfig.ts - coordinate transformations

3. **Critical Components**
   - MapContainer.vue - map initialization
   - PlayerLayer.vue - player rendering
   - TimelineChart.vue - historical viz

### Priority 3: Integration Tests ⏳

**Scenarios to test:**

1. Full map export flow
2. Live tile generation
3. Historical data recording → retrieval
4. Player tracking → map display

---

## Test Templates

### Backend Unit Test Template

```csharp
using Xunit;
using VintageAtlas.Export;

namespace VintageAtlas.Tests.Unit;

public class UnifiedTileGeneratorTests
{
    [Fact]
    public void RenderTile_AllModes_ProducesValidPng()
    {
        // Arrange
        var sapi = MockFactory.CreateMockServerAPI();
        var config = new ModConfig { Mode = ImageMode.ColorVariations };
        var colorCache = new BlockColorCache(sapi, config);
        var storage = new MbTilesStorage(":memory:");
        var generator = new UnifiedTileGenerator(sapi, config, colorCache, storage);

        // Act
        var result = await generator.GetTileDataAsync(9, 0, 0);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Length > 0);
        // Verify PNG header
        Assert.Equal(0x89, result[0]); // PNG signature
    }
    
    [Theory]
    [InlineData(ImageMode.OnlyOneColor)]
    [InlineData(ImageMode.ColorVariations)]
    [InlineData(ImageMode.ColorVariationsWithHeight)]
    [InlineData(ImageMode.ColorVariationsWithHillShading)]
    [InlineData(ImageMode.MedievalStyleWithHillShading)]
    public void RenderTile_EachMode_DoesNotThrow(ImageMode mode)
    {
        // Test all render modes produce output without crashing
        // ...
    }
}
```

### Frontend Unit Test Template

```typescript
// frontend/src/stores/__tests__/map.spec.ts
import { describe, it, expect, beforeEach } from 'vitest';
import { setActivePinia, createPinia } from 'pinia';
import { useMapStore } from '../map';

describe('Map Store', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it('toggles layer visibility', () => {
    const store = useMapStore();
    const initialVisibility = store.layerVisibility.traders;
    
    store.toggleLayer('traders');
    
    expect(store.layerVisibility.traders).toBe(!initialVisibility);
  });

  it('updates center when map view changes', async () => {
    const store = useMapStore();
    const mockMap = createMockMap();
    
    store.setMap(mockMap);
    
    // Simulate view change
    mockMap.getView().setCenter([1000, 2000]);
    await nextTick();
    
    expect(store.center).toEqual([1000, 2000]);
  });
});
```

### Integration Test Template

```csharp
// VintageAtlas.Tests/Integration/FullTileGenerationTests.cs
using Xunit;

namespace VintageAtlas.Tests.Integration;

public class FullTileGenerationTests
{
    [Fact]
    public async Task FullExport_WithTestWorld_GeneratesAllTiles()
    {
        // Arrange
        var testWorldPath = Path.Combine(TestContext.TestDataDirectory, "test_world");
        var sapi = await MockFactory.CreateServerWithWorld(testWorldPath);
        var exporter = new MapExporter(sapi, config);

        // Act
        await exporter.StartExportAsync();

        // Assert
        var tilesPath = Path.Combine(config.OutputDirectory, "data", "tiles.mbtiles");
        Assert.True(File.Exists(tilesPath));
        
        using var storage = new MbTilesStorage(tilesPath);
        var extent = await storage.GetTileExtentAsync(9);
        Assert.NotNull(extent);
        Assert.True(extent.TileCount > 0);
    }
}
```

---

## Test Coverage Goals

### Backend (Target: 70%)

| Component | Priority | Target % | Current % |
|-----------|----------|----------|-----------|
| UnifiedTileGenerator | 🔴 High | 80% | ~20% |
| ChunkDataExtractor | 🔴 High | 75% | 0% |
| BlockColorCache | 🔴 High | 85% | 0% |
| ServerStatusQuery | 🔴 High | 70% | 0% |
| HistoricalDataTracker | 🟡 Medium | 65% | 0% |
| Controllers | 🟡 Medium | 60% | 0% |
| MbTilesStorage | 🟡 Medium | 70% | 0% |
| Utils/Helpers | 🟢 Low | 50% | ~40% |

### Frontend (Target: 50%)

| Component | Priority | Target % | Current % |
|-----------|----------|----------|-----------|
| Stores | 🔴 High | 70% | 0% |
| API Services | 🔴 High | 65% | 0% |
| MapContainer | 🟡 Medium | 50% | 0% |
| Live Components | 🟡 Medium | 45% | 0% |
| Historical Components | 🟢 Low | 40% | 0% |
| Utils | 🟢 Low | 50% | 0% |

---

## Testing Tools & Setup

### Backend

**Tools:**

- xUnit - Test framework
- Moq - Mocking framework
- FluentAssertions - Better assertions

**Setup:**

```bash
cd VintageAtlas.Tests
dotnet test
```

**Add missing packages:**

```bash
dotnet add package Moq
dotnet add package FluentAssertions
```

### Frontend

**Tools:**

- Vitest - Test runner (already configured)
- @vue/test-utils - Vue component testing
- happy-dom - DOM environment

**Setup:**

```bash
cd VintageAtlas/frontend
npm install --save-dev @vue/test-utils happy-dom
npm test
```

**Vitest config** (already in `vite.config.ts`):

```typescript
test: {
  environment: 'happy-dom',
  coverage: {
    provider: 'v8',
    reporter: ['text', 'html'],
    exclude: ['node_modules/', 'src/**/*.spec.ts']
  }
}
```

---

## Implementation Roadmap

### Week 1: Backend Critical Tests

**Days 1-2:**

- [ ] UnifiedTileGenerator tests (all render modes)
- [ ] ChunkDataExtractor tests (thread safety)

**Days 3-4:**

- [ ] BlockColorCache tests (color lookups)
- [ ] PyramidTileDownsampler tests

**Day 5:**

- [ ] ServerStatusQuery tests
- [ ] Run coverage report

### Week 2: Backend Integration + Frontend Setup

**Days 1-2:**

- [ ] Integration test: Full tile export
- [ ] Integration test: Live tile generation

**Days 3-5:**

- [ ] Frontend: Store tests (all stores)
- [ ] Frontend: API service tests
- [ ] Run frontend coverage

### Week 3: Frontend Components + Polish

**Days 1-3:**

- [ ] MapContainer tests
- [ ] PlayerLayer tests
- [ ] TimelineChart tests

**Days 4-5:**

- [ ] E2E setup (optional - Playwright)
- [ ] Documentation updates
- [ ] Final coverage report

---

## Test Data

### Mock Chunks

```csharp
public static class TestDataFactory
{
    public static ChunkSnapshot CreateMockChunk(int chunkX, int chunkZ)
    {
        return new ChunkSnapshot
        {
            ChunkX = chunkX,
            ChunkZ = chunkZ,
            ChunkY = 4,
            HeightMap = CreateMockHeightMap(),
            BlockIds = CreateMockBlockIds(),
            IsLoaded = true
        };
    }

    private static int[] CreateMockHeightMap()
    {
        var heightMap = new int[32 * 32];
        var random = new Random(42); // Deterministic
        for (int i = 0; i < heightMap.Length; i++)
        {
            heightMap[i] = 120 + random.Next(20); // Heights 120-140
        }
        return heightMap;
    }
}
```

### Test World

For integration tests, create a minimal test world:

- 10x10 chunks
- Basic terrain (grass, stone, water)
- No complex structures
- Store in `VintageAtlas.Tests/TestData/test_world/`

---

## Running Tests

### Backend

```bash
# All tests
dotnet test

# Specific test file
dotnet test --filter "FullyQualifiedName~UnifiedTileGeneratorTests"

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Frontend

```bash
# All tests
npm test

# Watch mode
npm test -- --watch

# Coverage
npm test -- --coverage

# Specific test
npm test -- map.spec.ts
```

---

## Success Criteria

- [ ] Backend coverage ≥ 70%
- [ ] Frontend coverage ≥ 50%
- [ ] All critical paths tested
- [ ] CI/CD integration (optional)
- [ ] Tests run in < 5 minutes
- [ ] No flaky tests

---

## Next Steps

1. ✅ Review this strategy
2. 🟡 Implement Week 1 backend tests
3. ⏳ Set up frontend test infrastructure
4. ⏳ Write integration tests
5. ⏳ Generate coverage reports

---

**Maintained by:** daviaaze  
**Last Updated:** October 6, 2025

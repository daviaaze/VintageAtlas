# Testing Guide for VintageAtlas

## Overview

VintageAtlas uses a multi-layered testing approach to ensure reliability:

1. **Unit Tests** - Test pure logic without Vintage Story dependencies
2. **Integration Tests** - Test components with mocked VS API
3. **Manual Tests** - Test in actual game environment
4. **Frontend Tests** - Test Vue components and TypeScript

## Testing Challenges for Vintage Story Mods

### Key Constraints

1. **VS API Dependency**: Most mod code depends on `ICoreServerAPI`, which can't be easily instantiated
2. **Main Thread Requirements**: Game state access must happen on main thread
3. **Complex State**: World, chunks, and players have complex interdependencies
4. **No Official Test Framework**: Vintage Story doesn't provide testing utilities

### Our Approach

- **Testable Architecture**: Separate business logic from API calls
- **Dependency Injection**: Pass interfaces instead of concrete API types
- **Mocking**: Use mocks for VS API interfaces
- **Integration Tests**: Test against actual test server

---

## 1. Unit Testing Setup

### Prerequisites

```bash
# In nix develop shell
cd VintageAtlas
dotnet add VintageAtlas.Tests package xUnit
dotnet add VintageAtlas.Tests package xunit.runner.visualstudio
dotnet add VintageAtlas.Tests package Moq
dotnet add VintageAtlas.Tests package FluentAssertions
dotnet add VintageAtlas.Tests package Microsoft.NET.Test.Sdk
```

### Project Structure

```
VintageAtlas.Tests/
├── Unit/
│   ├── Core/
│   │   ├── ConfigValidatorTests.cs
│   │   └── ModConfigTests.cs
│   ├── Export/
│   │   ├── MapColorsTests.cs
│   │   ├── BlockColorCacheTests.cs
│   │   └── BlurToolTests.cs
│   ├── Models/
│   │   └── DataModelTests.cs
│   └── Web/
│       └── RequestRouterTests.cs
├── Integration/
│   ├── Export/
│   │   ├── MapExportIntegrationTests.cs
│   │   └── TileGenerationTests.cs
│   └── Web/
│       └── ApiEndpointTests.cs
├── Mocks/
│   ├── MockServerApi.cs
│   ├── MockWorldAccessor.cs
│   └── MockLogger.cs
└── Fixtures/
    ├── TestData.cs
    └── TestMapData.cs
```

### What to Test

#### ✅ Highly Testable (Unit Tests)

- **ConfigValidator**: Validation logic
- **MapColors**: Color calculation algorithms
- **BlurTool**: Image processing
- **BlockColorCache**: Caching logic
- **PyramidTileDownsampler**: Tile downsampling math
- **RequestRouter**: URL routing logic
- **Data Models**: Serialization/deserialization
- **Coordinate transformations**: Math operations

#### ⚠️ Requires Mocking (Integration Tests)

- **MapExporter**: Orchestration logic (mock chunk access)
- **DynamicTileGenerator**: Tile generation (mock world data)
- **ChunkDataExtractor**: Extraction logic (mock chunks)
- **SavegameDataLoader**: File reading (mock file system)
- **WebServer**: HTTP handling (mock API)
- **Controllers**: API endpoints (mock dependencies)

#### ❌ Manual Testing Only

- **Full export on real world**: Too complex to mock
- **Player tracking**: Requires actual players
- **Performance testing**: Needs real data volumes
- **UI/Frontend**: Use frontend testing tools

---

## 2. Example Tests

### Unit Test Example: ConfigValidator

```csharp
// VintageAtlas.Tests/Unit/Core/ConfigValidatorTests.cs
using Xunit;
using FluentAssertions;
using VintageAtlas.Core;

namespace VintageAtlas.Tests.Unit.Core;

public class ConfigValidatorTests
{
    [Fact]
    public void ValidateConfig_WithValidConfig_ReturnsTrue()
    {
        // Arrange
        var config = new ModConfig
        {
            Port = 8080,
            MaxZoom = 10,
            TileSize = 256,
            ThreadCount = 4
        };

        // Act
        var result = ConfigValidator.Validate(config, out var errors);

        // Assert
        result.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void ValidateConfig_WithInvalidPort_ReturnsFalse(int port)
    {
        // Arrange
        var config = new ModConfig { Port = port };

        // Act
        var result = ConfigValidator.Validate(config, out var errors);

        // Assert
        result.Should().BeFalse();
        errors.Should().Contain(e => e.Contains("port"));
    }

    [Fact]
    public void ValidateConfig_WithNegativeThreadCount_UsesDefaultValue()
    {
        // Arrange
        var config = new ModConfig { ThreadCount = -1 };

        // Act
        ConfigValidator.ApplyDefaults(config);

        // Assert
        config.ThreadCount.Should().BeGreaterThan(0);
    }
}
```

### Unit Test Example: MapColors

```csharp
// VintageAtlas.Tests/Unit/Export/MapColorsTests.cs
using Xunit;
using FluentAssertions;
using VintageAtlas.Export;

namespace VintageAtlas.Tests.Unit.Export;

public class MapColorsTests
{
    [Fact]
    public void BlendColors_WithEqualWeights_ReturnsAverage()
    {
        // Arrange
        var color1 = (R: 100, G: 100, B: 100);
        var color2 = (R: 200, G: 200, B: 200);

        // Act
        var result = MapColors.Blend(color1, color2, 0.5);

        // Assert
        result.R.Should().Be(150);
        result.G.Should().Be(150);
        result.B.Should().Be(150);
    }

    [Theory]
    [InlineData(0.0, 100)]
    [InlineData(0.25, 125)]
    [InlineData(0.5, 150)]
    [InlineData(0.75, 175)]
    [InlineData(1.0, 200)]
    public void BlendColors_WithVaryingWeights_ReturnsCorrectBlend(double weight, int expected)
    {
        // Arrange
        var color1 = (R: 100, G: 100, B: 100);
        var color2 = (R: 200, G: 200, B: 200);

        // Act
        var result = MapColors.Blend(color1, color2, weight);

        // Assert
        result.R.Should().Be(expected);
    }
}
```

### Integration Test Example: TileGeneration

```csharp
// VintageAtlas.Tests/Integration/Export/TileGenerationTests.cs
using Xunit;
using FluentAssertions;
using Moq;
using VintageAtlas.Export;
using Vintagestory.API.Server;
using Vintagestory.API.Common;

namespace VintageAtlas.Tests.Integration.Export;

public class TileGenerationTests
{
    private readonly Mock<ICoreServerAPI> _mockApi;
    private readonly Mock<ILogger> _mockLogger;

    public TileGenerationTests()
    {
        _mockApi = new Mock<ICoreServerAPI>();
        _mockLogger = new Mock<ILogger>();
        _mockApi.Setup(x => x.Logger).Returns(_mockLogger.Object);
    }

    [Fact]
    public async Task GenerateTile_WithValidCoordinates_CreatesTileFile()
    {
        // Arrange
        var generator = new DynamicTileGenerator(_mockApi.Object);
        var z = 0;
        var x = 0;
        var y = 0;

        // Mock chunk data
        MockChunkData(_mockApi, x * 32, y * 32);

        // Act
        var result = await generator.GenerateTileAsync(z, x, y);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ImageData.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateTile_WithMissingChunks_ReturnsEmptyTile()
    {
        // Arrange
        var generator = new DynamicTileGenerator(_mockApi.Object);
        
        // Mock returns null chunks
        _mockApi.Setup(x => x.World.BlockAccessor.GetChunk(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
               .Returns((IWorldChunk)null);

        // Act
        var result = await generator.GenerateTileAsync(0, 0, 0);

        // Assert
        result.Success.Should().BeFalse();
    }

    private void MockChunkData(Mock<ICoreServerAPI> api, int chunkX, int chunkZ)
    {
        var mockChunk = new Mock<IWorldChunk>();
        mockChunk.Setup(x => x.Blocks).Returns(new int[32768]);
        
        api.Setup(x => x.World.BlockAccessor.GetChunk(
            chunkX, It.IsAny<int>(), chunkZ))
           .Returns(mockChunk.Object);
    }
}
```

---

## 3. Running Tests

### Command Line

```bash
# Run all tests
nix develop
cd VintageAtlas.Tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~ConfigValidatorTests"

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### VS Code Integration

Add to `.vscode/tasks.json`:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "test",
      "command": "dotnet",
      "type": "process",
      "args": ["test", "${workspaceFolder}/VintageAtlas.Tests"],
      "problemMatcher": "$msCompile"
    }
  ]
}
```

### Continuous Integration

Add to `.github/workflows/test.yml`:

```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      - run: dotnet test --verbosity normal
```

---

## 4. Manual Testing

### Test Server Setup

```bash
# Build and run test server
nix develop
quick-test
```

### Testing Checklist

#### Map Export
- [ ] `/atlas export` command works
- [ ] Tiles are generated in correct directory
- [ ] All zoom levels are created
- [ ] Colors match block types
- [ ] Performance is acceptable

#### Web Interface
- [ ] Server starts on correct port
- [ ] Map loads and displays correctly
- [ ] Player positions update
- [ ] Markers show correct locations
- [ ] API endpoints return valid JSON

#### API Endpoints
```bash
# Test status endpoint
curl http://localhost:42422/api/status | jq

# Test config endpoint
curl http://localhost:42422/api/config | jq

# Test tile endpoint
curl -I http://localhost:42422/tiles/0/0/0.png

# Test player data
curl http://localhost:42422/api/players | jq
```

#### Performance Tests
```bash
# Test export time
time /atlas export

# Monitor memory usage
watch -n 1 'ps aux | grep vintagestory'

# Check tile generation rate
# (Watch logs during export)
```

---

## 5. Frontend Testing

### Unit Tests (Vitest)

```bash
cd VintageAtlas/frontend
npm run test
npm run test:coverage
```

### Component Tests

```typescript
// VintageAtlas/frontend/src/components/__tests__/MapContainer.test.ts
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import MapContainer from '../map/MapContainer.vue'

describe('MapContainer', () => {
  it('renders map element', () => {
    const wrapper = mount(MapContainer)
    expect(wrapper.find('#map').exists()).toBe(true)
  })

  it('initializes with correct zoom level', async () => {
    const wrapper = mount(MapContainer)
    await wrapper.vm.$nextTick()
    expect(wrapper.vm.map?.getView().getZoom()).toBe(0)
  })
})
```

### E2E Tests (Playwright)

```bash
cd VintageAtlas/frontend
npx playwright test
```

```typescript
// VintageAtlas/frontend/e2e/map.spec.ts
import { test, expect } from '@playwright/test'

test('map loads and displays tiles', async ({ page }) => {
  await page.goto('http://localhost:42422')
  
  // Wait for map to initialize
  await page.waitForSelector('.ol-viewport')
  
  // Check that tiles loaded
  const tiles = await page.locator('.ol-layer img').count()
  expect(tiles).toBeGreaterThan(0)
})

test('clicking marker shows popup', async ({ page }) => {
  await page.goto('http://localhost:42422')
  
  // Click first marker
  await page.click('.ol-layer svg')
  
  // Check popup appears
  await expect(page.locator('.ol-popup')).toBeVisible()
})
```

---

## 6. Test Data Management

### Test Fixtures

```csharp
// VintageAtlas.Tests/Fixtures/TestData.cs
public static class TestData
{
    public static ModConfig ValidConfig() => new()
    {
        Port = 42422,
        MaxZoom = 6,
        TileSize = 256,
        ThreadCount = Environment.ProcessorCount
    };

    public static byte[] SampleChunkData()
    {
        // Return mock chunk data
        return new byte[32768];
    }

    public static string SampleGeoJson()
    {
        return """
        {
          "type": "FeatureCollection",
          "features": []
        }
        """;
    }
}
```

### Test World

Create a minimal test world for integration tests:

```bash
# Create test world
cd test_server
./vintage_server --createworld test_world

# Keep it small for fast tests
# Edit serverconfig.json:
{
  "ChunkGenerateDistance": 4,
  "WorldConfig": {
    "WorldSize": 1024
  }
}
```

---

## 7. Mocking Strategies

### Mock Vintage Story API

```csharp
// VintageAtlas.Tests/Mocks/MockServerApi.cs
public class MockServerApi : ICoreServerAPI
{
    private readonly Mock<ILogger> _logger = new();
    private readonly Mock<IWorldAccessor> _world = new();

    public MockServerApi()
    {
        Logger = _logger.Object;
        World = _world.Object;
    }

    public ILogger Logger { get; }
    public IWorldAccessor World { get; }
    
    // Implement other required members...
    
    public void SetupChunk(int x, int y, int z, IWorldChunk chunk)
    {
        _world.Setup(w => w.BlockAccessor.GetChunk(x, y, z))
              .Returns(chunk);
    }
}
```

### Mock World Data

```csharp
// VintageAtlas.Tests/Mocks/MockWorldAccessor.cs
public class MockWorldAccessor : IWorldAccessor
{
    private readonly Dictionary<(int, int, int), IWorldChunk> _chunks = new();

    public IBlockAccessor BlockAccessor { get; }

    public MockWorldAccessor()
    {
        var mockBlockAccessor = new Mock<IBlockAccessor>();
        mockBlockAccessor.Setup(x => x.GetChunk(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
                        .Returns<int, int, int>((x, y, z) => 
                            _chunks.TryGetValue((x, y, z), out var chunk) ? chunk : null);
        
        BlockAccessor = mockBlockAccessor.Object;
    }

    public void AddChunk(int x, int y, int z, IWorldChunk chunk)
    {
        _chunks[(x, y, z)] = chunk;
    }
}
```

---

## 8. Coverage Goals

### Target Coverage

- **Core Logic**: 80%+ coverage
- **Export Logic**: 70%+ coverage
- **Web/API**: 60%+ coverage
- **Commands**: Manual testing only

### Measuring Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report
open coverage-report/index.html
```

---

## 9. Test Organization

### Naming Conventions

- Test files: `{ClassName}Tests.cs`
- Test methods: `{MethodName}_{Scenario}_{ExpectedResult}`
- Example: `ValidateConfig_WithInvalidPort_ReturnsFalse`

### Test Structure (AAA Pattern)

```csharp
[Fact]
public void TestMethod()
{
    // Arrange - Set up test data and dependencies
    var input = "test";
    var expected = "expected";

    // Act - Execute the method under test
    var result = MethodUnderTest(input);

    // Assert - Verify the result
    result.Should().Be(expected);
}
```

---

## 10. Best Practices

### DO ✅

- Test business logic without VS API dependencies
- Use mocks for external dependencies
- Write fast, isolated unit tests
- Test edge cases and error conditions
- Keep tests simple and readable
- Use descriptive test names
- Test one thing per test

### DON'T ❌

- Test Vintage Story's internal behavior
- Create tests that depend on test order
- Test implementation details
- Mock everything (only mock at boundaries)
- Write slow tests for unit testing
- Test trivial getters/setters
- Ignore flaky tests

---

## 11. Troubleshooting Tests

### Common Issues

**Issue**: Can't instantiate `ICoreServerAPI`
- **Solution**: Use mocking library (Moq) or create test implementation

**Issue**: Tests fail in CI but pass locally
- **Solution**: Check for file system dependencies, timing issues, or environment differences

**Issue**: Tests are too slow
- **Solution**: Use unit tests instead of integration tests, mock expensive operations

**Issue**: Can't test main thread operations
- **Solution**: Extract logic into separate methods that don't require main thread

---

## 12. Next Steps

1. Create `VintageAtlas.Tests` project
2. Add test dependencies (xUnit, Moq, FluentAssertions)
3. Write tests for `ConfigValidator` (easiest to start)
4. Add tests for `MapColors` and `BlurTool`
5. Mock VS API for integration tests
6. Set up CI/CD with GitHub Actions
7. Add code coverage reporting
8. Document test patterns for contributors

---

## Resources

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [FluentAssertions](https://fluentassertions.com/)
- [VS API Docs](https://apidocs.vintagestory.at/)
- [Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)


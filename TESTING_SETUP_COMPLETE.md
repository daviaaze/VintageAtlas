# Testing Infrastructure Setup - Complete ✅

## Summary

A comprehensive testing infrastructure has been added to VintageAtlas, including:

1. **Test Project** - `VintageAtlas.Tests/` with xUnit, Moq, and FluentAssertions
2. **Example Tests** - Unit tests for ConfigValidator, BlurTool, BlockColorCache, and TileResult
3. **Mock Implementations** - MockLogger for testing without VS API
4. **Test Scripts** - `run-tests.sh` for convenient test execution
5. **Comprehensive Guide** - Complete testing documentation

## What Was Created

### Test Project Structure

```
VintageAtlas.Tests/
├── VintageAtlas.Tests.csproj  # Test project with dependencies
├── README.md                   # Quick reference for running tests
├── Unit/
│   ├── Core/
│   │   └── ConfigValidatorTests.cs      # 10 tests for config validation
│   ├── Export/
│   │   ├── BlurToolTests.cs             # 7 tests for blur algorithm
│   │   └── BlockColorCacheTests.cs      # 8 tests for color cache
│   └── Models/
│       └── TileResultTests.cs           # 5 tests for TileResult model
└── Mocks/
    └── MockLogger.cs                     # Mock ILogger implementation
```

### Scripts

- **`run-tests.sh`** - Bash script to run tests with various options
  - `./run-tests.sh` - Run all tests
  - `./run-tests.sh --filter ConfigValidator` - Run specific tests
  - `./run-tests.sh --verbose` - Detailed output
  - `./run-tests.sh --coverage` - With code coverage

### Documentation

- **`docs/guides/testing-guide.md`** - 500+ line comprehensive guide covering:
  - Testing philosophy for VS mods
  - Unit vs integration test strategies
  - Example tests for different scenarios
  - Mocking strategies for VS API
  - Running tests locally and in CI
  - Coverage goals and best practices

## How to Use

### Running Tests (Quick Start)

```bash
# Using Nix shell
nix develop
cd VintageAtlas.Tests
dotnet test

# Or use the helper script
./run-tests.sh
```

### Test Coverage

**Current Tests:**
- ✅ ConfigValidator (10 tests) - Config validation logic
- ✅ BlurTool (7 tests) - Image blur algorithm
- ✅ BlockColorCache (8 tests) - Color caching with mocked API
- ✅ TileResult (5 tests) - Data model tests
- ✅ MockLogger - Test utility

**Total:** 30 tests covering core business logic

### Writing New Tests

1. **Choose test type:**
   - Unit tests for pure logic (no VS API deps)
   - Integration tests for components with VS API (use mocks)
   - Manual tests for full system testing

2. **Follow AAA pattern:**
   ```csharp
   [Fact]
   public void MethodName_Scenario_ExpectedResult()
   {
       // Arrange
       var input = GetTestData();
       
       // Act
       var result = MethodUnderTest(input);
       
       // Assert
       result.Should().Be(expected);
   }
   ```

3. **Use FluentAssertions:**
   ```csharp
   result.Should().BeTrue();
   result.Should().NotBeNull();
   collection.Should().HaveCount(3);
   ```

### Testing Challenges & Solutions

#### Challenge: VS API Dependencies
**Solution:** Use Moq to mock `ICoreServerAPI`, `ILogger`, etc.

```csharp
var mockApi = new Mock<ICoreServerAPI>();
var mockLogger = new MockLogger(); // Or new Mock<ILogger>()
mockApi.Setup(x => x.Logger).Returns(mockLogger);
```

#### Challenge: Main Thread Requirements
**Solution:** Extract business logic into testable methods that don't require main thread

#### Challenge: Complex World State
**Solution:** 
- Unit test pure logic
- Mock minimal state for integration tests
- Use actual test server for manual testing

## Next Steps

### Expand Test Coverage

**Immediate (Easy Wins):**
- [ ] Add tests for MapColors
- [ ] Add tests for ChunkPosExtension
- [ ] Add tests for coordinate transformation utilities
- [ ] Add tests for GeoJSON models

**Medium (Requires Mocking):**
- [ ] Add integration tests for DynamicTileGenerator
- [ ] Add tests for MapExporter orchestration
- [ ] Add tests for WebServer routing
- [ ] Add tests for API controllers

**Advanced (Complex Setup):**
- [ ] Add end-to-end tests with test server
- [ ] Add performance benchmarks
- [ ] Add frontend tests with Vitest

### CI/CD Integration

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
      - run: dotnet test --logger "trx;LogFileName=test-results.trx"
      - uses: actions/upload-artifact@v3
        if: always()
        with:
          name: test-results
          path: '**/test-results.trx'
```

### Coverage Reports

To generate HTML coverage reports:

```bash
# Install reportgenerator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Run tests with coverage
./run-tests.sh --coverage

# View report
xdg-open VintageAtlas.Tests/coverage-report/index.html
```

## Testing Philosophy

### What to Test ✅

- **Business logic** - Pure functions and algorithms
- **Edge cases** - Boundary conditions and error handling
- **Data validation** - Config validation, input sanitization
- **Data transformation** - Color calculations, coordinate transforms
- **API contract** - Ensure API responses match spec

### What NOT to Test ❌

- **Vintage Story internals** - Trust the game engine
- **External libraries** - Trust OpenLayers, SkiaSharp, etc.
- **Trivial code** - Simple getters/setters
- **UI rendering** - Use visual/manual testing instead

### Coverage Goals

- **Core logic:** 80%+ (critical business logic)
- **Export logic:** 70%+ (complex but has VS deps)
- **Web/API:** 60%+ (HTTP infrastructure)
- **Commands:** Manual testing (in-game commands)

## Resources

- **Testing Guide:** `docs/guides/testing-guide.md`
- **Test README:** `VintageAtlas.Tests/README.md`
- **xUnit Docs:** https://xunit.net/
- **Moq Docs:** https://github.com/moq/moq4
- **FluentAssertions:** https://fluentassertions.com/

## Example: Adding a New Test

Let's say you want to test a new utility class `CoordinateConverter`:

```csharp
// 1. Create test file
// VintageAtlas.Tests/Unit/Utils/CoordinateConverterTests.cs

using Xunit;
using FluentAssertions;
using VintageAtlas.Utils;

namespace VintageAtlas.Tests.Unit.Utils;

public class CoordinateConverterTests
{
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(100, 200, 50, 100)]  // gameX, gameZ, expectedMapX, expectedMapY
    public void GameToMap_WithValidCoords_ReturnsCorrectMapCoords(
        int gameX, int gameZ, double expectedMapX, double expectedMapY)
    {
        // Arrange
        var converter = new CoordinateConverter(scale: 2.0);

        // Act
        var (mapX, mapY) = converter.GameToMap(gameX, gameZ);

        // Assert
        mapX.Should().Be(expectedMapX);
        mapY.Should().Be(expectedMapY);
    }

    [Fact]
    public void MapToGame_RoundTrip_ReturnsOriginalCoords()
    {
        // Arrange
        var converter = new CoordinateConverter(scale: 1.0);
        var originalX = 123;
        var originalZ = 456;

        // Act
        var (mapX, mapY) = converter.GameToMap(originalX, originalZ);
        var (gameX, gameZ) = converter.MapToGame(mapX, mapY);

        // Assert
        gameX.Should().Be(originalX);
        gameZ.Should().Be(originalZ);
    }
}
```

```bash
# 2. Run the test
cd VintageAtlas.Tests
dotnet test --filter CoordinateConverterTests
```

## Success Metrics

✅ **30 unit tests** created covering core functionality  
✅ **Test project** with all dependencies configured  
✅ **Test script** for easy execution  
✅ **Comprehensive guide** for developers  
✅ **Mock utilities** for VS API testing  
✅ **Documentation** integrated into project docs  

## Questions?

- Review the [Testing Guide](docs/guides/testing-guide.md) for detailed information
- Check [Contributing Guidelines](CONTRIBUTING.md) for PR requirements
- Ask in GitHub Discussions for help

---

**Created:** 2025-10-03  
**Author:** AI Assistant  
**Status:** ✅ Complete and Ready to Use


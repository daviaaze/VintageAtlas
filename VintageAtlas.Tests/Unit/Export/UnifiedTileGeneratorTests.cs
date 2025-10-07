using System;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using VintageAtlas.Export;
using VintageAtlas.Core;
using VintageAtlas.Storage;
using VintageAtlas.Tests.Mocks;

namespace VintageAtlas.Tests.Unit.Export;

/// <summary>
/// Tests for UnifiedTileGenerator - the primary tile generation system
/// Tests all 5 render modes and core functionality
/// </summary>
public class UnifiedTileGeneratorTests : IDisposable
{
    private readonly Mock<ICoreServerAPI> _mockApi;
    private readonly MockLogger _mockLogger;
    private readonly Mock<IServerWorldAccessor> _mockWorld;
    private readonly ModConfig _config;
    private readonly MbTilesStorage _storage;
    private readonly BlockColorCache _colorCache;

    public UnifiedTileGeneratorTests()
    {
        // Setup mocks
        _mockApi = new Mock<ICoreServerAPI>();
        _mockLogger = new MockLogger();
        _mockWorld = new Mock<IServerWorldAccessor>();
        
        _mockApi.Setup(x => x.Logger).Returns(_mockLogger);
        _mockApi.Setup(x => x.World).Returns(_mockWorld.Object);
        
        // Mock empty block list for minimal initialization
        _mockWorld.Setup(x => x.Blocks).Returns(Array.Empty<Block>());
        
        // Use in-memory database for tests
        _storage = new MbTilesStorage(":memory:");
        
        // Config with different modes will be created per test
        _config = new ModConfig
        {
            TileSize = 256,
            BaseZoomLevel = 9,
            Mode = ImageMode.ColorVariations
        };
        
        // Initialize color cache
        _colorCache = new BlockColorCache(_mockApi.Object, _config);
        _colorCache.Initialize();
    }

    public void Dispose()
    {
        _storage?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Constructor_InitializesGenerator()
    {
        // Act
        var generator = new UnifiedTileGenerator(_mockApi.Object, _config, _colorCache, _storage);

        // Assert
        generator.Should().NotBeNull();
        _mockLogger.Notifications.Should().Contain(n => n.Contains("UnifiedTileGenerator initialized"));
    }

    [Fact]
    public async Task GetTileDataAsync_WithNonExistentTile_ReturnsNull()
    {
        // Arrange
        var generator = new UnifiedTileGenerator(_mockApi.Object, _config, _colorCache, _storage);
        
        // Act
        var result = await generator.GetTileDataAsync(9, 9999, 9999);

        // Assert
        result.Should().BeNull("tile does not exist and cannot be generated without world data");
    }

    [Fact]
    public async Task GetTileExtentAsync_WithEmptyStorage_ReturnsNull()
    {
        // Arrange
        var generator = new UnifiedTileGenerator(_mockApi.Object, _config, _colorCache, _storage);
        
        // Act
        var extent = await generator.GetTileExtentAsync(9);

        // Assert
        extent.Should().BeNull("no tiles exist in empty storage");
    }

    [Theory]
    [InlineData(ImageMode.OnlyOneColor)]
    [InlineData(ImageMode.ColorVariations)]
    [InlineData(ImageMode.ColorVariationsWithHeight)]
    [InlineData(ImageMode.ColorVariationsWithHillShading)]
    [InlineData(ImageMode.MedievalStyleWithHillShading)]
    public void Constructor_WithEachRenderMode_DoesNotThrow(ImageMode mode)
    {
        // Arrange
        var config = new ModConfig { Mode = mode };
        var colorCache = new BlockColorCache(_mockApi.Object, config);
        colorCache.Initialize();

        // Act
        Action act = () => new UnifiedTileGenerator(_mockApi.Object, config, colorCache, _storage);

        // Assert
        act.Should().NotThrow("all render modes should be supported");
    }

    [Fact]
    public void Constructor_RequiresNonNullParameters()
    {
        // Assert - All parameters are required
        Assert.Throws<ArgumentNullException>(() => 
            new UnifiedTileGenerator(null!, _config, _colorCache, _storage));
        
        Assert.Throws<ArgumentNullException>(() => 
            new UnifiedTileGenerator(_mockApi.Object, null!, _colorCache, _storage));
        
        Assert.Throws<ArgumentNullException>(() => 
            new UnifiedTileGenerator(_mockApi.Object, _config, null!, _storage));
        
        Assert.Throws<ArgumentNullException>(() => 
            new UnifiedTileGenerator(_mockApi.Object, _config, _colorCache, null!));
    }

    [Fact]
    public async Task GetTileExtentAsync_AfterStoringTile_ReturnsExtent()
    {
        // Arrange
        var generator = new UnifiedTileGenerator(_mockApi.Object, _config, _colorCache, _storage);
        var testTileData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        
        // Store a test tile
        await _storage.PutTileAsync(9, 0, 0, testTileData);

        // Act
        var extent = await generator.GetTileExtentAsync(9);

        // Assert
        extent.Should().NotBeNull();
        extent!.MinX.Should().Be(0);
        extent.MaxX.Should().Be(0);
        extent.MinY.Should().Be(0);
        extent.MaxY.Should().Be(0);
    }

    [Fact]
    public async Task GetTileDataAsync_WithStoredTile_ReturnsTileData()
    {
        // Arrange
        var generator = new UnifiedTileGenerator(_mockApi.Object, _config, _colorCache, _storage);
        var expectedData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // Full PNG signature
        
        // Pre-store a tile
        await _storage.PutTileAsync(9, 5, 5, expectedData);

        // Act
        var result = await generator.GetTileDataAsync(9, 5, 5);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedData);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public async Task GetTileExtentAsync_DifferentZoomLevels_HandlesCorrectly(int zoom)
    {
        // Arrange
        var generator = new UnifiedTileGenerator(_mockApi.Object, _config, _colorCache, _storage);
        
        // Act
        var extent = await generator.GetTileExtentAsync(zoom);

        // Assert - Should return null for empty storage at any zoom
        extent.Should().BeNull($"no tiles exist at zoom level {zoom}");
    }

    [Fact]
    public async Task GetTileDataAsync_ConcurrentRequests_HandlesSafely()
    {
        // Arrange
        var generator = new UnifiedTileGenerator(_mockApi.Object, _config, _colorCache, _storage);
        var testData = new byte[] { 1, 2, 3, 4 };
        
        // Pre-store tiles
        await _storage.PutTileAsync(9, 0, 0, testData);
        await _storage.PutTileAsync(9, 1, 0, testData);
        await _storage.PutTileAsync(9, 0, 1, testData);
        await _storage.PutTileAsync(9, 1, 1, testData);

        // Act - Concurrent requests
        var tasks = new[]
        {
            generator.GetTileDataAsync(9, 0, 0),
            generator.GetTileDataAsync(9, 1, 0),
            generator.GetTileDataAsync(9, 0, 1),
            generator.GetTileDataAsync(9, 1, 1)
        };

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(4);
        results.Should().OnlyContain(r => r != null && r.Length > 0);
    }

    [Fact]
    public void UnifiedTileGenerator_ImplementsITileGenerator()
    {
        // Arrange & Act
        var generator = new UnifiedTileGenerator(_mockApi.Object, _config, _colorCache, _storage);

        // Assert
        generator.Should().BeAssignableTo<ITileGenerator>();
    }

    [Fact]
    public async Task GetTileDataAsync_WithInvalidZoomLevel_HandlesGracefully()
    {
        // Arrange
        var generator = new UnifiedTileGenerator(_mockApi.Object, _config, _colorCache, _storage);

        // Act & Assert - Should not throw, just return null
        var result1 = await generator.GetTileDataAsync(-1, 0, 0);
        var result2 = await generator.GetTileDataAsync(999, 0, 0);

        result1.Should().BeNull("negative zoom level is invalid");
        result2.Should().BeNull("excessive zoom level has no data");
    }

    [Fact]
    public async Task GetTileDataAsync_WithInvalidCoordinates_HandlesGracefully()
    {
        // Arrange
        var generator = new UnifiedTileGenerator(_mockApi.Object, _config, _colorCache, _storage);

        // Act & Assert - Should not throw
        var result1 = await generator.GetTileDataAsync(9, -999999, -999999);
        var result2 = await generator.GetTileDataAsync(9, 999999, 999999);

        result1.Should().BeNull("extreme negative coordinates have no data");
        result2.Should().BeNull("extreme positive coordinates have no data");
    }

    [Fact]
    public void Config_ValidatesCorrectly()
    {
        // Arrange
        var validConfig = new ModConfig
        {
            TileSize = 256,
            BaseZoomLevel = 9,
            Mode = ImageMode.ColorVariations
        };

        // Act & Assert - Valid config should work
        Action act = () => new UnifiedTileGenerator(_mockApi.Object, validConfig, _colorCache, _storage);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(ImageMode.ColorVariations)]
    [InlineData(ImageMode.ColorVariationsWithHeight)]
    public async Task Storage_PreservesDataForEachMode(ImageMode mode)
    {
        // Arrange
        var config = new ModConfig { Mode = mode };
        var colorCache = new BlockColorCache(_mockApi.Object, config);
        colorCache.Initialize();
        var generator = new UnifiedTileGenerator(_mockApi.Object, config, colorCache, _storage);
        
        var tileData = new byte[] { 1, 2, 3, 4, 5 };
        await _storage.PutTileAsync(9, 10, 10, tileData);

        // Act
        var retrieved = await generator.GetTileDataAsync(9, 10, 10);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Should().BeEquivalentTo(tileData, $"data should be preserved for {mode}");
    }

    [Fact]
    public async Task GetTileExtentAsync_WithMultipleTiles_CalculatesCorrectExtent()
    {
        // Arrange
        var generator = new UnifiedTileGenerator(_mockApi.Object, _config, _colorCache, _storage);
        var testData = new byte[] { 1, 2, 3 };
        
        // Store tiles at various positions
        await _storage.PutTileAsync(9, -5, -5, testData);
        await _storage.PutTileAsync(9, 10, 10, testData);
        await _storage.PutTileAsync(9, 0, 0, testData);

        // Act
        var extent = await generator.GetTileExtentAsync(9);

        // Assert
        extent.Should().NotBeNull();
        extent!.MinX.Should().Be(-5);
        extent.MaxX.Should().Be(10);
        extent.MinY.Should().Be(-5);
        extent.MaxY.Should().Be(10);
    }
}

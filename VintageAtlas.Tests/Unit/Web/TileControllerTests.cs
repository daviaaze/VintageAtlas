using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Export;
using VintageAtlas.Web.API;
using VintageAtlas.Tests.Mocks;

namespace VintageAtlas.Tests.Unit.Web;

/// <summary>
/// Tests for TileController - coordinate transformation and tile serving
/// </summary>
public class TileControllerTests
{
    private readonly Mock<ICoreServerAPI> _mockApi;
    private readonly MockLogger _mockLogger;
    private readonly ModConfig _config;
    private readonly Mock<ITileGenerator> _mockTileGenerator;
    private readonly Mock<MapConfigController> _mockMapConfigController;
    private readonly CoordinateTransformService _coordinateService;
    private readonly TileController _controller;

    public TileControllerTests()
    {
        // Setup mocks
        _mockApi = new Mock<ICoreServerAPI>();
        _mockLogger = new MockLogger();
        _mockApi.Setup(x => x.Logger).Returns(_mockLogger);

        _config = new ModConfig
        {
            TileSize = 256,
            BaseZoomLevel = 9
        };

        _mockTileGenerator = new Mock<ITileGenerator>();
        
        // Mock MapConfigController (we'll mock GetCurrentConfig() method directly)
        _mockMapConfigController = new Mock<MapConfigController>(_mockApi.Object, _config, _mockTileGenerator.Object);
        
        // Create coordinate service with mocked MapConfigController
        _coordinateService = new CoordinateTransformService(_mockMapConfigController.Object, _config);

        _controller = new TileController(
            _mockApi.Object,
            _config,
            _mockTileGenerator.Object,
            _mockMapConfigController.Object
        );
    }

    #region Coordinate Transformation Tests

    [Theory]
    [InlineData(7, 0, 0, 490, 498, 490, 498)] // Grid origin = storage origin
    [InlineData(7, 7, 8, 490, 498, 497, 506)] // Grid (7,8) = storage (497,506)
    [InlineData(7, 14, 16, 490, 498, 504, 514)] // Grid max = storage max
    [InlineData(7, -5, -3, 490, 498, 485, 495)] // Negative grid coords (north/west of origin)
    public void TransformGridToStorage_WithValidCoordinates_ReturnsCorrectStorage(
        int zoom, int gridX, int gridY,
        int originTileX, int originTileY,
        int expectedStorageX, int expectedStorageZ)
    {
        // Arrange
        var mapConfig = CreateMapConfig(originTileX, originTileY, zoom);
        _mockMapConfigController.Setup(x => x.GetCurrentConfig()).Returns(mapConfig);

        // Act
        var result = InvokeTransformGridToStorage(zoom, gridX, gridY);

        // Assert
        result.storageTileX.Should().Be(expectedStorageX, 
            $"Grid X ({gridX}) + Origin X ({originTileX}) should equal Storage X ({expectedStorageX})");
        result.storageTileZ.Should().Be(expectedStorageZ,
            $"Grid Y ({gridY}) + Origin Y ({originTileY}) should equal Storage Z ({expectedStorageZ})");
    }

    [Fact]
    public void TransformGridToStorage_WithZoom0_UsesCorrectResolution()
    {
        // Arrange - Zoom 0 has highest resolution value
        int zoom = 0;
        var mapConfig = CreateMapConfig(0, 0, zoom);
        mapConfig.TileResolutions = new double[] { 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 };
        _mockMapConfigController.Setup(x => x.GetCurrentConfig()).Returns(mapConfig);

        // Act
        var result = InvokeTransformGridToStorage(zoom, 1, 1);

        // Assert
        result.Should().NotBeNull();
        // At zoom 0, 1 grid unit = 1 storage tile (verified by resolution calculation)
        result.storageTileX.Should().Be(1);
        result.storageTileZ.Should().Be(1);
    }

    [Fact]
    public void TransformGridToStorage_WithZoom9_UsesCorrectResolution()
    {
        // Arrange - Zoom 9 (base zoom) has resolution = 1
        int zoom = 9;
        var mapConfig = CreateMapConfig(100, 200, zoom);
        mapConfig.TileResolutions = new double[] { 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 };
        _mockMapConfigController.Setup(x => x.GetCurrentConfig()).Returns(mapConfig);

        // Act
        var result = InvokeTransformGridToStorage(zoom, 50, 75);

        // Assert
        result.storageTileX.Should().Be(150); // 100 + 50
        result.storageTileZ.Should().Be(275); // 200 + 75
    }

    [Fact]
    public void TransformGridToStorage_WithNullMapConfig_ReturnsFallbackCoordinates()
    {
        // Arrange
        _mockMapConfigController.Setup(x => x.GetCurrentConfig()).Returns((MapConfigData?)null);

        // Act
        var (storageTileX, storageTileZ) = InvokeTransformGridToStorage(7, 10, 20);

        // Assert - fallback returns grid coords as-is
        storageTileX.Should().Be(10);
        storageTileZ.Should().Be(20);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1000000, 2000000)]
    [InlineData(-1000000, -2000000)]
    public void TransformGridToStorage_WithLargeCoordinates_HandlesCorrectly(int largeX, int largeY)
    {
        // Arrange
        var mapConfig = CreateMapConfig(0, 0, 7);
        _mockMapConfigController.Setup(x => x.GetCurrentConfig()).Returns(mapConfig);

        // Act
        var result = InvokeTransformGridToStorage(7, largeX, largeY);

        // Assert - should handle large numbers without overflow
        result.storageTileX.Should().Be(largeX);
        result.storageTileZ.Should().Be(largeY);
    }

    #endregion

    #region Transparent Tile Tests

    [Fact]
    public void GetTransparentTile_ReturnsValidPng()
    {
        // Act
        var result = InvokeGetTransparentTile();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0);
        
        // Check PNG signature
        result.Should().StartWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
    }

    [Fact]
    public void GetTransparentTile_ReturnsCachedInstance()
    {
        // Act
        var result1 = InvokeGetTransparentTile();
        var result2 = InvokeGetTransparentTile();

        // Assert - should return same instance (cached)
        result1.Should().BeSameAs(result2);
    }

    [Fact]
    public void GenerateTransparentPng_CreatesMinimalValidPng()
    {
        // Act
        var result = InvokeGenerateTransparentPng();

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(60); // Minimal 1x1 transparent PNG is ~60-70 bytes
        result.Length.Should().BeLessThan(100); // Should be minimal, not full size
        
        // Verify PNG signature (first 8 bytes)
        result[0].Should().Be(0x89); // PNG signature byte 1
        result[1].Should().Be(0x50); // 'P'
        result[2].Should().Be(0x4E); // 'N'
        result[3].Should().Be(0x47); // 'G'
        result[4].Should().Be(0x0D); // CR
        result[5].Should().Be(0x0A); // LF  
        result[6].Should().Be(0x1A); // SUB
        result[7].Should().Be(0x0A); // LF
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Create a MapConfigData for testing
    /// </summary>
    private MapConfigData CreateMapConfig(int originTileX, int originTileY, int displayZoom)
    {
        // Resolution at this zoom level
        var resolutions = new double[] { 512, 256, 128, 64, 32, 16, 8, 4, 2, 1 };
        var resolution = resolutions[displayZoom];
        
        // blocks per tile = tileSize * resolution
        int blocksPerTile = (int)(256 * resolution);
        
        return new MapConfigData
        {
            WorldOrigin = new[] { originTileX * blocksPerTile, originTileY * blocksPerTile },
            WorldExtent = new[] { 
                originTileX * blocksPerTile, 
                originTileY * blocksPerTile, 
                (originTileX + 100) * blocksPerTile, 
                (originTileY + 100) * blocksPerTile 
            },
            DefaultCenter = new[] { (originTileX + 50) * blocksPerTile, (originTileY + 50) * blocksPerTile },
            DefaultZoom = displayZoom,
            MinZoom = 0,
            MaxZoom = 9,
            BaseZoomLevel = 9,
            TileSize = 256,
            TileResolutions = resolutions,
            ViewResolutions = new double[] { 512, 256, 128, 64, 32, 16, 8, 4, 2, 1, 0.5, 0.25, 0.125 }
        };
    }

    /// <summary>
    /// Call GridToStorage on the coordinate service (now public)
    /// </summary>
    private (int storageTileX, int storageTileZ) InvokeTransformGridToStorage(int zoom, int gridX, int gridY)
    {
        // No reflection needed anymore - method is public on the service!
        return _coordinateService.GridToStorage(zoom, gridX, gridY);
    }

    /// <summary>
    /// Invoke private GetTransparentTile method via reflection
    /// </summary>
    private byte[] InvokeGetTransparentTile()
    {
        var method = typeof(TileController).GetMethod(
            "GetTransparentTile",
            BindingFlags.NonPublic | BindingFlags.Static);
        
        if (method == null)
            throw new InvalidOperationException("GetTransparentTile method not found");

        return (byte[])method.Invoke(null, null)!;
    }

    /// <summary>
    /// Invoke private GenerateTransparentPng method via reflection
    /// </summary>
    private byte[] InvokeGenerateTransparentPng()
    {
        var method = typeof(TileController).GetMethod(
            "GenerateTransparentPng",
            BindingFlags.NonPublic | BindingFlags.Static);
        
        if (method == null)
            throw new InvalidOperationException("GenerateTransparentPng method not found");

        return (byte[])method.Invoke(null, null)!;
    }

    #endregion
}

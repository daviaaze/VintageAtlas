using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using SkiaSharp;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VintageAtlas.Export;
using VintageAtlas.Core;
using VintageAtlas.Tests.Mocks;

namespace VintageAtlas.Tests.Unit.Export;

/// <summary>
/// Tests for PyramidTileDownsampler with ITileGenerator interface
/// Verifies downsampling works with both old and new tile generators
/// </summary>
public class PyramidTileDownsamplerTests
{
    private readonly Mock<ICoreServerAPI> _mockApi;
    private readonly MockLogger _mockLogger;
    private readonly ModConfig _config;

    public PyramidTileDownsamplerTests()
    {
        _mockApi = new Mock<ICoreServerAPI>();
        _mockLogger = new MockLogger();
        _mockApi.Setup(x => x.Logger).Returns(_mockLogger);
        
        _config = new ModConfig
        {
            TileSize = 256,
            BaseZoomLevel = 10  // Set high enough so our test zooms (6-7) are below base
        };
    }

    [Fact]
    public void Constructor_WithMockGenerator_Initializes()
    {
        // Arrange
        var mockGenerator = new Mock<ITileGenerator>();

        // Act
        var downsampler = new PyramidTileDownsampler(_mockApi.Object, _config, mockGenerator.Object);

        // Assert
        downsampler.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateTileByDownsamplingAsync_WithFourSourceTiles_GeneratesDownsampledTile()
    {
        // Arrange
        var mockGenerator = new Mock<ITileGenerator>();
        
        // Create 4 valid PNG tiles (minimal valid PNG data)
        var pngTile = CreateMinimalPngTile();
        
        mockGenerator
            .Setup(g => g.GetTileDataAsync(8, It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(pngTile);

        var downsampler = new PyramidTileDownsampler(_mockApi.Object, _config, mockGenerator.Object);

        // Act
        var result = await downsampler.GenerateTileByDownsamplingAsync(7, 0, 0);

        // Assert
        result.Should().NotBeNull("downsampling should produce a tile");
        mockGenerator.Verify(g => g.GetTileDataAsync(8, It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(4),
            "should fetch 4 source tiles (2x2 grid)");
    }

    [Fact]
    public async Task GenerateTileByDownsamplingAsync_WithMissingSourceTile_ReturnsNull()
    {
        // Arrange
        var mockGenerator = new Mock<ITileGenerator>();
        var pngTile = CreateMinimalPngTile();
        
        // Return null for one of the 4 tiles
        mockGenerator
            .Setup(g => g.GetTileDataAsync(8, 0, 0))
            .ReturnsAsync(pngTile);
        mockGenerator
            .Setup(g => g.GetTileDataAsync(8, 1, 0))
            .ReturnsAsync(pngTile);
        mockGenerator
            .Setup(g => g.GetTileDataAsync(8, 0, 1))
            .ReturnsAsync((byte[]?)null); // Missing tile!
        mockGenerator
            .Setup(g => g.GetTileDataAsync(8, 1, 1))
            .ReturnsAsync(pngTile);

        var downsampler = new PyramidTileDownsampler(_mockApi.Object, _config, mockGenerator.Object);

        // Act
        var result = await downsampler.GenerateTileByDownsamplingAsync(7, 0, 0);

        // Assert
        result.Should().BeNull("cannot downsample with missing source tiles");
        _mockLogger.Warnings.Should().Contain(w => w.Contains("Could not get all source tiles"));
    }

    [Fact]
    public async Task GenerateTileByDownsamplingAsync_CalculatesCorrectSourceCoordinates()
    {
        // Arrange
        var mockGenerator = new Mock<ITileGenerator>();
        var pngTile = CreateMinimalPngTile();
        
        mockGenerator
            .Setup(g => g.GetTileDataAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(pngTile);

        var downsampler = new PyramidTileDownsampler(_mockApi.Object, _config, mockGenerator.Object);

        // Act - Request tile at zoom 6, position (3, 5)
        await downsampler.GenerateTileByDownsamplingAsync(6, 3, 5);

        // Assert - Should fetch from zoom 7 at positions (6,10), (7,10), (6,11), (7,11)
        mockGenerator.Verify(g => g.GetTileDataAsync(7, 6, 10), Times.Once, "top-left");
        mockGenerator.Verify(g => g.GetTileDataAsync(7, 7, 10), Times.Once, "top-right");
        mockGenerator.Verify(g => g.GetTileDataAsync(7, 6, 11), Times.Once, "bottom-left");
        mockGenerator.Verify(g => g.GetTileDataAsync(7, 7, 11), Times.Once, "bottom-right");
    }

    [Fact]
    public async Task GenerateTileByDownsamplingAsync_HandlesNegativeCoordinates()
    {
        // Arrange
        var mockGenerator = new Mock<ITileGenerator>();
        var pngTile = CreateMinimalPngTile();
        
        mockGenerator
            .Setup(g => g.GetTileDataAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(pngTile);

        var downsampler = new PyramidTileDownsampler(_mockApi.Object, _config, mockGenerator.Object);

        // Act - Request tile at negative coordinates
        var result = await downsampler.GenerateTileByDownsamplingAsync(5, -2, -3);

        // Assert
        result.Should().NotBeNull();
        mockGenerator.Verify(g => g.GetTileDataAsync(6, -4, -6), Times.Once);
        mockGenerator.Verify(g => g.GetTileDataAsync(6, -3, -6), Times.Once);
        mockGenerator.Verify(g => g.GetTileDataAsync(6, -4, -5), Times.Once);
        mockGenerator.Verify(g => g.GetTileDataAsync(6, -3, -5), Times.Once);
    }

    [Fact]
    public async Task GenerateTileByDownsamplingAsync_WorksWithAnyITileGenerator()
    {
        // Arrange - Create a simple mock generator
        var mockGenerator = new Mock<ITileGenerator>();
        mockGenerator
            .Setup(g => g.GetTileDataAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(CreateMinimalPngTile());

        var downsampler = new PyramidTileDownsampler(_mockApi.Object, _config, mockGenerator.Object);

        // Act
        var result = await downsampler.GenerateTileByDownsamplingAsync(3, 1, 1);

        // Assert
        result.Should().NotBeNull("any ITileGenerator implementation should work");
    }

    [Theory]
    [InlineData(6, 0, 0, 7, 0, 0)] // Center tile
    [InlineData(5, 10, 10, 6, 20, 20)] // Positive offset
    [InlineData(4, -5, -5, 5, -10, -10)] // Negative offset
    public async Task GenerateTileByDownsamplingAsync_CorrectZoomAndCoordinateMapping(
        int targetZoom, int targetX, int targetZ,
        int sourceZoom, int expectedSourceX, int expectedSourceZ)
    {
        // Arrange
        var mockGenerator = new Mock<ITileGenerator>();
        mockGenerator
            .Setup(g => g.GetTileDataAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(CreateMinimalPngTile());

        var downsampler = new PyramidTileDownsampler(_mockApi.Object, _config, mockGenerator.Object);

        // Act
        await downsampler.GenerateTileByDownsamplingAsync(targetZoom, targetX, targetZ);

        // Assert - Verify it fetched from correct source zoom and top-left coordinate
        mockGenerator.Verify(
            g => g.GetTileDataAsync(sourceZoom, expectedSourceX, expectedSourceZ), 
            Times.Once, 
            "top-left source tile should match expected coordinates");
    }

    [Fact]
    public async Task GenerateTileByDownsamplingAsync_ConcurrentRequests_HandledCorrectly()
    {
        // Arrange
        var mockGenerator = new Mock<ITileGenerator>();
        mockGenerator
            .Setup(g => g.GetTileDataAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(CreateMinimalPngTile());

        var downsampler = new PyramidTileDownsampler(_mockApi.Object, _config, mockGenerator.Object);

        // Act - Request multiple tiles concurrently
        var tasks = Enumerable.Range(0, 5)
            .Select(i => downsampler.GenerateTileByDownsamplingAsync(5, i, i))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(5);
        results.Should().OnlyContain(r => r != null);
        mockGenerator.Verify(
            g => g.GetTileDataAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), 
            Times.Exactly(20), // 5 tiles × 4 source tiles each
            "should fetch all required source tiles");
    }

    [Fact]
    public async Task GenerateTileByDownsamplingAsync_LogsVerboseDebugOnSuccess()
    {
        // Arrange
        _mockLogger.LogLevel = EnumLogType.VerboseDebug;
        
        var mockGenerator = new Mock<ITileGenerator>();
        mockGenerator
            .Setup(g => g.GetTileDataAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(CreateMinimalPngTile());

        var downsampler = new PyramidTileDownsampler(_mockApi.Object, _config, mockGenerator.Object);

        // Act
        await downsampler.GenerateTileByDownsamplingAsync(5, 2, 3);

        // Assert
        _mockLogger.DebugMessages.Should().Contain(m => m.Contains("Successfully downsampled"));
    }

    [Fact]
    public async Task GenerateTileByDownsamplingAsync_WithException_LogsErrorAndReturnsNull()
    {
        // Arrange
        var mockGenerator = new Mock<ITileGenerator>();
        mockGenerator
            .Setup(g => g.GetTileDataAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ThrowsAsync(new Exception("Test error"));

        var downsampler = new PyramidTileDownsampler(_mockApi.Object, _config, mockGenerator.Object);

        // Act
        var result = await downsampler.GenerateTileByDownsamplingAsync(5, 0, 0);

        // Assert
        result.Should().BeNull();
        _mockLogger.Errors.Should().Contain(e => e.Contains("Failed to downsample tile"));
    }

    /// <summary>
    /// Create a real valid PNG tile for testing using SkiaSharp
    /// This ensures the PNG can be properly decoded and downsampled
    /// </summary>
    private byte[] CreateMinimalPngTile()
    {
        // Create a 256x256 bitmap (standard tile size) with a solid color
        using var bitmap = new SKBitmap(_config.TileSize, _config.TileSize);
        using var canvas = new SKCanvas(bitmap);
        
        // Fill with a test color (light blue)
        canvas.Clear(new SKColor(100, 150, 200));
        
        // Encode to PNG
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}


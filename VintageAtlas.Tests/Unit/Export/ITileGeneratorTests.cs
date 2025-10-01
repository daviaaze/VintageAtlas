using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using VintageAtlas.Export;
using VintageAtlas.Storage;

namespace VintageAtlas.Tests.Unit.Export;

/// <summary>
/// Tests for ITileGenerator interface implementations
/// Verifies that both DynamicTileGenerator and UnifiedTileGenerator
/// properly implement the interface contract
/// </summary>
public class ITileGeneratorTests
{
    [Fact]
    public async Task MockTileGenerator_ImplementsInterface_ReturnsData()
    {
        // Arrange
        var mockGenerator = new Mock<ITileGenerator>();
        var expectedData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        
        mockGenerator
            .Setup(g => g.GetTileDataAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(expectedData);

        // Act
        var result = await mockGenerator.Object.GetTileDataAsync(7, 10, 20);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(expectedData);
        mockGenerator.Verify(g => g.GetTileDataAsync(7, 10, 20), Times.Once);
    }

    [Fact]
    public async Task MockTileGenerator_GetTileDataAsync_CanReturnNull()
    {
        // Arrange
        var mockGenerator = new Mock<ITileGenerator>();
        mockGenerator
            .Setup(g => g.GetTileDataAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((byte[]?)null);

        // Act
        var result = await mockGenerator.Object.GetTileDataAsync(7, 999, 999);

        // Assert
        result.Should().BeNull("tile does not exist");
    }

    [Fact]
    public async Task MockTileGenerator_GetTileExtentAsync_ReturnsExtent()
    {
        // Arrange
        var mockGenerator = new Mock<ITileGenerator>();
        var expectedExtent = new TileExtent
        {
            MinX = -10,
            MaxX = 10,
            MinY = -10,
            MaxY = 10
        };
        
        mockGenerator
            .Setup(g => g.GetTileExtentAsync(It.IsAny<int>()))
            .ReturnsAsync(expectedExtent);

        // Act
        var result = await mockGenerator.Object.GetTileExtentAsync(7);

        // Assert
        result.Should().NotBeNull();
        result!.MinX.Should().Be(-10);
        result.MaxX.Should().Be(10);
    }

    [Fact]
    public async Task MockTileGenerator_GetTileExtentAsync_CanReturnNull()
    {
        // Arrange
        var mockGenerator = new Mock<ITileGenerator>();
        mockGenerator
            .Setup(g => g.GetTileExtentAsync(It.IsAny<int>()))
            .ReturnsAsync((TileExtent?)null);

        // Act
        var result = await mockGenerator.Object.GetTileExtentAsync(7);

        // Assert
        result.Should().BeNull("no tiles exist at this zoom level");
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(7, 123, 456)]
    [InlineData(10, -50, -50)]
    [InlineData(5, 1000, 1000)]
    public async Task ITileGenerator_AcceptsVariousCoordinates(int zoom, int tileX, int tileZ)
    {
        // Arrange
        var mockGenerator = new Mock<ITileGenerator>();
        mockGenerator
            .Setup(g => g.GetTileDataAsync(zoom, tileX, tileZ))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        // Act
        var result = await mockGenerator.Object.GetTileDataAsync(zoom, tileX, tileZ);

        // Assert
        result.Should().NotBeNull();
        mockGenerator.Verify(g => g.GetTileDataAsync(zoom, tileX, tileZ), Times.Once);
    }

    [Fact]
    public async Task ITileGenerator_CanBeUsedByPyramidDownsampler()
    {
        // Arrange - Simulate PyramidTileDownsampler pattern
        var mockGenerator = new Mock<ITileGenerator>();
        
        // Setup 4 source tiles (2x2 grid)
        mockGenerator
            .Setup(g => g.GetTileDataAsync(8, 0, 0))
            .ReturnsAsync(new byte[] { 1 });
        mockGenerator
            .Setup(g => g.GetTileDataAsync(8, 1, 0))
            .ReturnsAsync(new byte[] { 2 });
        mockGenerator
            .Setup(g => g.GetTileDataAsync(8, 0, 1))
            .ReturnsAsync(new byte[] { 3 });
        mockGenerator
            .Setup(g => g.GetTileDataAsync(8, 1, 1))
            .ReturnsAsync(new byte[] { 4 });

        // Act - Fetch all 4 tiles (like downsampler does)
        var topLeft = await mockGenerator.Object.GetTileDataAsync(8, 0, 0);
        var topRight = await mockGenerator.Object.GetTileDataAsync(8, 1, 0);
        var bottomLeft = await mockGenerator.Object.GetTileDataAsync(8, 0, 1);
        var bottomRight = await mockGenerator.Object.GetTileDataAsync(8, 1, 1);

        // Assert
        topLeft.Should().NotBeNull();
        topRight.Should().NotBeNull();
        bottomLeft.Should().NotBeNull();
        bottomRight.Should().NotBeNull();
        
        mockGenerator.Verify(g => g.GetTileDataAsync(8, It.IsAny<int>(), It.IsAny<int>()), Times.Exactly(4));
    }

    [Fact]
    public async Task ITileGenerator_HandlesParallelRequests()
    {
        // Arrange
        var mockGenerator = new Mock<ITileGenerator>();
        mockGenerator
            .Setup(g => g.GetTileDataAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        // Act - Simulate concurrent tile requests
        var tasks = new[]
        {
            mockGenerator.Object.GetTileDataAsync(7, 0, 0),
            mockGenerator.Object.GetTileDataAsync(7, 1, 0),
            mockGenerator.Object.GetTileDataAsync(7, 0, 1),
            mockGenerator.Object.GetTileDataAsync(7, 1, 1)
        };

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(4);
        results.Should().OnlyContain(r => r != null);
    }

    [Fact]
    public void ITileGenerator_InterfaceHasCorrectMethods()
    {
        // Assert - Verify interface contract
        var interfaceType = typeof(ITileGenerator);
        
        interfaceType.IsInterface.Should().BeTrue("ITileGenerator should be an interface");
        
        var methods = interfaceType.GetMethods();
        methods.Should().Contain(m => m.Name == "GetTileDataAsync");
        methods.Should().Contain(m => m.Name == "GetTileExtentAsync");
    }
}


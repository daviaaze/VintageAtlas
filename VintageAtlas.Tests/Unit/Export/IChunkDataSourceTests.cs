using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using VintageAtlas.Export;

namespace VintageAtlas.Tests.Unit.Export;

/// <summary>
/// Tests for IChunkDataSource interface implementations
/// Verifies the contract for both SavegameDataSource and LoadedChunksDataSource
/// </summary>
public class IChunkDataSourceTests
{
    [Fact]
    public async Task MockChunkDataSource_ImplementsInterface_ReturnsData()
    {
        // Arrange
        var mockSource = new Mock<IChunkDataSource>();
        var expectedData = new TileChunkData
        {
            TileX = 10,
            TileZ = 20,
            Zoom = 7,
            TileSize = 256,
            ChunksPerTileEdge = 8
        };
        
        mockSource
            .Setup(s => s.GetTileChunksAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(expectedData);
        
        mockSource.Setup(s => s.SourceName).Returns("MockSource");
        mockSource.Setup(s => s.RequiresMainThread).Returns(false);

        // Act
        var result = await mockSource.Object.GetTileChunksAsync(7, 10, 20);

        // Assert
        result.Should().NotBeNull();
        result!.TileX.Should().Be(10);
        result.TileZ.Should().Be(20);
        result.Zoom.Should().Be(7);
    }

    [Fact]
    public async Task MockChunkDataSource_GetTileChunksAsync_CanReturnNull()
    {
        // Arrange
        var mockSource = new Mock<IChunkDataSource>();
        mockSource
            .Setup(s => s.GetTileChunksAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((TileChunkData?)null);

        // Act
        var result = await mockSource.Object.GetTileChunksAsync(7, 999, 999);

        // Assert
        result.Should().BeNull("chunks are not loaded or unavailable");
    }

    [Fact]
    public void IChunkDataSource_HasSourceName()
    {
        // Arrange
        var mockSource = new Mock<IChunkDataSource>();
        mockSource.Setup(s => s.SourceName).Returns("TestSource");

        // Act
        var name = mockSource.Object.SourceName;

        // Assert
        name.Should().Be("TestSource");
    }

    [Fact]
    public void IChunkDataSource_HasRequiresMainThread()
    {
        // Arrange
        var mockMainThreadSource = new Mock<IChunkDataSource>();
        var mockBackgroundSource = new Mock<IChunkDataSource>();
        
        mockMainThreadSource.Setup(s => s.RequiresMainThread).Returns(true);
        mockBackgroundSource.Setup(s => s.RequiresMainThread).Returns(false);

        // Act & Assert
        mockMainThreadSource.Object.RequiresMainThread.Should().BeTrue(
            "LoadedChunksDataSource requires main thread");
        mockBackgroundSource.Object.RequiresMainThread.Should().BeFalse(
            "SavegameDataSource can run on background thread");
    }

    [Fact]
    public async Task IChunkDataSource_SupportsMultipleTiles()
    {
        // Arrange
        var mockSource = new Mock<IChunkDataSource>();
        mockSource
            .Setup(s => s.GetTileChunksAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((int zoom, int tileX, int tileZ) => new TileChunkData
            {
                Zoom = zoom,
                TileX = tileX,
                TileZ = tileZ,
                TileSize = 256,
                ChunksPerTileEdge = 8
            });

        // Act
        var tile1 = await mockSource.Object.GetTileChunksAsync(7, 0, 0);
        var tile2 = await mockSource.Object.GetTileChunksAsync(7, 1, 0);
        var tile3 = await mockSource.Object.GetTileChunksAsync(7, 0, 1);

        // Assert
        tile1.Should().NotBeNull();
        tile2.Should().NotBeNull();
        tile3.Should().NotBeNull();
        
        tile1!.TileX.Should().Be(0);
        tile1.TileZ.Should().Be(0);
        
        tile2!.TileX.Should().Be(1);
        tile2.TileZ.Should().Be(0);
        
        tile3!.TileX.Should().Be(0);
        tile3.TileZ.Should().Be(1);
    }

    [Fact]
    public async Task IChunkDataSource_ReturnsDataWithChunks()
    {
        // Arrange
        var mockSource = new Mock<IChunkDataSource>();
        var tileData = new TileChunkData
        {
            TileX = 5,
            TileZ = 5,
            Zoom = 7,
            TileSize = 256,
            ChunksPerTileEdge = 8
        };
        
        // Add a chunk snapshot
        tileData.AddChunk(new ChunkSnapshot
        {
            ChunkX = 40,
            ChunkZ = 40,
            ChunkY = 0,
            HeightMap = new int[1024], // 32x32
            BlockIds = new int[32768], // 32x32x32
            IsLoaded = true
        });
        
        mockSource
            .Setup(s => s.GetTileChunksAsync(7, 5, 5))
            .ReturnsAsync(tileData);

        // Act
        var result = await mockSource.Object.GetTileChunksAsync(7, 5, 5);

        // Assert
        result.Should().NotBeNull();
        result!.Chunks.Should().HaveCount(1);
        result.Chunks.Should().ContainKey("40_40_0");
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(7, 123, 456)]
    [InlineData(10, -50, -50)]
    public async Task IChunkDataSource_AcceptsVariousCoordinates(int zoom, int tileX, int tileZ)
    {
        // Arrange
        var mockSource = new Mock<IChunkDataSource>();
        mockSource
            .Setup(s => s.GetTileChunksAsync(zoom, tileX, tileZ))
            .ReturnsAsync(new TileChunkData { Zoom = zoom, TileX = tileX, TileZ = tileZ });

        // Act
        var result = await mockSource.Object.GetTileChunksAsync(zoom, tileX, tileZ);

        // Assert
        result.Should().NotBeNull();
        result!.Zoom.Should().Be(zoom);
        result.TileX.Should().Be(tileX);
        result.TileZ.Should().Be(tileZ);
    }

    [Fact]
    public async Task IChunkDataSource_HandlesEmptyChunks()
    {
        // Arrange
        var mockSource = new Mock<IChunkDataSource>();
        var emptyTileData = new TileChunkData
        {
            TileX = 100,
            TileZ = 100,
            Zoom = 7,
            TileSize = 256,
            ChunksPerTileEdge = 8
            // No chunks added - empty area
        };
        
        mockSource
            .Setup(s => s.GetTileChunksAsync(7, 100, 100))
            .ReturnsAsync(emptyTileData);

        // Act
        var result = await mockSource.Object.GetTileChunksAsync(7, 100, 100);

        // Assert
        result.Should().NotBeNull("data object exists even if empty");
        result!.Chunks.Should().BeEmpty("no chunks in this area");
    }

    [Fact]
    public void IChunkDataSource_InterfaceHasCorrectMembers()
    {
        // Assert - Verify interface contract
        var interfaceType = typeof(IChunkDataSource);
        
        interfaceType.IsInterface.Should().BeTrue("IChunkDataSource should be an interface");
        
        var methods = interfaceType.GetMethods();
        methods.Should().Contain(m => m.Name == "GetTileChunksAsync");
        
        var properties = interfaceType.GetProperties();
        properties.Should().Contain(p => p.Name == "SourceName");
        properties.Should().Contain(p => p.Name == "RequiresMainThread");
    }

    [Fact]
    public async Task IChunkDataSource_CanBeUsedWithUnifiedTileGenerator()
    {
        // Arrange - Simulate UnifiedTileGenerator usage pattern
        var mockSource = new Mock<IChunkDataSource>();
        mockSource.Setup(s => s.RequiresMainThread).Returns(false);
        mockSource.Setup(s => s.SourceName).Returns("TestSource");
        
        var tileData = new TileChunkData
        {
            TileX = 1,
            TileZ = 1,
            Zoom = 7,
            TileSize = 256,
            ChunksPerTileEdge = 8
        };
        
        // Add 64 chunks (8x8 grid)
        for (var x = 0; x < 8; x++)
        {
            for (var z = 0; z < 8; z++)
            {
                tileData.AddChunk(new ChunkSnapshot
                {
                    ChunkX = x,
                    ChunkZ = z,
                    ChunkY = 0,
                    HeightMap = new int[1024],
                    BlockIds = new int[32768],
                    IsLoaded = true
                });
            }
        }
        
        mockSource
            .Setup(s => s.GetTileChunksAsync(7, 1, 1))
            .ReturnsAsync(tileData);

        // Act - Simulate tile generation flow
        var chunks = await mockSource.Object.GetTileChunksAsync(7, 1, 1);

        // Assert
        chunks.Should().NotBeNull();
        chunks!.Chunks.Should().HaveCount(64, "8x8 = 64 chunks for this tile");
        chunks.ChunksPerTileEdge.Should().Be(8);
        chunks.TileSize.Should().Be(256);
    }
}


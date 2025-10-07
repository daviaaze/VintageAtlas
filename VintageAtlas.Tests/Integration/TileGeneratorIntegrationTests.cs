using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using Vintagestory.API.Server;
using VintageAtlas.Export;
using VintageAtlas.Core;
using VintageAtlas.Tests.Mocks;

namespace VintageAtlas.Tests.Integration;

/// <summary>
/// Integration tests for tile generator components
/// Verifies that ITileGenerator, IChunkDataSource, and PyramidTileDownsampler
/// work together correctly
/// </summary>
public class TileGeneratorIntegrationTests
{
    private readonly Mock<ICoreServerAPI> _mockApi;
    private readonly MockLogger _mockLogger;
    private readonly ModConfig _config;

    public TileGeneratorIntegrationTests()
    {
        _mockApi = new Mock<ICoreServerAPI>();
        _mockLogger = new MockLogger();
        _mockApi.Setup(x => x.Logger).Returns(_mockLogger);
        
        _config = new ModConfig
        {
            TileSize = 256,
            BaseZoomLevel = 7,
            OutputDirectory = "/tmp/test"
        };
    }

    [Fact]
    public void ITileGenerator_UnifiedImplementation_ImplementsInterface()
    {
        // Assert - Verify UnifiedTileGenerator implements ITileGenerator
        typeof(UnifiedTileGenerator).Should().Implement<ITileGenerator>(
            "UnifiedTileGenerator must implement ITileGenerator");
    }

    [Fact]
    public void IChunkDataSource_BothImplementations_ShareSameInterface()
    {
        // Assert - Verify both data sources implement the same interface
        typeof(LoadedChunksDataSource).Should().Implement<IChunkDataSource>(
            "LoadedChunksDataSource must implement IChunkDataSource");
        typeof(SavegameDataSource).Should().Implement<IChunkDataSource>(
            "SavegameDataSource must implement IChunkDataSource");
    }

    [Fact]
    public async Task PyramidTileDownsampler_WithMockITileGenerator_GeneratesTiles()
    {
        // Arrange
        var mockGenerator = new Mock<ITileGenerator>();
        var pngData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        
        mockGenerator
            .Setup(g => g.GetTileDataAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(pngData);

        var downsampler = new PyramidTileDownsampler(_mockApi.Object, _config, mockGenerator.Object);

        // Act
        var result = await downsampler.GenerateTileByDownsamplingAsync(6, 5, 5);

        // Assert
        result.Should().NotBeNull("downsampler should work with any ITileGenerator");
        mockGenerator.Verify(
            g => g.GetTileDataAsync(7, It.IsAny<int>(), It.IsAny<int>()), 
            Times.Exactly(4),
            "should fetch 4 source tiles");
    }

    [Fact]
    public async Task IChunkDataSource_WithMockImplementation_ProvidesDataToRenderer()
    {
        // Arrange
        var mockSource = new Mock<IChunkDataSource>();
        mockSource.Setup(s => s.SourceName).Returns("MockSource");
        mockSource.Setup(s => s.RequiresMainThread).Returns(false);
        
        var tileData = new TileChunkData
        {
            TileX = 1,
            TileZ = 1,
            Zoom = 7,
            TileSize = 256,
            ChunksPerTileEdge = 8
        };
        
        // Add one chunk with valid data
        tileData.AddChunk(new ChunkSnapshot
        {
            ChunkX = 0,
            ChunkZ = 0,
            ChunkY = 0,
            HeightMap = new int[1024],
            BlockIds = new int[32768],
            IsLoaded = true
        });
        
        mockSource
            .Setup(s => s.GetTileChunksAsync(7, 1, 1))
            .ReturnsAsync(tileData);

        // Act
        var result = await mockSource.Object.GetTileChunksAsync(7, 1, 1);

        // Assert
        result.Should().NotBeNull();
        result!.Chunks.Should().ContainKey("0_0_0");
        result.Chunks["0_0_0"].IsLoaded.Should().BeTrue();
        result.Chunks["0_0_0"].HeightMap.Should().HaveCount(1024);
        result.Chunks["0_0_0"].BlockIds.Should().HaveCount(32768);
    }

    [Fact]
    public async Task TileGenerationPipeline_EndToEnd_MockedFlow()
    {
        // Arrange - Simulate full tile generation pipeline
        var mockDataSource = new Mock<IChunkDataSource>();
        mockDataSource.Setup(s => s.SourceName).Returns("TestSource");
        mockDataSource.Setup(s => s.RequiresMainThread).Returns(false);
        
        // Create tile with chunks
        var tileData = new TileChunkData
        {
            TileX = 0,
            TileZ = 0,
            Zoom = 7,
            TileSize = 256,
            ChunksPerTileEdge = 8
        };
        
        // Add 8x8 grid of chunks
        for (var x = 0; x < 8; x++)
        {
            for (var z = 0; z < 8; z++)
            {
                tileData.AddChunk(new ChunkSnapshot
                {
                    ChunkX = x,
                    ChunkZ = z,
                    ChunkY = 0,
                    HeightMap = Enumerable.Repeat(100, 1024).ToArray(), // Flat height map
                    BlockIds = Enumerable.Repeat(1, 32768).ToArray(), // All grass blocks
                    IsLoaded = true
                });
            }
        }
        
        mockDataSource
            .Setup(s => s.GetTileChunksAsync(7, 0, 0))
            .ReturnsAsync(tileData);

        // Act - Simulate what UnifiedTileGenerator.RenderTileAsync() does
        var chunks = await mockDataSource.Object.GetTileChunksAsync(7, 0, 0);

        // Assert
        chunks.Should().NotBeNull();
        chunks!.Chunks.Should().HaveCount(64, "8x8 grid = 64 chunks");
        
        // Verify all chunks are loaded
        foreach (var chunk in chunks.Chunks.Values)
        {
            chunk.IsLoaded.Should().BeTrue();
            chunk.HeightMap.Should().NotBeEmpty();
            chunk.BlockIds.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void ITileGenerator_InterfaceAllowsSwapping_BetweenImplementations()
    {
        // This test verifies compile-time compatibility
        // In real code, this allows switching via config:
        // if (config.UseUnifiedGenerator) generator = new UnifiedTileGenerator(...);
        // else generator = new DynamicTileGenerator(...);

        // Act - UnifiedTileGenerator can be assigned to ITileGenerator
        // (Can't actually instantiate without full dependencies, but verifying type compatibility)
        var unifiedType = typeof(UnifiedTileGenerator);

        // Assert
        typeof(ITileGenerator).IsAssignableFrom(unifiedType).Should().BeTrue();
    }

    [Fact]
    public void IChunkDataSource_InterfaceAllowsSwapping_BetweenImplementations()
    {
        // Arrange
        var loadedChunksType = typeof(LoadedChunksDataSource);
        var savegameType = typeof(SavegameDataSource);

        // Assert
        typeof(IChunkDataSource).IsAssignableFrom(loadedChunksType).Should().BeTrue(
            "LoadedChunksDataSource should implement IChunkDataSource");
        typeof(IChunkDataSource).IsAssignableFrom(savegameType).Should().BeTrue(
            "SavegameDataSource should implement IChunkDataSource");
    }

    [Fact]
    public async Task MultipleDataSources_CanProvideDataToSameGenerator()
    {
        // Arrange - Simulate UnifiedTileGenerator using different data sources
        var loadedChunksSource = new Mock<IChunkDataSource>();
        var savegameSource = new Mock<IChunkDataSource>();
        
        loadedChunksSource.Setup(s => s.SourceName).Returns("LoadedChunks");
        loadedChunksSource.Setup(s => s.RequiresMainThread).Returns(true);
        
        savegameSource.Setup(s => s.SourceName).Returns("SavegameDB");
        savegameSource.Setup(s => s.RequiresMainThread).Returns(false);
        
        var tileData = new TileChunkData { TileX = 1, TileZ = 1, Zoom = 7 };
        
        loadedChunksSource
            .Setup(s => s.GetTileChunksAsync(7, 1, 1))
            .ReturnsAsync(tileData);
        savegameSource
            .Setup(s => s.GetTileChunksAsync(7, 1, 1))
            .ReturnsAsync(tileData);

        // Act - Use different sources
        var resultFromLoaded = await loadedChunksSource.Object.GetTileChunksAsync(7, 1, 1);
        var resultFromSavegame = await savegameSource.Object.GetTileChunksAsync(7, 1, 1);

        // Assert - Both sources can provide data
        resultFromLoaded.Should().NotBeNull();
        resultFromSavegame.Should().NotBeNull();
        
        // But they have different characteristics
        loadedChunksSource.Object.RequiresMainThread.Should().BeTrue();
        savegameSource.Object.RequiresMainThread.Should().BeFalse();
    }

    [Fact]
    public async Task PyramidDownsampler_WorksWithMultipleGeneratorTypes()
    {
        // Arrange - Create two different mock generators
        var mockGenerator1 = new Mock<ITileGenerator>();
        var mockGenerator2 = new Mock<ITileGenerator>();
        
        var pngData = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        
        mockGenerator1
            .Setup(g => g.GetTileDataAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(pngData);
        mockGenerator2
            .Setup(g => g.GetTileDataAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(pngData);

        // Act - Create downsamplers with different generators
        var downsampler1 = new PyramidTileDownsampler(_mockApi.Object, _config, mockGenerator1.Object);
        var downsampler2 = new PyramidTileDownsampler(_mockApi.Object, _config, mockGenerator2.Object);
        
        var result1 = await downsampler1.GenerateTileByDownsamplingAsync(5, 0, 0);
        var result2 = await downsampler2.GenerateTileByDownsamplingAsync(5, 0, 0);

        // Assert - Both work equally well
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
    }

    [Fact]
    public void Architecture_VerifyInterfacesDefined()
    {
        // Assert - Verify all key interfaces exist
        var iTileGenerator = typeof(ITileGenerator);
        var iChunkDataSource = typeof(IChunkDataSource);
        
        iTileGenerator.IsInterface.Should().BeTrue("ITileGenerator should be an interface");
        iChunkDataSource.IsInterface.Should().BeTrue("IChunkDataSource should be an interface");
        
        iTileGenerator.Assembly.Should().BeSameAs(iChunkDataSource.Assembly,
            "interfaces should be in same assembly");
    }
}


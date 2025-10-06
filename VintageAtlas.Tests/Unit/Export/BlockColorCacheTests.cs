using System;
using System.Linq;
using Xunit;
using FluentAssertions;
using Moq;
using Vintagestory.API.Server;
using Vintagestory.API.Common;
using VintageAtlas.Export;
using VintageAtlas.Core;
using VintageAtlas.Tests.Mocks;

namespace VintageAtlas.Tests.Unit.Export;

/// <summary>
/// Tests for BlockColorCache - verifies color caching logic
/// Note: These tests use mocks since BlockColorCache depends on ICoreServerAPI
/// </summary>
public class BlockColorCacheTests
{
    private readonly Mock<ICoreServerAPI> _mockApi;
    private readonly MockLogger _mockLogger;
    private readonly Mock<IServerWorldAccessor> _mockWorld;
    private readonly ModConfig _config;

    public BlockColorCacheTests()
    {
        _mockApi = new Mock<ICoreServerAPI>();
        _mockLogger = new MockLogger();
        _mockWorld = new Mock<IServerWorldAccessor>();
        
        _mockApi.Setup(x => x.Logger).Returns(_mockLogger);
        _mockApi.Setup(x => x.World).Returns(_mockWorld.Object);
        
        // Mock empty block list for minimal initialization
        _mockWorld.Setup(x => x.Blocks).Returns(Array.Empty<Block>());
        
        _config = new ModConfig();
    }

    [Fact]
    public void Constructor_InitializesCache()
    {
        // Act
        var cache = new BlockColorCache(_mockApi.Object, _config);

        // Assert
        cache.Should().NotBeNull();
    }

    [Fact]
    public void GetColorVariations_WithNonExistentBlock_ReturnsNull()
    {
        // Arrange
        var cache = new BlockColorCache(_mockApi.Object, _config);
        cache.Initialize();
        var blockId = 999999;

        // Act
        var variations = cache.GetColorVariations(blockId);

        // Assert
        variations.Should().BeNull();
    }

    [Fact]
    public void GetBaseColor_WithInvalidBlockId_ReturnsDefaultColor()
    {
        // Arrange
        var cache = new BlockColorCache(_mockApi.Object, _config);
        cache.Initialize();
        var blockId = -1;

        // Act
        var color = cache.GetBaseColor(blockId);

        // Assert
        color.Should().NotBe(0); // Should return default land color
    }

    [Fact]
    public void IsLake_WithInvalidBlockId_ReturnsFalse()
    {
        // Arrange
        var cache = new BlockColorCache(_mockApi.Object, _config);
        cache.Initialize();

        // Act
        var isLake = cache.IsLake(-1);

        // Assert
        isLake.Should().BeFalse();
    }

    [Fact]
    public void Initialize_CanBeCalledOnlyOnce()
    {
        // Arrange
        var cache = new BlockColorCache(_mockApi.Object, _config);

        // Act
        cache.Initialize();
        var logsBefore = _mockLogger.Warnings.Count();
        cache.Initialize(); // Second call
        var logsAfter = _mockLogger.Warnings.Count();

        // Assert
        // Should log warning on second call
        (logsAfter - logsBefore).Should().Be(1, "second initialization should log a warning");
        _mockLogger.Warnings.Should().Contain(w => w.Contains("already initialized"));
    }

    [Fact]
    public void GetColorByMaterial_WithKnownMaterial_ReturnsColor()
    {
        // Act
        var color = BlockColorCache.GetColorByMaterial(EnumBlockMaterial.Stone);

        // Assert
        color.Should().NotBe(0);
    }

    [Fact]
    public void GetRandomColorVariation_WithNoVariations_ReturnsBaseColor()
    {
        // Arrange
        var cache = new BlockColorCache(_mockApi.Object, _config);
        cache.Initialize();
        var random = new Random(42);
        var blockId = 1;

        // Act
        var color = cache.GetRandomColorVariation(blockId, random);

        // Assert
        color.Should().NotBe(0);
    }

    [Fact]
    public void GetMedievalStyleColor_WithValidBlockId_ReturnsColor()
    {
        // Arrange
        var cache = new BlockColorCache(_mockApi.Object, _config);
        cache.Initialize();
        var blockId = 1;

        // Act
        var color = cache.GetMedievalStyleColor(blockId);

        // Assert
        color.Should().NotBe(0);
    }

    [Fact]
    public void GetMedievalStyleColor_WithWaterEdge_ReturnsColor()
    {
        // Arrange
        var cache = new BlockColorCache(_mockApi.Object, _config);
        cache.Initialize();
        var blockId = 1;

        // Act & Assert
        // With mocked setup, we're just testing the method doesn't crash
        // The actual color difference depends on block data which we don't have in this test
        var normalColor = cache.GetMedievalStyleColor(blockId, isWaterEdge: false);
        var edgeColor = cache.GetMedievalStyleColor(blockId, isWaterEdge: true);
        
        normalColor.Should().NotBe(0);
        edgeColor.Should().NotBe(0);
    }
}


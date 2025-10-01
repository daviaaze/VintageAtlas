using Xunit;
using FluentAssertions;
using VintageAtlas.Core;

namespace VintageAtlas.Tests.Unit.Core;

public class ConfigValidatorTests
{
    [Fact]
    public void Validate_WithValidConfig_ReturnsNoErrors()
    {
        // Arrange
        var config = new ModConfig
        {
            OutputDirectory = "ModData/VintageAtlas",
            TileSize = 256,
            BaseZoomLevel = 6,
            MaxDegreeOfParallelism = 4,
            ExtractWorldMap = true
        };

        // Act
        var errors = ConfigValidator.Validate(config);

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(16)]
    [InlineData(20)]
    public void Validate_WithInvalidBaseZoomLevel_ReturnsErrors(int zoomLevel)
    {
        // Arrange
        var config = new ModConfig 
        { 
            OutputDirectory = "ModData/VintageAtlas",
            BaseZoomLevel = zoomLevel,
            ExtractWorldMap = true
        };

        // Act
        var errors = ConfigValidator.Validate(config);

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain(e => e.Contains("BaseZoomLevel") || e.Contains("zoom"));
    }

    [Fact]
    public void Validate_WithEmptyOutputDirectory_ReturnsError()
    {
        // Arrange
        var config = new ModConfig 
        { 
            OutputDirectory = "",
            ExtractWorldMap = true
        };

        // Act
        var errors = ConfigValidator.Validate(config);

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain(e => e.Contains("OutputDirectory"));
    }

    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(1024)]
    public void Validate_WithValidTileSize_ReturnsNoErrors(int tileSize)
    {
        // Arrange
        var config = new ModConfig 
        { 
            OutputDirectory = "ModData/VintageAtlas",
            TileSize = tileSize,
            BaseZoomLevel = 6,
            ExtractWorldMap = true
        };

        // Act
        var errors = ConfigValidator.Validate(config);

        // Assert
        errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(31)]  // Not divisible by 32
    [InlineData(100)] // Not divisible by 32
    [InlineData(2000)] // Too large
    public void Validate_WithInvalidTileSize_ReturnsErrors(int tileSize)
    {
        // Arrange
        var config = new ModConfig 
        { 
            OutputDirectory = "ModData/VintageAtlas",
            TileSize = tileSize,
            ExtractWorldMap = true
        };

        // Act
        var errors = ConfigValidator.Validate(config);

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain(e => e.Contains("TileSize") || e.Contains("tile"));
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(0)]
    public void Validate_WithInvalidParallelism_ReturnsErrors(int parallelism)
    {
        // Arrange
        var config = new ModConfig 
        { 
            OutputDirectory = "ModData/VintageAtlas",
            MaxDegreeOfParallelism = parallelism,
            ExtractWorldMap = true
        };

        // Act
        var errors = ConfigValidator.Validate(config);

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain(e => e.Contains("MaxDegreeOfParallelism") || e.Contains("Parallelism"));
    }

    [Fact]
    public void Validate_WithMultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var config = new ModConfig 
        { 
            OutputDirectory = "",  // Invalid
            TileSize = 33,         // Invalid (not divisible by 32)
            BaseZoomLevel = 100,   // Invalid (too high)
            MaxDegreeOfParallelism = 0,  // Invalid
            ExtractWorldMap = true
        };

        // Act
        var errors = ConfigValidator.Validate(config);

        // Assert
        errors.Should().HaveCountGreaterThan(2);
    }

    [Fact]
    public void ApplyAutoFixes_WithInvalidTileSize_FixesToNearestValid()
    {
        // Arrange
        var config = new ModConfig 
        { 
            TileSize = 100,  // Not divisible by 32
            ExtractWorldMap = true
        };

        // Act
        ConfigValidator.ApplyAutoFixes(config);

        // Assert
        config.TileSize.Should().Be(96); // Nearest value divisible by 32
    }

    [Fact]
    public void Validate_WithZoomLevelsButNoExtract_ReturnsError()
    {
        // Arrange
        var config = new ModConfig 
        { 
            OutputDirectory = "ModData/VintageAtlas",
            ExtractWorldMap = false,
            CreateZoomLevels = true  // Requires ExtractWorldMap
        };

        // Act
        var errors = ConfigValidator.Validate(config);

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain(e => e.Contains("CreateZoomLevels") && e.Contains("ExtractWorldMap"));
    }
}


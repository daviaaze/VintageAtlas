using VintageAtlas.Application.DTOs;
using VintageAtlas.Application.Validation;

namespace VintageAtlas.Tests.Validation;

/// <summary>
/// Unit tests for ExportOptionsValidator.
/// Ensures business rules are properly enforced.
/// </summary>
public class ExportOptionsValidatorTests
{
    private readonly ExportOptionsValidator _validator = new();

    [Fact]
    public void Validate_WithValidOptions_ReturnsSuccess()
    {
        // Arrange
        var options = new ExportOptions
        {
            SaveMode = true,
            StopOnDone = false
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithNullOptions_ReturnsFailed()
    {
        // Act
        var result = _validator.Validate(null!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("cannot be null"));
    }

    [Fact]
    public void Validate_StopOnDoneWithoutSaveMode_ReturnsFailed()
    {
        // Arrange
        var options = new ExportOptions
        {
            SaveMode = false,
            StopOnDone = true  // Invalid: requires SaveMode
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("StopOnDone requires SaveMode"));
    }

    [Fact]
    public void Validate_StopOnDoneWithSaveMode_ReturnsSuccess()
    {
        // Arrange
        var options = new ExportOptions
        {
            SaveMode = true,
            StopOnDone = true  // Valid: SaveMode is enabled
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, false, true)]  // No options - valid
    [InlineData(true, false, true)]   // Just SaveMode - valid
    [InlineData(false, true, false)]  // Just StopOnDone - invalid
    [InlineData(true, true, true)]    // Both - valid
    public void Validate_VariousCombinations_ReturnsExpectedResult(
        bool saveMode, 
        bool stopOnDone, 
        bool expectedValid)
    {
        // Arrange
        var options = new ExportOptions
        {
            SaveMode = saveMode,
            StopOnDone = stopOnDone
        };

        // Act
        var result = _validator.Validate(options);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }
}


using System;
using Xunit;
using FluentAssertions;
using VintageAtlas.Models;

namespace VintageAtlas.Tests.Unit.Models;

public class TileResultTests
{
    [Fact]
    public void Constructor_InitializesWithDefaults()
    {
        // Act
        var result = new TileResult();

        // Assert
        result.Data.Should().BeNull();
        result.ETag.Should().BeNull();
        result.ContentType.Should().Be("image/png");
        result.NotModified.Should().BeFalse();
        result.NotFound.Should().BeFalse();
    }

    [Fact]
    public void Data_CanBeSetAndRetrieved()
    {
        // Arrange
        var result = new TileResult();
        var imageData = new byte[] { 1, 2, 3, 4 };

        // Act
        result.Data = imageData;

        // Assert
        result.Data.Should().BeEquivalentTo(imageData);
    }

    [Fact]
    public void ETag_CanBeSetAndRetrieved()
    {
        // Arrange
        var result = new TileResult();
        var etag = "\"abc123\"";

        // Act
        result.ETag = etag;

        // Assert
        result.ETag.Should().Be(etag);
    }

    [Fact]
    public void ContentType_DefaultsToImagePng()
    {
        // Act
        var result = new TileResult();

        // Assert
        result.ContentType.Should().Be("image/png");
    }

    [Fact]
    public void ContentType_CanBeChanged()
    {
        // Arrange
        var result = new TileResult();

        // Act
        result.ContentType = "image/jpeg";

        // Assert
        result.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public void LastModified_CanBeSet()
    {
        // Arrange
        var result = new TileResult();
        var timestamp = DateTime.UtcNow;

        // Act
        result.LastModified = timestamp;

        // Assert
        result.LastModified.Should().Be(timestamp);
    }

    [Fact]
    public void NotModified_DefaultsToFalse()
    {
        // Act
        var result = new TileResult();

        // Assert
        result.NotModified.Should().BeFalse();
    }

    [Fact]
    public void NotModified_CanBeSetToTrue()
    {
        // Arrange
        var result = new TileResult();

        // Act
        result.NotModified = true;

        // Assert
        result.NotModified.Should().BeTrue();
    }

    [Fact]
    public void NotFound_DefaultsToFalse()
    {
        // Act
        var result = new TileResult();

        // Assert
        result.NotFound.Should().BeFalse();
    }

    [Fact]
    public void NotFound_CanBeSetToTrue()
    {
        // Arrange
        var result = new TileResult();

        // Act
        result.NotFound = true;

        // Assert
        result.NotFound.Should().BeTrue();
    }

    [Fact]
    public void TileResult_SupportsFullConfiguration()
    {
        // Arrange
        var imageData = new byte[] { 10, 20, 30, 40 };
        var etag = "\"xyz789\"";
        var timestamp = new DateTime(2025, 10, 3, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var result = new TileResult
        {
            Data = imageData,
            ETag = etag,
            ContentType = "image/webp",
            LastModified = timestamp,
            NotModified = false,
            NotFound = false
        };

        // Assert
        result.Data.Should().BeEquivalentTo(imageData);
        result.ETag.Should().Be(etag);
        result.ContentType.Should().Be("image/webp");
        result.LastModified.Should().Be(timestamp);
        result.NotModified.Should().BeFalse();
        result.NotFound.Should().BeFalse();
    }
}


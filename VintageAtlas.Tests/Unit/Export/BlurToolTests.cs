using System;
using System.Linq;
using Xunit;
using FluentAssertions;
using VintageAtlas.Export;

namespace VintageAtlas.Tests.Unit.Export;

public class BlurToolTests
{
    [Fact]
    public void Blur_WithZeroRange_KeepsDataUnchanged()
    {
        // Arrange
        var data = new byte[9];
        data[4] = 255; // Center pixel bright
        var expected = (byte[])data.Clone();

        // Act
        BlurTool.Blur(data, 3, 3, 0);

        // Assert
        data.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Blur_WithSingleBrightPixel_SpreadsBrightness()
    {
        // Arrange
        var data = new byte[25]; // 5x5 grid
        data[12] = 255; // Center pixel (2,2) bright
        var originalCenterValue = data[12];

        // Act
        BlurTool.Blur(data, 5, 5, 2);

        // Assert
        // Center should be less bright after blur
        data[12].Should().BeLessThan(originalCenterValue);
        // Neighbors should have gained brightness
        data[11].Should().BeGreaterThan(0); // Left
        data[13].Should().BeGreaterThan(0); // Right
        data[7].Should().BeGreaterThan(0);  // Top
        data[17].Should().BeGreaterThan(0); // Bottom
    }

    [Fact]
    public void Blur_WithGradient_ModifiesData()
    {
        // Arrange
        var data = new byte[36]; // 6x6 grid (larger to see blur effect)
        // Create a step gradient from left to right
        for (int y = 0; y < 6; y++)
        {
            for (int x = 0; x < 6; x++)
            {
                data[y * 6 + x] = (byte)(x * 40); // 0, 40, 80, 120, 160, 200
            }
        }

        var originalData = (byte[])data.Clone();

        // Act
        BlurTool.Blur(data, 6, 6, 2);

        // Assert
        // After blur with range 2, at least some pixels should have changed
        // (checking center pixels which should be affected)
        var hasChanged = false;
        for (int i = 6; i < 30; i++) // Check middle area
        {
            if (data[i] != originalData[i])
            {
                hasChanged = true;
                break;
            }
        }
        hasChanged.Should().BeTrue("blur should modify at least some pixels in the center");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Blur_WithDifferentRanges_AffectsNeighbors(int range)
    {
        // Arrange
        var data = new byte[121]; // 11x11 grid (larger for clearer effect)
        data[60] = 200; // Center pixel (5,5) bright

        // Act
        BlurTool.Blur(data, 11, 11, range);

        // Assert
        // Blur should spread the brightness to neighbors
        // Check that at least one neighbor has non-zero value
        var neighborIndices = new[] { 59, 61, 49, 71 }; // Left, right, top, bottom
        var hasSpread = neighborIndices.Any(i => data[i] > 0);
        hasSpread.Should().BeTrue($"blur with range {range} should spread brightness to neighbors");
    }

    [Fact]
    public void Blur_PreservesTotalBrightness()
    {
        // Arrange
        var data = new byte[25]; // 5x5
        data[12] = 100; // Center
        data[6] = 50;
        data[18] = 75;
        
        var originalSum = SumArray(data);

        // Act
        BlurTool.Blur(data, 5, 5, 1);

        // Assert
        // Total brightness should be roughly preserved
        // (may have small differences due to integer rounding)
        var newSum = SumArray(data);
        newSum.Should().BeCloseTo(originalSum, 10);
    }

    [Fact]
    public void Blur_WithSmallImage_HandlesEdgesCorrectly()
    {
        // Arrange
        var data = new byte[4]; // 2x2 (minimal size)
        data[0] = 100;
        data[1] = 200;
        data[2] = 50;
        data[3] = 150;

        // Act
        Action act = () => BlurTool.Blur(data, 2, 2, 1);

        // Assert
        act.Should().NotThrow();
    }

    private static int SumArray(byte[] data)
    {
        int sum = 0;
        foreach (var b in data)
        {
            sum += b;
        }
        return sum;
    }
}


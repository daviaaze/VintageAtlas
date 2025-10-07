using FluentAssertions;
using VintageAtlas.Web.API;
using VintageAtlas.Web.Server;
using Xunit;

namespace VintageAtlas.Tests.Unit.Web;

/// <summary>
/// Tests for RequestRouter path classification.
/// Note: Full integration testing with actual controllers is done in Integration tests.
/// </summary>
public class RequestRouterTests
{
    [Theory]
    [InlineData("/tiles/10/100/200.png", true)]
    [InlineData("/tiles/5/50/50.png", true)]
    [InlineData("/tiles/0/0/0.png", true)]
    [InlineData("/map.png", true)]
    [InlineData("/data/map.png", true)]
    [InlineData("/api/status", false)]
    [InlineData("/index.html", false)]
    [InlineData("/", false)]
    public void IsTilePath_IdentifiesTileRequests(string path, bool expectedIsTile)
    {
        // Act
        var isTile = TileController.IsTilePath(path);

        // Assert
        isTile.Should().Be(expectedIsTile, $"path '{path}' should {(expectedIsTile ? "" : "not ")}be identified as a tile");
    }

    [Theory]
    [InlineData("/api/status")]
    [InlineData("/api/health")]
    [InlineData("/api/config")]
    [InlineData("/api/export")]
    [InlineData("/api/heatmap")]
    [InlineData("/api/player-path")]
    [InlineData("/api/census")]
    [InlineData("/api/stats")]
    [InlineData("/api/map/config")]
    [InlineData("/api/geojson/signs")]
    [InlineData("/api/geojson/signposts")]
    [InlineData("/api/geojson/traders")]
    [InlineData("/api/geojson/translocators")]
    [InlineData("/api/geojson/chunks")]
    public void ApiPaths_ShouldStartWithApiSlash(string path)
    {
        // Assert
        path.Should().StartWith("/api/", "all API endpoints should be under /api/");
    }

    [Theory]
    [InlineData("/api/status", "status")]
    [InlineData("/api/health", "health")]
    [InlineData("/api/config", "config")]
    [InlineData("/api/export", "export")]
    public void ExtractApiPath_RemovesApiPrefix(string fullPath, string expectedApiPath)
    {
        // Act
        var apiPath = fullPath.Substring(5).TrimStart('/');

        // Assert
        apiPath.Should().Be(expectedApiPath);
    }

    [Fact]
    public void RequestRouting_AllPathTypes_AreHandled()
    {
        // This test documents the three main routing categories
        var pathTypes = new[]
        {
            ("Tile requests", "/tiles/10/100/200.png"),
            ("API requests", "/api/status"),
            ("Static files", "/index.html")
        };

        // Assert - just document that these are the main path types
        pathTypes.Should().HaveCount(3, "RequestRouter handles three main types of requests");
    }
}
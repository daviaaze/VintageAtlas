using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using VintageAtlas.Storage;
using Xunit;

namespace VintageAtlas.Tests.Unit.Storage;

public class MbTilesStorageTests
{
    private readonly string _testDbPath;
    private readonly MbTilesStorage _storage;

    public MbTilesStorageTests()
    {
        // Use a unique temporary database for each test
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_mbtiles_{Guid.NewGuid()}.db");
        _storage = new MbTilesStorage(_testDbPath);
    }

    [Fact]
    public void Constructor_CreatesDatabase()
    {
        // Assert
        File.Exists(_testDbPath).Should().BeTrue("database file should be created");
    }

    [Fact]
    public void Constructor_CreatesDirectory_WhenNotExists()
    {
        // Arrange
        var subDir = Path.Combine(Path.GetTempPath(), $"test_subdir_{Guid.NewGuid()}");
        var dbPath = Path.Combine(subDir, "test.db");

        try
        {
            // Act
            using var storage = new MbTilesStorage(dbPath);

            // Assert
            Directory.Exists(subDir).Should().BeTrue("directory should be created");
            File.Exists(dbPath).Should().BeTrue("database should be created");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(subDir))
                Directory.Delete(subDir, true);
        }
    }

    [Fact]
    public async Task PutTileAsync_StoresTile()
    {
        // Arrange
        var zoom = 10;
        var x = 100;
        var y = 200;
        var tileData = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        await _storage.PutTileAsync(zoom, x, y, tileData);

        // Assert
        var retrieved = await _storage.GetTileAsync(zoom, x, y);
        retrieved.Should().NotBeNull();
        retrieved.Should().BeEquivalentTo(tileData);
    }

    [Fact]
    public async Task PutTileAsync_ReplacesExistingTile()
    {
        // Arrange
        var zoom = 10;
        var x = 100;
        var y = 200;
        var originalData = new byte[] { 1, 2, 3 };
        var newData = new byte[] { 4, 5, 6, 7 };

        // Act
        await _storage.PutTileAsync(zoom, x, y, originalData);
        await _storage.PutTileAsync(zoom, x, y, newData);

        // Assert
        var retrieved = await _storage.GetTileAsync(zoom, x, y);
        retrieved.Should().BeEquivalentTo(newData);
    }

    [Fact]
    public async Task GetTileAsync_ReturnsNull_WhenTileNotFound()
    {
        // Act
        var result = await _storage.GetTileAsync(10, 999, 999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task TileExistsAsync_ReturnsTrue_WhenTileExists()
    {
        // Arrange
        var zoom = 10;
        var x = 100;
        var y = 200;
        var tileData = new byte[] { 1, 2, 3 };
        await _storage.PutTileAsync(zoom, x, y, tileData);

        // Act
        var exists = await _storage.TileExistsAsync(zoom, x, y);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task TileExistsAsync_ReturnsFalse_WhenTileDoesNotExist()
    {
        // Act
        var exists = await _storage.TileExistsAsync(10, 999, 999);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTileAsync_RemovesTile()
    {
        // Arrange
        var zoom = 10;
        var x = 100;
        var y = 200;
        var tileData = new byte[] { 1, 2, 3 };
        await _storage.PutTileAsync(zoom, x, y, tileData);

        // Act
        await _storage.DeleteTileAsync(zoom, x, y);

        // Assert
        var exists = await _storage.TileExistsAsync(zoom, x, y);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTileAsync_DoesNotThrow_WhenTileDoesNotExist()
    {
        // Act
        Func<Task> act = async () => await _storage.DeleteTileAsync(10, 999, 999);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetTileCountAsync_ReturnsZero_WhenNoTiles()
    {
        // Act
        var count = await _storage.GetTileCountAsync();

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetTileCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        await _storage.PutTileAsync(10, 1, 1, [1]);
        await _storage.PutTileAsync(10, 2, 2, [2]);
        await _storage.PutTileAsync(11, 1, 1, [3]);

        // Act
        var totalCount = await _storage.GetTileCountAsync();
        var zoom10Count = await _storage.GetTileCountAsync(10);
        var zoom11Count = await _storage.GetTileCountAsync(11);

        // Assert
        totalCount.Should().Be(3);
        zoom10Count.Should().Be(2);
        zoom11Count.Should().Be(1);
    }

    [Fact]
    public async Task GetTileExtentAsync_ReturnsNull_WhenNoTiles()
    {
        // Act
        var extent = await _storage.GetTileExtentAsync(10);

        // Assert
        extent.Should().BeNull();
    }

    [Fact]
    public async Task GetTileExtentAsync_ReturnsCorrectExtent()
    {
        // Arrange
        await _storage.PutTileAsync(10, 100, 200, [1]);
        await _storage.PutTileAsync(10, 150, 250, [2]);
        await _storage.PutTileAsync(10, 120, 180, [3]);

        // Act
        var extent = await _storage.GetTileExtentAsync(10);

        // Assert
        extent.Should().NotBeNull();
        extent!.MinX.Should().Be(100);
        extent.MaxX.Should().Be(150);
        extent.MinY.Should().Be(180);
        extent.MaxY.Should().Be(250);
    }

    [Fact]
    public async Task GetTileExtentAsync_OnlyIncludesRequestedZoomLevel()
    {
        // Arrange
        await _storage.PutTileAsync(10, 100, 200, [1]);
        await _storage.PutTileAsync(10, 150, 250, [2]);
        await _storage.PutTileAsync(11, 999, 999, [3]);

        // Act
        var extent = await _storage.GetTileExtentAsync(10);

        // Assert
        extent.Should().NotBeNull();
        extent!.MinX.Should().Be(100);
        extent.MaxX.Should().Be(150);
        extent.MinY.Should().Be(200);
        extent.MaxY.Should().Be(250);
    }

    [Fact]
    public async Task VacuumAsync_DoesNotThrow()
    {
        // Arrange
        await _storage.PutTileAsync(10, 1, 1, [1]);

        // Act
        var act = async () => await _storage.VacuumAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetDatabaseSize_ReturnsPositiveNumber_AfterStoringTiles()
    {
        // Arrange
        await _storage.PutTileAsync(10, 1, 1, new byte[1024]);

        // Act
        var size = _storage.GetDatabaseSize();

        // Assert
        size.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CheckpointWal_DoesNotThrow()
    {
        // Arrange
        await _storage.PutTileAsync(10, 1, 1, [1]);

        // Act
        var act = () => _storage.CheckpointWal();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ConcurrentWrites_AllSucceed()
    {
        // Arrange
        var tasks = new Task[10];
        
        // Act
        for (var i = 0; i < 10; i++)
        {
            var index = i;
            tasks[i] = Task.Run(async () =>
            {
                await _storage.PutTileAsync(10, index, index, [(byte)index]);
            });
        }
        await Task.WhenAll(tasks);

        // Assert
        var count = await _storage.GetTileCountAsync(10);
        count.Should().Be(10);
    }

    [Fact]
    public async Task ConcurrentReads_AllSucceed()
    {
        // Arrange
        await _storage.PutTileAsync(10, 1, 1, [42]);
        var tasks = new Task<byte[]?>[10];
        
        // Act
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(async () => await _storage.GetTileAsync(10, 1, 1));
        }
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r =>
        {
            r.Should().NotBeNull();
            r.Should().BeEquivalentTo(new byte[] { 42 });
        });
    }

    [Fact]
    public async Task LargeTileData_StoresAndRetrievesCorrectly()
    {
        // Arrange
        var largeData = new byte[10 * 1024 * 1024]; // 10MB
        new Random().NextBytes(largeData);

        // Act
        await _storage.PutTileAsync(10, 1, 1, largeData);
        var retrieved = await _storage.GetTileAsync(10, 1, 1);

        // Assert
        retrieved.Should().BeEquivalentTo(largeData);
    }
}

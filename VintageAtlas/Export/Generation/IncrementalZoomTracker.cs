using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Server;

namespace VintageAtlas.Export.Generation;

/// <summary>
/// Tracks tile completion and triggers incremental zoom tile generation.
/// When all 4 child tiles for a zoom tile are ready, it immediately generates the parent tile.
/// This allows zoom generation to happen concurrently with base tile rendering.
/// </summary>
public class IncrementalZoomTracker
{
    private readonly ICoreServerAPI _sapi;
    private readonly UnifiedTileGenerator _generator;
    private readonly int _baseZoom;
    private readonly int _minZoom;

    // Track how many child tiles have completed for each parent tile
    // Key: (zoom, tileX, tileZ), Value: count of completed children (0-4)
    private readonly ConcurrentDictionary<(int zoom, int x, int z), int> _completionCount = new();

    // Track tiles that are currently being generated (to avoid duplicates)
    private readonly ConcurrentDictionary<(int zoom, int x, int z), byte> _generatingTiles = new();

    // Track tiles that have been generated (for statistics)
    private readonly ConcurrentDictionary<int, int> _tilesPerZoom = new();

    // Semaphore to limit concurrent zoom tile generation
    private readonly SemaphoreSlim _generationSemaphore;

    // Track in-progress generation tasks
    private readonly ConcurrentBag<Task> _activeTasks = new();

    private int _totalZoomTilesGenerated;
    private bool _isEnabled;

    public IncrementalZoomTracker(
        ICoreServerAPI sapi,
        UnifiedTileGenerator generator,
        int baseZoom,
        int minZoom = 0,
        int maxConcurrentZoomTiles = 4)
    {
        _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _baseZoom = baseZoom;
        _minZoom = minZoom;
        _generationSemaphore = new SemaphoreSlim(maxConcurrentZoomTiles, maxConcurrentZoomTiles);
        _isEnabled = true;
    }

    /// <summary>
    /// Enable or disable incremental zoom generation.
    /// When disabled, falls back to sequential generation.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
    }

    /// <summary>
    /// Notify that a tile has been completed.
    /// This will check if the parent zoom tile can now be generated.
    /// </summary>
    public void NotifyTileComplete(int zoom, int tileX, int tileZ)
    {
        if (!_isEnabled || zoom <= _minZoom)
        {
            return; // Already at minimum zoom or disabled
        }

        // Calculate parent tile coordinates
        var parentZoom = zoom - 1;
        var parentX = tileX / 2;
        var parentZ = tileZ / 2;
        var parentKey = (parentZoom, parentX, parentZ);

        // Check if we're already generating this tile
        if (_generatingTiles.ContainsKey(parentKey))
        {
            return;
        }

        // Increment the completion count for the parent tile
        var count = _completionCount.AddOrUpdate(
            parentKey,
            1,
            (key, oldValue) => oldValue + 1
        );

        // If all 4 children are ready, generate the parent tile
        if (count == 4)
        {
            // Mark as generating to prevent duplicates
            if (_generatingTiles.TryAdd(parentKey, 0))
            {
                // Generate asynchronously
                var task = Task.Run(async () =>
                {
                    try
                    {
                        await GenerateZoomTileAsync(parentZoom, parentX, parentZ);
                    }
                    catch (Exception ex)
                    {
                        _sapi.Logger.Error(
                            $"[VintageAtlas] Failed to generate incremental zoom tile {parentZoom}/{parentX}_{parentZ}: {ex.Message}");
                    }
                });

                _activeTasks.Add(task);
            }
        }
    }

    /// <summary>
    /// Generate a single zoom tile and notify about its completion (for cascading).
    /// </summary>
    private async Task GenerateZoomTileAsync(int zoom, int tileX, int tileZ)
    {
        // Wait for semaphore (limit concurrent generation)
        await _generationSemaphore.WaitAsync();

        try
        {
            // Generate the tile
            var tileData = await _generator.GetTileDataAsync(zoom + 1, tileX * 2, tileZ * 2);

            if (tileData != null)
            {
                var downsampler = new PyramidTileDownsampler(_sapi, _generator.Config, _generator);
                var downsampledTile = await downsampler.GenerateTileByDownsamplingAsync(zoom, tileX, tileZ);

                if (downsampledTile != null)
                {
                    await _generator.Storage.PutTileAsync(zoom, tileX, tileZ, downsampledTile);

                    // Track statistics
                    Interlocked.Increment(ref _totalZoomTilesGenerated);
                    _tilesPerZoom.AddOrUpdate(zoom, 1, (k, v) => v + 1);

                    // Log progress every 50 tiles
                    if (_totalZoomTilesGenerated % 50 == 0)
                    {
                        var stats = string.Join(", ", _tilesPerZoom
                            .OrderByDescending(kvp => kvp.Key)
                            .Select(kvp => $"z{kvp.Key}:{kvp.Value}"));
                        _sapi.Logger.Debug(
                            $"[VintageAtlas] Incremental zoom: {_totalZoomTilesGenerated} tiles ({stats})");
                    }

                    // Notify about this tile's completion (cascade to next zoom level)
                    NotifyTileComplete(zoom, tileX, tileZ);
                }
            }
        }
        finally
        {
            _generationSemaphore.Release();
            _generatingTiles.TryRemove((zoom, tileX, tileZ), out _);
        }
    }

    /// <summary>
    /// Wait for all pending zoom tile generation to complete.
    /// </summary>
    public async Task WaitForCompletionAsync()
    {
        if (!_isEnabled)
        {
            return;
        }

        _sapi.Logger.Notification("[VintageAtlas] Waiting for incremental zoom tile generation to complete...");

        // Wait for all active tasks
        var tasks = _activeTasks.ToArray();
        if (tasks.Length > 0)
        {
            await Task.WhenAll(tasks);
        }

        // Log final statistics
        if (_totalZoomTilesGenerated > 0)
        {
            var stats = string.Join(", ", _tilesPerZoom
                .OrderByDescending(kvp => kvp.Key)
                .Select(kvp => $"zoom {kvp.Key}: {kvp.Value} tiles"));
            _sapi.Logger.Notification(
                $"[VintageAtlas] Incremental zoom generation complete: {_totalZoomTilesGenerated} total tiles ({stats})");
        }
    }

    /// <summary>
    /// Get statistics about generated zoom tiles.
    /// </summary>
    public (int total, Dictionary<int, int> perZoom) GetStatistics()
    {
        return (_totalZoomTilesGenerated, new Dictionary<int, int>(_tilesPerZoom));
    }
}


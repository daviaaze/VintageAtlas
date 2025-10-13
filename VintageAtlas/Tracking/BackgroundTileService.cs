using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Export;

namespace VintageAtlas.Tracking;

/// <summary>
/// Background service for non-blocking tile generation
/// Processes a queue of tiles to generate, triggered by chunk updates
/// IMPROVED: Implements IAsyncServerSystem for proper Vintage Story API integration
/// </summary>
public class BackgroundTileService : IAsyncServerSystem, IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly TileGenerationState _state;
    private readonly ITileGenerator _tileGenerator;
    private readonly ChunkChangeTracker _chunkTracker;

    private Thread? _workerThread;
    private CancellationTokenSource? _cancellationToken;
    private readonly AutoResetEvent _wakeupSignal = new(false);

    private bool _isRunning;

    // IAsyncServerSystem implementation
    public long ElapsedMilliseconds { get; private set; }
    public bool Enabled { get; set; } = true;
    private long _lastChunkCheckTime;
    private long _lastStatisticsLogTime;
    private readonly int _chunkCheckIntervalMs = 5000; // Check for chunk updates every 5 seconds
    private readonly int _statisticsLogIntervalMs = 60000; // Log stats every minute

    // Configuration
    private readonly int _batchSize = 5; // Process 5 tiles at a time
    private readonly int _maxConcurrent = 2; // Max 2 tiles generating simultaneously
    private readonly int _minTimeBetweenBatchesMs = 1000; // Wait 1 second between batches

    public BackgroundTileService(
        ICoreServerAPI sapi,
        ModConfig config,
        TileGenerationState state,
        ITileGenerator tileGenerator,
        ChunkChangeTracker chunkTracker)
    {
        _sapi = sapi;
        _config = config;
        _state = state;
        _tileGenerator = tileGenerator;
        _chunkTracker = chunkTracker;

        _lastChunkCheckTime = sapi.World.ElapsedMilliseconds;
        _lastStatisticsLogTime = sapi.World.ElapsedMilliseconds;
    }

    /// <summary>
    /// Start the background tile generation service
    /// </summary>
    public void Start()
    {
        if (_isRunning)
        {
            _sapi.Logger.Warning("[VintageAtlas] Background tile service already running");
            return;
        }

        _isRunning = true;
        _cancellationToken = new CancellationTokenSource();

        _workerThread = new Thread(WorkerLoop)
        {
            Name = "VintageAtlas-TileGenerator",
            IsBackground = true, // Don't prevent server shutdown
            Priority = ThreadPriority.BelowNormal // Don't interfere with game thread
        };

        _workerThread.Start();

        _sapi.Logger.Notification("[VintageAtlas] Background tile generation service started");
    }

    /// <summary>
    /// Stop the background tile generation service
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;
        _cancellationToken?.Cancel();
        _wakeupSignal.Set(); // Wake up thread so it can exit

        _workerThread?.Join(TimeSpan.FromSeconds(5)); // Wait up to 5 seconds for graceful shutdown

        _sapi.Logger.Notification("[VintageAtlas] Background tile generation service stopped");
    }

    /// <summary>
    /// Main worker loop running in background thread
    /// </summary>
    private void WorkerLoop()
    {
        _sapi.Logger.Debug("[VintageAtlas] Background worker thread started");

        try
        {
            while (_isRunning && !_cancellationToken!.Token.IsCancellationRequested)
            {
                try
                {
                    // Check for chunk updates periodically
                    var now = _sapi.World.ElapsedMilliseconds;

                    if (now - _lastChunkCheckTime > _chunkCheckIntervalMs)
                    {
                        CheckForChunkUpdates();
                        _lastChunkCheckTime = now;
                    }

                    // Log statistics periodically
                    if (now - _lastStatisticsLogTime > _statisticsLogIntervalMs)
                    {
                        LogStatistics();
                        _lastStatisticsLogTime = now;
                    }

                    // Process next batch of tiles
                    ProcessNextBatch();

                    // Wait before processing next batch (or until woken up by new work)
                    _wakeupSignal.WaitOne(_minTimeBetweenBatchesMs);
                }
                catch (Exception ex)
                {
                    _sapi.Logger.Error($"[VintageAtlas] Error in background worker loop: {ex.Message}");
                    _sapi.Logger.Error(ex.StackTrace ?? "");

                    // Don't spam errors - wait a bit before continuing
                    Thread.Sleep(5000);
                }
            }
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Fatal error in background worker thread: {ex.Message}");
            _sapi.Logger.Error(ex.StackTrace ?? "");
        }

        _sapi.Logger.Debug("[VintageAtlas] Background worker thread exiting");
    }

    /// <summary>
    /// Check for chunk updates and queue affected tiles
    /// </summary>
    private void CheckForChunkUpdates()
    {
        try
        {
            var modifiedChunks = _chunkTracker.GetAllModifiedChunks();

            if (modifiedChunks.Count == 0) return;

            _sapi.Logger.Debug($"[VintageAtlas] Found {modifiedChunks.Count} modified chunks");

            // Find all tiles affected by these chunks
            var chunkList = modifiedChunks.Keys.ToList();
            var affectedTiles = _state.GetTilesAffectedByChunks(chunkList);

            if (affectedTiles.Count > 0)
            {
                // Queue for regeneration with high priority (chunk update)
                _state.QueueTilesForGeneration(affectedTiles, "chunk_update", priority: 8);

                _sapi.Logger.Notification($"[VintageAtlas] Queued {affectedTiles.Count} tiles for regeneration due to chunk updates");

                // Wake up worker to process immediately
                _wakeupSignal.Set();
            }
            else
            {
                // New chunks - calculate which tiles they belong to and queue them
                var newTiles = CalculateTilesForChunks(chunkList);
                if (newTiles.Count > 0)
                {
                    _state.QueueTilesForGeneration(newTiles, "new_chunks", priority: 6);

                    _sapi.Logger.Notification($"[VintageAtlas] Queued {newTiles.Count} new tiles for generation");

                    _wakeupSignal.Set();
                }
            }

            // Clear tracked changes now that we've processed them
            _chunkTracker.ClearAllChanges();
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Error checking for chunk updates: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculate which tiles contain the given chunks
    /// </summary>
    private List<TileCoordinate> CalculateTilesForChunks(List<Vec2i> chunks)
    {
        var tiles = new HashSet<TileCoordinate>();
        var chunksPerTile = _config.TileSize / 32; // 256 / 32 = 8

        foreach (var chunk in chunks)
        {
            // Calculate tile coordinates at base zoom level
            var tileX = chunk.X / chunksPerTile;
            var tileZ = chunk.Y / chunksPerTile;

            tiles.Add(new TileCoordinate
            {
                Zoom = _config.BaseZoomLevel,
                X = tileX,
                Z = tileZ
            });
        }

        return tiles.ToList();
    }

    /// <summary>
    /// Process next batch of tiles from the queue
    /// </summary>
    private void ProcessNextBatch()
    {
        try
        {
            var batch = _state.GetNextBatch(_batchSize);

            if (batch.Count == 0) return;

            _sapi.Logger.Debug($"[VintageAtlas] Processing batch of {batch.Count} tiles");

            // Process tiles with limited concurrency
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = _maxConcurrent,
                CancellationToken = _cancellationToken!.Token
            };

            Parallel.ForEach(batch, options, tile =>
            {
                ProcessTile(tile);
            });
        }
        catch (OperationCanceledException)
        {
            _sapi.Logger.Debug("[VintageAtlas] Batch processing cancelled");
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Error processing batch: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate a single tile
    /// </summary>
    private void ProcessTile(TileCoordinate tile)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            _sapi.Logger.VerboseDebug($"[VintageAtlas] Generating tile {tile.Zoom}/{tile.X}_{tile.Z}");

            // Generate the tile asynchronously (but wait for it in this background thread)
            var task = _tileGenerator.GetTileDataAsync(tile.Zoom, tile.X, tile.Z);
            task.Wait(_cancellationToken!.Token);

            var result = task.Result;

            if (result == null)
            {
                _state.RecordTileError(tile.Zoom, tile.X, tile.Z, "No data available");
                _sapi.Logger.Debug($"[VintageAtlas] No data for tile {tile.Zoom}/{tile.X}_{tile.Z}");
                return;
            }

            sw.Stop();

            // Record successful generation
            var chunks = CalculateChunksForTile(tile.Zoom, tile.X, tile.Z);
            _state.RecordTileGenerated(
                tile.Zoom,
                tile.X,
                tile.Z,
                sw.ElapsedMilliseconds,
                chunks.Count,
                result?.Length ?? 0
            );

            // Map chunks to tile for future invalidation
            _state.MapChunksToTile(tile.Zoom, tile.X, tile.Z, chunks);

            _sapi.Logger.Debug($"[VintageAtlas] Generated tile {tile.Zoom}/{tile.X}_{tile.Z} in {sw.ElapsedMilliseconds}ms");
        }
        catch (OperationCanceledException)
        {
            _sapi.Logger.Debug($"[VintageAtlas] Tile generation cancelled: {tile.Zoom}/{tile.X}_{tile.Z}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _state.RecordTileError(tile.Zoom, tile.X, tile.Z, ex.Message);
            _sapi.Logger.Error($"[VintageAtlas] Failed to generate tile {tile.Zoom}/{tile.X}_{tile.Z}: {ex.Message}");
        }
    }

    /// <summary>
    /// Calculate which chunks are contained in a tile
    /// </summary>
    private List<Vec2i> CalculateChunksForTile(int zoom, int tileX, int tileZ)
    {
        var chunks = new List<Vec2i>();
        var chunksPerTile = _config.TileSize / 32; // 256 / 32 = 8

        var startChunkX = tileX * chunksPerTile;
        var startChunkZ = tileZ * chunksPerTile;

        for (var cx = 0; cx < chunksPerTile; cx++)
        {
            for (var cz = 0; cz < chunksPerTile; cz++)
            {
                chunks.Add(new Vec2i(startChunkX + cx, startChunkZ + cz));
            }
        }

        return chunks;
    }

    /// <summary>
    /// Log statistics about tile generation
    /// </summary>
    private void LogStatistics()
    {
        try
        {
            var stats = _state.GetStatistics();

            _sapi.Logger.Notification(
                $"[VintageAtlas] Tile Statistics: " +
                $"{stats.ReadyTiles}/{stats.TotalTiles} ready, " +
                $"{stats.QueuedTiles} queued, " +
                $"{stats.ErrorTiles} errors, " +
                $"{stats.TotalSizeBytes / 1024 / 1024:F1} MB, " +
                $"avg {stats.AverageGenerationTimeMs:F0}ms"
            );
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Error logging statistics: {ex.Message}");
        }
    }

    /// <summary>
    /// Manually queue a tile for generation
    /// </summary>
    public void QueueTile(int zoom, int tileX, int tileZ, string reason = "manual", int priority = 5)
    {
        var tile = new TileCoordinate { Zoom = zoom, X = tileX, Z = tileZ };
        _state.QueueTilesForGeneration(new List<TileCoordinate> { tile }, reason, priority);
        _wakeupSignal.Set(); // Wake up worker
    }

    /// <summary>
    /// Queue an area of tiles for generation
    /// </summary>
    public void QueueArea(int zoom, int minX, int minZ, int maxX, int maxZ, string reason = "area", int priority = 3)
    {
        var tiles = new List<TileCoordinate>();

        for (var x = minX; x <= maxX; x++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                tiles.Add(new TileCoordinate { Zoom = zoom, X = x, Z = z });
            }
        }

        _state.QueueTilesForGeneration(tiles, reason, priority);
        _wakeupSignal.Set();

        _sapi.Logger.Notification($"[VintageAtlas] Queued {tiles.Count} tiles for area generation");
    }

    /// <summary>
    /// Get current statistics
    /// </summary>
    public TileStatistics GetStatistics()
    {
        return _state.GetStatistics();
    }

    /// <summary>
    /// IAsyncServerSystem: Called from server tick (non-blocking)
    /// Allows the server to monitor and control the async system
    /// </summary>
    public void OnAsyncServerTick(float dt)
    {
        // Update elapsed time for tracking
        ElapsedMilliseconds = _sapi.World.ElapsedMilliseconds;

        // This is called from the server thread, but our actual work
        // happens in the background thread - this is just for monitoring
        if (!Enabled || !_isRunning)
        {
            return;
        }

        // Optional: Could add heartbeat monitoring here
        // to detect if background thread has stalled
    }

    /// <summary>
    /// IAsyncServerSystem: Called before first tick
    /// </summary>
    public void OnStart()
    {
        Start();
    }

    /// <summary>
    /// IAsyncServerSystem: Called on server shutdown
    /// </summary>
    public void OnShutdown()
    {
        Stop();
    }

    /// <summary>
    /// IAsyncServerSystem: Called on restart (just stop/start)
    /// </summary>
    public void OnRestart()
    {
        Stop();
        Thread.Sleep(500); // Brief pause
        Start();
    }

    /// <summary>
    /// IAsyncServerSystem: Tick on separate thread
    /// </summary>
    public void OnSeparateThreadTick()
    {
        // This is called on the separate thread
        // Our worker loop handles this differently
    }

    /// <summary>
    /// IAsyncServerSystem: Dispose on separate thread
    /// </summary>
    public void ThreadDispose()
    {
        Stop();
    }

    /// <summary>
    /// IAsyncServerSystem: Get interval for off-thread ticks (milliseconds)
    /// </summary>
    public int OffThreadInterval()
    {
        return 1000; // Check every second
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Stop();
            _cancellationToken?.Dispose();
            _wakeupSignal.Dispose();

            _sapi.Logger.Notification("[VintageAtlas] Background tile service disposed");
        }
    }
}


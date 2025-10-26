using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Server;
using VintageAtlas.Core;
using VintageAtlas.Export.Data;
using VintageAtlas.Export.DataSources;
using VintageAtlas.Models.Domain;

namespace VintageAtlas.Export.Extraction;

/// <summary>
/// Orchestrates the extraction pipeline by iterating through chunks ONCE
/// and allowing multiple extractors to process each chunk.
/// This avoids redundant iteration and multiple database loads.
/// </summary>
public class ExportOrchestrator : IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly ServerMain _server;
    private readonly ModConfig _config;
    private readonly List<IDataExtractor> _extractors = new();

    private const int ChunkSize = 32;

    public ExportOrchestrator(ICoreServerAPI sapi, ModConfig config)
    {
        _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _server = (ServerMain)sapi.World;
    }

    /// <summary>
    /// Register an extractor to be included in the pipeline.
    /// </summary>
    public void RegisterExtractor(IDataExtractor extractor)
    {
        if (!_extractors.Contains(extractor))
        {
            _extractors.Add(extractor);
            _sapi.Logger.Debug($"[VintageAtlas] Registered extractor: {extractor.Name}");
        }
    }

    /// <summary>
    /// Execute full map export from savegame database.
    /// Iterates through chunks ONCE and calls all extractors for each chunk.
    /// </summary>
    public async Task ExecuteFullExportAsync(IProgress<ExportProgress>? progress = null)
    {
        _sapi.Logger.Notification("[VintageAtlas] Starting full export from savegame database...");

        // Create savegame data source for reading all chunks from database
        using var savegameDataSource = new SavegameDataSource(_server, _config, _sapi.Logger);

        // Get all chunk positions to process
        var chunkPositions = savegameDataSource.GetAllMapChunkPositions();
        _sapi.Logger.Notification($"[VintageAtlas] Found {chunkPositions.Count} chunks to process");

        if (chunkPositions.Count == 0)
        {
            _sapi.Logger.Warning("[VintageAtlas] No chunks found!");
            return;
        }

        // Detect if we need to load chunks into game memory (for OnDemand climate)
        var climateExtractor = _extractors.OfType<ClimateExtractor>().FirstOrDefault();
        var needsLoadedChunks = climateExtractor != null &&
                               _config.ClimateMode == ClimateExtractionMode.OnDemand;

        if (needsLoadedChunks)
        {
            _sapi.Logger.Notification("[VintageAtlas] 🔄 UNIFIED PROCESSING: Loading chunks into game memory for OnDemand climate");
            _sapi.Logger.Notification("[VintageAtlas] All extractors will process loaded chunks in a single pass");
            await ExecuteWithLoadedChunksAsync(chunkPositions, progress);
        }
        else
        {
            _sapi.Logger.Notification("[VintageAtlas] Using database chunks (Fast climate mode or no climate)");
            await ExecuteWithDatabaseChunksAsync(savegameDataSource, chunkPositions, progress);
        }

        _sapi.Logger.Notification("[VintageAtlas] Full export completed");
    }

    /// <summary>
    /// Execute export using database chunks (no game memory loading required).
    /// Used for Fast climate mode or when climate is disabled.
    /// </summary>
    private async Task ExecuteWithDatabaseChunksAsync(
        SavegameDataSource savegameDataSource,
        List<Vec2i> chunkPositions,
        IProgress<ExportProgress>? progress)
    {
        // Initialize all extractors
        _sapi.Logger.Notification("[VintageAtlas] Initializing extractors...");
        foreach (var extractor in _extractors)
        {
            try
            {
                await extractor.InitializeAsync();
                _sapi.Logger.Debug($"[VintageAtlas] Initialized: {extractor.Name}");
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to initialize '{extractor.Name}': {ex.Message}");
                _sapi.Logger.Error(ex.StackTrace ?? "");
            }
        }

        // Calculate tiles to process (for progress reporting and batching)
        var chunksPerTile = _config.TileSize / ChunkSize;
        var tiles = CalculateTileCoverage(chunkPositions, chunksPerTile);
        _sapi.Logger.Notification($"[VintageAtlas] Processing {tiles.Count} tiles with {_extractors.Count} extractors");

        var tilesProcessed = 0;
        var totalChunks = 0;

        // Process each tile (which contains multiple chunks)
        foreach (var tile in tiles)
        {
            try
            {
                // Load all chunks for this tile from database
                var tileData = await savegameDataSource.GetTileChunksAsync(_config.BaseZoomLevel, tile.X, tile.Y);

                if (tileData?.Chunks != null)
                {
                    // Process each chunk in this tile with all extractors
                    foreach (var chunkSnapshot in tileData.Chunks.Values)
                    {
                        // Call all extractors for this chunk
                        foreach (var extractor in _extractors)
                        {
                            try
                            {
                                await extractor.ProcessChunkAsync(chunkSnapshot);
                            }
                            catch (Exception ex)
                            {
                                _sapi.Logger.Error(
                                    $"[VintageAtlas] Extractor '{extractor.Name}' failed on chunk " +
                                    $"({chunkSnapshot.ChunkX},{chunkSnapshot.ChunkZ}): {ex.Message}");
                            }
                        }

                        totalChunks++;
                    }
                }

                tilesProcessed++;
                if (tilesProcessed % 100 == 0)
                {
                    _sapi.Logger.Notification(
                        $"[VintageAtlas] Processed {tilesProcessed}/{tiles.Count} tiles, {totalChunks} chunks");

                    progress?.Report(new ExportProgress
                    {
                        TilesCompleted = tilesProcessed,
                        TotalTiles = tiles.Count,
                        CurrentZoomLevel = _config.BaseZoomLevel
                    });
                }
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to process tile {tile.X},{tile.Y}: {ex.Message}");
            }
        }

        _sapi.Logger.Notification($"[VintageAtlas] Chunk iteration complete: processed {totalChunks} chunks");

        // Finalize all extractors
        _sapi.Logger.Notification("[VintageAtlas] Finalizing extractors...");
        foreach (var extractor in _extractors)
        {
            try
            {
                _sapi.Logger.Notification($"[VintageAtlas] Finalizing: {extractor.Name}");
                await extractor.FinalizeAsync(progress);
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to finalize '{extractor.Name}': {ex.Message}");
                _sapi.Logger.Error(ex.StackTrace ?? "");
            }
        }
    }

    /// <summary>
    /// Execute export by loading chunks into game memory.
    /// Used for OnDemand climate mode where all extractors process loaded chunks in one pass.
    /// </summary>
    private async Task ExecuteWithLoadedChunksAsync(
        List<Vec2i> chunkPositions,
        IProgress<ExportProgress>? progress)
    {
        // Initialize all extractors
        _sapi.Logger.Notification("[VintageAtlas] Initializing extractors...");
        foreach (var extractor in _extractors)
        {
            try
            {
                await extractor.InitializeAsync();
                _sapi.Logger.Debug($"[VintageAtlas] Initialized: {extractor.Name}");
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to initialize '{extractor.Name}': {ex.Message}");
                _sapi.Logger.Error(ex.StackTrace ?? "");
            }
        }

        var processedChunks = 0;
        var batchSize = 500; // Load chunks in batches
        var loadedChunks = new List<Vec2i>();

        _sapi.Logger.Notification($"[VintageAtlas] Processing {chunkPositions.Count} chunks in batches of {batchSize}");

        for (var i = 0; i < chunkPositions.Count; i += batchSize)
        {
            var batch = chunkPositions.Skip(i).Take(batchSize).ToList();
            var batchNum = (i / batchSize) + 1;
            var totalBatches = (chunkPositions.Count + batchSize - 1) / batchSize;

            _sapi.Logger.Debug($"[VintageAtlas] Loading batch {batchNum}/{totalBatches} ({batch.Count} chunks)");

            // Load batch of chunks into game memory
            foreach (var chunkPos in batch)
            {
                try
                {
                    var isLoaded = _sapi.World.BlockAccessor.GetChunk(chunkPos.X, 0, chunkPos.Y) != null;

                    if (!isLoaded)
                    {
                        var loadCompletionSource = new TaskCompletionSource<bool>();

                        _sapi.WorldManager.LoadChunkColumnPriority(
                            chunkPos.X * ChunkSize,
                            chunkPos.Y * ChunkSize,
                            new ChunkLoadOptions
                            {
                                KeepLoaded = true,
                                OnLoaded = () => { loadCompletionSource.TrySetResult(true); }
                            }
                        );

                        var loadedSuccessfully = await Task.WhenAny(
                            loadCompletionSource.Task,
                            Task.Delay(5000)
                        ) == loadCompletionSource.Task && loadCompletionSource.Task.Result;

                        if (loadedSuccessfully)
                        {
                            loadedChunks.Add(chunkPos);
                        }
                    }

                    // Process this chunk with ALL extractors
                    var worldMap = ((Vintagestory.Server.ServerMain)_sapi.World).WorldMap;
                    var mapChunk = worldMap.GetMapChunk(chunkPos.X, chunkPos.Y);

                    if (mapChunk != null)
                    {
                        var snapshot = CreateChunkSnapshot(chunkPos.X, chunkPos.Y, mapChunk);

                        foreach (var extractor in _extractors)
                        {
                            try
                            {
                                await extractor.ProcessChunkAsync(snapshot);
                            }
                            catch (Exception ex)
                            {
                                _sapi.Logger.Error(
                                    $"[VintageAtlas] Extractor '{extractor.Name}' failed on chunk " +
                                    $"({chunkPos.X},{chunkPos.Y}): {ex.Message}");
                            }
                        }
                    }

                    processedChunks++;
                    if (processedChunks % 100 == 0)
                    {
                        _sapi.Logger.Notification($"[VintageAtlas] UNIFIED: Processed {processedChunks}/{chunkPositions.Count} chunks");
                        progress?.Report(new ExportProgress
                        {
                            TilesCompleted = processedChunks,
                            TotalTiles = chunkPositions.Count,
                            CurrentZoomLevel = 0
                        });
                    }
                }
                catch (Exception ex)
                {
                    _sapi.Logger.Warning($"[VintageAtlas] UNIFIED: Failed to load/process chunk ({chunkPos.X},{chunkPos.Y}): {ex.Message}");
                }
            }

            // Unload the chunks we loaded
            foreach (var chunkPos in loadedChunks)
            {
                try
                {
                    _sapi.WorldManager.UnloadChunkColumn(chunkPos.X * ChunkSize, chunkPos.Y * ChunkSize);
                }
                catch (Exception ex)
                {
                    _sapi.Logger.VerboseDebug($"[VintageAtlas] Failed to unload chunk ({chunkPos.X},{chunkPos.Y}): {ex.Message}");
                }
            }
            loadedChunks.Clear();

            // Small delay between batches
            await Task.Delay(100);
        }

        _sapi.Logger.Notification($"[VintageAtlas] 🔄 UNIFIED PROCESSING complete: {processedChunks} chunks processed with all extractors");

        // Finalize all extractors
        _sapi.Logger.Notification("[VintageAtlas] Finalizing extractors...");
        foreach (var extractor in _extractors)
        {
            try
            {
                _sapi.Logger.Notification($"[VintageAtlas] Finalizing: {extractor.Name}");
                await extractor.FinalizeAsync(progress);
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to finalize '{extractor.Name}': {ex.Message}");
                _sapi.Logger.Error(ex.StackTrace ?? "");
            }
        }
    }

    /// <summary>
    /// Create a chunk snapshot from map chunk data (for unified processing).
    /// </summary>
    private ChunkSnapshot CreateChunkSnapshot(int chunkX, int chunkZ, Vintagestory.API.Common.IMapChunk mapChunk)
    {
        var heightMap = new int[ChunkSize * ChunkSize];

        if (mapChunk.RainHeightMap != null)
        {
            for (var i = 0; i < Math.Min(heightMap.Length, mapChunk.RainHeightMap.Length); i++)
            {
                heightMap[i] = mapChunk.RainHeightMap[i];
            }
        }

        return new ChunkSnapshot
        {
            ChunkX = chunkX,
            ChunkZ = chunkZ,
            HeightMap = heightMap,
            IsLoaded = true
        };
    }

    /// <summary>
    /// Execute live extraction from loaded chunks only.
    /// This processes only chunks that are currently loaded in the game's memory.
    /// Used for incremental updates during gameplay.
    /// </summary>
    public async Task ExecuteLiveExtractionAsync(IProgress<ExportProgress>? progress = null)
    {
        _sapi.Logger.Notification("[VintageAtlas] Starting live extraction from loaded chunks...");

        // Set extractors to live mode
        foreach (var extractor in _extractors)
        {
            if (extractor is ClimateExtractor climateExtractor)
            {
                climateExtractor.SetLiveChunkMode(true);
            }
        }

        // Initialize extractors
        foreach (var extractor in _extractors)
        {
            try
            {
                await extractor.InitializeAsync();
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to initialize '{extractor.Name}': {ex.Message}");
            }
        }

        // Get all currently loaded chunks
        var loadedChunks = GetLoadedChunks();
        _sapi.Logger.Notification($"[VintageAtlas] Found {loadedChunks.Count} loaded chunks");

        if (loadedChunks.Count == 0)
        {
            _sapi.Logger.Warning("[VintageAtlas] No chunks currently loaded");
            return;
        }

        // Process each loaded chunk with all extractors
        var processedChunks = 0;
        foreach (var chunk in loadedChunks)
        {
            foreach (var extractor in _extractors)
            {
                try
                {
                    await extractor.ProcessChunkAsync(chunk);
                }
                catch (Exception ex)
                {
                    _sapi.Logger.Error(
                        $"[VintageAtlas] Extractor '{extractor.Name}' failed on chunk " +
                        $"({chunk.ChunkX},{chunk.ChunkZ}): {ex.Message}");
                }
            }

            processedChunks++;
            if (processedChunks % 100 == 0)
            {
                _sapi.Logger.Debug($"[VintageAtlas] Processed {processedChunks}/{loadedChunks.Count} loaded chunks");
            }
        }

        // Finalize extractors
        foreach (var extractor in _extractors)
        {
            try
            {
                await extractor.FinalizeAsync(progress);
            }
            catch (Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] Failed to finalize '{extractor.Name}': {ex.Message}");
            }
        }

        _sapi.Logger.Notification($"[VintageAtlas] Live extraction completed: processed {processedChunks} chunks");
    }

    /// <summary>
    /// Get all currently loaded chunks from the game.
    /// This creates ChunkSnapshots from the loaded chunks in memory.
    /// </summary>
    private List<ChunkSnapshot> GetLoadedChunks()
    {
        var snapshots = new List<ChunkSnapshot>();

        // Get loaded chunk positions from the server's world map
        // We'll iterate through a reasonable area around spawn and check which chunks are loaded
        var worldMap = _server.WorldMap;

        // Get all players to determine active areas
        var players = _sapi.World.AllOnlinePlayers;
        var activeChunks = new HashSet<Vec2i>();

        // Collect chunks around each player
        foreach (var player in players)
        {
            if (player?.Entity == null) continue;

            var playerChunkX = (int)player.Entity.Pos.X / 32;
            var playerChunkZ = (int)player.Entity.Pos.Z / 32;

            // Get chunks in a radius around the player (typical view distance)
            var radius = 12; // chunks
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    activeChunks.Add(new Vec2i(playerChunkX + dx, playerChunkZ + dz));
                }
            }
        }

        // Create snapshots for active chunks that have data
        foreach (var chunkPos in activeChunks)
        {
            try
            {
                // Check if this chunk is actually loaded
                var chunk = _sapi.World.BlockAccessor.GetChunk(chunkPos.X, 0, chunkPos.Y);
                if (chunk == null) continue;

                // Get the map chunk (contains height map)
                var mapChunk = worldMap.GetMapChunk(chunkPos.X, chunkPos.Y);

                if (mapChunk == null || mapChunk.RainHeightMap == null)
                    continue;

                // Create a snapshot for this chunk
                var snapshot = CreateSnapshotFromLoadedChunk(chunkPos.X, chunkPos.Y, mapChunk);
                if (snapshot != null)
                {
                    snapshots.Add(snapshot);
                }
            }
            catch (Exception ex)
            {
                _sapi.Logger.VerboseDebug(
                    $"[VintageAtlas] Failed to snapshot loaded chunk ({chunkPos.X},{chunkPos.Y}): {ex.Message}");
            }
        }

        return snapshots;
    }

    /// <summary>
    /// Create a ChunkSnapshot from a currently loaded chunk.
    /// </summary>
    private ChunkSnapshot CreateSnapshotFromLoadedChunk(int chunkX, int chunkZ, Vintagestory.API.Common.IMapChunk mapChunk)
    {
        const int chunkSize = 32;
        var heightMap = new int[chunkSize * chunkSize];

        // Copy height map
        if (mapChunk.RainHeightMap != null)
        {
            for (var i = 0; i < Math.Min(mapChunk.RainHeightMap.Length, heightMap.Length); i++)
            {
                heightMap[i] = mapChunk.RainHeightMap[i];
            }
        }

        // Determine average surface Y level
        var validHeights = heightMap.Where(h => h > 0).ToArray();
        var avgHeight = validHeights.Length > 0 ? (int)validHeights.Average() : 128;
        var chunkY = Math.Clamp(avgHeight / chunkSize, 2, 8);

        return new ChunkSnapshot
        {
            ChunkX = chunkX,
            ChunkY = chunkY,
            ChunkZ = chunkZ,
            HeightMap = heightMap,
            BlockIds = new int[chunkSize * chunkSize * chunkSize], // Empty for live mode
            BlockEntities = new Dictionary<BlockPos, BlockEntity>(),
            Traders = new Dictionary<long, Models.Domain.Trader>(),
            IsLoaded = true
        };
    }

    /// <summary>
    /// Calculate which tiles cover the given chunk positions.
    /// </summary>
    private static List<Vec2i> CalculateTileCoverage(List<Vec2i> chunkPositions, int chunksPerTile)
    {
        var tiles = new HashSet<Vec2i>();

        foreach (var chunkPos in chunkPositions)
        {
            var tileX = chunkPos.X / chunksPerTile;
            var tileY = chunkPos.Y / chunksPerTile;
            tiles.Add(new Vec2i(tileX, tileY));
        }

        return tiles.ToList();
    }

    /// <summary>
    /// Get all registered extractors.
    /// </summary>
    public IReadOnlyList<IDataExtractor> GetExtractors() => _extractors.AsReadOnly();

    public void Dispose()
    {
        foreach (var extractor in _extractors.OfType<IDisposable>())
        {
            try
            {
                extractor.Dispose();
            }
            catch (Exception ex)
            {
                _sapi.Logger.Warning($"[VintageAtlas] Error disposing extractor: {ex.Message}");
            }
        }
    }
}

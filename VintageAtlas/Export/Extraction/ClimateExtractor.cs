using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Export.Climate;
using VintageAtlas.Export.Data;
using VintageAtlas.Export.DataSources;
using VintageAtlas.Storage;

namespace VintageAtlas.Export.Extraction;

/// <summary>
/// Extractor for climate data (temperature and rainfall) from chunks.
/// Can work in two modes:
/// 1. Full Export: Uses MapRegion data from database (fast, covers entire world)
/// 2. Live Updates: Uses loaded chunks via BlockAccessor (accurate, only loaded areas)
/// </summary>
public class ClimateExtractor : IDataExtractor
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly MetadataStorage _metadataStorage;
    private readonly int _samplesPerChunk;
    private const int ChunkSize = 32;
    private const int RegionChunkSize = 16; // MapRegion covers 16x16 chunks

    private readonly List<ClimatePoint> _temperaturePoints = new();
    private readonly List<ClimatePoint> _rainfallPoints = new();
    private int _offsetX;
    private int _offsetZ;
    
    // For live extraction: track which chunks are loaded
    private bool _useLiveChunks;

    public string Name => "Climate Data";
    public bool RequiresLoadedChunks => false; // Can work with either mode

    public ClimateExtractor(ICoreServerAPI sapi, ModConfig config, MetadataStorage metadataStorage, int samplesPerChunk = 2)
    {
        _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _metadataStorage = metadataStorage ?? throw new ArgumentNullException(nameof(metadataStorage));
        _samplesPerChunk = samplesPerChunk;
    }

    public Task InitializeAsync()
    {
        _temperaturePoints.Clear();
        _rainfallPoints.Clear();

        // Calculate coordinate offsets for display coordinates
        _offsetX = _sapi.World.BlockAccessor.MapSizeX / 2;
        _offsetZ = _sapi.World.BlockAccessor.MapSizeZ / 2;

        // Detect if we're doing live extraction (chunks are loaded) or full export (database)
        _useLiveChunks = false; // Will be set by orchestrator context

        _sapi.Logger.Notification("[VintageAtlas] ClimateExtractor initialized");
        return Task.CompletedTask;
    }

    public Task ProcessChunkAsync(ChunkSnapshot chunk)
    {
        if (chunk.HeightMap == null || !chunk.IsLoaded)
            return Task.CompletedTask;

        // If OnDemand mode (unified processing) or live mode, process loaded chunks inline
        if (_config.ClimateMode == ClimateExtractionMode.OnDemand || _useLiveChunks)
        {
            return ProcessLiveChunkAsync(chunk);
        }
        
        // For Fast mode, skip processing here - extract via MapRegions in Finalize
        return Task.CompletedTask;
    }

    /// <summary>
    /// Process a chunk that is loaded in the game's memory.
    /// Uses ICachingBlockAccessor for 10-50% better performance in tight loops.
    /// </summary>
    private Task ProcessLiveChunkAsync(ChunkSnapshot chunk)
    {
        var chunkWorldX = chunk.ChunkX * ChunkSize;
        var chunkWorldZ = chunk.ChunkZ * ChunkSize;
        
        // Check if this chunk column is actually loaded
        var chunkColumn = _sapi.World.BlockAccessor.GetChunk(chunk.ChunkX, 0, chunk.ChunkZ);
        if (chunkColumn == null)
            return Task.CompletedTask;

        var step = ChunkSize / _samplesPerChunk;

        // Use caching block accessor for better performance (10-50% faster in loops)
        var cba = _sapi.World.GetCachingBlockAccessor(false, false);
        cba.Begin(); // CRITICAL: Must call before loop
        
        try
        {
            // Sample multiple points within the chunk
            for (var x = 0; x < _samplesPerChunk; x++)
            {
                for (var z = 0; z < _samplesPerChunk; z++)
                {
                    var worldX = chunkWorldX + (x * step) + (step / 2);
                    var worldZ = chunkWorldZ + (z * step) + (step / 2);

                    // Get height at this position
                    var localX = worldX - chunkWorldX;
                    var localZ = worldZ - chunkWorldZ;
                    var heightIndex = localZ * ChunkSize + localX;

                    if (heightIndex >= 0 && heightIndex < chunk.HeightMap.Length)
                    {
                        var worldY = chunk.HeightMap[heightIndex];

                        try
                        {
                            // Get climate data using caching accessor (requires loaded chunk!)
                            var climate = cba.GetClimateAt(
                                new BlockPos(worldX, worldY, worldZ),
                                EnumGetClimateMode.ForSuppliedDateValues,
                                _sapi.World.Calendar.DaysPerYear
                            );

                            if (climate != null)
                            {
                                AddClimatePoint(worldX, worldZ, climate);
                            }
                        }
                        catch (Exception ex)
                        {
                            _sapi.Logger.VerboseDebug(
                                $"[VintageAtlas] Failed to get climate at ({worldX},{worldZ}): {ex.Message}");
                        }
                    }
                }
            }
        }
        finally
        {
            // Always cleanup caching accessor
            cba.Dispose();
        }

        return Task.CompletedTask;
    }

    public async Task FinalizeAsync(IProgress<ExportProgress>? progress = null)
    {
        // For Fast mode, extract climate from MapRegions (database)
        // OnDemand mode already processed data inline during chunk loading
        if (_config.ClimateMode == ClimateExtractionMode.Fast && _temperaturePoints.Count == 0)
        {
            await ExtractClimateFromMapRegionsAsync();
        }

        if (_temperaturePoints.Count == 0)
        {
            _sapi.Logger.Warning("[VintageAtlas] No climate data extracted");
            return;
        }

        _sapi.Logger.Notification(
            $"[VintageAtlas] Writing {_temperaturePoints.Count} climate points to storage...");

        // Store the collected climate data
        await _metadataStorage.StoreClimateDataAsync("temperature", _temperaturePoints);
        await _metadataStorage.StoreClimateDataAsync("rainfall", _rainfallPoints);

        _sapi.Logger.Notification(
            $"[VintageAtlas] Climate extraction complete! Generated {_temperaturePoints.Count} " +
            $"temperature and {_rainfallPoints.Count} rainfall points.");
    }

    /// <summary>
    /// Extract climate data from MapRegions (database).
    /// Option to load chunks on-demand for more accurate data.
    /// </summary>
    private async Task ExtractClimateFromMapRegionsAsync()
    {
        _sapi.Logger.Notification("[VintageAtlas] 🚀 FAST MODE: Extracting climate from MapRegions (database mode)...");
        _sapi.Logger.Notification("[VintageAtlas] Using EnumGetClimateMode.WorldGenValues (static world gen values)");

        await Task.Run(() =>
        {
            var worldMap = ((Vintagestory.Server.ServerMain)_sapi.World).WorldMap;
            
            if (worldMap == null)
            {
                _sapi.Logger.Error("[VintageAtlas] Cannot access world map for climate extraction");
                return;
            }

            // Calculate region coverage
            var regionSize = RegionChunkSize * ChunkSize;
            var worldSizeBlocks = _sapi.World.BlockAccessor.MapSizeX;
            var regionsPerSide = (worldSizeBlocks / regionSize) + 1;

            var processedRegions = 0;
            for (var regionX = -regionsPerSide / 2; regionX < regionsPerSide / 2; regionX++)
            {
                for (var regionZ = -regionsPerSide / 2; regionZ < regionsPerSide / 2; regionZ++)
                {
                    try
                    {
                        var mapRegion = worldMap.GetMapRegion(regionX, regionZ);
                        
                        if (mapRegion == null)
                            continue;

                        // Sample climate data from this region
                        ExtractClimateFromRegion(mapRegion, regionX, regionZ);
                        processedRegions++;

                        if (processedRegions % 100 == 0)
                        {
                            _sapi.Logger.Debug($"[VintageAtlas] FAST MODE: Processed {processedRegions} map regions, {_temperaturePoints.Count} climate points...");
                        }
                    }
                    catch (Exception ex)
                    {
                        _sapi.Logger.VerboseDebug(
                            $"[VintageAtlas] Failed to access region ({regionX},{regionZ}): {ex.Message}");
                    }
                }
            }

            _sapi.Logger.Notification($"[VintageAtlas] 🚀 FAST MODE COMPLETE: {processedRegions} map regions processed");
            _sapi.Logger.Notification($"[VintageAtlas] Total climate data: {_temperaturePoints.Count} temperature + {_rainfallPoints.Count} rainfall points");
        });
    }

    /// <summary>
    /// Extract climate data by loading chunks on-demand.
    /// This is more accurate than WorldGenValues but slower.
    /// Chunks are loaded temporarily and unloaded after extraction.
    /// </summary>
    public async Task ExtractClimateWithChunkLoadingAsync(List<Vec2i> chunkPositions, IProgress<ExportProgress>? progress = null)
    {
        _sapi.Logger.Notification($"[VintageAtlas] ⚙️ ON-DEMAND MODE: Extracting climate with chunk loading ({chunkPositions.Count} chunks)...");
        _sapi.Logger.Notification($"[VintageAtlas] Using EnumGetClimateMode.ForSuppliedDateValues for accurate seasonal data");
        _sapi.Logger.Notification($"[VintageAtlas] Calendar time: {_sapi.World.Calendar.TotalDays:F1} days");

        var processedChunks = 0;
        var dataPointsCollected = 0;
        var batchSize = 50; // Load chunks in batches to avoid memory issues
        var loadedChunks = new List<Vec2i>();

        for (var i = 0; i < chunkPositions.Count; i += batchSize)
        {
            var batch = chunkPositions.Skip(i).Take(batchSize).ToList();
            _sapi.Logger.Debug($"[VintageAtlas] Loading batch {(i/batchSize) + 1}/{(chunkPositions.Count + batchSize - 1)/batchSize} ({batch.Count} chunks)");
            
            // Load batch of chunks
            foreach (var chunkPos in batch)
            {
                try
                {
                    var pointsBefore = _temperaturePoints.Count;
                    var isLoaded = _sapi.World.BlockAccessor.GetChunk(chunkPos.X, 0, chunkPos.Y) != null;
                    
                    if (!isLoaded)
                    {
                        // Load chunk and keep it loaded temporarily
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

                        // Wait for chunk to load with timeout (non-blocking)
                        var loadedSuccessfully = await Task.WhenAny(
                            loadCompletionSource.Task,
                            Task.Delay(5000) // 5 second timeout
                        ) == loadCompletionSource.Task && loadCompletionSource.Task.Result;

                        if (loadedSuccessfully)
                        {
                            loadedChunks.Add(chunkPos);
                        }
                        else
                        {
                            _sapi.Logger.Warning($"[VintageAtlas] Chunk ({chunkPos.X},{chunkPos.Y}) load timeout");
                        }
                    }

                    // Extract climate from the loaded chunk
                    var mapChunk = ((Vintagestory.Server.ServerMain)_sapi.World).WorldMap.GetMapChunk(chunkPos.X, chunkPos.Y);
                    if (mapChunk != null)
                    {
                        var snapshot = CreateChunkSnapshot(chunkPos.X, chunkPos.Y, mapChunk);
                        await ProcessLiveChunkAsync(snapshot);
                        
                        var pointsAdded = _temperaturePoints.Count - pointsBefore;
                        dataPointsCollected += pointsAdded;
                    }

                    processedChunks++;
                    if (processedChunks % 100 == 0)
                    {
                        _sapi.Logger.Notification($"[VintageAtlas] ON-DEMAND: Processed {processedChunks}/{chunkPositions.Count} chunks, collected {dataPointsCollected} climate points");
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
                    _sapi.Logger.Warning($"[VintageAtlas] ON-DEMAND: Failed to load/process chunk ({chunkPos.X},{chunkPos.Y}): {ex.Message}");
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

            // Small delay between batches to avoid overwhelming the server
            await Task.Delay(100);
        }

        _sapi.Logger.Notification($"[VintageAtlas] ⚙️ ON-DEMAND MODE COMPLETE: {processedChunks} chunks processed, {dataPointsCollected} climate points collected");
        _sapi.Logger.Notification($"[VintageAtlas] Total climate data: {_temperaturePoints.Count} temperature + {_rainfallPoints.Count} rainfall points");
    }

    /// <summary>
    /// Create a chunk snapshot from map chunk data.
    /// </summary>
    private ChunkSnapshot CreateChunkSnapshot(int chunkX, int chunkZ, Vintagestory.API.Common.IMapChunk mapChunk)
    {
        var heightMap = new int[ChunkSize * ChunkSize];
        
        if (mapChunk.RainHeightMap != null)
        {
            for (var i = 0; i < Math.Min(mapChunk.RainHeightMap.Length, heightMap.Length); i++)
            {
                heightMap[i] = mapChunk.RainHeightMap[i];
            }
        }

        var validHeights = heightMap.Where(h => h > 0).ToArray();
        var avgHeight = validHeights.Length > 0 ? (int)validHeights.Average() : 128;
        var chunkY = Math.Clamp(avgHeight / ChunkSize, 2, 8);

        return new ChunkSnapshot
        {
            ChunkX = chunkX,
            ChunkY = chunkY,
            ChunkZ = chunkZ,
            HeightMap = heightMap,
            IsLoaded = true
        };
    }

    /// <summary>
    /// Extract climate samples from a MapRegion.
    /// Uses BlockAccessor which has climate system access.
    /// </summary>
    private void ExtractClimateFromRegion(Vintagestory.API.Common.IMapRegion mapRegion, int regionX, int regionZ)
    {
        // MapRegion covers 16x16 chunks
        // Sample at a coarser resolution for performance
        var sampleStep = 2; // Sample every 2 chunks
        
        for (var chunkOffsetX = 0; chunkOffsetX < RegionChunkSize; chunkOffsetX += sampleStep)
        {
            for (var chunkOffsetZ = 0; chunkOffsetZ < RegionChunkSize; chunkOffsetZ += sampleStep)
            {
                // Calculate world coordinates
                var chunkX = regionX * RegionChunkSize + chunkOffsetX;
                var chunkZ = regionZ * RegionChunkSize + chunkOffsetZ;
                var worldX = chunkX * ChunkSize + ChunkSize / 2; // Center of chunk
                var worldZ = chunkZ * ChunkSize + ChunkSize / 2;

                try
                {
                    // Use BlockAccessor to get climate at this position
                    // This works without needing chunks loaded because climate is calculated from seeds
                    var worldY = 100; // Use a reasonable height for climate calculation
                    var climate = _sapi.World.BlockAccessor.GetClimateAt(
                        new BlockPos(worldX, worldY, worldZ),
                        EnumGetClimateMode.WorldGenValues
                    );
                    
                    if (climate != null)
                    {
                        AddClimatePoint(worldX, worldZ, climate);
                    }
                }
                catch (Exception ex)
                {
                    _sapi.Logger.VerboseDebug(
                        $"[VintageAtlas] Failed to get climate from region at chunk ({chunkX},{chunkZ}): {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Add a climate point to the accumulated lists.
    /// </summary>
    private void AddClimatePoint(int worldX, int worldZ, ClimateCondition climate)
    {
        // Convert to display coordinates (subtract offset, flip Z)
        var displayX = worldX - _offsetX;
        var displayZ = -(worldZ - _offsetZ); // Flip Z for north-up

        _temperaturePoints.Add(new ClimatePoint
        {
            X = displayX,
            Z = displayZ,
            Value = climate.Temperature,
            RealValue = climate.Temperature
        });

        _rainfallPoints.Add(new ClimatePoint
        {
            X = displayX,
            Z = displayZ,
            Value = climate.Rainfall,
            RealValue = climate.Rainfall
        });
    }

    /// <summary>
    /// Enable live chunk mode (for on-demand updates during gameplay).
    /// </summary>
    public void SetLiveChunkMode(bool enabled)
    {
        _useLiveChunks = enabled;
    }
}

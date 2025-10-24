using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VintageAtlas.Storage;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VintageAtlas.Export;

/// <summary>
/// Generates GeoJSON point features with climate data for heatmap visualization
/// Replaces raster PNG tile generation with vector point data
/// </summary>
public class ClimateGeoJsonGenerator(ICoreServerAPI sapi, MetadataStorage metadataStorage)
{
    private readonly ICoreServerAPI _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
    private readonly MetadataStorage _metadataStorage = metadataStorage ?? throw new ArgumentNullException(nameof(metadataStorage));

    /// <summary>
    /// Generate climate GeoJSON data for the entire world with annual climate averages
    /// Relies on game's natural chunk loading via BlockAccessor to read climate data
    /// </summary>
    /// <param name="dataSource">Data source to get actual chunk positions</param>
    /// <param name="samplesPerChunk">Number of samples per chunk edge (default: 2 = 4 samples per chunk)</param>
    public async Task GenerateClimateGeoJsonAsync(SavegameDataSource dataSource, int samplesPerChunk = 2)
    {
        _sapi.Logger.Notification("[VintageAtlas] Starting climate GeoJSON generation...");

        // Use regular lists for sequential processing
        var temperaturePoints = new List<ClimatePoint>();
        var rainfallPoints = new List<ClimatePoint>();

        // Calculate coordinate offsets for display coordinates
        var offsetX = _sapi.World.BlockAccessor.MapSizeX / 2;
        var offsetZ = _sapi.World.BlockAccessor.MapSizeZ / 2;

        // Get actual map chunk positions from the world (same as UnifiedTileGenerator)
        var chunkPositions = dataSource.GetAllMapChunkPositions();
        _sapi.Logger.Notification($"[VintageAtlas] Found {chunkPositions.Count} map chunks to sample");

        if (chunkPositions.Count == 0)
        {
            _sapi.Logger.Warning("[VintageAtlas] No map chunks found in world!");
            return;
        }

        // Process chunks sequentially in small batches to avoid overwhelming the server
        var totalChunks = chunkPositions.Count;
        var processedChunks = 0;
        const int chunkSize = 32; // Vintage Story chunk size
        const int batchSize = 50; // Load max 50 chunks at a time

        // Split chunks into batches
        var batches = chunkPositions.Chunk(batchSize).ToList();
        _sapi.Logger.Notification($"[VintageAtlas] Processing {batches.Count} batches of up to {batchSize} chunks each");

        foreach (var batch in batches)
        {
            // Process chunks in this batch
            foreach (var chunkPos in batch)
            {
                // Calculate world coordinates for this chunk
                var chunkWorldX = chunkPos.X * chunkSize;
                var chunkWorldZ = chunkPos.Y * chunkSize;

                // Sample multiple points within the chunk
                var step = chunkSize / samplesPerChunk;

                try
                {
                    _sapi.WorldManager.LoadChunkColumnPriority(chunkWorldX, chunkWorldZ, new ChunkLoadOptions
                    {
                        KeepLoaded = false,
                        OnLoaded = () =>
                        {
                            // Get climate data from the loaded chunk
                            var blockAccessor = _sapi.World.BlockAccessor;
                            for (var x = 0; x < samplesPerChunk; x++)
                            {
                                for (var z = 0; z < samplesPerChunk; z++)
                                {
                                    var worldX = chunkWorldX + (x * step) + (step / 2); // Sample from center of grid cell
                                    var worldZ = chunkWorldZ + (z * step) + (step / 2);
                                    var climate = blockAccessor.GetClimateAt(
                                        new BlockPos(worldX, blockAccessor.GetTerrainMapheightAt(new BlockPos(worldX, 0, worldZ)), worldZ),
                                        EnumGetClimateMode.ForSuppliedDateValues,
                                        _sapi.World.Calendar.DaysPerYear
                                    );
                                    if (climate != null)
                                    {
                                        // Convert to display coordinates (subtract offset, flip Z)
                                        var displayX = worldX - offsetX;
                                        var displayZ = -(worldZ - offsetZ); // Flip Z for north-up
                                        temperaturePoints.Add(new ClimatePoint
                                        {
                                            X = displayX,
                                            Z = displayZ,
                                            Value = climate.Temperature,
                                            RealValue = climate.Temperature
                                        });
                                        rainfallPoints.Add(new ClimatePoint
                                        {
                                            X = displayX,
                                            Z = displayZ,
                                            Value = climate.Rainfall,
                                            RealValue = climate.Rainfall
                                        });
                                    }
                                }
                            }

                            processedChunks++;

                            if (processedChunks % 1000 == 0)
                            {
                                _sapi.Logger.Notification(
                                    $"[VintageAtlas] Climate generation: {processedChunks}/{totalChunks} chunks - {temperaturePoints.Count} points generated");
                            }
                            _sapi.WorldManager.UnloadChunkColumn(chunkWorldX, chunkWorldZ);
                        }
                    });

                }
                catch (Exception ex)
                {
                    _sapi.Logger.Debug($"[VintageAtlas] Failed to get climate at {chunkPos.X},{chunkPos.Y}: {ex.Message}");
                }
            }
        }

        // Store the collected climate data
        await _metadataStorage.StoreClimateDataAsync("temperature", temperaturePoints);
        await _metadataStorage.StoreClimateDataAsync("rainfall", rainfallPoints);

        _sapi.Logger.Notification($"[VintageAtlas] Climate GeoJSON generation complete! Generated {temperaturePoints.Count} climate points.");
    }
}


public class ClimatePoint
{
    public int X { get; set; }
    public int Z { get; set; }
    public float Value { get; set; }
    public float RealValue { get; set; }
}
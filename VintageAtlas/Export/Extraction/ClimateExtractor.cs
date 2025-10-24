using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Export.Climate;
using VintageAtlas.Export.Data;
using VintageAtlas.Storage;

namespace VintageAtlas.Export.Extraction;

/// <summary>
/// Extractor for climate data (temperature and rainfall) from chunks.
/// Samples climate data at multiple points per chunk and generates GeoJSON for heatmap visualization.
/// </summary>
public class ClimateExtractor : IDataExtractor
{
    private readonly ICoreServerAPI _sapi;
    private readonly MetadataStorage _metadataStorage;
    private readonly int _samplesPerChunk;
    private const int ChunkSize = 32;

    private readonly List<ClimatePoint> _temperaturePoints = new();
    private readonly List<ClimatePoint> _rainfallPoints = new();
    private int _offsetX;
    private int _offsetZ;

    public string Name => "Climate Data";
    public bool RequiresLoadedChunks => false; // Can work with savegame DB directly

    public ClimateExtractor(ICoreServerAPI sapi, ModConfig config, MetadataStorage metadataStorage, int samplesPerChunk = 2)
    {
        _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
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

        _sapi.Logger.Notification("[VintageAtlas] ClimateExtractor initialized");
        return Task.CompletedTask;
    }

    public Task ProcessChunkAsync(ChunkSnapshot chunk)
    {
        if (chunk.HeightMap == null || !chunk.IsLoaded)
            return Task.CompletedTask;

        var chunkWorldX = chunk.ChunkX * ChunkSize;
        var chunkWorldZ = chunk.ChunkZ * ChunkSize;
        var step = ChunkSize / _samplesPerChunk;

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
                        // Get climate data using the block accessor
                        var climate = _sapi.World.BlockAccessor.GetClimateAt(
                            new BlockPos(worldX, worldY, worldZ),
                            EnumGetClimateMode.ForSuppliedDateValues,
                            _sapi.World.Calendar.DaysPerYear
                        );

                        if (climate != null)
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
                    }
                    catch (Exception ex)
                    {
                        _sapi.Logger.VerboseDebug(
                            $"[VintageAtlas] Failed to get climate at ({worldX},{worldZ}): {ex.Message}");
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    public async Task FinalizeAsync(IProgress<ExportProgress>? progress = null)
    {
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
}

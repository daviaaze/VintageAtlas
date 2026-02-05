using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Core.Configuration;
using VintageAtlas.Export.Data;
using VintageAtlas.Export.DataSources;
using VintageAtlas.Export.Rendering;
using VintageAtlas.Storage;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common;

namespace VintageAtlas.Export.Generation;

public class ClimateTileGenerator
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly ITileStorage _storage;
    private readonly ClimateRenderer _renderer;
    private readonly ClimateType _type;

    public ClimateTileGenerator(
        ICoreServerAPI sapi,
        ModConfig config,
        ITileStorage storage,
        ClimateType type)
    {
        _sapi = sapi;
        _config = config;
        _storage = storage;
        _type = type;
        _renderer = new ClimateRenderer();
    }

    public async Task ExportClimateMapAsync(
        SavegameDataSource dataSource,
        IProgress<ExportProgress>? progress = null)
    {
        _sapi.Logger.Notification($"[VintageAtlas] Starting {_type} map export...");

        var startTime = DateTime.UtcNow;
        var totalTiles = 0;

        try
        {
            var chunkPositions = dataSource.GetAllMapChunkPositions();
            var tiles = CalculateTileCoverageFromChunks(chunkPositions);

            _sapi.Logger.Notification($"[VintageAtlas] Exporting {tiles.Count} tiles for {_type}");

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _config.Export.MaxDegreeOfParallelism == -1
                    ? Environment.ProcessorCount
                    : _config.Export.MaxDegreeOfParallelism
            };

            await Parallel.ForEachAsync(tiles, parallelOptions, async (tile, _) =>
            {
                try
                {
                    var tileData = await dataSource.GetTileChunksAsync(_config.Export.BaseZoomLevel, tile.X, tile.Y);
                    
                    if (tileData != null)
                    {
                        var image = _renderer.RenderClimateTile(tileData, _type);
                        
                        if (image != null)
                        {
                            await _storage.PutTileAsync(_config.Export.BaseZoomLevel, tile.X, tile.Y, image);
                            System.Threading.Interlocked.Increment(ref totalTiles);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _sapi.Logger.Error($"[VintageAtlas] Failed to export {_type} tile {tile.X}_{tile.Y}: {ex.Message}");
                }
            });

            // Commit
            _storage.CheckpointWal();
            
            var duration = DateTime.UtcNow - startTime;
            _sapi.Logger.Notification($"[VintageAtlas] {_type} export complete: {totalTiles} tiles in {duration.TotalSeconds:F1}s");
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] {_type} export failed: {ex.Message}");
        }
    }

    public async Task<byte[]?> GetTileDataAsync(int zoom, int tileX, int tileZ)
    {
        var result = await _storage.GetTileAsync(zoom, tileX, tileZ);
        return result;
    }

    private List<Vec2i> CalculateTileCoverageFromChunks(List<Vec2i> chunkPositions)
    {
        var tiles = new HashSet<Vec2i>();
        var chunksPerTile = _config.Export.TileSize / 32;

        foreach (var chunkPos in chunkPositions)
        {
            var tileX = chunkPos.X / chunksPerTile;
            var tileY = chunkPos.Y / chunksPerTile;
            tiles.Add(new Vec2i(tileX, tileY));
        }

        return new List<Vec2i>(tiles);
    }
}

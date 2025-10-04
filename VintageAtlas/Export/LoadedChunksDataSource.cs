using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Models;

namespace VintageAtlas.Export;

/// <summary>
/// Chunk data source that reads from currently loaded game chunks.
/// Used for on-demand tile generation (web requests).
/// Requires main thread access to read game state safely.
/// </summary>
public class LoadedChunksDataSource : IChunkDataSource
{
    private readonly ICoreServerAPI _sapi;
    private readonly ChunkDataExtractor _extractor;

    public string SourceName => "LoadedChunks";
    public bool RequiresMainThread => true;

    public LoadedChunksDataSource(ICoreServerAPI sapi, ModConfig config)
    {
        _sapi = sapi;
        _extractor = new ChunkDataExtractor(sapi, config);
    }

    /// <summary>
    /// Get chunks for a tile from loaded game state.
    /// This MUST be called from the main thread or via EnqueueMainThreadTask.
    /// </summary>
    public async Task<TileChunkData?> GetTileChunksAsync(int zoom, int tileX, int tileZ)
    {
        // Always queue to main thread for safety
        var tcs = new TaskCompletionSource<TileChunkData?>();
        
        _sapi.Event.EnqueueMainThreadTask(() =>
        {
            try
            {
                var data = _extractor.ExtractTileData(zoom, tileX, tileZ);
                tcs.SetResult(data);
            }
            catch (System.Exception ex)
            {
                _sapi.Logger.Error($"[VintageAtlas] LoadedChunksDataSource: Failed to extract tile data: {ex.Message}");
                tcs.SetException(ex);
            }
        }, $"extract-tile-unified-{zoom}-{tileX}-{tileZ}");
        
        return await tcs.Task;
    }
}


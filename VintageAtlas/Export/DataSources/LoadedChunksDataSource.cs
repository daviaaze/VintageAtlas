using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Export.Data;
using VintageAtlas.Export.Utils;

namespace VintageAtlas.Export.DataSources;

/// <summary>
/// Chunk data source that reads from currently loaded game chunks.
/// Used for on-demand tile generation (web requests).
/// Requires main thread access to read game state safely.
/// </summary>
public class LoadedChunksDataSource(ICoreServerAPI sapi, ModConfig config) : IChunkDataSource
{
    private readonly ChunkDataExtractor _extractor = new(sapi, config);

    public string SourceName => "LoadedChunks";
    public bool RequiresMainThread => true;

    /// <summary>
    /// Get chunks for a tile from the loaded game state.
    /// This MUST be called from the main thread or via EnqueueMainThreadTask.
    /// </summary>
    public async Task<TileChunkData?> GetTileChunksAsync(int zoom, int tileX, int tileZ)
    {
        // Always queue to the main thread for safety
        var tcs = new TaskCompletionSource<TileChunkData?>();

        sapi.Event.EnqueueMainThreadTask(() =>
        {
            try
            {
                var data = _extractor.ExtractTileData(zoom, tileX, tileZ);
                tcs.SetResult(data);
            }
            catch (System.Exception ex)
            {
                sapi.Logger.Error($"[VintageAtlas] LoadedChunksDataSource: Failed to extract tile data: {ex.Message}");
                tcs.SetException(ex);
            }
        }, $"extract-tile-unified-{zoom}-{tileX}-{tileZ}");

        return await tcs.Task;
    }
}


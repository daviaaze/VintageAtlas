using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Core.Configuration;
using VintageAtlas.Export.Data;
using VintageAtlas.Models.Domain;
using VintageAtlas.Storage;

namespace VintageAtlas.Export.Extraction;

/// <summary>
/// Extractor for finding and storing trader locations.
/// Accumulates traders as chunks are processed, then writes to storage in one batch.
/// Thread-safe for parallel processing.
/// </summary>
public class TraderExtractor(ICoreServerAPI sapi, IMetadataStorage metadataStorage) : IDataExtractor
{
    private readonly ICoreServerAPI _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
    private readonly IMetadataStorage _metadataStorage = metadataStorage ?? throw new ArgumentNullException(nameof(metadataStorage));
    // Thread-safe dictionary for parallel chunk processing
    private readonly ConcurrentDictionary<long, Trader> _traders = new();

    public string Name => "Traders";
    public bool RequiresLoadedChunks => false; // Can work with savegame DB

    public Task InitializeAsync()
    {
        _traders.Clear();
        _sapi.Logger.Notification("[VintageAtlas] TraderExtractor initialized");
        return Task.CompletedTask;
    }

    public Task ProcessChunkAsync(ChunkSnapshot chunk)
    {
        if (chunk.Traders != null && chunk.Traders.Count > 0)
        {
            foreach (var trader in chunk.Traders)
            {
                // Store in our accumulator (deduplicates by trader ID)
                _traders[trader.Key] = trader.Value;
            }
        }

        return Task.CompletedTask;
    }

    public Task FinalizeAsync(IProgress<Application.UseCases.ExportProgress>? progress = null)
    {
        if (_traders.Count == 0)
        {
            _sapi.Logger.Notification("[VintageAtlas] No traders found");
            return Task.CompletedTask;
        }

        _sapi.Logger.Notification($"[VintageAtlas] Writing {_traders.Count} traders to metadata storage...");

        // Use batched insert for performance (chunks of 500)
        var allTraders = _traders.Values.ToList();
        var batchSize = 500;
        
        for (int i = 0; i < allTraders.Count; i += batchSize)
        {
            var batch = allTraders.Skip(i).Take(batchSize);
            _metadataStorage.AddTraders(batch);
            
            // Small delay to prevent locking DB for too long if there are massive amounts
            if (i + batchSize < allTraders.Count)
            {
                await Task.Delay(1); 
            }
        }

        _sapi.Logger.Notification($"[VintageAtlas] Trader extraction complete: {_traders.Count} traders stored");
        return Task.CompletedTask;
    }
}

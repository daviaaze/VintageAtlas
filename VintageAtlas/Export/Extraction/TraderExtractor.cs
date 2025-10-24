using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Export.Data;
using VintageAtlas.Models.Domain;
using VintageAtlas.Storage;

namespace VintageAtlas.Export.Extraction;

/// <summary>
/// Extractor for finding and storing trader locations.
/// Accumulates traders as chunks are processed, then writes to storage in one batch.
/// </summary>
public class TraderExtractor : IDataExtractor
{
    private readonly ICoreServerAPI _sapi;
    private readonly MetadataStorage _metadataStorage;
    private readonly Dictionary<long, Trader> _traders = new();

    public string Name => "Traders";
    public bool RequiresLoadedChunks => false; // Can work with savegame DB

    public TraderExtractor(ICoreServerAPI sapi, ModConfig config, MetadataStorage metadataStorage)
    {
        _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
        _metadataStorage = metadataStorage ?? throw new ArgumentNullException(nameof(metadataStorage));
    }

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

    public Task FinalizeAsync(IProgress<ExportProgress>? progress = null)
    {
        if (_traders.Count == 0)
        {
            _sapi.Logger.Notification("[VintageAtlas] No traders found");
            return Task.CompletedTask;
        }

        _sapi.Logger.Notification($"[VintageAtlas] Writing {_traders.Count} traders to metadata storage...");

        foreach (var trader in _traders.Values)
        {
            _metadataStorage.AddTrader(trader.Id, trader.Name, trader.Type, trader.Pos);
        }

        _sapi.Logger.Notification($"[VintageAtlas] Trader extraction complete: {_traders.Count} traders stored");
        return Task.CompletedTask;
    }
}

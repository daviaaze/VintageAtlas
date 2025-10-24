using System;
using System.Threading.Tasks;
using VintageAtlas.Export.Data;

namespace VintageAtlas.Export.Extraction;

/// <summary>
/// Interface for pluggable data extractors.
/// Extractors process chunks one at a time as the orchestrator iterates through them.
/// This avoids redundant iteration and multiple database loads of the same chunks.
/// </summary>
public interface IDataExtractor
{
    /// <summary>
    /// Human-readable name for this extractor (for logging).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Indicates if this extractor requires loaded chunks from the game.
    /// If true, should use LoadedChunksDataSource during live gameplay.
    /// If false, can work with SavegameDataSource during full exports.
    /// </summary>
    bool RequiresLoadedChunks { get; }

    /// <summary>
    /// Called once before extraction starts.
    /// Use this to initialize resources, clear caches, open files, etc.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Process a single chunk snapshot.
    /// Called by the orchestrator for each chunk during iteration.
    /// This is where the actual extraction work happens.
    /// </summary>
    /// <param name="chunk">The chunk snapshot to process</param>
    Task ProcessChunkAsync(ChunkSnapshot chunk);

    /// <summary>
    /// Called once after all chunks have been processed.
    /// Use this to finalize output, write to storage, generate summaries, etc.
    /// </summary>
    /// <param name="progress">Optional progress reporting for finalization steps</param>
    Task FinalizeAsync(IProgress<ExportProgress>? progress = null);
}

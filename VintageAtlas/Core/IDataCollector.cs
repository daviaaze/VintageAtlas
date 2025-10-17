using System.Collections.Generic;
using VintageAtlas.Models.Domain;

namespace VintageAtlas.Core;

/// <summary>
/// Interface for collecting live game data
/// Thread-safe: UpdateCache() called from game tick, CollectData() called from HTTP threads
/// </summary>
public interface IDataCollector
{
    /// <summary>
    /// Returns pre-computed server status from cache
    /// Safe to call from any thread (HTTP threads)
    /// </summary>
    ServerStatusData CollectData();
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Vintagestory.API.MathTools;
using VintageAtlas.Models.Domain;

namespace VintageAtlas.Storage;

/// <summary>
/// Interface for metadata storage operations (traders, translocators, etc.)
/// Abstracts the underlying storage mechanism
/// </summary>
public interface IMetadataStorage : IDisposable
{
    /// <summary>
    /// Add or update a trader in storage
    /// </summary>
    void AddTrader(long id, string name, string type, BlockPos pos);

    /// <summary>
    /// Remove a trader from storage
    /// </summary>
    void RemoveTrader(long id);

    /// <summary>
    /// Get all traders from storage
    /// </summary>
    Task<List<Trader>> GetTraders();

    /// <summary>
    /// Perform database vacuum operation to reclaim space
    /// </summary>
    Task VacuumAsync();

    /// <summary>
    /// Checkpoint WAL (Write-Ahead Log) to main database file
    /// </summary>
    void CheckpointWal();
}


using System;
using System.Threading.Tasks;
using VintageAtlas.Application.DTOs;

namespace VintageAtlas.Application.UseCases;

/// <summary>
/// Use case interface for map export operations.
/// Defines the contract for exporting game maps with various options.
/// </summary>
public interface IExportMapUseCase
{
    /// <summary>
    /// Execute the map export operation with specified options
    /// </summary>
    /// <param name="options">Export configuration options</param>
    /// <param name="progress">Optional progress reporting</param>
    /// <returns>Result of the export operation</returns>
    Task<ExportResult> ExecuteAsync(ExportOptions options, IProgress<ExportProgress>? progress = null);

    /// <summary>
    /// Check if an export is currently running
    /// </summary>
    bool IsRunning { get; }
}

/// <summary>
/// Progress information for export operations
/// </summary>
public class ExportProgress
{
    public int TilesCompleted { get; init; }
    public int TotalTiles { get; init; }
    public int ChunksProcessed { get; init; }
    public string? CurrentPhase { get; init; }
    public int PercentComplete => TotalTiles > 0 ? (TilesCompleted * 100 / TotalTiles) : 0;
}


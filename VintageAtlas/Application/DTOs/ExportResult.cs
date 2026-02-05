using System;

namespace VintageAtlas.Application.DTOs;

/// <summary>
/// Result of a map export operation.
/// Data Transfer Object returned by use cases.
/// </summary>
public class ExportResult
{
    /// <summary>
    /// Whether the export succeeded
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if export failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Exception details if export failed
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Total time taken for export
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Number of tiles processed
    /// </summary>
    public int TilesProcessed { get; init; }

    /// <summary>
    /// Number of chunks processed
    /// </summary>
    public int ChunksProcessed { get; init; }

    /// <summary>
    /// Create a successful result
    /// </summary>
    public static ExportResult Successful(TimeSpan duration, int tilesProcessed, int chunksProcessed) => new()
    {
        Success = true,
        Duration = duration,
        TilesProcessed = tilesProcessed,
        ChunksProcessed = chunksProcessed
    };

    /// <summary>
    /// Create a failed result
    /// </summary>
    public static ExportResult Failed(string errorMessage, Exception? exception = null) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        Exception = exception
    };
}

namespace VintageAtlas.Application.DTOs;

/// <summary>
/// Options for map export operation.
/// Data Transfer Object used by use cases.
/// </summary>
public class ExportOptions
{
    /// <summary>
    /// Enable save mode (disconnect players, pause game)
    /// </summary>
    public bool SaveMode { get; init; }

    /// <summary>
    /// Stop server after export completes
    /// </summary>
    public bool StopOnDone { get; init; }

    /// <summary>
    /// Report progress during export
    /// </summary>
    public bool ReportProgress { get; init; } = true;
}

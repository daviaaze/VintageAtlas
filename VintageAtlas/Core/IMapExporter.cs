namespace VintageAtlas.Core;

/// <summary>
/// Interface for map export functionality
/// </summary>
public interface IMapExporter
{
    /// <summary>
    /// Start the map export process
    /// </summary>
    void StartExport();

    /// <summary>
    /// Check if export is currently running
    /// </summary>
    bool IsRunning { get; }
}
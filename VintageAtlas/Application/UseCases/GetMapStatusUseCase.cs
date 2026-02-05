using System.Threading.Tasks;
using VintageAtlas.Web.API;

namespace VintageAtlas.Application.UseCases;

/// <summary>
/// Use case for retrieving current map status.
/// Provides information about map configuration and extent.
/// </summary>
public class GetMapStatusUseCase(
    IMapConfigController mapConfigController,
    IExportMapUseCase exportUseCase)
{
    private readonly IMapConfigController _mapConfigController = mapConfigController;
    private readonly IExportMapUseCase _exportUseCase = exportUseCase;

    /// <summary>
    /// Get current map status
    /// </summary>
    public Task<MapStatus> ExecuteAsync()
    {
        var config = _mapConfigController.GetCurrentConfig();
        var isExporting = _exportUseCase.IsRunning;

        var status = new MapStatus
        {
            IsAvailable = config != null,
            IsExporting = isExporting,
            MapExtent = config?.WorldExtent?.Length == 4 ? new MapExtent
            {
                MinX = config.WorldExtent[0],
                MinZ = config.WorldExtent[1],
                MaxX = config.WorldExtent[2],
                MaxZ = config.WorldExtent[3]
            } : null,
            ZoomLevels = config?.TileResolutions?.Length ?? 0
        };

        return Task.FromResult(status);
    }
}

/// <summary>
/// DTO for map status information
/// </summary>
public class MapStatus
{
    public bool IsAvailable { get; init; }
    public bool IsExporting { get; init; }
    public MapExtent? MapExtent { get; init; }
    public int ZoomLevels { get; init; }
}

/// <summary>
/// DTO for map extent (world coordinates)
/// </summary>
public class MapExtent
{
    public int MinX { get; init; }
    public int MinZ { get; init; }
    public int MaxX { get; init; }
    public int MaxZ { get; init; }
}


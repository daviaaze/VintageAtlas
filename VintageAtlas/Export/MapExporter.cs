using System;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Application.DTOs;
using VintageAtlas.Application.UseCases;
using VintageAtlas.Core;
using VintageAtlas.Core.Configuration;

namespace VintageAtlas.Export;

/// <summary>
/// Adapter for map export operations.
/// Delegates to ExportMapUseCase following Clean Architecture principles.
/// This class is now a thin infrastructure adapter that bridges the game API
/// with the application layer.
/// </summary>
public class MapExporter(
    ICoreServerAPI sapi,
    ModConfig config,
    IExportMapUseCase exportUseCase) : IMapExporter
{
    private readonly ICoreServerAPI _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
    private readonly ModConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly IExportMapUseCase _exportUseCase = exportUseCase ?? throw new ArgumentNullException(nameof(exportUseCase));

    public bool IsRunning => _exportUseCase.IsRunning;

    /// <summary>
    /// Start the map export process asynchronously
    /// </summary>
    public void StartExport()
    {
        if (IsRunning)
        {
            _sapi.Logger.Warning("[VintageAtlas] Export already running, skipping request");
            return;
        }

        Task.Run(ExecuteExportAsync);
    }

    /// <summary>
    /// Execute the export by delegating to the use case
    /// </summary>
    private async Task ExecuteExportAsync()
    {
        var options = new ExportOptions
        {
            SaveMode = _config.Export.SaveMode,
            StopOnDone = _config.Export.StopOnDone,
            ReportProgress = true
        };

        var result = await _exportUseCase.ExecuteAsync(options);

        if (result.Success)
        {
            _sapi.Logger.Notification(
                $"[VintageAtlas] Export completed: {result.TilesProcessed} tiles, " +
                $"{result.ChunksProcessed} chunks in {result.Duration.TotalSeconds:F2}s"
            );
        }
        else
        {
            _sapi.Logger.Error($"[VintageAtlas] Export failed: {result.ErrorMessage}");
            if (result.Exception != null)
            {
                _sapi.Logger.Error(result.Exception.StackTrace ?? "");
            }
        }
    }
}

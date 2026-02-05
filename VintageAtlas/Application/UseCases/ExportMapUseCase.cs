using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Application.DTOs;
using VintageAtlas.Application.Validation;
using VintageAtlas.Infrastructure.VintageStory;

namespace VintageAtlas.Application.UseCases;

/// <summary>
/// Simplified use case for exporting game maps.
/// Contains pure business logic without infrastructure concerns.
/// </summary>
public class ExportMapUseCase(
    ICoreServerAPI sapi,
    ServerStateManager serverStateManager,
    Func<ExportOptions, IProgress<ExportProgress>?, Task<ExportResult>> exportAction,
    IValidator<ExportOptions>? validator = null) : IExportMapUseCase
{
    private readonly ICoreServerAPI _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
    private readonly ServerStateManager _serverStateManager = serverStateManager ?? throw new ArgumentNullException(nameof(serverStateManager));
    private readonly Func<ExportOptions, IProgress<ExportProgress>?, Task<ExportResult>> _exportAction = exportAction ?? throw new ArgumentNullException(nameof(exportAction));
    private readonly IValidator<ExportOptions> _validator = validator ?? new ExportOptionsValidator();
    private bool _isRunning;

    public bool IsRunning => _isRunning;

    /// <summary>
    /// Execute the map export with specified options
    /// </summary>
    public async Task<ExportResult> ExecuteAsync(ExportOptions options, IProgress<ExportProgress>? progress = null)
    {
        // Validate options
        var validationResult = _validator.Validate(options);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors);
            _sapi.Logger.Error($"[VintageAtlas] Invalid export options: {errors}");
            return ExportResult.Failed($"Validation failed: {errors}");
        }

        if (_isRunning)
        {
            return ExportResult.Failed("Export already running");
        }

        _isRunning = true;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _sapi.Logger.Notification("[VintageAtlas] Starting map export...");

            // Enter save mode if requested
            if (options.SaveMode)
            {
                await _serverStateManager.EnterSaveModeAsync();
            }

            // Execute the export through the provided action
            var result = await _exportAction(options, progress);

            stopwatch.Stop();

            if (result.Success)
            {
                _sapi.Logger.Notification($"[VintageAtlas] Map export completed in {stopwatch.Elapsed.TotalSeconds:F2}s");

                // Stop server if requested
                if (options.StopOnDone)
                {
                    _serverStateManager.StopServer("Map export complete");
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _sapi.Logger.Error($"[VintageAtlas] Map export failed: {ex.Message}");
            _sapi.Logger.Error(ex.StackTrace ?? "");

            return ExportResult.Failed("Export failed: " + ex.Message, ex);
        }
        finally
        {
            // Always exit save mode if it was entered
            if (options.SaveMode)
            {
                _serverStateManager.ExitSaveMode();
            }

            _isRunning = false;
        }
    }

}


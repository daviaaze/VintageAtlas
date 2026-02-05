using System;
using System.Collections.Generic;
using System.IO;
using VintageAtlas.Core.Configuration;
using VintageAtlas.Export.Colors;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace VintageAtlas.Core;

/// <summary>
/// Validates mod configuration and provides helpful error messages
/// </summary>
public static class ConfigValidator
{
    /// <summary>
    /// Validates the configuration and returns any errors found
    /// </summary>
    private static List<string> Validate(ModConfig config)
    {
        var errors = new List<string>();

        ValidateOutputDirectory(config, errors);
        ValidateTileSize(config, errors);
        ValidateZoomLevel(config, errors);
        ValidateParallelism(config, errors);
        ValidateWebServer(config, errors);
        ValidateHistoricalTracking(config, errors);
        ValidateFeatureDependencies(config, errors);

        return errors;
    }

    private static void ValidateOutputDirectory(ModConfig config, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(config.Export.OutputDirectory))
        {
            errors.Add("OutputDirectory cannot be empty");
            return;
        }

        if (config.Export.OutputDirectory == GamePaths.DataPath)
        {
            errors.Add("OutputDirectory cannot be the root data folder - please specify a subfolder (e.g., ModData/VintageAtlas)");
        }
    }

    private static void ValidateTileSize(ModConfig config, List<string> errors)
    {
        if (config.Export.TileSize % Constants.ChunkSize != 0)
        {
            errors.Add($"TileSize ({config.Export.TileSize}) must be evenly divisible by {Constants.ChunkSize}");
        }

        if (config.Export.TileSize is < Constants.MinTileSize or > Constants.MaxTileSize)
        {
            errors.Add($"TileSize ({config.Export.TileSize}) must be between {Constants.MinTileSize} and {Constants.MaxTileSize}");
        }
    }

    private static void ValidateZoomLevel(ModConfig config, List<string> errors)
    {
        if (config.Export.BaseZoomLevel is < Constants.MinZoomLevel or > Constants.MaxZoomLevel)
        {
            errors.Add($"BaseZoomLevel ({config.Export.BaseZoomLevel}) must be between {Constants.MinZoomLevel} and {Constants.MaxZoomLevel}");
        }
    }

    private static void ValidateParallelism(ModConfig config, List<string> errors)
    {
        if (config.Export.MaxDegreeOfParallelism is < -1 or 0)
        {
            errors.Add($"MaxDegreeOfParallelism must be -1 (auto) or a positive number");
        }
    }

    private static void ValidateWebServer(ModConfig config, List<string> errors)
    {
        if (!config.WebServer.EnableLiveServer)
        {
            return;
        }

        if (config.WebServer.LiveServerPort is < Constants.MinPortNumber or > Constants.MaxPortNumber)
        {
            errors.Add($"LiveServerPort ({config.WebServer.LiveServerPort.Value}) must be between {Constants.MinPortNumber} and {Constants.MaxPortNumber}");
        }

        if (config.WebServer.MaxConcurrentRequests < Constants.MinConcurrentRequests || config.WebServer.MaxConcurrentRequests > Constants.MaxConcurrentRequests)
        {
            errors.Add($"MaxConcurrentRequests ({config.WebServer.MaxConcurrentRequests}) must be between {Constants.MinConcurrentRequests} and {Constants.MaxConcurrentRequests}");
        }

        if (config.WebServer.MapExportIntervalMs < Constants.MinMapExportIntervalMs)
        {
            errors.Add($"MapExportIntervalMs ({config.WebServer.MapExportIntervalMs}) should be at least {Constants.MinMapExportIntervalMs}ms to avoid performance issues");
        }
    }

    private static void ValidateHistoricalTracking(ModConfig config, List<string> errors)
    {
        if (config.Tracking is { EnableHistoricalTracking: true, HistoricalTickIntervalMs: < Constants.MinHistoricalTickIntervalMs })
        {
            errors.Add($"HistoricalTickIntervalMs ({config.Tracking.HistoricalTickIntervalMs}) should be at least {Constants.MinHistoricalTickIntervalMs}ms");
        }
    }

    private static void ValidateFeatureDependencies(ModConfig config, List<string> errors)
    {
        if (config.Export is { CreateZoomLevels: true, ExtractWorldMap: false })
        {
            errors.Add("CreateZoomLevels requires ExtractWorldMap to be enabled (it generates zoom levels from the base map tiles)");
        }

        if (config.Export is { ExportHeightmap: true, ExtractWorldMap: false })
        {
            errors.Add("ExportHeightmap requires ExtractWorldMap to be enabled");
        }
    }

    /// <summary>
    /// Load, validate, and initialize mod configuration
    /// </summary>
    /// <param name="sapi">Server API instance for config operations and logging</param>
    /// <returns>Validated and ready-to-use configuration</returns>
    public static ModConfig LoadAndValidateConfig(ICoreServerAPI sapi)
    {
        var config = sapi.LoadModConfig<ModConfig>("VintageAtlasConfig.json");

        if (config is not null)
            return FinalizeConfig(sapi, config);

        sapi.Logger.Warning("[VintageAtlas] No configuration found, creating default config");

        // Use ModData directory for all VintageAtlas data
        var modDataPath = Path.Combine(GamePaths.DataPath, "ModData", "VintageAtlas");
        config = new ModConfig
        {
            Export = new MapExportSettings
            {
                Mode = ImageMode.MedievalStyleWithHillShading,
                OutputDirectory = modDataPath
            }
        };

        sapi.StoreModConfig(config, "VintageAtlasConfig.json");
        sapi.Logger.Notification($"[VintageAtlas] Created default config at: {Path.Combine(GamePaths.ModConfig, "VintageAtlasConfig.json")}");
        sapi.Logger.Notification($"[VintageAtlas] Data will be stored in: {modDataPath}");

        return FinalizeConfig(sapi, config);
    }

    /// <summary>
    /// Apply validation and auto-fixes to configuration
    /// </summary>
    private static ModConfig FinalizeConfig(ICoreServerAPI sapi, ModConfig config)
    {
        // Validate configuration
        var validationErrors = Validate(config);
        if (validationErrors.Count > 0)
        {
            sapi.Logger.Error("[VintageAtlas] Configuration errors:");
            foreach (var error in validationErrors)
            {
                sapi.Logger.Error($"  - {error}");
            }
            sapi.Logger.Error("[VintageAtlas] Please fix configuration and restart server");
        }

        // Apply auto-fixes
        ApplyAutoFixes(config);
        return config ?? throw new InvalidOperationException("Config initialization failed");
    }

    /// <summary>
    /// Apply automatic fixes to configuration where possible
    /// </summary>
    private static void ApplyAutoFixes(ModConfig config)
    {
        // Auto-fix tile size to nearest valid value
        if (config.Export.TileSize % Constants.ChunkSize != 0)
        {
            config.Export.TileSize = config.Export.TileSize / Constants.ChunkSize * Constants.ChunkSize;
            if (config.Export.TileSize < Constants.ChunkSize) config.Export.TileSize = Constants.ChunkSize;
        }

        // Limit parallelism to processor count
        if (config.Export.MaxDegreeOfParallelism == -1 || config.Export.MaxDegreeOfParallelism > Environment.ProcessorCount * 2)
        {
            config.Export.MaxDegreeOfParallelism = Environment.ProcessorCount;
        }

        // Ensure the base path format
        if (!config.WebServer.BasePath.StartsWith('/'))
        {
            config.WebServer.BasePath = $"/{config.WebServer.BasePath}";
        }
    }
}


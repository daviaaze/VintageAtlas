using System;
using System.Collections.Generic;
using System.IO;
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
    public static List<string> Validate(ModConfig config)
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
        if (string.IsNullOrWhiteSpace(config.OutputDirectory))
        {
            errors.Add("OutputDirectory cannot be empty");
            return;
        }

        if (config.OutputDirectory == GamePaths.DataPath)
        {
            errors.Add("OutputDirectory cannot be the root data folder - please specify a subfolder (e.g., ModData/VintageAtlas)");
        }
    }

    private static void ValidateTileSize(ModConfig config, List<string> errors)
    {
        if (config.TileSize % 32 != 0)
        {
            errors.Add($"TileSize ({config.TileSize}) must be evenly divisible by 32");
        }

        if (config.TileSize is < 32 or > 1024)
        {
            errors.Add($"TileSize ({config.TileSize}) must be between 32 and 1024");
        }
    }

    private static void ValidateZoomLevel(ModConfig config, List<string> errors)
    {
        if (config.BaseZoomLevel is < 1 or > 15)
        {
            errors.Add($"BaseZoomLevel ({config.BaseZoomLevel}) must be between 1 and 15");
        }
    }

    private static void ValidateParallelism(ModConfig config, List<string> errors)
    {
        if (config.MaxDegreeOfParallelism is < -1 or 0)
        {
            errors.Add($"MaxDegreeOfParallelism must be -1 (auto) or a positive number");
        }
    }

    private static void ValidateWebServer(ModConfig config, List<string> errors)
    {
        if (!config.EnableLiveServer)
        {
            return;
        }

        if (config.LiveServerPort is < 1 or > 65535)
        {
            errors.Add($"LiveServerPort ({config.LiveServerPort.Value}) must be between 1 and 65535");
        }

        if (config.MaxConcurrentRequests is < 1 or > 1000)
        {
            errors.Add($"MaxConcurrentRequests ({config.MaxConcurrentRequests.Value}) must be between 1 and 1000");
        }

        if (config.MaxConcurrentTileRequests is < 10 or > 2000)
        {
            errors.Add($"MaxConcurrentTileRequests ({config.MaxConcurrentTileRequests.Value}) must be between 10 and 2000");
        }

        if (config.MaxConcurrentStaticRequests is < 10 or > 1000)
        {
            errors.Add($"MaxConcurrentStaticRequests ({config.MaxConcurrentStaticRequests.Value}) must be between 10 and 1000");
        }

        if (config.MapExportIntervalMs < 10000)
        {
            errors.Add($"MapExportIntervalMs ({config.MapExportIntervalMs}) should be at least 10000ms (10 seconds) to avoid performance issues");
        }
    }

    private static void ValidateHistoricalTracking(ModConfig config, List<string> errors)
    {
        if (config is { EnableHistoricalTracking: true, HistoricalTickIntervalMs: < 1000 })
        {
            errors.Add($"HistoricalTickIntervalMs ({config.HistoricalTickIntervalMs}) should be at least 1000ms (1 second)");
        }
    }

    private static void ValidateFeatureDependencies(ModConfig config, List<string> errors)
    {
        if (config is { CreateZoomLevels: true, ExtractWorldMap: false })
        {
            errors.Add("CreateZoomLevels requires ExtractWorldMap to be enabled (it generates zoom levels from the base map tiles)");
        }

        if (config is { ExportHeightmap: true, ExtractWorldMap: false })
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
            Mode = ImageMode.MedievalStyleWithHillShading,
            OutputDirectory = modDataPath
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
        // DEBUG: Log the actual Mode value loaded
        sapi.Logger.Notification($"[VintageAtlas] ════════ CONFIG LOADED ════════");
        sapi.Logger.Notification($"[VintageAtlas] Mode = {config.Mode} ({(int)config.Mode})");
        sapi.Logger.Notification($"[VintageAtlas] ════════════════════════════════");

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
    public static void ApplyAutoFixes(ModConfig config)
    {
        // Auto-fix tile size to nearest valid value
        if (config.TileSize % 32 != 0)
        {
            config.TileSize = config.TileSize / 32 * 32;
            if (config.TileSize < 32) config.TileSize = 32;
        }

        // Limit parallelism to processor count
        if (config.MaxDegreeOfParallelism == -1 || config.MaxDegreeOfParallelism > Environment.ProcessorCount * 2)
        {
            config.MaxDegreeOfParallelism = Environment.ProcessorCount;
        }

        // Ensure the base path format
        if (!config.BasePath.StartsWith('/'))
        {
            config.BasePath = $"/{config.BasePath}";
        }
    }
}


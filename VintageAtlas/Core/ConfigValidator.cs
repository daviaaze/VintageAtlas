using System;
using System.Collections.Generic;
using Vintagestory.API.Config;

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

        // Output directory validation
        if (string.IsNullOrWhiteSpace(config.OutputDirectory))
        {
            errors.Add("OutputDirectory cannot be empty");
        }
        else if (config.OutputDirectory == GamePaths.DataPath)
        {
            errors.Add("OutputDirectory cannot be the root data folder - please specify a subfolder (e.g., ModData/VintageAtlas)");
        }

        // Tile size validation
        if (config.TileSize % 32 != 0)
        {
            errors.Add($"TileSize ({config.TileSize}) must be evenly divisible by 32");
        }

        if (config.TileSize is < 32 or > 1024)
        {
            errors.Add($"TileSize ({config.TileSize}) must be between 32 and 1024");
        }

        // Zoom level validation
        if (config.BaseZoomLevel is < 1 or > 15)
        {
            errors.Add($"BaseZoomLevel ({config.BaseZoomLevel}) must be between 1 and 15");
        }

        // Parallelism validation
        if (config.MaxDegreeOfParallelism is < -1 or 0)
        {
            errors.Add($"MaxDegreeOfParallelism must be -1 (auto) or a positive number");
        }

        // Web server validation
        if (config.EnableLiveServer)
        {
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

        // Historical tracking validation
        if (config is { EnableHistoricalTracking: true, HistoricalTickIntervalMs: < 1000 })
        {
            errors.Add($"HistoricalTickIntervalMs ({config.HistoricalTickIntervalMs}) should be at least 1000ms (1 second)");
        }

        // Feature dependency validation
        if (config is { CreateZoomLevels: true, ExtractWorldMap: false })
        {
            errors.Add("CreateZoomLevels requires ExtractWorldMap to be enabled (it generates zoom levels from the base map tiles)");
        }

        if (config is { ExportHeightmap: true, ExtractWorldMap: false })
        {
            errors.Add("ExportHeightmap requires ExtractWorldMap to be enabled");
        }

        return errors;
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


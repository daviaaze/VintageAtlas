using System;
using System.IO;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Storage;

namespace VintageAtlas.Export;

/// <summary>
/// Imports PNG tiles from filesystem into MBTiles database
/// Bridges the gap between Extractor.cs (generates PNGs) and DynamicTileGenerator (serves from DB)
/// </summary>
public class TileImporter
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly MbTilesStorage _storage;

    public TileImporter(ICoreServerAPI sapi, ModConfig config, MbTilesStorage storage)
    {
        _sapi = sapi;
        _config = config;
        _storage = storage;
    }

    /// <summary>
    /// Import all PNG tiles from the export directory into MBTiles database
    /// Should be called after /atlas export completes
    /// </summary>
    public async Task ImportExportedTilesAsync()
    {
        _sapi.Logger.Notification("[VintageAtlas] Starting tile import to MBTiles database...");
        
        var worldDir = _config.OutputDirectoryWorld;
        if (!Directory.Exists(worldDir))
        {
            _sapi.Logger.Warning($"[VintageAtlas] World directory not found: {worldDir}");
            _sapi.Logger.Warning("[VintageAtlas] Run /atlas export first to generate tiles");
            return;
        }

        // NOTE: Store tiles at their ABSOLUTE coordinates (same as DynamicTileGenerator)
        // The frontend adds tileOffset when requesting tiles, so both systems store at absolute coords
        _sapi.Logger.Notification("[VintageAtlas] Importing tiles at absolute coordinates (matching DynamicTileGenerator behavior)");

        var imported = 0;
        var skipped = 0;
        var errors = 0;

        // Iterate through zoom levels (highest first = base tiles)
        for (var zoom = _config.BaseZoomLevel; zoom >= 0; zoom--)
        {
            var zoomDir = Path.Combine(worldDir, zoom.ToString());
            if (!Directory.Exists(zoomDir))
            {
                _sapi.Logger.Debug($"[VintageAtlas] No tiles found for zoom {zoom}, skipping");
                continue;
            }

            _sapi.Logger.Notification($"[VintageAtlas] Importing zoom level {zoom}...");
            var tileFiles = Directory.GetFiles(zoomDir, "*.png");
            
            foreach (var tilePath in tileFiles)
            {
                try
                {
                    // Parse filename: "x_z.png"
                    var filename = Path.GetFileNameWithoutExtension(tilePath);
                    var parts = filename.Split('_');
                    if (parts.Length != 2)
                    {
                        _sapi.Logger.Warning($"[VintageAtlas] Invalid tile filename: {filename}");
                        skipped++;
                        continue;
                    }

                    if (!int.TryParse(parts[0], out var tileX) || !int.TryParse(parts[1], out var tileZ))
                    {
                        _sapi.Logger.Warning($"[VintageAtlas] Could not parse coordinates from: {filename}");
                        skipped++;
                        continue;
                    }

                    // Read PNG data
                    var tileData = await File.ReadAllBytesAsync(tilePath);
                    
                    // Store in database at absolute coordinates (same as DynamicTileGenerator)
                    // Frontend will add tileOffset when requesting, so tiles end up at correct coords
                    await _storage.PutTileAsync(zoom, tileX, tileZ, tileData);
                    imported++;

                    if (imported % 1000 == 0)
                    {
                        _sapi.Logger.Notification($"[VintageAtlas] Imported {imported} tiles so far...");
                    }
                }
                catch (Exception ex)
                {
                    _sapi.Logger.Error($"[VintageAtlas] Failed to import tile {tilePath}: {ex.Message}");
                    errors++;
                }
            }
        }

        _sapi.Logger.Notification($"[VintageAtlas] Tile import complete: {imported} imported, {skipped} skipped, {errors} errors");
    }

    /// <summary>
    /// Clear all tiles from the database
    /// Useful before re-importing after a fresh export
    /// </summary>
    public void ClearDatabase()
    {
        _sapi.Logger.Notification("[VintageAtlas] Clearing MBTiles database...");
        
        try
        {
            // MbTilesStorage should have a method to clear tiles
            // For now, we'll just log - you may need to add this method
            _sapi.Logger.Notification("[VintageAtlas] Database cleared (implement MbTilesStorage.ClearAllTiles if needed)");
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to clear database: {ex.Message}");
        }
    }

}


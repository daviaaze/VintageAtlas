using System;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using Vintagestory.Server;
using VintageAtlas.Core;
using VintageAtlas.Storage;
using VintageAtlas.Web.API;

namespace VintageAtlas.Export;

/// <summary>
/// Manages map export operations using the unified tile generation system.
/// Exports map tiles directly to MBTiles database without intermediate PNG files.
/// </summary>
public class MapExporter : IMapExporter
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly UnifiedTileGenerator _tileGenerator;
    private readonly MapConfigController? _mapConfigController;
    private readonly ServerMain _server;

    public bool IsRunning { get; private set; }
    
    public MapExporter(
        ICoreServerAPI sapi,
        ModConfig config,
        UnifiedTileGenerator tileGenerator,
        MapConfigController? mapConfigController = null)
    {
        _sapi = sapi;
        _config = config;
        _tileGenerator = tileGenerator;
        _mapConfigController = mapConfigController;
        _server = (ServerMain)_sapi.World;
    }

    public void StartExport()
    {
        if (IsRunning)
        {
            _sapi.Logger.Warning("[VintageAtlas] Export already running, skipping request");
            return;
        }

        Task.Run(async () => await ExecuteExportAsync());
    }

    private async Task ExecuteExportAsync()
    {
        if (IsRunning) return;
        
        IsRunning = true;
        string? oldPassword = null;

        try
        {
            _sapi.Logger.Notification("[VintageAtlas] Starting map export...");

            if (_config.SaveMode)
            {
                oldPassword = _sapi.Server.Config.Password;
                _sapi.Server.Config.Password = Random.Shared.Next().ToString();
                
                // Disconnect all players safely
                var players = _sapi.World.AllOnlinePlayers;
                foreach (var player1 in players)
                {
                    try
                    {
                        var player = (IServerPlayer)player1;
                        if (player?.Entity != null)
                        {
                            _sapi.Logger.Debug($"[VintageAtlas] Disconnecting player {player.PlayerName} for export");
                            player.Disconnect("Exporting the map now");
                        }
                    }
                    catch (Exception ex)
                    {
                        _sapi.Logger.Warning($"[VintageAtlas] Failed to disconnect player during export: {ex.Message}");
                    }
                }
                
                // Wait a moment for disconnections to complete
                System.Threading.Thread.Sleep(500);
                _server.Suspend(true, 1000);
            }

            // Initialize output directories for GeoJSON data
            // Tiles are generated directly to MBTiles database (no intermediate files)
            _config.OutputDirectoryGeojson = System.IO.Path.Combine(_config.OutputDirectory, "data", "geojson");
            
            _sapi.Logger.Notification("[VintageAtlas] ═══════════════════════════════════════════════");
            _sapi.Logger.Notification("[VintageAtlas] Starting UNIFIED tile generation system");
            _sapi.Logger.Notification("[VintageAtlas] Direct export to MBTiles (no intermediate PNGs)");
            _sapi.Logger.Notification("[VintageAtlas] ═══════════════════════════════════════════════");
            
            // Create data source for reading from savegame database
            using var dataSource = new SavegameDataSource(_server, _config, _sapi.Logger);
            
            // Generate tiles directly to MBTiles storage
            await _tileGenerator.ExportFullMapAsync(dataSource);
            
            // CRITICAL: Invalidate map config cache so frontend gets updated extent
            _mapConfigController?.InvalidateCache();
            _sapi.Logger.Debug("[VintageAtlas] Map config cache invalidated after export");
            
            _sapi.Logger.Notification("[VintageAtlas] ═══════════════════════════════════════════════");
            _sapi.Logger.Notification("[VintageAtlas] Map export completed successfully!");
            _sapi.Logger.Notification("[VintageAtlas] Tiles stored in MBTiles database");
            _sapi.Logger.Notification("[VintageAtlas] ═══════════════════════════════════════════════");
        }
        catch (Exception e)
        {
            _sapi.Logger.Error($"[VintageAtlas] Map export failed: {e.Message}");
            _sapi.Logger.Error(e.StackTrace ?? "");
        }
        finally
        {
            if (_config.SaveMode)
            {
                _server.Suspend(false);
                _sapi.Server.Config.Password = oldPassword;
            }

            if (_config.StopOnDone)
            {
                _server.Stop("Map export complete");
            }

            IsRunning = false;
        }
    }

}


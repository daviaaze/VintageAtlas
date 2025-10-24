using System;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using Vintagestory.Server;
using VintageAtlas.Core;
using VintageAtlas.Export.Extraction;

namespace VintageAtlas.Export;

/// <summary>
/// Manages map export operations using the extraction orchestrator.
/// Coordinates the extraction pipeline with appropriate server modes (save mode, etc.)
/// </summary>
public class MapExporter : IMapExporter
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly ExportOrchestrator _orchestrator;
    private readonly ServerMain _server;
    private string? _serverPassword;

    public bool IsRunning { get; private set; }

    public MapExporter(
        ICoreServerAPI sapi,
        ModConfig config,
        ExportOrchestrator orchestrator)
    {
        _sapi = sapi;
        _config = config;
        _orchestrator = orchestrator;
        _server = (ServerMain)_sapi.World;
    }

    public void StartExport()
    {
        if (IsRunning)
        {
            _sapi.Logger.Warning("[VintageAtlas] Export already running, skipping request");
            return;
        }

        Task.Run(ExecuteExportAsync);
    }

    private async Task ExecuteExportAsync()
    {
        if (IsRunning) return;

        IsRunning = true;

        try
        {
            _sapi.Logger.Notification("[VintageAtlas] Starting full map export...");

            if (_config.SaveMode)
            {
                EnableSaveMode();
            }

            // Execute all registered extractors through the orchestrator
            await _orchestrator.ExecuteFullExportAsync();

            _sapi.Logger.Notification("[VintageAtlas] Full map export completed successfully!");
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
                DisableSaveMode();
            }

            if (_config.StopOnDone)
            {
                _server.Stop("Map export complete");
            }

            IsRunning = false;
        }
    }

    private void EnableSaveMode()
    {
        _serverPassword = _sapi.Server.Config.Password;
        _sapi.Server.Config.Password = Random.Shared.Next().ToString();

        // Disconnect all players safely
        var players = _sapi.World.AllOnlinePlayers;
        foreach (var player1 in players)
        {
            try
            {
                var player = (IServerPlayer)player1;
                if (player?.Entity == null)
                    continue;

                _sapi.Logger.Debug($"[VintageAtlas] Disconnecting player {player.PlayerName} for export");
                player.Disconnect("Exporting the map now");
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

    private void DisableSaveMode()
    {
        _server.Suspend(false);
        _sapi.Server.Config.Password = _serverPassword;
    }
}

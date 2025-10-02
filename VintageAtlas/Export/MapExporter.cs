using System;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using Vintagestory.Server;
using VintageAtlas.Core;

namespace VintageAtlas.Export;

/// <summary>
/// Manages map export operations
/// </summary>
public class MapExporter(ICoreServerAPI sapi, ModConfig config) : IMapExporter
{
    private readonly ServerMain _server = (ServerMain)sapi.World;
    private Extractor? _extractor;

    public bool IsRunning { get; private set; }

    public void StartExport()
    {
        if (IsRunning)
        {
            sapi.Logger.Warning("[VintageAtlas] Export already running, skipping request");
            return;
        }

        Task.Run(ExecuteExport);
    }

    private void ExecuteExport()
    {
        if (IsRunning) return;
        
        IsRunning = true;
        string? oldPassword = null;

        try
        {
            sapi.Logger.Notification("[VintageAtlas] Starting map export...");

            if (config.SaveMode)
            {
                oldPassword = sapi.Server.Config.Password;
                sapi.Server.Config.Password = Random.Shared.Next().ToString();
                
                // Disconnect all players safely
                var players = sapi.World.AllOnlinePlayers;
                foreach (var player1 in players)
                {
                    try
                    {
                        var player = (IServerPlayer)player1;
                        if (player?.Entity != null)
                        {
                            sapi.Logger.Debug($"[VintageAtlas] Disconnecting player {player.PlayerName} for export");
                            player.Disconnect("Exporting the map now");
                        }
                    }
                    catch (Exception ex)
                    {
                        sapi.Logger.Warning($"[VintageAtlas] Failed to disconnect player during export: {ex.Message}");
                    }
                }
                
                // Wait a moment for disconnections to complete
                System.Threading.Thread.Sleep(500);
                _server.Suspend(true, 1000);
            }

            // Initialize output directories for generated data only
            // HTML is served directly from mod bundle, so we only store data here
            config.OutputDirectoryWorld = System.IO.Path.Combine(config.OutputDirectory, "data", "world");
            config.OutputDirectoryGeojson = System.IO.Path.Combine(config.OutputDirectory, "data", "geojson");
            
            _extractor = new Extractor(_server, config, sapi.Logger);
            _extractor.Run();

            sapi.Logger.Notification("[VintageAtlas] Map export completed successfully");
        }
        catch (Exception e)
        {
            sapi.Logger.Error($"[VintageAtlas] Map export failed: {e.Message}");
            sapi.Logger.Error(e.StackTrace ?? "");
        }
        finally
        {
            if (config.SaveMode)
            {
                _server.Suspend(false);
                sapi.Server.Config.Password = oldPassword;
            }

            if (config.StopOnDone)
            {
                _server.Stop("Map export complete");
            }

            IsRunning = false;
            _extractor = null;
        }
    }

}


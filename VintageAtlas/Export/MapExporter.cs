using System;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using Vintagestory.Server;
using VintageAtlas.Core;

namespace VintageAtlas.Export;

/// <summary>
/// Manages map export operations
/// </summary>
public class MapExporter : IMapExporter
{
    private readonly ICoreServerAPI _sapi;
    private readonly ServerMain _server;
    private readonly ModConfig _config;
    private Extractor? _extractor;
    private bool _isRunning;

    public MapExporter(ICoreServerAPI sapi, ModConfig config)
    {
        _sapi = sapi;
        _server = (ServerMain)sapi.World;
        _config = config;
    }

    public bool IsRunning => _isRunning;

    public void StartExport()
    {
        if (_isRunning)
        {
            _sapi.Logger.Warning("[VintageAtlas] Export already running, skipping request");
            return;
        }

        Task.Run(ExecuteExport);
    }

    private void ExecuteExport()
    {
        if (_isRunning) return;
        
        _isRunning = true;
        string? oldPassword = null;

        try
        {
            _sapi.Logger.Notification("[VintageAtlas] Starting map export...");

            if (_config.SaveMode)
            {
                oldPassword = _sapi.Server.Config.Password;
                _sapi.Server.Config.Password = Random.Shared.Next().ToString();
                
                foreach (var player1 in _sapi.World.AllOnlinePlayers)
                {
                    var player = (IServerPlayer)player1;
                    player.Disconnect("Exporting the map now");
                }
                
                _server.Suspend(true, 1000);
            }

            _extractor = new Extractor(_server, _config, _sapi.Logger);
            _extractor.Run();

            _sapi.Logger.Notification("[VintageAtlas] Map export completed successfully");
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

            _isRunning = false;
            _extractor = null;
        }
    }
}


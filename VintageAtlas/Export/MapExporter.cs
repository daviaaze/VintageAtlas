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

            // Initialize output directories
            _config.OutputDirectoryWorld = System.IO.Path.Combine(_config.OutputDirectory, "html", "data", "world");
            _config.OutputDirectoryGeojson = System.IO.Path.Combine(_config.OutputDirectory, "html", "data", "geojson");
            
            // Ensure HTML files exist in output directory for web server
            EnsureHtmlFilesExist();
            
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

    private void EnsureHtmlFilesExist()
    {
        try
        {
            var targetHtmlDir = System.IO.Path.Combine(_config.OutputDirectory, "html");
            
            // If HTML files already exist in output directory, skip
            if (System.IO.Directory.Exists(targetHtmlDir) && System.IO.File.Exists(System.IO.Path.Combine(targetHtmlDir, "index.html")))
            {
                _sapi.Logger.Debug("[VintageAtlas] HTML files already exist in output directory");
                return;
            }
            
            // Find HTML source directory (bundled with mod)
            var modDir = System.IO.Path.GetDirectoryName(GetType().Assembly.Location);
            if (string.IsNullOrEmpty(modDir))
            {
                _sapi.Logger.Warning("[VintageAtlas] Could not find mod directory, HTML files not copied");
                return;
            }
            
            var sourceHtmlDir = System.IO.Path.Combine(modDir, "html");
            if (!System.IO.Directory.Exists(sourceHtmlDir))
            {
                _sapi.Logger.Warning($"[VintageAtlas] HTML source directory not found: {sourceHtmlDir}");
                return;
            }
            
            _sapi.Logger.Notification($"[VintageAtlas] Copying HTML files from mod to output directory...");
            _sapi.Logger.Debug($"[VintageAtlas] Source: {sourceHtmlDir}");
            _sapi.Logger.Debug($"[VintageAtlas] Target: {targetHtmlDir}");
            
            // Copy all HTML files recursively
            CopyDirectory(sourceHtmlDir, targetHtmlDir);
            
            _sapi.Logger.Notification("[VintageAtlas] HTML files copied successfully");
        }
        catch (Exception ex)
        {
            _sapi.Logger.Warning($"[VintageAtlas] Failed to copy HTML files: {ex.Message}");
        }
    }

    private void CopyDirectory(string sourceDir, string targetDir)
    {
        System.IO.Directory.CreateDirectory(targetDir);
        
        // Copy all files
        foreach (var file in System.IO.Directory.GetFiles(sourceDir))
        {
            var fileName = System.IO.Path.GetFileName(file);
            var targetFile = System.IO.Path.Combine(targetDir, fileName);
            System.IO.File.Copy(file, targetFile, overwrite: true);
        }
        
        // Copy all subdirectories
        foreach (var subDir in System.IO.Directory.GetDirectories(sourceDir))
        {
            var dirName = System.IO.Path.GetFileName(subDir);
            
            // Skip the data directory to avoid overwriting exports
            if (dirName == "data") continue;
            
            var targetSubDir = System.IO.Path.Combine(targetDir, dirName);
            CopyDirectory(subDir, targetSubDir);
        }
    }
}


using System;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using Vintagestory.Server;
using VintageAtlas.Core;

namespace VintageAtlas.Infrastructure.VintageStory;

/// <summary>
/// Manages Vintage Story server state transitions.
/// Handles save mode, player disconnections, and server pause/resume.
/// </summary>
public class ServerStateManager(ICoreServerAPI sapi)
{
    private readonly ICoreServerAPI _sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
    private readonly ServerMain _server = (ServerMain)sapi.World;
    private string? _savedPassword;

    public bool IsRunning => _server != null;

    /// <summary>
    /// Enter save mode: disconnect all players, set temporary password, and pause game
    /// </summary>
    public async Task EnterSaveModeAsync()
    {
        _sapi.Logger.Notification("[VintageAtlas] Entering save mode...");

        // Save current password and set temporary one to prevent new connections
        _savedPassword = _sapi.Server.Config.Password;
        _sapi.Server.Config.Password = Random.Shared.Next().ToString();

        // Disconnect all players
        await DisconnectAllPlayersAsync();

        // Pause the game
        _server.Suspend(true, 1000);

        _sapi.Logger.Notification("[VintageAtlas] Save mode active - game paused, players disconnected");
    }

    /// <summary>
    /// Exit save mode: resume game and restore original password
    /// </summary>
    public void ExitSaveMode()
    {
        _sapi.Logger.Notification("[VintageAtlas] Exiting save mode...");

        // Resume the game
        _server.Suspend(false);

        // Restore original password
        _sapi.Server.Config.Password = _savedPassword;
        _savedPassword = null;

        _sapi.Logger.Notification("[VintageAtlas] Save mode exited - game resumed");
    }

    /// <summary>
    /// Stop the server gracefully with a message
    /// </summary>
    public void StopServer(string message)
    {
        _sapi.Logger.Notification($"[VintageAtlas] Stopping server: {message}");
        _server.Stop(message);
    }

    /// <summary>
    /// Disconnect all online players with a message
    /// </summary>
    private async Task DisconnectAllPlayersAsync()
    {
        var players = _sapi.World.AllOnlinePlayers;
        var disconnectedCount = 0;

        foreach (var player in players)
        {
            try
            {
                if (player is IServerPlayer serverPlayer && serverPlayer.Entity != null)
                {
                    _sapi.Logger.Debug($"[VintageAtlas] Disconnecting player {serverPlayer.PlayerName}");
                    serverPlayer.Disconnect("Exporting the map now");
                    disconnectedCount++;
                }
            }
            catch (Exception ex)
            {
                _sapi.Logger.Warning($"[VintageAtlas] Failed to disconnect player: {ex.Message}");
            }
        }

        if (disconnectedCount > 0)
        {
            _sapi.Logger.Notification($"[VintageAtlas] Disconnected {disconnectedCount} player(s)");
            
            // Wait for disconnections to complete
            await Task.Delay(Constants.DisconnectionDelayMs);
        }
    }
}


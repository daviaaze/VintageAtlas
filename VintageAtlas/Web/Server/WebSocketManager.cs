using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Server;

namespace VintageAtlas.Web.Server;

/// <summary>
/// Manages WebSocket connections and broadcasting
/// </summary>
public class WebSocketManager
{
    private readonly ICoreServerAPI _sapi;
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();

    public WebSocketManager(ICoreServerAPI sapi)
    {
        _sapi = sapi;
    }

    /// <summary>
    /// Handle a new WebSocket connection
    /// </summary>
    public async Task HandleConnectionAsync(WebSocketContext context)
    {
        var socket = context.WebSocket;
        var socketId = Guid.NewGuid().ToString();

        if (_sockets.TryAdd(socketId, socket))
        {
            _sapi.Logger.Debug($"[VintageAtlas] WebSocket client connected: {socketId}");

            try
            {
                await ReceiveLoopAsync(socket, socketId);
            }
            catch (Exception ex)
            {
                _sapi.Logger.Warning($"[VintageAtlas] WebSocket error for {socketId}: {ex.Message}");
            }
            finally
            {
                if (_sockets.TryRemove(socketId, out _))
                {
                    _sapi.Logger.Debug($"[VintageAtlas] WebSocket client disconnected: {socketId}");
                }
                
                if (socket.State == WebSocketState.Open)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                socket.Dispose();
            }
        }
    }

    /// <summary>
    /// Broadcast a message to all connected clients
    /// </summary>
    /// <summary>
    /// Broadcast a message to all connected clients
    /// </summary>
    public async Task BroadcastAsync(string message)
    {
        if (_sockets.IsEmpty) return;

        var buffer = Encoding.UTF8.GetBytes(message);
        var segment = new ArraySegment<byte>(buffer);

        // Optimization: Use a pooled list or just iterate if we don't need to wait for all strictly
        // For now, we'll just iterate and fire-and-forget the sends to avoid allocating a huge Task list
        // The socket.SendAsync is thread-safe
        
        foreach (var pair in _sockets)
        {
            var socket = pair.Value;
            if (socket.State == WebSocketState.Open)
            {
                // Fire and forget individual sends to avoid allocating a List<Task>
                // We catch exceptions inside SendAsync to prevent crashing the loop
                _ = SendAsync(socket, segment);
            }
            else
            {
                // Cleanup dead sockets lazily
                _sockets.TryRemove(pair.Key, out _);
            }
        }
        
        await Task.CompletedTask;
    }

    private async Task SendAsync(WebSocket socket, ArraySegment<byte> buffer)
    {
        try
        {
            await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch
        {
            // Ignore send errors, socket will be cleaned up in ReceiveLoop or next broadcast
        }
    }

    private async Task ReceiveLoopAsync(WebSocket socket, string socketId)
    {
        var buffer = new byte[1024 * 4];
        
        while (socket.State == WebSocketState.Open)
        {
            // We don't really expect messages from clients yet, but we need to keep the loop open
            // to detect disconnections
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
                break;
            }
        }
    }
}

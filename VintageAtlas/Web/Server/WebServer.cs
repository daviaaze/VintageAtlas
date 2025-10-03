using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Core;

namespace VintageAtlas.Web.Server;

/// <summary>
/// Manages the HTTP server lifecycle and request handling
/// </summary>
public class WebServer : IDisposable
{
    private readonly ICoreServerAPI _sapi;
    private readonly ModConfig _config;
    private readonly RequestRouter _router;
    private HttpListener? _httpListener;
    private SemaphoreSlim? _requestSemaphore;
    private bool _isRunning;

    public WebServer(ICoreServerAPI sapi, ModConfig config, RequestRouter router)
    {
        _sapi = sapi;
        _config = config;
        _router = router;
    }

    /// <summary>
    /// Start the HTTP server
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;

        try
        {
            var port = _config.LiveServerPort ?? _sapi.Server.Config.Port + 1;
            
            _sapi.Logger.Notification($"[VintageAtlas] Starting web server on port {port}");
            
            // Initialize request throttling semaphore
            var maxRequests = _config.MaxConcurrentRequests ?? 50;
            _requestSemaphore = new SemaphoreSlim(maxRequests, maxRequests);
            _sapi.Logger.Notification($"[VintageAtlas] Request throttling enabled: max {maxRequests} concurrent requests");

            // Start HTTP listener - use + prefix for all-interface binding (like ServerstatusQuery)
            _httpListener = new HttpListener();
            var uriPrefix = $"http://+:{port}/";
            _httpListener.Prefixes.Add(uriPrefix);
            _httpListener.Start();
            
            _isRunning = true;
            
            _sapi.Logger.Notification($"[VintageAtlas] Web server started successfully");
            _sapi.Logger.Notification($"  - Web UI: http://localhost:{port}/");
            _sapi.Logger.Notification($"  - API Status: http://localhost:{port}/api/{_config.LiveServerEndpoint}");
            _sapi.Logger.Notification($"  - Accessible from network: http://<server-ip>:{port}/");
            
            _ = Task.Run(ListenAsync);
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Failed to start web server: {ex.Message}");
            _isRunning = false;
        }
    }

    /// <summary>
    /// Stop the HTTP server
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;

        try
        {
            _isRunning = false;
            
            if (_httpListener != null && _httpListener.IsListening)
            {
                _sapi.Logger.Notification("[VintageAtlas] Shutting down web server");
                _httpListener.Stop();
                _httpListener.Close();
            }
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Error stopping web server: {ex.Message}");
        }
    }

    private async Task ListenAsync()
    {
        if (_httpListener == null || _sapi == null) return;
        
        while (_httpListener.IsListening && _isRunning)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();
                
                // Request throttling - prevent DoS attacks
                if (_requestSemaphore != null && await _requestSemaphore.WaitAsync(0))
                {
                    // Slot acquired - process request
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessRequestAsync(context);
                        }
                        finally
                        {
                            // Always release the slot
                            _requestSemaphore?.Release();
                        }
                    });
                }
                else
                {
                    // Server too busy - reject with 503 Service Unavailable
                    await RejectRequest(context);
                }
            }
            catch (Exception ex)
            {
                if (_httpListener.IsListening && _sapi != null)
                {
                    _sapi.Logger.Error($"[VintageAtlas] Web server error: {ex.Message}");
                }
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            // Add CORS headers if enabled
            if (_config.EnableCORS)
            {
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            }

            // Handle OPTIONS preflight (CORS requirement)
            if (context.Request.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 204; // No Content
                context.Response.OutputStream.Close();
                return;
            }

            // Route the request
            await _router.RouteRequest(context);
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Request processing error: {ex.Message}");
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch { }
        }
    }

    private async Task RejectRequest(HttpListenerContext context)
    {
        try
        {
            context.Response.StatusCode = 503;
            context.Response.Headers.Add("Retry-After", "5");
            
            if (_config.EnableCORS)
            {
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            }
            
            var errorMsg = Encoding.UTF8.GetBytes("{\"error\":\"Server too busy, please retry later\"}");
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = errorMsg.Length;
            await context.Response.OutputStream.WriteAsync(errorMsg, 0, errorMsg.Length);
            context.Response.Close();
            
            _sapi?.Logger.Debug("[VintageAtlas] Request rejected - server at capacity");
        }
        catch
        {
            // Failed to send rejection response
        }
    }

    public void Dispose()
    {
        Stop();
        _requestSemaphore?.Dispose();
        _httpListener?.Close();
    }
}


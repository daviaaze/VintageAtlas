using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Core.Configuration;
using VintageAtlas.Web.Server.Routing;

namespace VintageAtlas.Web.Server;

/// <summary>
/// Manages the HTTP server lifecycle and request handling
/// </summary>
public sealed class WebServer(ICoreServerAPI sapi, ModConfig config, RequestRouter router, WebSocketManager webSocketManager)
    : IDisposable
{
    private HttpListener? _httpListener;
    private readonly WebSocketManager _webSocketManager = webSocketManager;

    // Separate semaphores for different request types
    private SemaphoreSlim? _apiRequestSemaphore;     // Limited for API calls
    private SemaphoreSlim? _tileRequestSemaphore;    // Higher limit for tiles
    private SemaphoreSlim? _staticRequestSemaphore;  // Higher limit for static files

    private bool _isRunning;

    /// <summary>
    /// Start the HTTP server with production-optimized settings
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;

        try
        {
            // Use configured port if set, otherwise default to game port + 1
            var port = config.WebServer.LiveServerPort ?? sapi.Server.Config.Port + 1;

            sapi.Logger.Notification($"[VintageAtlas] Starting production web server on port {port}");

            // PRODUCTION OPTIMIZATION: Increase ServicePointManager limits for high-volume requests
            // This affects client connections FROM this server (not incoming connections)
            ServicePointManager.DefaultConnectionLimit = 1000;
            ServicePointManager.MaxServicePointIdleTime = 10000; // 10-second idle timeout
            ServicePointManager.Expect100Continue = false; // Disable 100-continue handshake
            ServicePointManager.UseNagleAlgorithm = false; // Disable Nagle's algorithm for lower latency
            sapi.Logger.Notification($"[VintageAtlas] ServicePointManager optimized for high-volume serving");

            // Initialize separate throttling semaphores for different request types
            var maxApiRequests = config.WebServer.MaxConcurrentRequests;
            var maxTileRequests = config.WebServer.MaxConcurrentRequests;  // Simplified single limit
            var maxStaticRequests = config.WebServer.MaxConcurrentRequests;

            _apiRequestSemaphore = new SemaphoreSlim(maxApiRequests, maxApiRequests);
            _tileRequestSemaphore = new SemaphoreSlim(maxTileRequests, maxTileRequests);
            _staticRequestSemaphore = new SemaphoreSlim(maxStaticRequests, maxStaticRequests);

            sapi.Logger.Notification($"[VintageAtlas] Request throttling configured:");
            sapi.Logger.Notification($"  - API requests: {maxApiRequests} concurrent");
            sapi.Logger.Notification($"  - Tile requests: {maxTileRequests} concurrent");
            sapi.Logger.Notification($"  - Static files: {maxStaticRequests} concurrent");

            // Start HTTP listener - use + prefix for all-interface binding (like ServerstatusQuery)
            _httpListener = new HttpListener();
            var uriPrefix = $"http://+:{port}/";
            _httpListener.Prefixes.Add(uriPrefix);
            _httpListener.Start();

            _isRunning = true;

            sapi.Logger.Notification($"[VintageAtlas] Web server started successfully");
            sapi.Logger.Notification($"  - Web UI: http://localhost:{port}/");
            sapi.Logger.Notification($"  - API Status: http://localhost:{port}/api/{config.WebServer.LiveServerEndpoint}");
            sapi.Logger.Notification($"  - Accessible from network: http://<server-ip>:{port}/");

            _ = Task.Run(ListenAsync);
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Failed to start web server: {ex.Message}");
            _isRunning = false;
        }
    }

    /// <summary>
    /// Stop the HTTP server
    /// </summary>
    private void Stop()
    {
        if (!_isRunning) return;

        try
        {
            _isRunning = false;

            if (_httpListener is not { IsListening: true })
                return;

            sapi.Logger.Notification("[VintageAtlas] Shutting down web server");
            _httpListener.Stop();
            _httpListener.Close();
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error stopping web server: {ex.Message}");
        }
    }

    private async Task ListenAsync()
    {
        if (_httpListener == null)
            return;

        while (_httpListener.IsListening && _isRunning)
        {
            try
            {
                var context = await _httpListener.GetContextAsync();

                // Handle WebSocket requests
                if (context.Request.IsWebSocketRequest)
                {
                    var wsContext = await context.AcceptWebSocketAsync(subProtocol: null);
                    _ = _webSocketManager.HandleConnectionAsync(wsContext);
                    continue;
                }

                // Determine the request type and apply appropriate throttling
                var rawPath = context.Request.Url?.AbsolutePath ?? "/";
                var path = NormalizePath(rawPath);
                var requestType = ClassifyRequest(path);
                var semaphore = GetSemaphoreForRequestType(requestType);

                // Request throttling with type-specific limits
                // Wait up to 100ms for a slot (better than immediate rejection)
                var timeout = requestType == RequestType.Tile ? 50 : 100; // Shorter for tiles
                if (semaphore != null && await semaphore.WaitAsync(TimeSpan.FromMilliseconds(timeout)))
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
                            semaphore.Release();
                        }
                    });
                }
                else
                {
                    // Server too busy for this request type - waited but no slot available
                    await RejectRequest(context, requestType);
                }
            }

            catch (Exception ex)
            {
                if (_httpListener.IsListening && sapi != null)
                {
                    sapi.Logger.Error($"[VintageAtlas] Web server error: {ex.Message}");
                }
            }
        }
    }

    private string NormalizePath(string path)
    {
        try
        {
            var basePath = config.WebServer.BasePath ?? "/";
            if (string.IsNullOrWhiteSpace(basePath) || basePath == "/") return path;
            if (!basePath.StartsWith('/')) basePath = $"/{basePath}";
            if (!basePath.EndsWith('/')) basePath += '/';
            if (!path.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
                return path;
            var trimmed = path[basePath.Length..];
            return "/" + trimmed;
        }
        catch
        {
            return path;
        }
    }

    private static RequestType ClassifyRequest(string path)
    {
        if (path.StartsWith("/api/"))
            return RequestType.Api;

        if (path.StartsWith("/tiles/") || path.Contains(".png"))
            return RequestType.Tile;

        return RequestType.Static;
    }

    private SemaphoreSlim? GetSemaphoreForRequestType(RequestType type)
    {
        return type switch
        {
            RequestType.Api => _apiRequestSemaphore,
            RequestType.Tile => _tileRequestSemaphore,
            RequestType.Static => _staticRequestSemaphore,
            _ => _apiRequestSemaphore
        };
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        try
        {
            // Add CORS headers if enabled
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

            // Add timeout to prevent requests from hanging indefinitely
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(30)); // 30 second timeout

            try
            {
                // Route the request with timeout
                await router.RouteRequest(context).WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Timeout - try to send error response if not already closed
                try
                {
                    if (!context.Response.SendChunked) // Check if response hasn't started
                    {
                        context.Response.StatusCode = 504; // Gateway Timeout
                        context.Response.Close();
                    }
                }
                catch
                {
                    // Response already closed/disposed - ignore
                }
                sapi.Logger.Warning($"[VintageAtlas] Request timeout: {context.Request.Url?.PathAndQuery}");
            }
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Request processing error: {ex.Message}");
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch
            {
                // Ignore - client likely disconnected
            }
        }
    }

    private async Task RejectRequest(HttpListenerContext context, RequestType requestType)
    {
        try
        {
            context.Response.StatusCode = 503;
            context.Response.Headers.Add("Retry-After", "2"); // Shorter retry for tiles

            if (config.WebServer.EnableCors)
            {
                context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            }

            var errorMsg = requestType == RequestType.Tile
                ? "{\"error\":\"Tile server at capacity\"}"u8.ToArray()
                : "{\"error\":\"Server too busy, please retry later\"}"u8.ToArray();

            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = errorMsg.Length;
            await context.Response.OutputStream.WriteAsync(errorMsg);
            context.Response.Close();

            sapi.Logger.Debug($"[VintageAtlas] {requestType} request rejected - server at capacity");
        }
        catch
        {
            // Failed to send a rejection response
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        Stop();
        _apiRequestSemaphore?.Dispose();
        _tileRequestSemaphore?.Dispose();
        _staticRequestSemaphore?.Dispose();
        _httpListener?.Close();
    }
}
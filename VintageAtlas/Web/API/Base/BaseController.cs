using System;
using System.Net;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Web.API.Responses;

namespace VintageAtlas.Web.API.Base;

/// <summary>
/// Base controller with common HTTP response handling functionality
/// </summary>
public abstract class BaseController(ICoreServerAPI sapi)
{
    protected readonly ICoreServerAPI Sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));

    /// <summary>
    /// Serve an error response with proper formatting
    /// </summary>
    protected Task ServeError(HttpListenerContext context, string message, int statusCode = 500)
        => ErrorResponse.ServeAsync(context, message, statusCode);

    /// <summary>
    /// Check If-None-Match header and return 304 if ETag matches
    /// </summary>
    protected bool CheckETagMatch(HttpListenerContext context, string etag)
    {
        var clientETag = context.Request.Headers["If-None-Match"];
        if (string.IsNullOrEmpty(clientETag) || clientETag != etag)
            return false;

        context.Response.StatusCode = 304;
        context.Response.Headers.Add("ETag", etag);
        context.Response.Close();
        return true;
    }

    /// <summary>
    /// Log an error with controller context
    /// </summary>
    protected void LogError(string message, Exception? ex = null)
    {
        Sapi.Logger.Error($"[VintageAtlas] [{GetType().Name}] {message}");
        if (ex != null)
        {
            Sapi.Logger.Error(ex.StackTrace ?? "");
        }
    }

    /// <summary>
    /// Log a warning with controller context
    /// </summary>
    protected void LogWarning(string message)
    {
        Sapi.Logger.Warning($"[VintageAtlas] [{GetType().Name}] {message}");
    }

    /// <summary>
    /// Log debug information with controller context
    /// </summary>
    protected void LogDebug(string message)
    {
        Sapi.Logger.Debug($"[VintageAtlas] [{GetType().Name}] {message}");
    }
}


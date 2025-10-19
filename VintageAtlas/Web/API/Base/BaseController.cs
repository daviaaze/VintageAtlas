using System;
using System.Net;
using System.Threading.Tasks;
using Vintagestory.API.Server;

namespace VintageAtlas.Web.API.Base;

/// <summary>
/// Base controller with common HTTP response handling functionality
/// </summary>
public abstract class BaseController
{
    protected readonly ICoreServerAPI Sapi;

    protected BaseController(ICoreServerAPI sapi)
    {
        Sapi = sapi ?? throw new ArgumentNullException(nameof(sapi));
    }

    /// <summary>
    /// Serve an error response with proper formatting
    /// </summary>
    protected async Task ServeError(HttpListenerContext context, string message, int statusCode = 500)
    {
        try
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var errorJson = $"{{\"error\":\"{EscapeJson(message)}\"}}";
            var errorBytes = System.Text.Encoding.UTF8.GetBytes(errorJson);

            context.Response.ContentLength64 = errorBytes.Length;
            await context.Response.OutputStream.WriteAsync(errorBytes);
            context.Response.Close();
        }
        catch
        {
            // Silently fail if we can't write error response
        }
    }

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
    /// Escape JSON string values
    /// </summary>
    private static string EscapeJson(string str)
    {
        return str
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
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


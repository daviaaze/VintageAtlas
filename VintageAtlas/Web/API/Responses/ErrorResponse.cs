using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace VintageAtlas.Web.API.Responses;

/// <summary>
/// Centralized error response handling for Web API.
/// Provides consistent error formatting and HTTP status codes.
/// </summary>
public static class ErrorResponse
{
    /// <summary>
    /// Serve a JSON error response with proper formatting and status code
    /// </summary>
    public static async Task ServeAsync(HttpListenerContext context, string message, int statusCode = 500, string? details = null)
    {
        try
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var errorObject = details != null
                ? $"{{\"error\":\"{EscapeJson(message)}\",\"details\":\"{EscapeJson(details)}\"}}"
                : $"{{\"error\":\"{EscapeJson(message)}\"}}";

            var errorBytes = Encoding.UTF8.GetBytes(errorObject);
            context.Response.ContentLength64 = errorBytes.Length;
            
            await context.Response.OutputStream.WriteAsync(errorBytes);
            context.Response.Close();
        }
        catch
        {
            // Silently fail if we can't write an error response
            // This can happen if the client has already disconnected
        }
    }

    /// <summary>
    /// Serve a 400 Bad Request error
    /// </summary>
    public static Task ServeBadRequestAsync(HttpListenerContext context, string message)
        => ServeAsync(context, message, 400);

    /// <summary>
    /// Serve a 404 Not Found error
    /// </summary>
    public static Task ServeNotFoundAsync(HttpListenerContext context, string message = "Resource not found")
        => ServeAsync(context, message, 404);

    /// <summary>
    /// Serve a 405 Method Not Allowed error
    /// </summary>
    public static Task ServeMethodNotAllowedAsync(HttpListenerContext context, string message = "Method not allowed")
        => ServeAsync(context, message, 405);

    /// <summary>
    /// Serve a 500 Internal Server Error
    /// </summary>
    public static Task ServeInternalErrorAsync(HttpListenerContext context, string message, Exception? ex = null)
    {
        var details = ex != null ? $"{ex.Message}\n{ex.StackTrace}" : null;
        return ServeAsync(context, message, 500, details);
    }

    /// <summary>
    /// Escape JSON string values to prevent injection
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
}


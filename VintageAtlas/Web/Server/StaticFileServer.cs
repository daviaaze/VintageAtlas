using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Server;
using VintageAtlas.Core;

namespace VintageAtlas.Web.Server;

/// <summary>
/// Serves static files (HTML, CSS, JS, images) with async I/O and ETag caching
/// </summary>
public class StaticFileServer(ICoreServerAPI sapi, ModConfig config)
{
    private readonly string _basePath = config.BasePath.EndsWith('/') ? config.BasePath : $"{config.BasePath}/";

    // ETag cache for static files (file path -> ETag)
    private readonly ConcurrentDictionary<string, string> _etagCache = new();

    private static readonly Dictionary<string, string> MimeTypes = new()
    {
        { ".html", "text/html" },
        { ".htm", "text/html" },
        { ".css", "text/css" },
        { ".js", "application/javascript" },
        { ".json", "application/json" },
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".svg", "image/svg+xml" },
        { ".webp", "image/webp" },
        { ".ico", "image/x-icon" },
        { ".woff", "font/woff" },
        { ".woff2", "font/woff2" },
        { ".ttf", "font/ttf" },
        { ".eot", "application/vnd.ms-fontobject" },
        { ".otf", "font/otf" }
    };

    private string? FindWebRoot()
    {
        // Serve HTML directly from the mod's bundled html directory
        // No need to copy - static files are served from the mod
        var modHtml = Path.Combine(Path.GetDirectoryName(GetType().Assembly.Location) ?? "", "html");

        if (Directory.Exists(modHtml) && File.Exists(Path.Combine(modHtml, "index.html")))
        {
            return modHtml;
        }

        return null;
    }

    /// <summary>
    /// Try to serve a static file from the web root with async I/O and ETag support
    /// </summary>
    private async Task<bool> TryServeFileAsync(HttpListenerContext context, string requestPath)
    {
        try
        {
            // Default to index.html for a root path
            if (requestPath is "/" or "")
            {
                requestPath = "/index.html";
            }

            // Security: prevent directory traversal
            var safePath = requestPath.TrimStart('/').Replace("..", "");
            var filePath = Path.Combine(FindWebRoot() ?? "", safePath);

            if (!File.Exists(filePath))
            {
                return false;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var mimeType = MimeTypes.GetValueOrDefault(extension, "application/octet-stream");

            // Get or compute ETag for this file
            var fileInfo = new FileInfo(filePath);
            var etag = GetOrComputeETag(filePath, fileInfo);

            // Check the If-None-Match header for conditional requests
            var ifNoneMatch = context.Request.Headers["If-None-Match"];
            if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch == etag)
            {
                // Client has the current version, return 304 Not Modified
                context.Response.StatusCode = 304;
                context.Response.Headers.Add("ETag", etag);
                context.Response.Close();
                return true;
            }

            // Read file asynchronously
            byte[] buffer;
            await using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                buffer = new byte[fs.Length];
                await fs.ReadExactlyAsync(buffer, 0, buffer.Length);
            }

            // Inject a base path into HTML files for nginx sub-path support
            if (extension == ".html")
            {
                var html = Encoding.UTF8.GetString(buffer);
                html = html.Replace("__BASE_PATH__", _basePath);
                buffer = Encoding.UTF8.GetBytes(html);
            }

            context.Response.ContentType = mimeType;
            context.Response.StatusCode = 200;
            context.Response.ContentLength64 = buffer.Length;
            context.Response.Headers.Add("ETag", etag);
            context.Response.Headers.Add("Last-Modified", fileInfo.LastWriteTimeUtc.ToString("R"));

            // Cache control strategy:
            // - HTML/JS/JSON: No cache (always fresh for updates) but with ETag
            // - Map data: 5-minute cache (updates on export)
            // - Static assets (CSS/fonts/images): 1-hour cache (rarely change)
            if (extension is ".html" or ".js" or ".json")
            {
                // Use ETag validation instead of no-cache for better performance
                context.Response.Headers.Add("Cache-Control", "no-cache, must-revalidate");
            }
            else if (safePath.Contains("/data/"))
            {
                // Short cache for map data that updates on export
                context.Response.Headers.Add("Cache-Control", "public, max-age=300");  // 5 minutes
            }
            else
            {
                // Longer cache for static assets that rarely change
                context.Response.Headers.Add("Cache-Control", "public, max-age=3600, immutable");  // 1 hour
            }

            await context.Response.OutputStream.WriteAsync(buffer);

            return true;
        }
        catch (Exception ex)
        {
            sapi.Logger.Error($"[VintageAtlas] Error serving static file {requestPath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Synchronous wrapper for backward compatibility
    /// </summary>
    public bool TryServeFile(HttpListenerContext context, string requestPath)
    {
        return TryServeFileAsync(context, requestPath).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Get or compute ETag for a file (based on last modified time and size)
    /// </summary>
    private string GetOrComputeETag(string filePath, FileInfo fileInfo)
    {
        var cacheKey = filePath;
        var expectedETag = $"\"{fileInfo.LastWriteTimeUtc.Ticks:X}-{fileInfo.Length:X}\"";

        // Check cache
        if (_etagCache.TryGetValue(cacheKey, out var cachedETag) && cachedETag == expectedETag)
        {
            return cachedETag;
        }

        // Update cache
        _etagCache[cacheKey] = expectedETag;
        return expectedETag;
    }

    /// <summary>
    /// Serve a 404 Not Found response
    /// </summary>
    public static void ServeNotFound(HttpListenerContext context)
    {
        context.Response.StatusCode = 404;
        context.Response.ContentType = "text/plain";
        var message = "404 - Not Found"u8.ToArray();
        context.Response.ContentLength64 = message.Length;
        context.Response.OutputStream.Write(message, 0, message.Length);
    }
}


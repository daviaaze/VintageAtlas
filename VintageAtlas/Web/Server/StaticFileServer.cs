using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using Vintagestory.API.Server;
using VintageAtlas.Core;

namespace VintageAtlas.Web.Server;

/// <summary>
/// Serves static files (HTML, CSS, JS, images) from the web root
/// </summary>
public class StaticFileServer
{
    private readonly ICoreServerAPI _sapi;
    private readonly string _webRoot;
    private readonly string _basePath;
    
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

    public StaticFileServer(ICoreServerAPI sapi, string webRoot, ModConfig config)
    {
        _sapi = sapi;
        _webRoot = webRoot;
        _basePath = config.BasePath.EndsWith("/") ? config.BasePath : config.BasePath + "/";
    }

    /// <summary>
    /// Try to serve a static file from the web root
    /// </summary>
    public bool TryServeFile(HttpListenerContext context, string requestPath)
    {
        try
        {
            // Default to index.html for root path
            if (requestPath == "/" || requestPath == "")
            {
                requestPath = "/index.html";
            }

            // Security: prevent directory traversal
            var safePath = requestPath.TrimStart('/').Replace("..", "");
            var filePath = Path.Combine(_webRoot, safePath);

            if (!File.Exists(filePath))
            {
                return false;
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var mimeType = MimeTypes.ContainsKey(extension) ? MimeTypes[extension] : "application/octet-stream";

            context.Response.ContentType = mimeType;
            context.Response.StatusCode = 200;

            // Cache static assets (but not HTML/JSON which may change)
            if (extension != ".html" && extension != ".json")
            {
                context.Response.Headers.Add("Cache-Control", "public, max-age=3600");
            }

            var buffer = File.ReadAllBytes(filePath);
            
            // Inject base path into HTML files for nginx sub-path support
            if (extension == ".html")
            {
                var html = Encoding.UTF8.GetString(buffer);
                html = html.Replace("__BASE_PATH__", _basePath);
                buffer = Encoding.UTF8.GetBytes(html);
            }
            
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            
            return true;
        }
        catch (Exception ex)
        {
            _sapi.Logger.Error($"[VintageAtlas] Error serving static file {requestPath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Serve a 404 Not Found response
    /// </summary>
    public void ServeNotFound(HttpListenerContext context)
    {
        context.Response.StatusCode = 404;
        context.Response.ContentType = "text/plain";
        var message = Encoding.UTF8.GetBytes("404 - Not Found");
        context.Response.ContentLength64 = message.Length;
        context.Response.OutputStream.Write(message, 0, message.Length);
    }
}


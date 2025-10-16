using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace VintageAtlas.Web.Server;

/// <summary>
/// Delegate for API route handlers
/// </summary>
public delegate Task RouteHandler(HttpListenerContext context);

/// <summary>
/// Represents an API route with pattern matching and handler
/// </summary>
public class ApiRoute(string[] patterns, RouteHandler handler, string? httpMethod = null)
{
    private string[] Patterns { get; } = patterns;
    public RouteHandler Handler { get; } = handler;
    private string? HttpMethod { get; } = httpMethod;

    public ApiRoute(string pattern, RouteHandler handler, string? httpMethod = null)
        : this([pattern], handler, httpMethod)
    {
    }

    /// <summary>
    /// Check if this route matches the given path and HTTP method
    /// </summary>
    public bool Matches(string path, string httpMethod)
    {
        if (HttpMethod != null && !string.Equals(HttpMethod, httpMethod, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Patterns.Any(pattern => 
            string.Equals(path, pattern, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, pattern + "/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(pattern + "/", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Simple routing table for API endpoints
/// </summary>
public class ApiRouter
{
    private readonly List<ApiRoute> _routes = [];

    /// <summary>
    /// Register a new route
    /// </summary>
    public void AddRoute(string pattern, RouteHandler handler, string? httpMethod = null)
    {
        _routes.Add(new ApiRoute(pattern, handler, httpMethod));
    }

    /// <summary>
    /// Register a new route with multiple path patterns
    /// </summary>
    public void AddRoute(string[] patterns, RouteHandler handler, string? httpMethod = null)
    {
        _routes.Add(new ApiRoute(patterns, handler, httpMethod));
    }

    /// <summary>
    /// Find a matching route for the given path and HTTP method
    /// </summary>
    public ApiRoute? FindRoute(string path, string httpMethod)
    {
        return _routes.FirstOrDefault(route => route.Matches(path, httpMethod));
    }
}


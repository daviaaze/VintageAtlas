using System.Collections.Generic;
using System.Linq;

namespace VintageAtlas.Web.Server.Routing;

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
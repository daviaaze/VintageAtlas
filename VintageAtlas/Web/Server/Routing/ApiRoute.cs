using System;
using System.Linq;

namespace VintageAtlas.Web.Server.Routing;

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
using System.Net;
using System.Threading.Tasks;
using VintageAtlas.Models.API;

namespace VintageAtlas.Web.API;

/// <summary>
/// Interface for map configuration controller.
/// Provides map metadata (extent, center, zoom levels) to clients.
/// </summary>
public interface IMapConfigController
{
    /// <summary>
    /// Serve map configuration as JSON to HTTP client
    /// </summary>
    Task ServeMapConfig(HttpListenerContext context);

    /// <summary>
    /// Get the current map configuration (used for coordinate transformations)
    /// </summary>
    /// <returns>Map configuration or null if not available</returns>
    MapConfigData? GetCurrentConfig();

    /// <summary>
    /// Invalidate cached configuration (call after map changes)
    /// </summary>
    void InvalidateCache();
}


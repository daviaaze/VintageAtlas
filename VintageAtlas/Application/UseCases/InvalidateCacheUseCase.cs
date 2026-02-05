using Vintagestory.API.Server;
using VintageAtlas.Web.API;

namespace VintageAtlas.Application.UseCases;

/// <summary>
/// Use case for invalidating map cache after changes.
/// Ensures map reflects latest data.
/// </summary>
public class InvalidateCacheUseCase(ICoreServerAPI sapi, IMapConfigController mapConfigController)
{
    private readonly ICoreServerAPI _sapi = sapi;
    private readonly IMapConfigController _mapConfigController = mapConfigController;

    /// <summary>
    /// Invalidate all cached map data
    /// </summary>
    public void Execute()
    {
        _sapi.Logger.Notification("[VintageAtlas] Invalidating map cache...");
        
        _mapConfigController.InvalidateCache();
        
        _sapi.Logger.Notification("[VintageAtlas] Cache invalidated successfully");
    }
}


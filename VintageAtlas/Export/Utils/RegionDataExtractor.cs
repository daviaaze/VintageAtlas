using Vintagestory.API.Server;

namespace VintageAtlas.Export.Utils;

public class RegionDataExtractor
{
    private readonly ICoreServerAPI _sapi;

    public RegionDataExtractor(ICoreServerAPI sapi)
    {
        _sapi = sapi;
    }
}
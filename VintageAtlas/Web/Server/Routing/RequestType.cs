namespace VintageAtlas.Web.Server.Routing;

/// <summary>
/// Request type classification for throttling
/// </summary>
internal enum RequestType
{
    Api,
    Tile,
    Static
}
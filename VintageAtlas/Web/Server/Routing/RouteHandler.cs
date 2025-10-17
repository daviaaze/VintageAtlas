using System.Net;
using System.Threading.Tasks;

namespace VintageAtlas.Web.Server.Routing;

/// <summary>
/// Delegate for API route handlers
/// </summary>
public delegate Task RouteHandler(HttpListenerContext context);
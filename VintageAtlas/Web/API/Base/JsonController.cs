using System.Text;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Vintagestory.API.Server;

namespace VintageAtlas.Web.API.Base;

/// <summary>
/// Base controller for JSON API endpoints with serialization support
/// </summary>
public abstract class JsonController : BaseController
{
    protected readonly JsonSerializerSettings JsonSettings;

    protected JsonController(ICoreServerAPI sapi) : base(sapi)
    {
        JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore
        };
    }

    /// <summary>
    /// Serialize and serve a JSON response
    /// </summary>
    protected async Task ServeJson<T>(HttpListenerContext context, T data, int statusCode = 200, string? etag = null, string? cacheControl = null)
    {
        var json = JsonConvert.SerializeObject(data, JsonSettings);
        await ServeJsonString(context, json, statusCode, etag, cacheControl);
    }

    /// <summary>
    /// Serve a pre-serialized JSON string
    /// </summary>
    protected async Task ServeJsonString(HttpListenerContext context, string json, int statusCode = 200, string? etag = null, string? cacheControl = null)
    {
        var bytes = Encoding.UTF8.GetBytes(json);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;

        if (etag != null)
        {
            context.Response.Headers.Add("ETag", etag);
        }

        if (cacheControl != null)
        {
            context.Response.Headers.Add("Cache-Control", cacheControl);
        }

        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    /// <summary>
    /// Serve GeoJSON with proper content type
    /// </summary>
    protected async Task ServeGeoJson<T>(HttpListenerContext context, T data, string? etag = null, string? cacheControl = "public, max-age=30")
    {
        var json = JsonConvert.SerializeObject(data, JsonSettings);
        var bytes = Encoding.UTF8.GetBytes(json);

        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/geo+json";
        context.Response.ContentLength64 = bytes.Length;

        if (etag != null)
        {
            context.Response.Headers.Add("ETag", etag);
            context.Response.Headers.Add("Vary", "If-None-Match");
        }

        if (cacheControl != null)
        {
            context.Response.Headers.Add("Cache-Control", cacheControl);
        }

        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }
}


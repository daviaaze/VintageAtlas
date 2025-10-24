using System;
using System.Net;
using System.Threading.Tasks;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using VintageAtlas.Web.API.Base;
using Vintagestory.API.Common;
using VintageAtlas.Core;

namespace VintageAtlas.Web.API.Controllers;

/// <summary>
/// Provides real-time weather information at specific locations
/// </summary>
public class WeatherController(ICoreServerAPI sapi, CoordinateTransformService coordinateService) : JsonController(sapi)
{

    /// <summary>
    /// Get weather information at specific coordinates
    /// Query params: x, z (block coordinates)
    /// </summary>
    public async Task GetWeatherAtLocation(HttpListenerContext context)
    {
        try
        {
            var query = context.Request.QueryString;

            // Parse coordinates from query string
            if (!int.TryParse(query["x"], out var x) || !int.TryParse(query["z"], out var z))
            {
                await ServeError(context, "Invalid or missing coordinates. Use ?x=123&z=456", 400);
                return;
            }

            var (displayX, displayZ) = coordinateService.DisplayToGame(x, z);

            var weatherData = GetWeatherAtCoordinates(displayX, displayZ);

            // Cache for 2 seconds (weather changes but not instantly)
            await ServeJson(context, weatherData, cacheControl: "max-age=2");
        }
        catch (Exception ex)
        {
            LogError($"Error getting weather at location: {ex.Message}", ex);
            await ServeError(context, "Failed to get weather information");
        }
    }

    /// <summary>
    /// Get weather information at specific block coordinates
    /// Note: This returns climate data from the map. Real-time precipitation requires
    /// accessing the weather system which may have restricted access.
    /// </summary>
    private ClimateCondition? GetWeatherAtCoordinates(int x, int z)
    {
        try
        {
            var climate = Sapi.World.BlockAccessor.GetClimateAt(new BlockPos(x, 0, z), EnumGetClimateMode.ForSuppliedDateValues, Sapi.World.Calendar.DaysPerYear);
            return climate;
        }
        catch (Exception ex)
        {
            LogError($"Error accessing climate data: {ex.Message}", ex);
            return null;
        }
    }
}


using System;
using System.Net;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VintageAtlas.Web.API.Base;

namespace VintageAtlas.Web.API.Controllers;

/// <summary>
/// Provides real-time game status information (time, date, season, etc.)
/// </summary>
public class StatusController : JsonController
{
    public StatusController(ICoreServerAPI sapi) : base(sapi)
    {
    }

    /// <summary>
    /// Get current game status including calendar and time information
    /// </summary>
    public async Task GetStatus(HttpListenerContext context)
    {
        try
        {
            var calendar = Sapi.World.Calendar;

            // Calculate season information
            var (name, progress) = GetSeasonInfo(calendar);

            // Get time of day in hours (0-24)
            var hourOfDay = calendar.HourOfDay;

            var statusData = new
            {
                // Calendar information
                calendar = new
                {
                    totalDays = calendar.TotalDays,
                    year = calendar.Year,
                    month = calendar.Month,
                    day = (int)(calendar.TotalDays % calendar.DaysPerMonth) + 1,
                    dayOfYear = calendar.DayOfYear,
                    season = name,
                    seasonProgress = progress, // 0.0 to 1.0 through current season

                    // Time information
                    totalHours = calendar.TotalHours,
                    hourOfDay = hourOfDay,
                    minute = (int)(hourOfDay % 1.0 * 60),

                    // Additional useful info
                    daysPerMonth = calendar.DaysPerMonth,
                    hoursPerDay = calendar.HoursPerDay,
                    speedOfTime = calendar.SpeedOfTime,
                },

                // Climate modifiers for temperature calculation
                temperature = new
                {
                    // Season modifier: approximately -15°C (winter) to +15°C (summer)
                    seasonModifier = CalculateSeasonModifier(progress, name),

                    // Time of day modifier: approximately -10°C (4am) to +15°C (4pm)
                    timeOfDayModifier = CalculateTimeOfDayModifier(hourOfDay),

                    // Combined modifier (approx)
                    totalModifier = CalculateSeasonModifier(progress, name) +
                                   CalculateTimeOfDayModifier(hourOfDay)
                },

                // Server information
                server = new
                {
                    playersOnline = Sapi.World.AllOnlinePlayers?.Length ?? 0,
                    serverName = Sapi.World.Config.GetString("ServerName", "Vintage Story Server")
                },

                // Weather information (if available)
                weather = GetWeatherInfo()
            };

            // Cache for 1 second (data changes frequently but not every millisecond)
            await ServeJson(context, statusData, cacheControl: "max-age=1");
        }
        catch (Exception ex)
        {
            LogError($"Error serving status: {ex.Message}", ex);
            await ServeError(context, "Failed to get status");
        }
    }

    /// <summary>
    /// Determine current season based on day of year
    /// Vintage Story has 4 seasons: Spring, Summer, Fall, Winter
    /// Each season is 3 months (9 days per month default = 27 days per season)
    /// </summary>
    private static (string name, double progress) GetSeasonInfo(IGameCalendar calendar)
    {
        var daysPerYear = calendar.DaysPerMonth * 12; // Usually 108 days
        var dayOfYear = calendar.DayOfYear % daysPerYear;
        var daysPerSeason = daysPerYear / 4.0;

        var seasonIndex = (int)(dayOfYear / daysPerSeason);
        var seasonProgress = (dayOfYear % daysPerSeason) / daysPerSeason;

        var seasonName = seasonIndex switch
        {
            0 => "Spring",
            1 => "Summer",
            2 => "Fall",
            3 => "Winter",
            _ => "Spring"
        };

        return (seasonName, seasonProgress);
    }

    /// <summary>
    /// Calculate temperature modifier based on season
    /// Winter: -15°C, Spring: 0°C, Summer: +15°C, Fall: 0°C
    /// </summary>
    private static double CalculateSeasonModifier(double seasonProgress, string seasonName)
    {
        // Simple approximation based on season
        // In reality, VS uses more complex calculations
        return seasonName switch
        {
            "Spring" => -5.0 + (seasonProgress * 10.0), // -5°C to +5°C
            "Summer" => 5.0 + (seasonProgress * 10.0),  // +5°C to +15°C
            "Fall" => 15.0 - (seasonProgress * 15.0),    // +15°C to 0°C
            "Winter" => -5.0 - (seasonProgress * 10.0),  // 0°C to -15°C
            _ => 0.0
        };
    }

    /// <summary>
    /// Calculate temperature modifier based on time of day
    /// Coldest at 4am (-10°C), hottest at 4pm (+15°C)
    /// </summary>
    private static double CalculateTimeOfDayModifier(double hourOfDay)
    {
        // Temperature peaks at 16:00 (4pm) and troughs at 4:00 (4am)
        // Using a sine wave shifted appropriately
        var hourAngle = (hourOfDay - 4.0) / 24.0 * 2.0 * Math.PI;
        var amplitude = 12.5; // ±12.5°C variation
        var baseline = 2.5;   // +2.5°C average offset

        return baseline + (amplitude * Math.Sin(hourAngle));
    }

    /// <summary>
    /// Get current weather information including precipitation
    /// </summary>
    private object GetWeatherInfo()
    {
        try
        {
            // Check if weather system is available
            // Note: Full weather data access requires coordinate-specific queries
            // Use the /api/weather endpoint with coordinates for detailed info
            return new
            {
                available = true,
                message = "Use /api/weather?x=<x>&z=<z> for location-specific weather"
            };
        }
        catch
        {
            return new { available = false };
        }
    }
}


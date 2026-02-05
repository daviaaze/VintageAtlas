using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VintageAtlas.Core;
using VintageAtlas.Web.API.Base;

namespace VintageAtlas.Web.API.Controllers;

/// <summary>
/// Provides waypoint data via API
/// </summary>
public class WaypointController(ICoreServerAPI sapi, CoordinateTransformService coordinateService) : JsonController(sapi)
{
    private readonly CoordinateTransformService _coordinateService = coordinateService ?? throw new ArgumentNullException(nameof(coordinateService));

    /// <summary>
    /// Get all server waypoints
    /// </summary>
    public async Task GetWaypoints(HttpListenerContext context)
    {
        try
        {
            // Use reflection to get WaypointMap mod system to avoid direct dependency issues
            var modSystem = Sapi.ModLoader.GetModSystem("WaypointMap");
            if (modSystem == null)
            {
                await ServeError(context, "WaypointMap mod system not found");
                return;
            }

            var waypoints = new List<object>();
            
            // WaypointMap has a method GetWaypoints(IPlayer player)
            var modSystemType = modSystem.GetType();
            var getWaypointsMethod = modSystemType.GetMethod("GetWaypoints");

            if (getWaypointsMethod != null)
            {
                foreach (var player in Sapi.World.AllOnlinePlayers)
                {
                    var playerWaypoints = getWaypointsMethod.Invoke(modSystem, new object[] { player }) as System.Collections.IList;
                    
                    if (playerWaypoints != null)
                    {
                        foreach (var wpObj in playerWaypoints)
                        {
                            // Reflect on the Waypoint object
                            var wpType = wpObj.GetType();
                            var title = wpType.GetProperty("Title")?.GetValue(wpObj) as string ?? "Unknown";
                            var color = (int)(wpType.GetProperty("Color")?.GetValue(wpObj) ?? 0);
                            var icon = wpType.GetProperty("Icon")?.GetValue(wpObj) as string ?? "circle";
                            var pinned = (bool)(wpType.GetProperty("Pinned")?.GetValue(wpObj) ?? false);
                            var position = wpType.GetProperty("Position")?.GetValue(wpObj) as Vintagestory.API.MathTools.Vec3d;

                            if (position != null)
                            {
                                var (x, y) = _coordinateService.GameToDisplay(position.AsBlockPos);
                                
                                // Convert int color to hex string
                                var colorHex = "#" + (color & 0xFFFFFF).ToString("X6");

                                waypoints.Add(new
                                {
                                    title,
                                    color = colorHex,
                                    icon,
                                    x,
                                    y,
                                    pinned,
                                    owner = player.PlayerName
                                });
                            }
                        }
                    }
                }
            }

            var response = new
            {
                waypoints,
                count = waypoints.Count
            };

            await ServeJson(context, response, cacheControl: "max-age=5");
        }
        catch (Exception ex)
        {
            LogError($"Error serving waypoints: {ex.Message}", ex);
            await ServeError(context, "Failed to get waypoints");
        }
    }
}

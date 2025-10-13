using Vintagestory.API.Common;
using Vintagestory.API.Server;
using VintageAtlas.Core;

namespace VintageAtlas.Commands;

/// <summary>
/// Handles the /atlas export command
/// </summary>
public static class ExportCommand
{
    public static void Register(ICoreServerAPI sapi, IMapExporter mapExporter)
    {
        sapi.ChatCommands.GetOrCreate("atlas")
            .WithDescription("Manage the VintageAtlas mod")
            .WithAlias("va")
            .RequiresPrivilege(Privilege.controlserver)
            .BeginSubCommand("export")
            .WithDescription("Start the map export")
            .HandleWith(_ =>
            {
                if (mapExporter.IsRunning)
                {
                    return TextCommandResult.Success("Export already running.");
                }

                mapExporter.StartExport();
                return TextCommandResult.Success("Map export started. Check server console for progress.");
            })
            .EndSubCommand();
    }
}


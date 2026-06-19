using System.Net;
using System.Text.Json;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Core.Agent.Handlers;

internal static class StructuresHandler
{
    public static void HandleGetStructures(IGameAgentBridge? bridge, HttpListenerResponse response)
    {
        var placements = StructureGallery.GetPlacements();
        var structures = new List<object>(placements.Count);
        foreach (var placement in placements)
        {
            structures.Add(new
            {
                id = placement.Id,
                tier = placement.Tier.ToString(),
                index = placement.Index,
                anchor = new
                {
                    x = placement.AnchorX,
                    y = placement.SurfaceY,
                    z = placement.AnchorZ
                },
                footprintRadius = placement.FootprintRadius,
                cellSize = placement.CellSize
            });
        }

        var payload = new
        {
            seed = StructureGallery.Seed,
            worldType = WorldType.StructureGallery.ToString(),
            cellSize = placements.Count > 0 ? placements[0].CellSize : 0,
            columns = StructureGallery.Columns,
            surfaceY = StructureGallery.SurfaceY,
            padding = StructureGallery.Padding,
            spawn = new
            {
                x = StructureGallery.GetPlayerSpawn().X,
                z = StructureGallery.GetPlayerSpawn().Z
            },
            active = bridge?.IsStructureGalleryWorld ?? false,
            structures
        };

        string json = JsonSerializer.Serialize(payload);
        Agent.AgentHttpHelpers.SendResponse(response, HttpStatusCode.OK, json, "application/json");
    }
}

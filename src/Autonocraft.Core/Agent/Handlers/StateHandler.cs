using System.Net;
using Autonocraft.Core.Agent.Serialization;
using Autonocraft.Domain.World;

namespace Autonocraft.Core.Agent.Handlers;

internal static class StateHandler
{
    public static void HandleGetHealth(IGameAgentBridge? bridge, HttpListenerResponse response)
    {
        if (bridge == null)
        {
            var dto = new AgentHealthDto(false, "Unknown");
            AgentHttpHelpers.SendJsonResponse(response, HttpStatusCode.ServiceUnavailable, dto);
            return;
        }

        var state = bridge.CurrentGameState;
        bool ready = state == GameState.Playing;
        var statusCode = ready ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
        var health = new AgentHealthDto(ready, state.ToString());
        AgentHttpHelpers.SendJsonResponse(response, statusCode, health);
    }

    public static void HandleGetMetrics(HttpListenerResponse response)
    {
        AgentHttpHelpers.SendResponse(response, HttpStatusCode.OK, RuntimeMetrics.ToJson(), "application/json");
    }

    public static void HandleGetSlabScan(IGameAgentBridge? bridge, HttpListenerRequest request, HttpListenerResponse response)
    {
        if (bridge == null || bridge.CurrentGameState != GameState.Playing)
        {
            AgentHttpHelpers.SendJsonResponse(response, HttpStatusCode.ServiceUnavailable, new { error = "Game not playing" });
            return;
        }

        int radius = 12;
        if (int.TryParse(request.QueryString["radius"], out int parsed))
        {
            radius = Math.Clamp(parsed, 1, 32);
        }

        var world = bridge.Host.Session.Grid;
        var player = bridge.Host.Session.Player;
        int px = (int)MathF.Floor(player.Position.X);
        int py = (int)MathF.Floor(player.Position.Y);
        int pz = (int)MathF.Floor(player.Position.Z);

        int slabCount = 0;
        int stoneSlabCount = 0;
        int snowSlabCount = 0;
        int maxSlabY = int.MinValue;
        var samples = new List<object>();

        for (int dz = -radius; dz <= radius; dz++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int wx = px + dx;
                int wz = pz + dz;
                int surfaceY = world.GetHighestSolidY(wx, wz);
                if (surfaceY < 0)
                {
                    continue;
                }

                var block = world.GetBlock(wx, surfaceY, wz);
                if (!block.IsSlab())
                {
                    continue;
                }

                slabCount++;
                maxSlabY = Math.Max(maxSlabY, surfaceY);
                if (block == BlockType.StoneSlab)
                {
                    stoneSlabCount++;
                }
                else if (block == BlockType.SnowSlab)
                {
                    snowSlabCount++;
                }

                if (samples.Count < 8)
                {
                    samples.Add(new { x = wx, y = surfaceY, z = wz, block = block.ToString() });
                }
            }
        }

        AgentHttpHelpers.SendJsonResponse(response, HttpStatusCode.OK, new
        {
            slabCount,
            stoneSlabCount,
            snowSlabCount,
            maxSlabY = maxSlabY == int.MinValue ? -1 : maxSlabY,
            player = new { x = px, y = py, z = pz },
            samples
        });
    }

    public static void HandleGetState(IGameAgentBridge? bridge, HttpListenerResponse response)
    {
        if (bridge == null)
        {
            AgentHttpHelpers.SendResponse(response, HttpStatusCode.InternalServerError, "{\"error\": \"Game not initialized\"}", "application/json");
            return;
        }

        if (bridge.CurrentGameState != GameState.Playing)
        {
            AgentHttpHelpers.SendResponse(response, HttpStatusCode.ServiceUnavailable,
                $"{{\"error\": \"Game not in Playing state\", \"gameState\": \"{bridge.CurrentGameState}\"}}",
                "application/json");
            return;
        }

        var stateDto = AgentStateSerializer.BuildStateDto(bridge);
        AgentHttpHelpers.SendJsonResponse(response, HttpStatusCode.OK, stateDto);
    }

    public static void HandleGetVillageDebug(IGameAgentBridge? bridge, HttpListenerResponse response)
    {
        if (bridge == null)
        {
            AgentHttpHelpers.SendResponse(response, HttpStatusCode.InternalServerError, "{\"error\": \"Game not initialized\"}", "application/json");
            return;
        }

        if (bridge.CurrentGameState != GameState.Playing)
        {
            AgentHttpHelpers.SendResponse(response, HttpStatusCode.ServiceUnavailable,
                $"{{\"error\": \"Game not in Playing state\", \"gameState\": \"{bridge.CurrentGameState}\"}}",
                "application/json");
            return;
        }

        var session = bridge.Host.Session;
        var village = session.Villages.GetActiveVillage(session.Player.Position);
        if (village == null)
        {
            AgentHttpHelpers.SendResponse(response, HttpStatusCode.NotFound, "{\"error\": \"No active village\"}", "application/json");
            return;
        }

        var payload = AgentStateSerializer.BuildVillageDebugDto(session, village);
        AgentHttpHelpers.SendJsonResponse(response, HttpStatusCode.OK, payload);
    }
}

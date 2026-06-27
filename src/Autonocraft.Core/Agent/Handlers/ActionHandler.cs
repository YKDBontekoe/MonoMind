using System.Net;
using Autonocraft.Core.Agent;

namespace Autonocraft.Core.Agent.Handlers;

internal static class ActionHandler
{
    private static readonly Dictionary<string, IAgentAction> Actions = CreateActionRegistry();

    public static void HandlePostAction(IGameAgentBridge? bridge, HttpListenerRequest request, HttpListenerResponse response)
    {
        if (bridge == null)
        {
            AgentHttpHelpers.SendResponse(response, HttpStatusCode.InternalServerError, "{\"error\": \"Game not initialized\"}", "application/json");
            return;
        }

        string? cmd = request.QueryString["cmd"];
        if (string.IsNullOrEmpty(cmd))
        {
            AgentHttpHelpers.SendResponse(response, HttpStatusCode.BadRequest, "{\"error\": \"Missing 'cmd' parameter\"}", "application/json");
            return;
        }

        if (!Actions.TryGetValue(cmd.ToLowerInvariant(), out var action))
        {
            var unknown = new AgentActionResponseDto(false, $"Unknown action cmd: {cmd}");
            AgentHttpHelpers.SendJsonResponse(response, HttpStatusCode.BadRequest, unknown);
            return;
        }

        var result = action.Execute(bridge, request);
        var statusCode = result.Success ? HttpStatusCode.OK : HttpStatusCode.BadRequest;
        AgentHttpHelpers.SendJsonResponse(response, statusCode, result);
    }

    private static Dictionary<string, IAgentAction> CreateActionRegistry()
    {
        var actions = new IAgentAction[]
        {
            new KeyDownAction(),
            new KeyUpAction(),
            new ReleaseKeysAction(),
            new ClickAction(),
            new SetLookAction(),
            new LookAction(),
            new TeleportAction(),
            new SetCreativeAction(),
            new SelectSlotAction(),
            new ShutdownAction(),
            new SetTimeAction(),
            new SetTimeScaleAction(),
            new OpenCrucibleAction(),
            new DevConsoleAction(),
            new RecruitVillagerAction(),
            new AssignJobAction(),
            new QueueBuildAction(),
            new OpenVillageAction(),
            new CloseVillageAction(),
            new CloseVillageUiAliasAction(),
            new LoadStructureGalleryAction(),
            new ScreenshotAction()
        };

        var dict = new Dictionary<string, IAgentAction>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in actions)
        {
            dict[action.Command] = action;
        }

        return dict;
    }
}

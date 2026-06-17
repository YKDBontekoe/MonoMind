using System.Net;
using Autonocraft.Ai;
using Autonocraft.Core.Agent;

namespace Autonocraft.Core.Agent.Handlers;

internal static class VillageChatHandler
{
    public static void HandlePostVillageChat(IGameAgentBridge? bridge, HttpListenerRequest request, HttpListenerResponse response)
    {
        if (bridge == null || bridge.CurrentGameState != GameState.Playing)
        {
            AgentHttpHelpers.SendResponse(response, HttpStatusCode.ServiceUnavailable, "{\"error\": \"Game not ready\"}", "application/json");
            return;
        }

        var settings = bridge.Host.Settings;
        if (!settings.PlayWithAi || settings.AiProvider == AiProviderKind.Disabled)
        {
            AgentHttpHelpers.SendResponse(response, HttpStatusCode.BadRequest, "{\"error\": \"Village AI is disabled in settings\"}", "application/json");
            return;
        }

        string body;
        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
        {
            body = reader.ReadToEnd();
        }

        string message = AgentHttpHelpers.ExtractJsonString(body, "message") ?? request.QueryString["message"] ?? string.Empty;
        string target = AgentHttpHelpers.ExtractJsonString(body, "target") ?? request.QueryString["target"] ?? "mayor";
        var chatTcs = new TaskCompletionSource<(string reply, List<string> actions)>();

        bridge.PendingActions.Enqueue(() =>
        {
            try
            {
                var orchestrator = bridge.Host.Session.VillageAi;
                string reply = orchestrator.HandleChatAsync(message, target, bridge.Host.Session).GetAwaiter().GetResult();
                var actions = new List<string>(orchestrator.LastExecutedActions);
                chatTcs.SetResult((reply, actions));
            }
            catch (Exception ex)
            {
                chatTcs.SetException(ex);
            }
        });

        try
        {
            if (!chatTcs.Task.Wait(TimeSpan.FromSeconds(30)))
            {
                AgentHttpHelpers.SendResponse(response, HttpStatusCode.RequestTimeout, "{\"error\": \"Chat timed out\"}", "application/json");
                return;
            }

            var (reply, actions) = chatTcs.Task.Result;
            var dto = new AgentChatResponseDto(reply, actions);
            AgentHttpHelpers.SendJsonResponse(response, HttpStatusCode.OK, dto);
        }
        catch (Exception ex)
        {
            AgentHttpHelpers.SendJsonResponse(response, HttpStatusCode.InternalServerError, new { error = ex.Message });
        }
    }

    public static void HandlePostVillageChatConfirm(IGameAgentBridge? bridge, HttpListenerRequest request, HttpListenerResponse response)
    {
        if (bridge == null || bridge.CurrentGameState != GameState.Playing)
        {
            AgentHttpHelpers.SendResponse(response, HttpStatusCode.ServiceUnavailable, "{\"error\": \"Game not ready\"}", "application/json");
            return;
        }

        string body;
        using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
        {
            body = reader.ReadToEnd();
        }

        bool confirmed = string.Equals(
            AgentHttpHelpers.ExtractJsonString(body, "confirm") ?? request.QueryString["confirm"],
            "true",
            StringComparison.OrdinalIgnoreCase);
        string target = AgentHttpHelpers.ExtractJsonString(body, "target") ?? request.QueryString["target"] ?? "mayor";
        var confirmTcs = new TaskCompletionSource<string>();

        bridge.PendingActions.Enqueue(() =>
        {
            try
            {
                string reply = bridge.Host.Session.VillageAi
                    .ConfirmPendingAsync(bridge.Host.Session, confirmed, target)
                    .GetAwaiter().GetResult();
                confirmTcs.SetResult(reply);
            }
            catch (Exception ex)
            {
                confirmTcs.SetException(ex);
            }
        });

        try
        {
            if (!confirmTcs.Task.Wait(TimeSpan.FromSeconds(15)))
            {
                AgentHttpHelpers.SendResponse(response, HttpStatusCode.RequestTimeout, "{\"error\": \"Confirm timed out\"}", "application/json");
                return;
            }

            string reply = confirmTcs.Task.Result;
            var dto = new AgentChatConfirmResponseDto(true, reply);
            AgentHttpHelpers.SendJsonResponse(response, HttpStatusCode.OK, dto);
        }
        catch (Exception ex)
        {
            AgentHttpHelpers.SendJsonResponse(response, HttpStatusCode.InternalServerError, new { error = ex.Message });
        }
    }
}

using System.Net;
using System.Threading.Tasks;
using Autonocraft.Core.Agent;

namespace Autonocraft.Core.Agent.Handlers;

internal static class ScreenshotHandler
{
    public static void HandleGetScreenshot(IGameAgentBridge? bridge, HttpListenerRequest request, HttpListenerResponse response)
    {
        if (bridge == null)
        {
            AgentHttpHelpers.SendResponse(response, HttpStatusCode.InternalServerError, "{\"error\": \"Game not initialized\"}", "application/json");
            return;
        }

        string? customPath = request.QueryString["path"];
        string? screenshotPath = string.IsNullOrWhiteSpace(customPath)
            ? null
            : customPath;

        try
        {
            Task<byte[]> captureTask = bridge.RequestScreenshotAsync(screenshotPath);
            if (!captureTask.Wait(AgentActionTimeouts.QueuedActionWaitMs))
            {
                AgentHttpHelpers.SendResponse(response, HttpStatusCode.RequestTimeout, "{\"error\": \"Screenshot capture timed out\"}", "application/json");
                return;
            }

            byte[] bytes = captureTask.Result;
            response.ContentType = "image/png";
            response.ContentLength64 = bytes.Length;
            response.StatusCode = (int)HttpStatusCode.OK;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            AgentHttpHelpers.SendJsonResponse(response, HttpStatusCode.InternalServerError, new { error = ex.InnerException?.Message ?? ex.Message });
        }
    }
}

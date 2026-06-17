using System.Net;
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
        string screenshotPath = customPath ?? Path.Combine(AppContext.BaseDirectory, "screenshot.png");

        var tcs = new TaskCompletionSource<byte[]>();

        bridge.PendingActions.Enqueue(() =>
        {
            try
            {
                bridge.SaveScreenshot(screenshotPath);
                if (File.Exists(screenshotPath))
                {
                    byte[] bytes = File.ReadAllBytes(screenshotPath);
                    tcs.SetResult(bytes);
                }
                else
                {
                    tcs.SetException(new FileNotFoundException("Screenshot file was not generated"));
                }
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        try
        {
            if (tcs.Task.Wait(AgentActionTimeouts.QueuedActionWaitMs))
            {
                byte[] bytes = tcs.Task.Result;
                response.ContentType = "image/png";
                response.ContentLength64 = bytes.Length;
                response.StatusCode = (int)HttpStatusCode.OK;
                response.OutputStream.Write(bytes, 0, bytes.Length);
                response.OutputStream.Close();
            }
            else
            {
                AgentHttpHelpers.SendResponse(response, HttpStatusCode.RequestTimeout, "{\"error\": \"Screenshot capture timed out\"}", "application/json");
            }
        }
        catch (Exception ex)
        {
            AgentHttpHelpers.SendJsonResponse(response, HttpStatusCode.InternalServerError, new { error = ex.InnerException?.Message ?? ex.Message });
        }
    }
}

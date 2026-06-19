using System.Net;
using Autonocraft.Core.Agent.Handlers;

namespace Autonocraft.Core;

public static class AgentHttpServer
{
    private static HttpListener? _listener;
    private static IGameAgentBridge? _bridge;
    private static bool _isRunning;

    public static void Start(IGameAgentBridge bridge, int port = 5000)
    {
        if (_isRunning) return;

        _bridge = bridge;
        _listener = new HttpListener();
        AddListenPrefixes(_listener, port);
        try
        {
            _listener.Start();
        }
        catch (Exception ex)
        {
            _listener.Close();
            _listener = null;
            throw new InvalidOperationException($"Failed to start HTTP listener on port {port}: {ex.Message}", ex);
        }

        _isRunning = true;

        Console.WriteLine($"[Agent HTTP Server] Started and listening on http://127.0.0.1:{port}/");

        Task.Run(ListenLoop);
    }

    private static void AddListenPrefixes(HttpListener listener, int port)
    {
        if (port < 1 || port > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Agent HTTP port must be between 1 and 65535.");
        }

        string[] prefixes =
        [
            $"http://127.0.0.1:{port}/",
            $"http://localhost:{port}/",
        ];

        if (OperatingSystem.IsWindows())
        {
            prefixes =
            [
                $"http://127.0.0.1:{port}/",
                $"http://[::1]:{port}/",
                $"http://localhost:{port}/"
            ];
        }

        foreach (string prefix in prefixes)
        {
            try
            {
                listener.Prefixes.Add(prefix);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Agent HTTP Server] Skipped prefix {prefix}: {ex.Message}");
            }
        }

        if (listener.Prefixes.Count == 0)
        {
            throw new InvalidOperationException($"No HTTP listener prefixes could be registered for port {port}.");
        }
    }

    private static async Task ListenLoop()
    {
        while (_isRunning && _listener != null)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context));
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Agent HTTP Server Error] {ex.Message}");
            }
        }
    }

    private static void HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
        }
        catch
        {
            // HttpListener header quirks on some platforms.
        }

        if (request.HttpMethod == "OPTIONS")
        {
            response.StatusCode = (int)HttpStatusCode.OK;
            response.Close();
            return;
        }

        try
        {
            string path = request.Url?.AbsolutePath.ToLower() ?? "";

            switch (path, request.HttpMethod)
            {
                case ("/health", "GET"):
                    StateHandler.HandleGetHealth(_bridge, response);
                    break;
                case ("/metrics", "GET"):
                    StateHandler.HandleGetMetrics(response);
                    break;
                case ("/state", "GET"):
                    StateHandler.HandleGetState(_bridge, response);
                    break;
                case ("/village/debug", "GET"):
                    StateHandler.HandleGetVillageDebug(_bridge, response);
                    break;
                case ("/debug/slabscan", "GET"):
                    StateHandler.HandleGetSlabScan(_bridge, request, response);
                    break;
                case ("/structures", "GET"):
                    StructuresHandler.HandleGetStructures(_bridge, response);
                    break;
                case ("/screenshot", "GET"):
                    ScreenshotHandler.HandleGetScreenshot(_bridge, request, response);
                    break;
                case ("/action", "POST"):
                    ActionHandler.HandlePostAction(_bridge, request, response);
                    break;
                case ("/village/chat", "POST"):
                    VillageChatHandler.HandlePostVillageChat(_bridge, request, response);
                    break;
                case ("/village/chat/confirm", "POST"):
                    VillageChatHandler.HandlePostVillageChatConfirm(_bridge, request, response);
                    break;
                default:
                    Agent.AgentHttpHelpers.SendResponse(response, HttpStatusCode.NotFound, "{\"error\": \"Not Found\"}", "application/json");
                    break;
            }
        }
        catch (Exception ex)
        {
            Agent.AgentHttpHelpers.SendJsonResponse(response, HttpStatusCode.InternalServerError, new { error = ex.Message });
        }
    }

    internal static bool TryParseKeyInternal(string? keyStr, out Key key)
    {
        key = default;
        if (string.IsNullOrEmpty(keyStr)) return false;

        switch (keyStr.ToLower())
        {
            case "w": key = Key.W; return true;
            case "s": key = Key.S; return true;
            case "a": key = Key.A; return true;
            case "d": key = Key.D; return true;
            case "space": key = Key.Space; return true;
            case "shift":
            case "left-shift":
            case "shiftleft":
                key = Key.ShiftLeft; return true;
        }

        return Enum.TryParse(keyStr, true, out key);
    }

    public static void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;
        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch
        {
            // Listener may already be disposed.
        }

        _listener = null;
        _bridge = null;
        Console.WriteLine("[Agent HTTP Server] Stopped.");
    }
}

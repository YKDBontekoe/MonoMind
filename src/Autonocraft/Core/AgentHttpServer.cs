using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Autonocraft.Core
{
    public static class AgentHttpServer
    {
        private static HttpListener? _listener;
        private static AutonocraftGame? _game;
        private static bool _isRunning = false;

        public static void Start(AutonocraftGame game, int port = 5000)
        {
            if (_isRunning) return;

            _game = game;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();
            _isRunning = true;

            Console.WriteLine($"[Agent HTTP Server] Started and listening on http://localhost:{port}/");

            Task.Run(ListenLoop);
        }

        private static async Task ListenLoop()
        {
            while (_isRunning && _listener != null)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context)); // Handle request concurrently
                }
                catch (HttpListenerException)
                {
                    // Listener stopped
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

            // Allow CORS
            // Adding a try-catch for Headers configuration as standard HttpListener can be picky
            try
            {
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            }
            catch {}

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.Close();
                return;
            }

            try
            {
                string path = request.Url?.AbsolutePath.ToLower() ?? "";

                if (path == "/state" && request.HttpMethod == "GET")
                {
                    HandleGetState(response);
                }
                else if (path == "/screenshot" && request.HttpMethod == "GET")
                {
                    HandleGetScreenshot(request, response);
                }
                else if (path == "/action" && request.HttpMethod == "POST")
                {
                    HandlePostAction(request, response);
                }
                else
                {
                    SendResponse(response, HttpStatusCode.NotFound, "{\"error\": \"Not Found\"}", "application/json");
                }
            }
            catch (Exception ex)
            {
                SendResponse(response, HttpStatusCode.InternalServerError, $"{{\"error\": \"{ex.Message}\"}}", "application/json");
            }
        }

        private static void HandleGetState(HttpListenerResponse response)
        {
            if (_game == null)
            {
                SendResponse(response, HttpStatusCode.InternalServerError, "{\"error\": \"Game not initialized\"}", "application/json");
                return;
            }

            var player = _game.Player;
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"position\": {{\"x\": {player.Position.X}, \"y\": {player.Position.Y}, \"z\": {player.Position.Z}}},");
            sb.Append($"\"velocity\": {{\"x\": {player.Velocity.X}, \"y\": {player.Velocity.Y}, \"z\": {player.Velocity.Z}}},");
            sb.Append($"\"yaw\": {player.Yaw},");
            sb.Append($"\"pitch\": {player.Pitch},");
            sb.Append($"\"flyingMode\": {player.FlyingMode.ToString().ToLower()},");
            sb.Append($"\"isGrounded\": {player.IsGrounded.ToString().ToLower()},");
            sb.Append($"\"health\": {player.Health},");
            sb.Append($"\"maxHealth\": {player.MaxHealth},");
            sb.Append($"\"timeOfDay\": {_game.TimeOfDay},");
            sb.Append($"\"timeScale\": {_game.TimeScale},");
            sb.Append($"\"timePaused\": {_game.TimePaused.ToString().ToLower()},");
            sb.Append($"\"selectedSlot\": {player.SelectedSlot},");
            sb.Append("\"hotbar\": [");
            for (int i = 0; i < 9; i++)
            {
                var slot = player.Hotbar[i];
                sb.Append($"{{\"slot\": {i + 1}, \"type\": \"{slot.Type}\", \"count\": {slot.Count}}}");
                if (i < 8) sb.Append(",");
            }
            sb.Append("]");
            sb.Append(",\"animals\": [");

            var nearbyAnimals = _game.Animals.GetAnimalsInRange(player.Position, 32f);
            for (int i = 0; i < nearbyAnimals.Count; i++)
            {
                var animal = nearbyAnimals[i];
                sb.Append("{");
                sb.Append($"\"id\": {animal.Id},");
                sb.Append($"\"type\": \"{animal.Type}\",");
                sb.Append($"\"health\": {animal.Health},");
                sb.Append($"\"maxHealth\": {animal.MaxHealth},");
                sb.Append($"\"x\": {animal.Position.X},");
                sb.Append($"\"y\": {animal.Position.Y},");
                sb.Append($"\"z\": {animal.Position.Z}");
                sb.Append("}");
                if (i < nearbyAnimals.Count - 1) sb.Append(",");
            }

            sb.Append("]");
            sb.Append("}");

            SendResponse(response, HttpStatusCode.OK, sb.ToString(), "application/json");
        }

        private static void HandleGetScreenshot(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (_game == null)
            {
                SendResponse(response, HttpStatusCode.InternalServerError, "{\"error\": \"Game not initialized\"}", "application/json");
                return;
            }

            // Optional custom path
            string? customPath = request.QueryString["path"];
            string screenshotPath = customPath ?? Path.Combine(AppContext.BaseDirectory, "screenshot.png");

            var tcs = new TaskCompletionSource<byte[]>();

            _game.PendingActions.Enqueue(() =>
            {
                try
                {
                    _game.SaveScreenshot(screenshotPath);
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
                // Wait for the screenshot operation on the main thread (timeout 5s)
                if (tcs.Task.Wait(5000))
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
                    SendResponse(response, HttpStatusCode.RequestTimeout, "{\"error\": \"Screenshot capture timed out\"}", "application/json");
                }
            }
            catch (Exception ex)
            {
                SendResponse(response, HttpStatusCode.InternalServerError, $"{{\"error\": \"{ex.InnerException?.Message ?? ex.Message}\"}}", "application/json");
            }
        }

        private static void HandlePostAction(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (_game == null)
            {
                SendResponse(response, HttpStatusCode.InternalServerError, "{\"error\": \"Game not initialized\"}", "application/json");
                return;
            }

            string? cmd = request.QueryString["cmd"];
            if (string.IsNullOrEmpty(cmd))
            {
                SendResponse(response, HttpStatusCode.BadRequest, "{\"error\": \"Missing 'cmd' parameter\"}", "application/json");
                return;
            }

            bool success = true;
            string message = "Action queued";

            switch (cmd.ToLower())
            {
                case "key_down":
                    string? keyStrDown = request.QueryString["key"];
                    if (TryParseKey(keyStrDown, out var keyValDown))
                    {
                        _game.SimulatedKeys.Add(keyValDown);
                        message = $"Key {keyValDown} pressed";
                    }
                    else
                    {
                        success = false;
                        message = $"Invalid key: {keyStrDown}";
                    }
                    break;

                case "key_up":
                    string? keyStrUp = request.QueryString["key"];
                    if (TryParseKey(keyStrUp, out var keyValUp))
                    {
                        _game.SimulatedKeys.Remove(keyValUp);
                        message = $"Key {keyValUp} released";
                    }
                    else
                    {
                        success = false;
                        message = $"Invalid key: {keyStrUp}";
                    }
                    break;

                case "click":
                    string? btnStr = request.QueryString["button"];
                    if (btnStr?.ToLower() == "left")
                    {
                        _game.SimulateClick(MouseButton.Left);
                        message = "Left click simulated";
                    }
                    else if (btnStr?.ToLower() == "right")
                    {
                        _game.SimulateClick(MouseButton.Right);
                        message = "Right click simulated";
                    }
                    else
                    {
                        success = false;
                        message = "Invalid or missing 'button' parameter (must be 'left' or 'right')";
                    }
                    break;

                case "set_look":
                    string? yawStr = request.QueryString["yaw"];
                    string? pitchStr = request.QueryString["pitch"];
                    if (float.TryParse(yawStr, out float yaw) && float.TryParse(pitchStr, out float pitch))
                    {
                        _game.PendingActions.Enqueue(() =>
                        {
                            _game.Player.Yaw = yaw;
                            _game.Player.Pitch = Math.Clamp(pitch, -89f, 89f);
                        });
                        message = $"Look direction set to yaw={yaw}, pitch={pitch}";
                    }
                    else
                    {
                        success = false;
                        message = "Invalid or missing 'yaw' or 'pitch' parameters";
                    }
                    break;

                case "look":
                    string? dxStr = request.QueryString["dx"];
                    string? dyStr = request.QueryString["dy"];
                    if (float.TryParse(dxStr, out float dx) && float.TryParse(dyStr, out float dy))
                    {
                        _game.PendingActions.Enqueue(() =>
                        {
                            _game.Player.Yaw += dx * 0.15f;
                            _game.Player.Pitch = Math.Clamp(_game.Player.Pitch - dy * 0.15f, -89f, 89f);
                        });
                        message = $"Rotated yaw by {dx}, pitch by {-dy}";
                    }
                    else
                    {
                        success = false;
                        message = "Invalid or missing 'dx' or 'dy' parameters";
                    }
                    break;

                case "teleport":
                    string? xStr = request.QueryString["x"];
                    string? yStr = request.QueryString["y"];
                    string? zStr = request.QueryString["z"];
                    if (float.TryParse(xStr, out float tx) && float.TryParse(yStr, out float ty) && float.TryParse(zStr, out float tz))
                    {
                        _game.PendingActions.Enqueue(() =>
                        {
                            _game.Player.Position = new System.Numerics.Vector3(tx, ty, tz);
                            _game.Player.Velocity = System.Numerics.Vector3.Zero;
                        });
                        message = $"Teleported player to ({tx}, {ty}, {tz})";
                    }
                    else
                    {
                        success = false;
                        message = "Invalid or missing 'x', 'y', or 'z' parameters";
                    }
                    break;

                case "set_flying":
                    string? flyStr = request.QueryString["flying"];
                    if (bool.TryParse(flyStr, out bool flying))
                    {
                        _game.PendingActions.Enqueue(() =>
                        {
                            _game.Player.FlyingMode = flying;
                            _game.Player.Velocity = System.Numerics.Vector3.Zero;
                        });
                        message = $"Set flying mode to {flying}";
                    }
                    else
                    {
                        success = false;
                        message = "Invalid or missing 'flying' parameter (must be true or false)";
                    }
                    break;

                case "select_slot":
                    string? slotStr = request.QueryString["slot"];
                    if (int.TryParse(slotStr, out int slot) && slot >= 0 && slot < 9)
                    {
                        _game.PendingActions.Enqueue(() =>
                        {
                            _game.Player.SelectedSlot = slot;
                        });
                        message = $"Selected inventory slot {slot + 1}";
                    }
                    else
                    {
                        success = false;
                        message = "Invalid or missing 'slot' parameter (must be 0-8)";
                    }
                    break;

                case "shutdown":
                    _game.PendingActions.Enqueue(() =>
                    {
                        _game.Exit();
                    });
                    message = "Shutdown command received";
                    break;

                case "set_time":
                    string? timeStr = request.QueryString["value"];
                    if (float.TryParse(timeStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float timeVal))
                    {
                        _game.PendingActions.Enqueue(() => _game.SetTimeOfDay(timeVal));
                        message = $"Time set to {timeVal}";
                    }
                    else
                    {
                        success = false;
                        message = "Invalid or missing 'value' parameter (0-1)";
                    }
                    break;

                case "set_time_scale":
                    string? scaleStr = request.QueryString["value"];
                    if (float.TryParse(scaleStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float scaleVal))
                    {
                        _game.PendingActions.Enqueue(() =>
                        {
                            _game.TimeScale = Math.Max(0f, scaleVal);
                            _game.TimePaused = scaleVal <= 0f;
                        });
                        message = $"Time scale set to {scaleVal}";
                    }
                    else
                    {
                        success = false;
                        message = "Invalid or missing 'value' parameter";
                    }
                    break;

                case "dev":
                    string? devCmd = request.QueryString["cmd_line"];
                    if (string.IsNullOrWhiteSpace(devCmd))
                    {
                        success = false;
                        message = "Missing 'cmd_line' parameter";
                    }
                    else
                    {
                        var tcs = new TaskCompletionSource<string>();
                        _game.PendingActions.Enqueue(() =>
                        {
                            tcs.SetResult(_game.ExecuteDevCommand(devCmd));
                        });

                        if (tcs.Task.Wait(2000))
                        {
                            message = tcs.Task.Result;
                            if (string.IsNullOrEmpty(message)) message = "OK";
                        }
                        else
                        {
                            success = false;
                            message = "Dev command timed out";
                        }
                    }
                    break;

                default:
                    success = false;
                    message = $"Unknown action cmd: {cmd}";
                    break;
            }

            var statusCode = success ? HttpStatusCode.OK : HttpStatusCode.BadRequest;
            string resJson = $"{{\"success\": {success.ToString().ToLower()}, \"message\": \"{message}\"}}";
            SendResponse(response, statusCode, resJson, "application/json");
        }

        private static bool TryParseKey(string? keyStr, out Key key)
        {
            key = default;
            if (string.IsNullOrEmpty(keyStr)) return false;

            // Handle common abbreviations
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

        private static void SendResponse(HttpListenerResponse response, HttpStatusCode statusCode, string content, string contentType)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                response.ContentType = contentType;
                response.ContentLength64 = bytes.Length;
                response.StatusCode = (int)statusCode;
                response.OutputStream.Write(bytes, 0, bytes.Length);
                response.OutputStream.Close();
            }
            catch {}
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
            catch {}
            _listener = null;
            Console.WriteLine("[Agent HTTP Server] Stopped.");
        }
    }
}

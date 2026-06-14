using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Autonocraft.Items;
using Autonocraft.World;
using Autonocraft.Ai;
using Autonocraft.Domain.Village;

namespace Autonocraft.Core
{
    public static class AgentHttpServer
    {
        private static HttpListener? _listener;
        private static IGameAgentBridge? _bridge;
        private static bool _isRunning = false;

        public static void Start(IGameAgentBridge bridge, int port = 5000)
        {
            if (_isRunning) return;

            _bridge = bridge;
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

                if (path == "/health" && request.HttpMethod == "GET")
                {
                    HandleGetHealth(response);
                }
                else if (path == "/metrics" && request.HttpMethod == "GET")
                {
                    HandleGetMetrics(response);
                }
                else if (path == "/state" && request.HttpMethod == "GET")
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
                else if (path == "/village/chat" && request.HttpMethod == "POST")
                {
                    HandlePostVillageChat(request, response);
                }
                else if (path == "/village/chat/confirm" && request.HttpMethod == "POST")
                {
                    HandlePostVillageChatConfirm(request, response);
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

        private static void HandleGetHealth(HttpListenerResponse response)
        {
            if (_bridge == null)
            {
                SendResponse(response, HttpStatusCode.ServiceUnavailable,
                    "{\"ready\": false, \"gameState\": \"Unknown\"}", "application/json");
                return;
            }

            var state = _bridge.CurrentGameState;
            bool ready = state == GameState.Playing;
            var statusCode = ready ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
            SendResponse(response, statusCode,
                $"{{\"ready\": {ready.ToString().ToLower()}, \"gameState\": \"{state}\"}}",
                "application/json");
        }

        private static void HandleGetMetrics(HttpListenerResponse response)
        {
            SendResponse(response, HttpStatusCode.OK, RuntimeMetrics.ToJson(), "application/json");
        }

        private static void HandleGetState(HttpListenerResponse response)
        {
            if (_bridge == null)
            {
                SendResponse(response, HttpStatusCode.InternalServerError, "{\"error\": \"Game not initialized\"}", "application/json");
                return;
            }

            if (_bridge.CurrentGameState != GameState.Playing)
            {
                SendResponse(response, HttpStatusCode.ServiceUnavailable,
                    $"{{\"error\": \"Game not in Playing state\", \"gameState\": \"{_bridge.CurrentGameState}\"}}",
                    "application/json");
                return;
            }

            var host = _bridge.Host;
            var session = host.Session;
            var player = session.Player;
            var interaction = session.BlockInteraction;
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"gameState\": \"{_bridge.CurrentGameState}\",");
            sb.Append($"\"worldSeed\": {session.Grid.Seed},");
            sb.Append($"\"oxygen\": {player.Oxygen},");
            sb.Append($"\"position\": {{\"x\": {player.Position.X}, \"y\": {player.Position.Y}, \"z\": {player.Position.Z}}},");
            sb.Append($"\"velocity\": {{\"x\": {player.Velocity.X}, \"y\": {player.Velocity.Y}, \"z\": {player.Velocity.Z}}},");
            sb.Append($"\"yaw\": {player.Yaw},");
            sb.Append($"\"pitch\": {player.Pitch},");
            sb.Append($"\"flyingMode\": {player.FlyingMode.ToString().ToLower()},");
            sb.Append($"\"isGrounded\": {player.IsGrounded.ToString().ToLower()},");
            sb.Append($"\"health\": {player.Health},");
            sb.Append($"\"maxHealth\": {player.MaxHealth},");
            sb.Append($"\"timeOfDay\": {host.TimeOfDay},");
            sb.Append($"\"timeScale\": {host.TimeScale},");
            sb.Append($"\"timePaused\": {host.TimePaused.ToString().ToLower()},");
            sb.Append($"\"selectedSlot\": {player.SelectedSlot},");
            sb.Append("\"hotbar\": [");
            for (int i = 0; i < 9; i++)
            {
                var slot = player.Hotbar[i];
                if (slot.IsEmpty)
                {
                    sb.Append($"{{\"slot\": {i}, \"kind\": \"empty\"}}");
                }
                else if (slot.IsTool())
                {
                    sb.Append($"{{\"slot\": {i}, \"kind\": \"tool\", \"toolId\": \"{slot.ToolId}\", \"name\": \"{slot.GetDisplayName()}\", \"durability\": {slot.Durability}, \"maxDurability\": {slot.MaxDurability}}}");
                }
                else
                {
                    sb.Append($"{{\"slot\": {i}, \"kind\": \"block\", \"type\": \"{slot.BlockType}\", \"count\": {slot.Count}}}");
                }

                if (i < 8) sb.Append(",");
            }
            sb.Append("]");
            sb.Append($",\"skills\": {{\"mining\": {{\"level\": {player.Skills.Mining.Level}, \"xp\": {player.Skills.Mining.Xp}}}, \"woodcutting\": {{\"level\": {player.Skills.Woodcutting.Level}, \"xp\": {player.Skills.Woodcutting.Xp}}}, \"combat\": {{\"level\": {player.Skills.Combat.Level}, \"xp\": {player.Skills.Combat.Xp}}}}}");
            sb.Append(",\"animals\": [");

            var nearbyAnimals = session.Animals.GetAnimalsInRange(player.Position, 32f);
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

            sb.Append(",\"targetBlock\": ");
            if (interaction.TargetBlockPos.HasValue && interaction.TargetBlockType != BlockType.Air)
            {
                var tpos = interaction.TargetBlockPos.Value;
                sb.Append("{");
                sb.Append($"\"x\": {tpos.X}, \"y\": {tpos.Y}, \"z\": {tpos.Z},");
                sb.Append($"\"type\": \"{interaction.TargetBlockType}\",");
                sb.Append($"\"breakProgress\": {interaction.BreakProgress},");
                sb.Append($"\"isMining\": {interaction.IsMining.ToString().ToLower()}");
                sb.Append("}");
            }
            else
            {
                sb.Append("null");
            }

            string? nearbyStation = null;
            var target = session.BlockInteraction.TargetBlockType;
            if (target.IsStation())
            {
                nearbyStation = target switch
                {
                    BlockType.StationForge => "Forge",
                    BlockType.StationCrucible => "Crucible",
                    BlockType.StationBench => "Bench",
                    _ => target.ToString()
                };
            }

            sb.Append(",\"nearbyStation\": ");
            sb.Append(nearbyStation == null ? "null" : $"\"{nearbyStation}\"");
            sb.Append(",\"unlockedRecipes\": [");
            var unlocked = session.Crafting.Journal.Export();
            for (int i = 0; i < unlocked.Count; i++)
            {
                sb.Append($"\"{unlocked[i]}\"");
                if (i < unlocked.Count - 1) sb.Append(",");
            }
            sb.Append("]");

            var village = session.Villages.GetPrimaryVillage();
            var settings = host.Settings;
            sb.Append(",\"playWithAi\": ");
            sb.Append(settings.PlayWithAi.ToString().ToLower());
            sb.Append(",\"aiProvider\": \"");
            sb.Append(settings.AiProvider);
            sb.Append("\",\"llmAvailable\": ");
            sb.Append(LlmClientFactory.IsAvailable(settings).ToString().ToLower());
            sb.Append(",\"village\": ");
            if (village == null)
            {
                sb.Append("null");
            }
            else
            {
                sb.Append("{");
                sb.Append($"\"id\": {village.Id},");
                sb.Append($"\"name\": \"{village.Name}\",");
                sb.Append($"\"population\": {village.Population},");
                sb.Append($"\"populationCap\": {village.PopulationCap},");
                sb.Append($"\"tier\": \"{village.Tier}\",");
                sb.Append($"\"happiness\": {village.Happiness:F2},");
                sb.Append($"\"foodStock\": {village.FoodStock:F1}");
                sb.Append("}");
            }

            sb.Append(",\"villagers\": [");
            var nearbyVillagers = session.Villagers.GetVillagersInRange(player.Position, 32f);
            for (int i = 0; i < nearbyVillagers.Count; i++)
            {
                var v = nearbyVillagers[i];
                sb.Append("{");
                sb.Append($"\"id\": {v.Id},");
                sb.Append($"\"name\": \"{v.Name}\",");
                sb.Append($"\"role\": \"{v.Role}\",");
                sb.Append($"\"job\": \"{v.CurrentJob}\",");
                sb.Append($"\"x\": {v.Position.X},");
                sb.Append($"\"y\": {v.Position.Y},");
                sb.Append($"\"z\": {v.Position.Z}");
                sb.Append("}");
                if (i < nearbyVillagers.Count - 1) sb.Append(",");
            }
            sb.Append("]");

            var chatTarget = session.Villagers.GetNearest(player.Position, 5f);
            sb.Append(",\"nearbyVillagerForChat\": ");
            if (chatTarget == null)
            {
                sb.Append("null");
            }
            else
            {
                sb.Append($"{{\"id\": {chatTarget.Id}, \"name\": \"{chatTarget.Name}\"}}");
            }

            sb.Append("}");

            SendResponse(response, HttpStatusCode.OK, sb.ToString(), "application/json");
        }

        private static void HandleGetScreenshot(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (_bridge == null)
            {
                SendResponse(response, HttpStatusCode.InternalServerError, "{\"error\": \"Game not initialized\"}", "application/json");
                return;
            }

            // Optional custom path
            string? customPath = request.QueryString["path"];
            string screenshotPath = customPath ?? Path.Combine(AppContext.BaseDirectory, "screenshot.png");

            var tcs = new TaskCompletionSource<byte[]>();

            _bridge.PendingActions.Enqueue(() =>
            {
                try
                {
                    _bridge.SaveScreenshot(screenshotPath);
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
            if (_bridge == null)
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
                        _bridge.SimulatedKeys.Add(keyValDown);
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
                        _bridge.SimulatedKeys.Remove(keyValUp);
                        message = $"Key {keyValUp} released";
                    }
                    else
                    {
                        success = false;
                        message = $"Invalid key: {keyStrUp}";
                    }
                    break;

                case "release_keys":
                    _bridge.ReleaseSimulatedKeys();
                    message = "All simulated keys released";
                    break;

                case "click":
                    string? btnStr = request.QueryString["button"];
                    if (btnStr?.ToLower() == "left")
                    {
                        _bridge.SimulateClick(MouseButton.Left);
                        message = "Left click simulated";
                    }
                    else if (btnStr?.ToLower() == "right")
                    {
                        _bridge.SimulateClick(MouseButton.Right);
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
                        _bridge.PendingActions.Enqueue(() =>
                        {
                            var p = _bridge.Host.Session.Player;
                            p.Yaw = yaw;
                            p.Pitch = Math.Clamp(pitch, -89f, 89f);
                            _bridge.SyncCameraFromPlayer();
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
                        _bridge.PendingActions.Enqueue(() =>
                        {
                            var p = _bridge.Host.Session.Player;
                            p.Yaw += dx * 0.15f;
                            p.Pitch = Math.Clamp(p.Pitch - dy * 0.15f, -89f, 89f);
                            _bridge.SyncCameraFromPlayer();
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
                        _bridge.PendingActions.Enqueue(() =>
                        {
                            var p = _bridge.Host.Session.Player;
                            p.Position = new System.Numerics.Vector3(tx, ty, tz);
                            p.Velocity = System.Numerics.Vector3.Zero;
                            _bridge.SyncCameraFromPlayer();
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
                        _bridge.PendingActions.Enqueue(() =>
                        {
                            var p = _bridge.Host.Session.Player;
                            p.FlyingMode = flying;
                            p.Velocity = System.Numerics.Vector3.Zero;
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
                        _bridge.PendingActions.Enqueue(() =>
                        {
                            _bridge.Host.Session.Player.SelectedSlot = slot;
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
                        _bridge.PendingActions.Enqueue(() => _bridge.RequestExit());
                    message = "Shutdown command received";
                    break;

                case "set_time":
                    string? timeStr = request.QueryString["value"];
                    if (float.TryParse(timeStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float timeVal))
                    {
                        _bridge.PendingActions.Enqueue(() => _bridge.SetTimeOfDay(timeVal));
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
                        _bridge.PendingActions.Enqueue(() =>
                        {
                            _bridge.Host.TimeScale = Math.Max(0f, scaleVal);
                            _bridge.Host.TimePaused = scaleVal <= 0f;
                            _bridge.SyncTimeFromHost();
                        });
                        message = $"Time scale set to {scaleVal}";
                    }
                    else
                    {
                        success = false;
                        message = "Invalid or missing 'value' parameter";
                    }
                    break;

                case "open_crucible":
                {
                    var openTcs = new TaskCompletionSource<bool>();
                    _bridge.PendingActions.Enqueue(() =>
                    {
                        var interaction = _bridge.Host.Session.BlockInteraction;
                        if (interaction.TargetBlockPos.HasValue && interaction.TargetBlockType.IsStation())
                        {
                            var pos = interaction.TargetBlockPos.Value;
                            _bridge.OpenCrucibleAt(
                                (int)pos.X,
                                (int)pos.Y,
                                (int)pos.Z,
                                interaction.TargetBlockType);
                            openTcs.SetResult(true);
                        }
                        else
                        {
                            openTcs.SetResult(false);
                        }
                    });

                    if (openTcs.Task.Wait(2000) && openTcs.Task.Result)
                    {
                        message = "Station UI opened for targeted station";
                    }
                    else
                    {
                        success = false;
                        message = "No crafting station targeted";
                    }
                    break;
                }

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
                        _bridge.PendingActions.Enqueue(() =>
                        {
                            tcs.SetResult(_bridge.ExecuteDevCommand(devCmd));
                            _bridge.SyncTimeFromHost();
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

                case "recruit_villager":
                {
                    var recruitTcs = new TaskCompletionSource<bool>();
                    _bridge.PendingActions.Enqueue(() =>
                    {
                        var v = _bridge.Host.Session.Villages.GetPrimaryVillage();
                        recruitTcs.SetResult(v != null && _bridge.Host.Session.Villages.TryRecruit(v));
                    });
                    success = recruitTcs.Task.Wait(2000) && recruitTcs.Task.Result;
                    message = success ? "Recruited villager" : "Recruit failed";
                    break;
                }

                case "assign_job":
                {
                    if (!int.TryParse(request.QueryString["villager_id"], out int vid) ||
                        !Enum.TryParse<JobType>(request.QueryString["job"], true, out var jobType))
                    {
                        success = false;
                        message = "Need villager_id and job params";
                        break;
                    }

                    float? tgx = float.TryParse(request.QueryString["target_x"], out float tfx) ? tfx : null;
                    float? tgy = float.TryParse(request.QueryString["target_y"], out float tfy) ? tfy : null;
                    float? tgz = float.TryParse(request.QueryString["target_z"], out float tfz) ? tfz : null;
                    System.Numerics.Vector3? target = tgx.HasValue && tgy.HasValue && tgz.HasValue
                        ? new System.Numerics.Vector3(tgx.Value, tgy.Value, tgz.Value)
                        : null;

                    var assignTcs = new TaskCompletionSource<bool>();
                    _bridge.PendingActions.Enqueue(() =>
                    {
                        var session = _bridge.Host.Session;
                        var village = session.Villages.GetPrimaryVillage();
                        if (village == null || !session.Villagers.TryGet(vid, out var villager))
                        {
                            assignTcs.SetResult(false);
                            return;
                        }

                        assignTcs.SetResult(session.Villages.TryAssignJob(village, villager, jobType, target));
                    });
                    success = assignTcs.Task.Wait(2000) && assignTcs.Task.Result;
                    message = success ? $"Assigned {jobType}" : "Assign failed";
                    break;
                }

                case "open_village":
                    message = "Use in-game V key or POST /village/chat for steward";
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

        private static void HandlePostVillageChat(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (_bridge == null || _bridge.CurrentGameState != GameState.Playing)
            {
                SendResponse(response, HttpStatusCode.ServiceUnavailable, "{\"error\": \"Game not ready\"}", "application/json");
                return;
            }

            var settings = _bridge.Host.Settings;
            if (!settings.PlayWithAi || settings.AiProvider == AiProviderKind.Disabled)
            {
                SendResponse(response, HttpStatusCode.BadRequest, "{\"error\": \"Village AI is disabled in settings\"}", "application/json");
                return;
            }

            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                body = reader.ReadToEnd();
            }

            string message = ExtractJsonString(body, "message") ?? request.QueryString["message"] ?? string.Empty;
            string target = ExtractJsonString(body, "target") ?? request.QueryString["target"] ?? "mayor";
            var orchestrator = new VillageAiOrchestrator(settings: settings);
            var chatTcs = new TaskCompletionSource<string>();

            _bridge.PendingActions.Enqueue(() =>
            {
                try
                {
                    string reply = orchestrator.HandleChatAsync(message, target, _bridge.Host.Session).GetAwaiter().GetResult();
                    chatTcs.SetResult(reply);
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
                    SendResponse(response, HttpStatusCode.RequestTimeout, "{\"error\": \"Chat timed out\"}", "application/json");
                    return;
                }

                string reply = chatTcs.Task.Result.Replace("\"", "\\\"");
                SendResponse(response, HttpStatusCode.OK, $"{{\"reply\": \"{reply}\", \"actions\": []}}", "application/json");
            }
            catch (Exception ex)
            {
                SendResponse(response, HttpStatusCode.InternalServerError, $"{{\"error\": \"{ex.Message}\"}}", "application/json");
            }
        }

        private static void HandlePostVillageChatConfirm(HttpListenerRequest request, HttpListenerResponse response)
        {
            SendResponse(response, HttpStatusCode.OK, "{\"success\": true, \"message\": \"Confirmation queued\"}", "application/json");
        }

        private static string? ExtractJsonString(string json, string key)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            string pattern = $"\"{key}\"";
            int idx = json.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return null;
            }

            int start = json.IndexOf('"', idx + pattern.Length);
            if (start < 0)
            {
                return null;
            }

            start++;
            int end = json.IndexOf('"', start);
            return end > start ? json.Substring(start, end - start) : null;
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

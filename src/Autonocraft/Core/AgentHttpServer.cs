using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Autonocraft.Items;
using Autonocraft.World;
using Autonocraft.Ai;
using Autonocraft.Domain.Village;
using Autonocraft.Village;

namespace Autonocraft.Core
{
    public static class AgentHttpServer
    {
        private static HttpListener? _listener;
        private static IGameAgentBridge? _bridge;
        private static bool _isRunning = false;
        private const int QueuedActionWaitMs = 10000;

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

            // IPv6 literal [::1] prefixes break HttpListener.Start on Linux (managed parser bug).
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
                    _ = Task.Run(() => HandleRequest(context)); // Handle request concurrently
                }
                catch (HttpListenerException)
                {
                    // Listener stopped
                    break;
                }
                catch (ObjectDisposedException)
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
            catch { }

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
                else if (path == "/village/debug" && request.HttpMethod == "GET")
                {
                    HandleGetVillageDebug(response);
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
                SendJsonResponse(response, HttpStatusCode.InternalServerError, new { error = ex.Message });
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
            sb.Append($"\"creativeMode\": {player.CreativeMode.ToString().ToLower()},");
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
                else if (slot.IsFluidContainer())
                {
                    sb.Append($"{{\"slot\": {i}, \"kind\": \"fluid_container\", \"itemId\": \"{slot.ToolId}\", \"name\": \"{slot.GetDisplayName()}\", \"filled\": {slot.IsWaterBucket().ToString().ToLower()}}}");
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
            int writtenAnimals = 0;
            for (int i = 0; i < nearbyAnimals.Count; i++)
            {
                var animal = nearbyAnimals[i];
                if (!animal.IsAlive)
                {
                    continue;
                }

                if (writtenAnimals > 0) sb.Append(",");
                sb.Append("{");
                sb.Append($"\"id\": {animal.Id},");
                sb.Append($"\"type\": \"{animal.Type}\",");
                sb.Append($"\"health\": {animal.Health},");
                sb.Append($"\"maxHealth\": {animal.MaxHealth},");
                sb.Append($"\"x\": {animal.Position.X},");
                sb.Append($"\"y\": {animal.Position.Y},");
                sb.Append($"\"z\": {animal.Position.Z}");
                sb.Append("}");
                writtenAnimals++;
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

            var village = session.Villages.GetActiveVillage(player.Position);
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
                session.Villages.SyncCitizensForVillage(village);
                int livePopulation = VillageSettlementHealth.GetLivePopulation(village, session.Villagers);
                sb.Append("{");
                sb.Append($"\"id\": {village.Id},");
                sb.Append($"\"name\": \"{village.Name}\",");
                sb.Append($"\"population\": {livePopulation},");
                sb.Append($"\"populationCap\": {village.PopulationCap},");
                sb.Append($"\"tier\": \"{village.Tier}\",");
                sb.Append($"\"happiness\": {village.Happiness:F2},");
                sb.Append($"\"foodStock\": {village.FoodStock:F1},");
                sb.Append($"\"anchorX\": {village.AnchorX},");
                sb.Append($"\"anchorZ\": {village.AnchorZ}");
                sb.Append("}");
            }

            sb.Append(",\"villagers\": [");
            List<Entities.Villager> nearbyVillagers = new();
            if (village != null)
            {
                foreach (var v in VillageSettlementHealth.EnumerateLiveCitizens(village, session.Villagers))
                {
                    nearbyVillagers.Add(v);
                }
            }
            else
            {
                nearbyVillagers = session.Villagers.GetVillagersInRange(player.Position, 32f);
            }

            for (int i = 0; i < nearbyVillagers.Count; i++)
            {
                var v = nearbyVillagers[i];
                sb.Append("{");
                sb.Append($"\"id\": {v.Id},");
                sb.Append($"\"villageId\": {v.VillageId},");
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

        private static void HandleGetVillageDebug(HttpListenerResponse response)
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

            var session = _bridge.Host.Session;
            var village = session.Villages.GetActiveVillage(session.Player.Position);
            if (village == null)
            {
                SendResponse(response, HttpStatusCode.NotFound, "{\"error\": \"No active village\"}", "application/json");
                return;
            }

            session.Villages.SyncCitizensForVillage(village);

            var villagers = new List<object>();
            foreach (var villager in VillageSettlementHealth.EnumerateLiveCitizens(village, session.Villagers))
            {
                villagers.Add(new
                {
                    id = villager.Id,
                    villageId = villager.VillageId,
                    name = villager.Name,
                    role = villager.Role.ToString(),
                    job = villager.CurrentJob.ToString(),
                    phase = villager.AiPhase.ToString(),
                    x = villager.Position.X,
                    y = villager.Position.Y,
                    z = villager.Position.Z,
                    target = villager.JobTarget.HasValue
                        ? new { x = villager.JobTarget.Value.X, y = villager.JobTarget.Value.Y, z = villager.JobTarget.Value.Z }
                        : null,
                    buildingSiteId = villager.AssignedBuildingSiteId,
                    buildingId = villager.AssignedBuildingId,
                    haulSourceVillagerId = villager.HaulSourceVillagerId,
                    haulSourceChestId = villager.HaulSourceChestId,
                    haulDelivering = villager.HaulIsDelivering,
                    breakProgress = villager.BreakProgress,
                    workTimer = villager.WorkTimer,
                    equippedTool = FormatDebugStack(villager.EquippedTool),
                    inventory = ExportInventory(villager.Inventory)
                });
            }

            var buildings = new List<object>();
            foreach (var building in village.Buildings)
            {
                buildings.Add(new
                {
                    id = building.Id,
                    blueprintId = building.BlueprintId,
                    kind = building.Kind.ToString(),
                    complete = building.IsComplete,
                    anchorX = building.AnchorX,
                    anchorY = building.AnchorY,
                    anchorZ = building.AnchorZ
                });
            }

            var sites = new List<object>();
            foreach (var site in village.BuildingSites)
            {
                sites.Add(new
                {
                    id = site.Id,
                    blueprintId = site.BlueprintId,
                    complete = site.IsComplete,
                    remaining = site.RemainingCount,
                    completion = site.CompletionRatio,
                    anchorX = site.AnchorX,
                    anchorY = site.AnchorY,
                    anchorZ = site.AnchorZ
                });
            }

            var payload = new
            {
                village = new
                {
                    id = village.Id,
                    name = village.Name,
                    anchorX = village.AnchorX,
                    anchorY = village.AnchorY,
                    anchorZ = village.AnchorZ,
                    radius = village.Radius,
                    population = VillageSettlementHealth.GetLivePopulation(village, session.Villagers),
                    registryPopulation = village.Population,
                    populationCap = village.PopulationCap,
                    housingCapacity = village.HousingCapacity,
                    tier = village.Tier.ToString(),
                    foodStock = village.FoodStock,
                    happiness = village.Happiness,
                    workQueue = village.WorkQueue.Count
                },
                storage = ExportInventory(village.Storage),
                buildings,
                buildingSites = sites,
                villagers
            };

            SendResponse(
                response,
                HttpStatusCode.OK,
                JsonSerializer.Serialize(payload),
                "application/json");
        }

        private static List<object> ExportInventory(IItemContainer inventory)
        {
            var slots = new List<object>();
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var stack = inventory.GetSlot(i);
                if (stack.IsEmpty)
                {
                    continue;
                }

                slots.Add(new
                {
                    slot = i,
                    stack = FormatDebugStack(stack)
                });
            }

            return slots;
        }

        private static object FormatDebugStack(ItemStack stack)
        {
            if (stack.IsEmpty)
            {
                return new { kind = "empty" };
            }

            if (stack.IsTool())
            {
                return new
                {
                    kind = "tool",
                    toolId = stack.ToolId.ToString(),
                    name = stack.GetDisplayName(),
                    durability = stack.Durability,
                    maxDurability = stack.MaxDurability
                };
            }

            if (stack.IsFluidContainer())
            {
                return new
                {
                    kind = "fluid_container",
                    itemId = stack.ToolId.ToString(),
                    name = stack.GetDisplayName(),
                    filled = stack.IsWaterBucket()
                };
            }

            return new
            {
                kind = "block",
                blockType = stack.BlockType.ToString(),
                count = stack.Count
            };
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
                // Chunk-heavy scenes can delay the main-thread screenshot action briefly.
                if (tcs.Task.Wait(10000))
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
                SendJsonResponse(response, HttpStatusCode.InternalServerError, new { error = ex.InnerException?.Message ?? ex.Message });
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
                            p.ForceAirborne();
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

                case "set_creative":
                case "set_flying":
                    string? creativeStr = request.QueryString["creative"] ?? request.QueryString["flying"];
                    if (bool.TryParse(creativeStr, out bool creative))
                    {
                        _bridge.PendingActions.Enqueue(() =>
                        {
                            var p = _bridge.Host.Session.Player;
                            p.CreativeMode = creative;
                            p.Velocity = System.Numerics.Vector3.Zero;
                            if (!creative)
                            {
                                p.ForceAirborne();
                            }
                        });
                        message = $"Set creative mode to {creative}";
                    }
                    else
                    {
                        success = false;
                        message = "Invalid or missing 'creative' parameter (must be true or false)";
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

                        if (openTcs.Task.Wait(QueuedActionWaitMs) && openTcs.Task.Result)
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

                        if (tcs.Task.Wait(QueuedActionWaitMs))
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
                            var v = _bridge.Host.Session.Villages.GetActiveVillage(_bridge.Host.Session.Player.Position);
                            recruitTcs.SetResult(v != null && _bridge.Host.Session.Villages.TryRecruit(v, _bridge.Host.Session.Grid));
                        });
                        success = recruitTcs.Task.Wait(QueuedActionWaitMs) && recruitTcs.Task.Result;
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
                            var village = session.Villages.GetActiveVillage(session.Player.Position);
                            if (village == null || !session.Villagers.TryGet(vid, out var villager))
                            {
                                assignTcs.SetResult(false);
                                return;
                            }

                            assignTcs.SetResult(session.Villages.TryAssignJob(village, villager, jobType, target));
                        });
                        success = assignTcs.Task.Wait(QueuedActionWaitMs) && assignTcs.Task.Result;
                        message = success ? $"Assigned {jobType}" : "Assign failed";
                        break;
                    }

                case "queue_build":
                    {
                        string? blueprintId = request.QueryString["blueprint_id"];
                        if (string.IsNullOrWhiteSpace(blueprintId) ||
                            !int.TryParse(request.QueryString["anchor_x"], out int anchorX) ||
                            !int.TryParse(request.QueryString["anchor_z"], out int anchorZ))
                        {
                            success = false;
                            message = "Need blueprint_id, anchor_x, and anchor_z params";
                            break;
                        }

                        int? anchorY = int.TryParse(request.QueryString["anchor_y"], out int parsedAnchorY)
                            ? parsedAnchorY
                            : null;
                        var queueTcs = new TaskCompletionSource<bool>();
                        _bridge.PendingActions.Enqueue(() =>
                        {
                            var session = _bridge.Host.Session;
                            var village = session.Villages.GetActiveVillage(session.Player.Position);
                            if (village == null)
                            {
                                queueTcs.SetResult(false);
                                return;
                            }

                            session.Villages.CreativeMode = session.Player.CreativeMode;
                            queueTcs.SetResult(session.Villages.TryQueueBlueprint(
                                session.Grid,
                                village,
                                blueprintId,
                                anchorX,
                                anchorZ,
                                village.Storage,
                                anchorY ?? -1));
                        });
                        success = queueTcs.Task.Wait(QueuedActionWaitMs) && queueTcs.Task.Result;
                        message = success ? $"Queued {blueprintId}" : $"Queue build failed for {blueprintId}";
                        break;
                    }

                case "open_village":
                    {
                        var openTcs = new TaskCompletionSource<bool>();
                        _bridge.PendingActions.Enqueue(() =>
                        {
                            _bridge.RequestOpenVillageUi();
                            openTcs.SetResult(true);
                        });
                        success = openTcs.Task.Wait(QueuedActionWaitMs) && openTcs.Task.Result;
                        message = success ? "Village UI opened" : "Failed to open village UI";
                        break;
                    }

                case "close_village":
                case "close_village_ui":
                    {
                        var closeTcs = new TaskCompletionSource<bool>();
                        _bridge.PendingActions.Enqueue(() =>
                        {
                            _bridge.RequestCloseVillageUi();
                            closeTcs.SetResult(true);
                        });
                        success = closeTcs.Task.Wait(QueuedActionWaitMs) && closeTcs.Task.Result;
                        message = success ? "Village UI closed" : "Failed to close village UI";
                        break;
                    }

                case "summon_settlers":
                    {
                        var repairTcs = new TaskCompletionSource<bool>();
                        _bridge.PendingActions.Enqueue(() =>
                        {
                            var session = _bridge.Host.Session;
                            var village = session.Villages.GetActiveVillage(session.Player.Position);
                            repairTcs.SetResult(village != null && session.Villages.RepairVillageCitizens(village, session.Grid));
                        });
                        success = repairTcs.Task.Wait(QueuedActionWaitMs) && repairTcs.Task.Result;
                        message = success ? "Settlers summoned" : "Summon failed (stand near Town Heart)";
                        break;
                    }

                default:
                    success = false;
                    message = $"Unknown action cmd: {cmd}";
                    break;
            }

            var statusCode = success ? HttpStatusCode.OK : HttpStatusCode.BadRequest;
            SendJsonResponse(response, statusCode, new { success, message });
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
            var chatTcs = new TaskCompletionSource<(string reply, List<string> actions)>();

            _bridge.PendingActions.Enqueue(() =>
            {
                try
                {
                    var orchestrator = _bridge.Host.Session.VillageAi;
                    string reply = orchestrator.HandleChatAsync(message, target, _bridge.Host.Session).GetAwaiter().GetResult();
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
                    SendResponse(response, HttpStatusCode.RequestTimeout, "{\"error\": \"Chat timed out\"}", "application/json");
                    return;
                }

                var (reply, actions) = chatTcs.Task.Result;
                SendJsonResponse(response, HttpStatusCode.OK, new { reply, actions });
            }
            catch (Exception ex)
            {
                SendJsonResponse(response, HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        private static void HandlePostVillageChatConfirm(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (_bridge == null || _bridge.CurrentGameState != GameState.Playing)
            {
                SendResponse(response, HttpStatusCode.ServiceUnavailable, "{\"error\": \"Game not ready\"}", "application/json");
                return;
            }

            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                body = reader.ReadToEnd();
            }

            bool confirmed = string.Equals(
                ExtractJsonString(body, "confirm") ?? request.QueryString["confirm"],
                "true",
                StringComparison.OrdinalIgnoreCase);
            string target = ExtractJsonString(body, "target") ?? request.QueryString["target"] ?? "mayor";
            var confirmTcs = new TaskCompletionSource<string>();

            _bridge.PendingActions.Enqueue(() =>
            {
                try
                {
                    string reply = _bridge.Host.Session.VillageAi
                        .ConfirmPendingAsync(_bridge.Host.Session, confirmed, target)
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
                    SendResponse(response, HttpStatusCode.RequestTimeout, "{\"error\": \"Confirm timed out\"}", "application/json");
                    return;
                }

                string reply = confirmTcs.Task.Result;
                SendJsonResponse(response, HttpStatusCode.OK, new { success = true, reply });
            }
            catch (Exception ex)
            {
                SendJsonResponse(response, HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
        }

        private static string SerializeActions(IReadOnlyList<string> actions)
        {
            if (actions.Count == 0)
            {
                return "[]";
            }

            var parts = new List<string>();
            foreach (var action in actions)
            {
                parts.Add($"\"{action.Replace("\"", "\\\"")}\"");
            }

            return $"[{string.Join(",", parts)}]";
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
            catch { }
        }

        private static void SendJsonResponse(HttpListenerResponse response, HttpStatusCode statusCode, object payload)
        {
            SendResponse(response, statusCode, JsonSerializer.Serialize(payload), "application/json");
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
            catch { }
            _listener = null;
            _bridge = null;
            Console.WriteLine("[Agent HTTP Server] Stopped.");
        }
    }
}

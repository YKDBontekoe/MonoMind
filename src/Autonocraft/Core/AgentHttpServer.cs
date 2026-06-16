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
using Autonocraft.Core.Agent;

namespace Autonocraft.Core
{
    public static class AgentHttpServer
    {
        private static HttpListener? _listener;
        private static IGameAgentBridge? _bridge;
        private static bool _isRunning = false;
        private static readonly Dictionary<string, IAgentAction> _actions = CreateActionRegistry();

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
                var dto = new AgentHealthDto(false, "Unknown");
                SendJsonResponse(response, HttpStatusCode.ServiceUnavailable, dto);
                return;
            }

            var state = _bridge.CurrentGameState;
            bool ready = state == GameState.Playing;
            var statusCode = ready ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
            var health = new AgentHealthDto(ready, state.ToString());
            SendJsonResponse(response, statusCode, health);
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
            var primaryVillage = session.Villages.GetActiveVillage(player.Position);
            string guidanceHint = EarlyGameGuide.GetGuidanceHint(player, primaryVillage, session.Villagers);

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

            var unlocked = session.Crafting.Journal.Export();

            var nearbyAnimals = session.Animals.GetAnimalsInRange(player.Position, 32f);
            List<AgentAnimalDto> animalDtos = new();
            for (int i = 0; i < nearbyAnimals.Count; i++)
            {
                var animal = nearbyAnimals[i];
                if (!animal.IsAlive)
                {
                    continue;
                }

                animalDtos.Add(new AgentAnimalDto(
                    animal.Id,
                    animal.Type.ToString(),
                    animal.Health,
                    animal.MaxHealth,
                    animal.Position.X,
                    animal.Position.Y,
                    animal.Position.Z));
            }

            AgentTargetBlockDto? targetBlockDto = null;
            if (interaction.TargetBlockPos.HasValue && interaction.TargetBlockType != BlockType.Air)
            {
                var tpos = interaction.TargetBlockPos.Value;
                targetBlockDto = new AgentTargetBlockDto(
                    (int)tpos.X,
                    (int)tpos.Y,
                    (int)tpos.Z,
                    interaction.TargetBlockType.ToString(),
                    interaction.BreakProgress,
                    interaction.IsMining);
            }

            var village = session.Villages.GetActiveVillage(player.Position);
            var settings = host.Settings;

            AgentVillageSummaryDto? villageDto = null;
            List<Entities.Villager> nearbyVillagers = new();
            if (village != null)
            {
                session.Villages.SyncCitizensForVillage(village);
                int livePopulation = VillageSettlementHealth.GetLivePopulation(village, session.Villagers);
                villageDto = new AgentVillageSummaryDto(
                    village.Id,
                    village.Name,
                    livePopulation,
                    village.PopulationCap,
                    village.Tier.ToString(),
                    (float)Math.Round(village.Happiness, 2),
                    (float)Math.Round(village.FoodStock, 1),
                    village.AnchorX,
                    village.AnchorZ);

                foreach (var v in VillageSettlementHealth.EnumerateLiveCitizens(village, session.Villagers))
                {
                    nearbyVillagers.Add(v);
                }
            }
            else
            {
                nearbyVillagers = session.Villagers.GetVillagersInRange(player.Position, 32f);
            }

            List<AgentVillagerDto> villagerDtos = new();
            for (int i = 0; i < nearbyVillagers.Count; i++)
            {
                var v = nearbyVillagers[i];
                villagerDtos.Add(new AgentVillagerDto(
                    v.Id,
                    v.VillageId,
                    v.Name,
                    v.Role.ToString(),
                    v.CurrentJob.ToString(),
                    v.Position.X,
                    v.Position.Y,
                    v.Position.Z));
            }

            var chatTarget = session.Villagers.GetNearest(player.Position, 5f);
            AgentNearbyVillagerDto? nearbyVillagerDto = chatTarget == null
                ? null
                : new AgentNearbyVillagerDto(chatTarget.Id, chatTarget.Name);

            List<object> hotbar = new();
            for (int i = 0; i < 9; i++)
            {
                var slot = player.Hotbar[i];
                if (slot.IsEmpty)
                {
                    hotbar.Add(new
                    {
                        slot = i,
                        kind = "empty"
                    });
                }
                else if (slot.IsTool())
                {
                    hotbar.Add(new
                    {
                        slot = i,
                        kind = "tool",
                        toolId = slot.ToolId,
                        name = slot.GetDisplayName(),
                        durability = slot.Durability,
                        maxDurability = slot.MaxDurability
                    });
                }
                else if (slot.IsFood())
                {
                    hotbar.Add(new
                    {
                        slot = i,
                        kind = "food",
                        itemId = slot.FoodId,
                        name = slot.GetDisplayName(),
                        count = slot.Count
                    });
                }
                else if (slot.IsFluidContainer())
                {
                    hotbar.Add(new
                    {
                        slot = i,
                        kind = "fluid_container",
                        itemId = slot.ToolId,
                        name = slot.GetDisplayName(),
                        filled = slot.IsWaterBucket()
                    });
                }
                else
                {
                    hotbar.Add(new
                    {
                        slot = i,
                        kind = "block",
                        type = slot.BlockType.ToString(),
                        count = slot.Count
                    });
                }
            }

            var skills = new AgentSkillsDto(
                new AgentSkillDto(player.Skills.Mining.Level, player.Skills.Mining.Xp),
                new AgentSkillDto(player.Skills.Woodcutting.Level, player.Skills.Woodcutting.Xp),
                new AgentSkillDto(player.Skills.Combat.Level, player.Skills.Combat.Xp));

            var stateDto = new AgentStateDto(
                _bridge.CurrentGameState.ToString(),
                session.Grid.Seed,
                player.Oxygen,
                new AgentVector3Dto(player.Position.X, player.Position.Y, player.Position.Z),
                new AgentVector3Dto(player.Velocity.X, player.Velocity.Y, player.Velocity.Z),
                player.Yaw,
                player.Pitch,
                player.CreativeMode,
                player.IsGrounded,
                player.Health,
                player.MaxHealth,
                player.Hunger,
                player.MaxHunger,
                player.Stats.EarlyGuideStage,
                guidanceHint,
                host.TimeOfDay,
                host.TimeScale,
                host.TimePaused,
                player.SelectedSlot,
                hotbar,
                skills,
                animalDtos,
                targetBlockDto,
                nearbyStation,
                unlocked,
                settings.PlayWithAi,
                settings.AiProvider.ToString(),
                LlmClientFactory.IsAvailable(settings),
                villageDto,
                villagerDtos,
                nearbyVillagerDto);

            SendJsonResponse(response, HttpStatusCode.OK, stateDto);
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

            var villagers = new List<AgentVillageDebugVillagerDto>();
            foreach (var villager in VillageSettlementHealth.EnumerateLiveCitizens(village, session.Villagers))
            {
                villagers.Add(new AgentVillageDebugVillagerDto(
                    villager.Id,
                    villager.VillageId,
                    villager.Name,
                    villager.Role.ToString(),
                    villager.CurrentJob.ToString(),
                    villager.AiPhase.ToString(),
                    villager.Position.X,
                    villager.Position.Y,
                    villager.Position.Z,
                    villager.JobTarget.HasValue
                        ? new AgentVector3Dto(villager.JobTarget.Value.X, villager.JobTarget.Value.Y, villager.JobTarget.Value.Z)
                        : null,
                    villager.AssignedBuildingSiteId,
                    villager.AssignedBuildingId,
                    villager.HaulSourceVillagerId,
                    villager.HaulSourceChestId,
                    villager.HaulIsDelivering,
                    villager.BreakProgress,
                    villager.WorkTimer,
                    FormatDebugStack(villager.EquippedTool),
                    ExportInventory(villager.Inventory)));
            }

            var buildings = new List<AgentVillageBuildingDto>();
            foreach (var building in village.Buildings)
            {
                buildings.Add(new AgentVillageBuildingDto(
                    building.Id,
                    building.BlueprintId,
                    building.Kind.ToString(),
                    building.IsComplete,
                    building.AnchorX,
                    building.AnchorY,
                    building.AnchorZ));
            }

            var sites = new List<AgentVillageBuildingSiteDto>();
            foreach (var site in village.BuildingSites)
            {
                sites.Add(new AgentVillageBuildingSiteDto(
                    site.Id,
                    site.BlueprintId,
                    site.IsComplete,
                    site.RemainingCount,
                    site.CompletionRatio,
                    site.AnchorX,
                    site.AnchorY,
                    site.AnchorZ));
            }

            var payload = new AgentVillageDebugDto(
                new AgentVillageDebugSummaryDto(
                    village.Id,
                    village.Name,
                    village.AnchorX,
                    village.AnchorY,
                    village.AnchorZ,
                    village.Radius,
                    VillageSettlementHealth.GetLivePopulation(village, session.Villagers),
                    village.Population,
                    village.PopulationCap,
                    village.HousingCapacity,
                    village.Tier.ToString(),
                    village.FoodStock,
                    village.Happiness,
                    village.WorkQueue.Count),
                ExportInventory(village.Storage),
                buildings,
                sites,
                villagers);

            SendJsonResponse(response, HttpStatusCode.OK, payload);
        }

        private static List<AgentInventorySlotDto> ExportInventory(IItemContainer inventory)
        {
            var slots = new List<AgentInventorySlotDto>();
            for (int i = 0; i < inventory.SlotCount; i++)
            {
                var stack = inventory.GetSlot(i);
                if (stack.IsEmpty)
                {
                    continue;
                }

                slots.Add(new AgentInventorySlotDto(i, FormatDebugStack(stack)));
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

            if (!_actions.TryGetValue(cmd.ToLowerInvariant(), out var action))
            {
                var unknown = new AgentActionResponseDto(false, $"Unknown action cmd: {cmd}");
                SendJsonResponse(response, HttpStatusCode.BadRequest, unknown);
                return;
            }

            var result = action.Execute(_bridge, request);
            var statusCode = result.Success ? HttpStatusCode.OK : HttpStatusCode.BadRequest;
            SendJsonResponse(response, statusCode, result);
        }

        internal static bool TryParseKeyInternal(string? keyStr, out Key key)
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

        // Backwards-compatible private wrapper kept in case of future internal callers.
        private static bool TryParseKey(string? keyStr, out Key key) => TryParseKeyInternal(keyStr, out key);

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
                var dto = new AgentChatResponseDto(reply, actions);
                SendJsonResponse(response, HttpStatusCode.OK, dto);
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
                var dto = new AgentChatConfirmResponseDto(true, reply);
                SendJsonResponse(response, HttpStatusCode.OK, dto);
            }
            catch (Exception ex)
            {
                SendJsonResponse(response, HttpStatusCode.InternalServerError, new { error = ex.Message });
            }
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
                new SummonSettlersAction()
            };

            var dict = new Dictionary<string, IAgentAction>(StringComparer.OrdinalIgnoreCase);
            foreach (var action in actions)
            {
                dict[action.Command] = action;
            }

            return dict;
        }

        private static string EscapeJson(string value) =>
            value.Replace("\\", "\\\\").Replace("\"", "\\\"");

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

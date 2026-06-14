using System.Text.Json;
using Autonocraft.Core;
using Autonocraft.Items;
using Autonocraft.World;
using VillageEntity = Autonocraft.Village.Village;

namespace Autonocraft.Ai
{
    public sealed class VillageAiOrchestrator
    {
        private static readonly HashSet<string> ConfirmationTools = new(StringComparer.OrdinalIgnoreCase)
        {
            "recruit_villager",
            "assign_job",
            "queue_build",
            "mark_resource",
            "cancel_job"
        };

        private readonly ConversationSession _conversation = new();
        private readonly IOpenRouterClient _client;
        private readonly bool _usingMock;

        public string? PendingConfirmation { get; private set; }
        public string? PendingToolName { get; private set; }
        public string? PendingToolArgs { get; private set; }

        public VillageAiOrchestrator(IOpenRouterClient? client = null, GameSettings? settings = null)
        {
            var resolvedSettings = settings ?? GameSettingsManager.Load();
            if (client != null)
            {
                _client = client;
                _usingMock = client is MockOpenRouterClient;
            }
            else
            {
                _client = LlmClientFactory.Create(resolvedSettings);
                _usingMock = _client is MockOpenRouterClient;
            }
        }

        public async Task<string> HandleChatAsync(string message, string target, GameSession session)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            VillageEntity? village = session.Villages.GetPrimaryVillage();
            if (village == null)
            {
                return "No village has been founded yet.";
            }

            if (IsConfirmationReply(message))
            {
                return await HandleConfirmationAsync(message, target, session, village).ConfigureAwait(false);
            }

            _conversation.AddMessage(target, "user", message);

            string systemPrompt = BuildSystemPrompt(village, target, session);
            var history = _conversation.GetHistory(target);
            string rawResponse = await _client.CompleteChatAsync(systemPrompt, history).ConfigureAwait(false);

            string reply = ExtractReply(rawResponse);
            if (TryParseToolCall(rawResponse, out string? toolName, out string? toolArgs, out string? embeddedReply))
            {
                if (!string.IsNullOrWhiteSpace(embeddedReply))
                {
                    reply = embeddedReply;
                }

                if (RequiresConfirmation(toolName!))
                {
                    PendingConfirmation = $"Confirm {toolName}? Reply yes or no.";
                    PendingToolName = toolName;
                    PendingToolArgs = toolArgs;
                    reply = string.IsNullOrWhiteSpace(reply)
                        ? PendingConfirmation
                        : $"{reply}\n{PendingConfirmation}";
                }
                else
                {
                    var (success, toolMessage) = VillageAiTools.ExecuteTool(
                        toolName!,
                        toolArgs ?? "{}",
                        session.Villages,
                        session.Villagers,
                        village,
                        GetPlayerContainer(session.Player));

                    reply = success
                        ? AppendToolResult(reply, toolMessage)
                        : AppendToolResult(reply, toolMessage);
                }
            }

            if (string.IsNullOrWhiteSpace(reply))
            {
                reply = _usingMock
                    ? "The steward is listening. (Mock AI — choose OpenRouter or llama.cpp in Settings.)"
                    : "The steward ponders your words.";
            }

            _conversation.AddMessage(target, "assistant", reply);
            return reply;
        }

        public void ClearPendingConfirmation()
        {
            PendingConfirmation = null;
            PendingToolName = null;
            PendingToolArgs = null;
        }

        private async Task<string> HandleConfirmationAsync(
            string message,
            string target,
            GameSession session,
            VillageEntity village)
        {
            if (PendingToolName == null)
            {
                return "There is nothing pending to confirm.";
            }

            bool confirmed = IsAffirmative(message);
            bool declined = IsNegative(message);
            if (!confirmed && !declined)
            {
                return PendingConfirmation ?? "Reply yes or no to confirm.";
            }

            string toolName = PendingToolName;
            string toolArgs = PendingToolArgs ?? "{}";
            ClearPendingConfirmation();

            if (!confirmed)
            {
                string cancelled = "Action cancelled.";
                _conversation.AddMessage(target, "assistant", cancelled);
                return cancelled;
            }

            var (success, toolMessage) = VillageAiTools.ExecuteTool(
                toolName,
                toolArgs,
                session.Villages,
                session.Villagers,
                village,
                GetPlayerContainer(session.Player));

            string reply = success ? toolMessage : $"Failed: {toolMessage}";
            _conversation.AddMessage(target, "assistant", reply);
            return reply;
        }

        private static string BuildSystemPrompt(VillageEntity village, string target, GameSession session)
        {
            string context = VillageContextBuilder.BuildSummary(session.Villages, village, session.Villagers);
            string persona = target.Equals("mayor", StringComparison.OrdinalIgnoreCase)
                ? "You are the village steward (mayor). Speak plainly and help manage the settlement."
                : $"You are villager #{target}. Stay in character based on your persona in the village context.";

            return $"{persona}\n" +
                "Village context JSON:\n" +
                $"{context}\n\n" +
                "When you need to act, respond with JSON like {{\"tool\":\"assign_job\",\"args\":{{\"villager_id\":1,\"job\":\"Gather\"}},\"reply\":\"short player-facing text\"}}.\n" +
                "Available tools: get_village_summary, list_villagers, assign_job, recruit_villager, queue_build, mark_resource, cancel_job, set_village_goal.\n" +
                "For normal conversation, reply with plain text only.";
        }

        private static bool RequiresConfirmation(string toolName)
            => ConfirmationTools.Contains(toolName);

        private static bool TryParseToolCall(string raw, out string? toolName, out string? toolArgs, out string? reply)
        {
            toolName = null;
            toolArgs = null;
            reply = null;

            string json = ExtractJsonObject(raw);
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("tool", out var toolNode) || toolNode.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                toolName = toolNode.GetString();
                if (root.TryGetProperty("args", out var argsNode))
                {
                    toolArgs = argsNode.GetRawText();
                }

                if (root.TryGetProperty("reply", out var replyNode) && replyNode.ValueKind == JsonValueKind.String)
                {
                    reply = replyNode.GetString();
                }

                return !string.IsNullOrWhiteSpace(toolName);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string ExtractReply(string raw)
        {
            if (TryParseToolCall(raw, out _, out _, out string? reply) && !string.IsNullOrWhiteSpace(reply))
            {
                return reply!;
            }

            string json = ExtractJsonObject(raw);
            if (!string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            return raw.Trim();
        }

        private static string ExtractJsonObject(string raw)
        {
            int start = raw.IndexOf('{');
            int end = raw.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return string.Empty;
            }

            return raw[start..(end + 1)];
        }

        private static string AppendToolResult(string reply, string toolMessage)
        {
            if (string.IsNullOrWhiteSpace(reply))
            {
                return toolMessage;
            }

            return $"{reply}\n{toolMessage}";
        }

        private static bool IsConfirmationReply(string message)
        {
            string lower = message.Trim().ToLowerInvariant();
            return lower is "yes" or "y" or "no" or "n" or "confirm" or "cancel";
        }

        private static bool IsAffirmative(string message)
        {
            string lower = message.Trim().ToLowerInvariant();
            return lower is "yes" or "y" or "confirm";
        }

        private static bool IsNegative(string message)
        {
            string lower = message.Trim().ToLowerInvariant();
            return lower is "no" or "n" or "cancel";
        }

        private static HotbarContainer GetPlayerContainer(Player player) => new(player.Hotbar);

        private sealed class HotbarContainer : IItemContainer
        {
            private readonly ItemStack[] _slots;

            public HotbarContainer(ItemStack[] slots) => _slots = slots;

            public int SlotCount => _slots.Length;
            public ItemStack GetSlot(int index) => _slots[index];
            public void SetSlot(int index, ItemStack stack) => _slots[index] = stack;
            public bool AddItem(ItemStack item)
            {
                var proxy = CopyToProxy();
                if (!proxy.AddItem(item))
                {
                    return false;
                }

                CopyFromProxy(proxy);
                return true;
            }
            public bool TryConsumeBlock(BlockType blockType, int count)
            {
                var proxy = CopyToProxy();
                if (!proxy.TryConsumeBlock(blockType, count))
                {
                    return false;
                }

                CopyFromProxy(proxy);
                return true;
            }

            public int CountBlock(BlockType blockType)
            {
                int total = 0;
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i].IsBlock() && _slots[i].BlockType == blockType)
                    {
                        total += _slots[i].Count;
                    }
                }

                return total;
            }

            public bool HasSpaceFor(ItemStack item) => CopyToProxy().HasSpaceFor(item);

            private Inventory CopyToProxy()
            {
                var proxy = new Inventory(_slots.Length);
                for (int i = 0; i < _slots.Length; i++)
                {
                    proxy.SetSlot(i, _slots[i]);
                }

                return proxy;
            }

            private void CopyFromProxy(Inventory proxy)
            {
                for (int i = 0; i < _slots.Length; i++)
                {
                    _slots[i] = proxy.GetSlot(i);
                }
            }
        }
    }
}

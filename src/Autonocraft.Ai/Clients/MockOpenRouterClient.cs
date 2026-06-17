using System.Text.Json;

namespace Autonocraft.Ai
{
    public sealed class MockOpenRouterClient : IOpenRouterClient
    {
        public Task<string> CompleteChatAsync(
            string systemPrompt,
            IReadOnlyList<(string role, string content)> messages,
            string? toolsJson = null)
        {
            string lastUser = string.Empty;
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                if (messages[i].role == "user")
                {
                    lastUser = messages[i].content;
                    break;
                }
            }

            string lower = lastUser.ToLowerInvariant();
            string reply;

            if (lower.Contains("recruit") || lower.Contains("villager"))
            {
                reply = JsonSerializer.Serialize(new
                {
                    tool = "recruit_villager",
                    args = new { },
                    reply = "I can recruit a new villager for 4 oak planks. Shall I proceed?"
                });
            }
            else if (lower.Contains("assign") || lower.Contains("job") || lower.Contains("gather"))
            {
                reply = JsonSerializer.Serialize(new
                {
                    tool = "assign_job",
                    args = new { villager_id = 1, job = "Lumber" },
                    reply = "I will assign that villager to cut lumber."
                });
            }
            else if (lower.Contains("build") || lower.Contains("house"))
            {
                reply = JsonSerializer.Serialize(new
                {
                    tool = "queue_build",
                    args = new { blueprint_id = "peasant_house", anchor_x = 0, anchor_z = 0 },
                    reply = "Queued a peasant house for construction."
                });
            }
            else if (lower.Contains("summary") || lower.Contains("status"))
            {
                reply = JsonSerializer.Serialize(new
                {
                    tool = "get_village_summary",
                    args = new { },
                    reply = "Here is the current village status."
                });
            }
            else
            {
                reply = "The steward nods thoughtfully. Ask about villagers, jobs, building, or village status.";
            }

            return Task.FromResult(reply);
        }
    }
}

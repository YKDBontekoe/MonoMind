namespace Autonocraft.Ai
{
    public sealed class DisabledLlmClient : IOpenRouterClient
    {
        public Task<string> CompleteChatAsync(
            string systemPrompt,
            IReadOnlyList<(string role, string content)> messages,
            string? toolsJson = null)
        {
            return Task.FromResult("Village AI is disabled. Enable it in Settings from the main menu.");
        }
    }
}

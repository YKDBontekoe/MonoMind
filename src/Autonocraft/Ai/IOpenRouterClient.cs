namespace Autonocraft.Ai
{
    public interface IOpenRouterClient
    {
        Task<string> CompleteChatAsync(
            string systemPrompt,
            IReadOnlyList<(string role, string content)> messages,
            string? toolsJson = null);
    }
}

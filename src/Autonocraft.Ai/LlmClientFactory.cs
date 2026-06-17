namespace Autonocraft.Ai
{
    public static class LlmClientFactory
    {
        public static IOpenRouterClient Create(GameSettings settings)
        {
            if (!settings.PlayWithAi || settings.AiProvider == AiProviderKind.Disabled)
            {
                return new DisabledLlmClient();
            }

            return settings.AiProvider switch
            {
                AiProviderKind.OpenRouter when ResolveOpenRouter(settings).IsConfigured
                    => new OpenRouterClient(ResolveOpenRouter(settings)),
                AiProviderKind.LlamaCpp
                    => new LlamaCppClient(LlamaCppConfig.FromSettings(settings)),
                AiProviderKind.Mock => new MockOpenRouterClient(),
                _ => new MockOpenRouterClient()
            };
        }

        public static bool IsAvailable(GameSettings settings)
        {
            if (!settings.PlayWithAi || settings.AiProvider == AiProviderKind.Disabled)
            {
                return false;
            }

            return settings.AiProvider switch
            {
                AiProviderKind.Mock => true,
                AiProviderKind.OpenRouter => ResolveOpenRouter(settings).IsConfigured,
                AiProviderKind.LlamaCpp => !string.IsNullOrWhiteSpace(settings.LlamaCppBaseUrl),
                _ => false
            };
        }

        public static string DescribeProvider(GameSettings settings)
        {
            if (!settings.PlayWithAi)
            {
                return "AI off";
            }

            return settings.AiProvider switch
            {
                AiProviderKind.Disabled => "Disabled",
                AiProviderKind.Mock => "Mock (offline)",
                AiProviderKind.OpenRouter => ResolveOpenRouter(settings).IsConfigured ? $"OpenRouter ({settings.OpenRouterModel})" : "OpenRouter (no API key)",
                AiProviderKind.LlamaCpp => $"llama.cpp @ {settings.LlamaCppBaseUrl}",
                _ => "Unknown"
            };
        }

        private static OpenRouterConfig ResolveOpenRouter(GameSettings settings)
            => OpenRouterConfig.FromSettings(settings);
    }
}

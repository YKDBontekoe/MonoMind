namespace Autonocraft.Ai
{
    public static class LlmClientFactory
    {
        private static readonly object LlamaAvailabilityGate = new();
        private static string? _cachedLlamaBaseUrl;
        private static bool _cachedLlamaAvailable;
        private static DateTime _cachedLlamaCheckedUtc = DateTime.MinValue;

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
                AiProviderKind.LlamaCpp => IsLlamaCppReachable(settings),
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
                AiProviderKind.OpenRouter => ResolveOpenRouter(settings).IsConfigured
                    ? $"OpenRouter ({settings.OpenRouterModel})"
                    : $"OpenRouter ({settings.OpenRouterModel}, no API key)",
                AiProviderKind.LlamaCpp => $"llama.cpp @ {settings.LlamaCppBaseUrl}",
                _ => "Unknown"
            };
        }

        private static OpenRouterConfig ResolveOpenRouter(GameSettings settings)
            => OpenRouterConfig.FromSettings(settings);

        private static bool IsLlamaCppReachable(GameSettings settings)
        {
            var config = LlamaCppConfig.FromSettings(settings);
            if (!Uri.TryCreate(config.BaseUrl, UriKind.Absolute, out var uri) ||
                string.IsNullOrWhiteSpace(uri.Host))
            {
                return false;
            }

            lock (LlamaAvailabilityGate)
            {
                if (string.Equals(_cachedLlamaBaseUrl, config.BaseUrl, StringComparison.OrdinalIgnoreCase) &&
                    DateTime.UtcNow - _cachedLlamaCheckedUtc < TimeSpan.FromSeconds(2))
                {
                    return _cachedLlamaAvailable;
                }

                _cachedLlamaBaseUrl = config.BaseUrl;
                _cachedLlamaCheckedUtc = DateTime.UtcNow;
                _cachedLlamaAvailable = CanConnect(uri);
                return _cachedLlamaAvailable;
            }
        }

        private static bool CanConnect(Uri uri)
        {
            int port = uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 80);
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                var task = client.ConnectAsync(uri.Host, port);
                return task.Wait(TimeSpan.FromMilliseconds(100)) && client.Connected;
            }
            catch
            {
                return false;
            }
        }
    }
}

using Autonocraft.Core;

namespace Autonocraft.Ai
{
    public sealed class OpenRouterConfig
    {
        public const string DefaultModel = "openai/gpt-4o-mini";

        public string? ApiKey { get; private set; }
        public string Model { get; private set; } = DefaultModel;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

        public static OpenRouterConfig Load() => FromSettings(null);

        public static OpenRouterConfig FromSettings(GameSettings? settings)
        {
            var config = new OpenRouterConfig();

            if (settings != null)
            {
                if (!string.IsNullOrWhiteSpace(settings.OpenRouterApiKey))
                {
                    config.ApiKey = settings.OpenRouterApiKey.Trim();
                }

                if (!string.IsNullOrWhiteSpace(settings.OpenRouterModel))
                {
                    config.Model = settings.OpenRouterModel.Trim();
                }
            }

            string? envKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                config.ApiKey = envKey.Trim();
            }

            string? envModel = Environment.GetEnvironmentVariable("OPENROUTER_MODEL");
            if (!string.IsNullOrWhiteSpace(envModel))
            {
                config.Model = envModel.Trim();
            }

            if (!config.IsConfigured)
            {
                TryLoadKeyFile(Path.Combine(GameSettingsManager.GetSettingsDirectory(), "openrouter_key.txt"), config);
            }

            if (!config.IsConfigured)
            {
                TryLoadKeyFile(Path.Combine(AppContext.BaseDirectory, "openrouter_key.txt"), config);
            }

            return config;
        }

        private static void TryLoadKeyFile(string path, OpenRouterConfig config)
        {
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                foreach (string rawLine in File.ReadAllLines(path))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0 || line.StartsWith('#'))
                    {
                        continue;
                    }

                    if (line.StartsWith("API_KEY=", StringComparison.OrdinalIgnoreCase))
                    {
                        config.ApiKey = line["API_KEY=".Length..].Trim();
                        continue;
                    }

                    if (line.StartsWith("MODEL=", StringComparison.OrdinalIgnoreCase))
                    {
                        config.Model = line["MODEL=".Length..].Trim();
                        continue;
                    }

                    if (config.ApiKey == null)
                    {
                        config.ApiKey = line;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OpenRouter] Failed to read '{path}': {ex.Message}");
            }
        }
    }
}

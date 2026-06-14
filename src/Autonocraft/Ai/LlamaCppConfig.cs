using Autonocraft.Core;

namespace Autonocraft.Ai
{
    public sealed class LlamaCppConfig
    {
        public const string DefaultModel = "local";

        public string BaseUrl { get; init; } = GameSettings.DefaultLlamaCppBaseUrl;
        public string Model { get; init; } = DefaultModel;

        public Uri ChatCompletionsUri => new(new Uri(BaseUrl.TrimEnd('/') + "/"), "v1/chat/completions");

        public static LlamaCppConfig FromSettings(GameSettings settings)
        {
            return new LlamaCppConfig
            {
                BaseUrl = string.IsNullOrWhiteSpace(settings.LlamaCppBaseUrl)
                    ? GameSettings.DefaultLlamaCppBaseUrl
                    : settings.LlamaCppBaseUrl.Trim(),
                Model = string.IsNullOrWhiteSpace(settings.LlamaCppModel)
                    ? DefaultModel
                    : settings.LlamaCppModel.Trim()
            };
        }
    }
}

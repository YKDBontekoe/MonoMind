namespace Autonocraft.Domain.Core
{
    public sealed class GameSettings
    {
        public const int MinRenderDistance = 2;
        public const int MaxRenderDistance = 48;
        public const int DefaultRenderDistance = 24;

        public const string DefaultLlamaCppBaseUrl = "http://127.0.0.1:8080";

        public static int GetDefaultRenderDistance() => DefaultRenderDistance;

        public static bool GetDefaultHighQualityLighting() =>
            !OperatingSystem.IsMacOS();

        public int RenderDistance { get; set; } = GetDefaultRenderDistance();

        public bool VSync { get; set; } = true;

        public bool HighQualityLighting { get; set; } = GetDefaultHighQualityLighting();

        /// <summary>When true, village steward chat and HTTP /village/chat are enabled.</summary>
        public bool PlayWithAi { get; set; } = true;

        public AiProviderKind AiProvider { get; set; } = AiProviderKind.Mock;

        public string OpenRouterModel { get; set; } = "openai/gpt-4o-mini";

        public string? OpenRouterApiKey { get; set; }

        public string LlamaCppBaseUrl { get; set; } = DefaultLlamaCppBaseUrl;

        /// <summary>Optional model id sent to llama-server (empty = server default).</summary>
        public string LlamaCppModel { get; set; } = string.Empty;

        public float MasterVolume { get; set; } = 1f;
        public float SfxVolume { get; set; } = 1f;
        public float AmbientVolume { get; set; } = 0.6f;
        public float MusicVolume { get; set; } = 0.5f;
        public bool MuteAudio { get; set; }

        public void Clamp()
        {
            RenderDistance = Math.Clamp(RenderDistance, MinRenderDistance, MaxRenderDistance);
            MasterVolume = Math.Clamp(MasterVolume, 0f, 1f);
            SfxVolume = Math.Clamp(SfxVolume, 0f, 1f);
            AmbientVolume = Math.Clamp(AmbientVolume, 0f, 1f);
            MusicVolume = Math.Clamp(MusicVolume, 0f, 1f);
            if (string.IsNullOrWhiteSpace(LlamaCppBaseUrl))
            {
                LlamaCppBaseUrl = DefaultLlamaCppBaseUrl;
            }

            if (string.IsNullOrWhiteSpace(OpenRouterModel))
            {
                OpenRouterModel = "openai/gpt-4o-mini";
            }
        }
    }
}

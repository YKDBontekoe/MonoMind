namespace Autonocraft.Core
{
    /// <summary>
    /// Host context for dev commands and HTTP API — session plus time/render state.
    /// </summary>
    public sealed class GameHostContext
    {
        public GameSession Session { get; }
        public float TimeOfDay { get; set; }
        public float TimeScale { get; set; }
        public bool TimePaused { get; set; }
        public int RenderDistance { get; }
        public GameSettings Settings { get; }
        public Action<float>? SetMoveSpeedOverride { get; init; }

        public GameHostContext(GameSession session, int renderDistance, GameSettings settings)
        {
            Session = session;
            RenderDistance = renderDistance;
            Settings = settings;
        }

        public void SetTimeOfDay(float value)
        {
            TimeOfDay = value - MathF.Floor(value);
            if (TimeOfDay < 0f) TimeOfDay += 1f;
        }
    }
}

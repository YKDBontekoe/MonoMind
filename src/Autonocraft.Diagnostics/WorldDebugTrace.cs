namespace Autonocraft.Diagnostics
{
    /// <summary>Optional hooks for world streaming / mesh debug output (wired from Core at startup).</summary>
    public static class WorldDebugTrace
    {
        public static Action<string>? LogChunkEvent { get; set; }
    }
}

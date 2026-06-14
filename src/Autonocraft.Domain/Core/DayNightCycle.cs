namespace Autonocraft.Domain.Core
{
    public enum TimePhase
    {
        Night,
        Dawn,
        Day,
        Dusk
    }

    /// <summary>
    /// Shared day/night thresholds for rendering, crafting, villages, and HUD.
    /// </summary>
    public static class DayNightCycle
    {
        public const float NightEnd = 0.2f;
        public const float DawnEnd = 0.3f;
        public const float DayEnd = 0.7f;
        public const float DuskEnd = 0.82f;

        public static float NormalizeTime(float timeOfDay)
        {
            float t = timeOfDay - MathF.Floor(timeOfDay);
            if (t < 0f)
            {
                t += 1f;
            }

            return t;
        }

        public static TimePhase GetTimePhase(float timeOfDay)
        {
            float t = NormalizeTime(timeOfDay);

            if (t >= NightEnd && t < DawnEnd)
            {
                return TimePhase.Dawn;
            }

            if (t >= DawnEnd && t < DayEnd)
            {
                return TimePhase.Day;
            }

            if (t >= DayEnd && t < DuskEnd)
            {
                return TimePhase.Dusk;
            }

            return TimePhase.Night;
        }

        public static bool IsNight(float timeOfDay) => GetTimePhase(timeOfDay) == TimePhase.Night;

        /// <summary>Broad daytime window used by HUD accent styling.</summary>
        public static bool IsBroadDaytime(float timeOfDay)
        {
            float t = NormalizeTime(timeOfDay);
            return t > 0.22f && t < 0.78f;
        }

        public static string GetHudTimeLabel(float timeOfDay)
        {
            float t = NormalizeTime(timeOfDay);
            if (t < NightEnd) return "NIGHT";
            if (t < 0.28f) return "DAWN";
            if (t < 0.42f) return "MORNING";
            if (t < 0.55f) return "NOON";
            if (t < DayEnd) return "DAY";
            if (t < 0.78f) return "DUSK";
            return "NIGHT";
        }
    }
}

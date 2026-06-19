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
    /// Sun arc: sunrise at 0, noon at <see cref="Noon"/>, sunset at <see cref="Sunset"/>, midnight at <see cref="Midnight"/>.
    /// </summary>
    public static class DayNightCycle
    {
        /// <summary>Real seconds per full cycle at default scale (~1/0.001 ≈ 16.7 min).</summary>
        public const float DefaultTimeScale = 0.001f;

        /// <summary>Fraction of the cycle with the sun above the horizon (rest is night).</summary>
        public const float DaylightFraction = 0.62f;

        public const float Sunrise = 0.0f;
        public const float Noon = DaylightFraction * 0.5f;
        public const float Sunset = DaylightFraction;
        public const float Midnight = DaylightFraction + (1f - DaylightFraction) * 0.5f;

        public const float NightEnd = 0.0f;
        public const float DawnEnd = 0.06f;
        public const float DayEnd = 0.54f;
        public const float DuskEnd = DaylightFraction;

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

            if (t < DawnEnd)
            {
                return TimePhase.Dawn;
            }

            if (t < DayEnd)
            {
                return TimePhase.Day;
            }

            if (t < DuskEnd)
            {
                return TimePhase.Dusk;
            }

            return TimePhase.Night;
        }

        public static bool IsNight(float timeOfDay) => GetTimePhase(timeOfDay) == TimePhase.Night;

        /// <summary>
        /// Maps linear clock time to the sun's phase angle so daylight lasts longer than night.
        /// </summary>
        public static float WarpTimeForSun(float timeOfDay)
        {
            float t = NormalizeTime(timeOfDay);
            if (t < DaylightFraction)
            {
                return (t / DaylightFraction) * 0.5f;
            }

            return 0.5f + ((t - DaylightFraction) / (1f - DaylightFraction)) * 0.5f;
        }

        /// <summary>Broad daytime window used by HUD accent styling.</summary>
        public static bool IsBroadDaytime(float timeOfDay)
        {
            float t = NormalizeTime(timeOfDay);
            return t >= DawnEnd && t < DuskEnd + 0.02f;
        }

        public static string GetHudTimeLabel(float timeOfDay)
        {
            float t = NormalizeTime(timeOfDay);
            if (t >= DuskEnd) return "NIGHT";
            if (t < DawnEnd) return "DAWN";
            if (t < Noon - 0.03f) return "MORNING";
            if (t < Noon + 0.03f) return "NOON";
            if (t < DayEnd) return "DAY";
            return "DUSK";
        }
    }
}

using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.Engine
{
    /// <summary>
    /// CPU implementation of the former SkyEffect.fx sky dome shading.
    /// </summary>
    internal static class SkyColor
    {
        public static Vector3 Compute(Vector3 direction, SceneLighting lighting, float timeOfDay, int worldSeed)
        {
            var dir = Vector3.Normalize(direction);
            float elevation = Math.Clamp(dir.Y, 0f, 1f);

            // Steeper zenith gradient for deeper blue skies.
            float skyBlend = MathF.Pow(elevation, 0.65f);
            var sky = Vector3.Lerp(lighting.SkyHorizon, lighting.SkyZenith, skyBlend);

            // Twilight band: warm tint near horizon during dusk/dawn.
            sky = Vector3.Lerp(
                sky,
                lighting.SkyHorizon * new Vector3(1.22f, 0.82f, 0.68f),
                lighting.TwilightFactor * 0.52f * (1f - skyBlend));

            // Horizon haze: soft aerial perspective, stronger at sunset.
            float haze = Math.Clamp(1f - elevation / 0.12f, 0f, 1f) * (0.35f + lighting.SunsetFactor * 0.72f);
            var hazeColor = Vector3.Lerp(lighting.SkyHorizon, new Vector3(1.0f, 0.55f, 0.20f), lighting.SunsetFactor * 0.85f);
            sky = Vector3.Lerp(sky, hazeColor, haze);

            // Broad sun glow around the sun direction.
            float sunGlow = lighting.DayLight * lighting.DayLight;
            if (sunGlow > 0.01f)
            {
                float sunDot = Math.Clamp(Vector3.Dot(dir, Vector3.Normalize(lighting.SunDirection)), 0f, 1f);
                float corona = MathF.Pow(sunDot, 18f) * sunGlow * 0.72f;
                var coronaColor = Vector3.Lerp(new Vector3(1.0f, 0.88f, 0.58f), new Vector3(1.0f, 0.58f, 0.18f), lighting.SunsetFactor);
                sky += coronaColor * corona;

                // Soft crepuscular rays near the horizon when the sun is low.
                float rayBand = Math.Clamp(1f - MathF.Abs(sunDot - 0.15f) / 0.22f, 0f, 1f);
                float rayElev = Math.Clamp(1f - elevation / 0.35f, 0f, 1f);
                float rays = rayBand * rayElev * lighting.SunsetFactor * sunGlow * 0.18f;
                sky += new Vector3(1.0f, 0.62f, 0.28f) * rays;
            }

            // Moon glow when moon is up.
            float moonGlow = lighting.MoonEnabled ? 1f : 0f;
            if (moonGlow > 0f && lighting.DayLight < 0.3f)
            {
                float moonDot = Math.Clamp(Vector3.Dot(dir, Vector3.Normalize(lighting.MoonDirection)), 0f, 1f);
                float moonCorona = MathF.Pow(moonDot, 40f) * (1f - lighting.DayLight) * 0.25f;
                sky += new Vector3(0.70f, 0.80f, 1.0f) * moonCorona;
            }

            float starFade = Math.Clamp(1f - lighting.DayLight / 0.15f, 0f, 1f);
            sky += SampleStars(dir, worldSeed, timeOfDay) * starFade;

            return sky;
        }

        private static Vector3 SampleStars(Vector3 dir, int worldSeed, float timeOfDay)
        {
            float seed = worldSeed;
            var q = new Vector3(
                MathF.Floor(dir.X * 90f + seed * 0.001f),
                MathF.Floor(dir.Y * 90f + seed * 0.002f),
                MathF.Floor(dir.Z * 90f + seed * 0.003f));
            float h = Hash31(q);
            if (h < 0.988f)
            {
                return Vector3.Zero;
            }

            float twinkle = 0.55f + 0.45f * MathF.Sin(timeOfDay * MathF.PI * 2f * (2f + h * 6f) + h * 40f);
            float bright = Math.Clamp((h - 0.988f) / 0.012f, 0f, 1f);
            return new Vector3(1f, 1f, Math.Min(1f, 1f + bright * 0.08f)) * twinkle * bright;
        }

        private static float Hash31(Vector3 p)
        {
            p = new Vector3(
                Frac(p.X * 0.3183099f + 0.1f),
                Frac(p.Y * 0.3183099f + 0.2f),
                Frac(p.Z * 0.3183099f + 0.3f));
            p *= 17f;
            return Frac(p.X * p.Y * p.Z * (p.X + p.Y + p.Z));
        }

        private static float Frac(float value) => value - MathF.Floor(value);
    }
}

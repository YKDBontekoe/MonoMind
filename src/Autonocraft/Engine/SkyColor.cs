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
            float skyBlend = MathF.Pow(elevation, 0.55f);
            var sky = Vector3.Lerp(lighting.SkyHorizon, lighting.SkyZenith, skyBlend);

            sky = Vector3.Lerp(
                sky,
                lighting.SkyHorizon * new Vector3(1.15f, 0.88f, 0.78f),
                lighting.TwilightFactor * 0.35f * (1f - skyBlend));

            float haze = Math.Clamp(1f - elevation / 0.14f, 0f, 1f) * (0.22f + lighting.SunsetFactor * 0.58f);
            var hazeColor = Vector3.Lerp(lighting.SkyHorizon, new Vector3(1f, 0.52f, 0.22f), lighting.SunsetFactor * 0.75f);
            sky = Vector3.Lerp(sky, hazeColor, haze);

            float starFade = Math.Clamp(1f - lighting.DayLight / 0.2f, 0f, 1f);
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

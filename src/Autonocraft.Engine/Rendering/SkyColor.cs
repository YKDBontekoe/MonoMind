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

            float clouds = SampleClouds(dir, timeOfDay, lighting.DayLight);
            if (clouds > 0.01f)
            {
                var cloudTint = Vector3.Lerp(
                    new Vector3(0.94f, 0.96f, 1.0f),
                    new Vector3(1.0f, 0.84f, 0.68f),
                    lighting.SunsetFactor * 0.65f);
                sky = Vector3.Lerp(sky, cloudTint, clouds * 0.72f);
            }

            // Broad sun glow around the sun direction.
            float sunGlow = lighting.DayLight * lighting.DayLight;
            if (sunGlow > 0.01f)
            {
                float sunDot = Math.Clamp(Vector3.Dot(dir, Vector3.Normalize(lighting.SunDirection)), 0f, 1f);
                float corona = MathF.Pow(sunDot, 10f) * sunGlow * 0.42f;
                var coronaColor = Vector3.Lerp(new Vector3(1.0f, 0.88f, 0.58f), new Vector3(1.0f, 0.58f, 0.18f), lighting.SunsetFactor);
                sky += coronaColor * corona;

                float rayBand = Math.Clamp(1f - MathF.Abs(sunDot - 0.15f) / 0.22f, 0f, 1f);
                float rayElev = Math.Clamp(1f - elevation / 0.35f, 0f, 1f);
                float rays = rayBand * rayElev * lighting.SunsetFactor * sunGlow * 0.14f;
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

        private static float SampleClouds(Vector3 dir, float timeOfDay, float dayLight)
        {
            if (dayLight < 0.05f || dir.Y < 0.04f)
            {
                return 0f;
            }

            float wind = timeOfDay * 9.5f;
            float scale = 2.1f + dir.Y * 1.4f;
            float n = CloudNoise(dir.X * scale + wind, dir.Y * 2.8f + 0.2f, dir.Z * scale + wind * 0.65f);
            n += CloudNoise(dir.X * scale * 2.1f - wind * 0.4f, dir.Y * 4.5f, dir.Z * scale * 2.1f + 1.7f) * 0.48f;
            n = Math.Clamp(n, 0f, 1f);

            float coverage = Math.Clamp((n - 0.46f) * 2.4f, 0f, 1f);
            float heightFade = Math.Clamp((dir.Y - 0.04f) / 0.55f, 0f, 1f);
            return coverage * heightFade * dayLight;
        }

        private static float CloudNoise(float x, float y, float z)
        {
            float ix = MathF.Floor(x);
            float iy = MathF.Floor(y);
            float iz = MathF.Floor(z);
            float fx = x - ix;
            float fy = y - iy;
            float fz = z - iz;
            float ux = fx * fx * (3f - 2f * fx);
            float uy = fy * fy * (3f - 2f * fy);
            float uz = fz * fz * (3f - 2f * fz);

            float c000 = Hash31(new Vector3(ix, iy, iz));
            float c100 = Hash31(new Vector3(ix + 1f, iy, iz));
            float c010 = Hash31(new Vector3(ix, iy + 1f, iz));
            float c110 = Hash31(new Vector3(ix + 1f, iy + 1f, iz));
            float c001 = Hash31(new Vector3(ix, iy, iz + 1f));
            float c101 = Hash31(new Vector3(ix + 1f, iy, iz + 1f));
            float c011 = Hash31(new Vector3(ix, iy + 1f, iz + 1f));
            float c111 = Hash31(new Vector3(ix + 1f, iy + 1f, iz + 1f));

            float x00 = c000 + (c100 - c000) * ux;
            float x10 = c010 + (c110 - c010) * ux;
            float x01 = c001 + (c101 - c001) * ux;
            float x11 = c011 + (c111 - c011) * ux;
            float y0 = x00 + (x10 - x00) * uy;
            float y1 = x01 + (x11 - x01) * uy;
            return y0 + (y1 - y0) * uz;
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

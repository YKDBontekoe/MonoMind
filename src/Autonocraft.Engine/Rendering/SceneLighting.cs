using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;
using Autonocraft.Domain.Core;
using Autonocraft.World;

namespace Autonocraft.Engine
{
    public readonly struct SceneLighting
    {
        public Vector3 SunDirection { get; init; }
        public Vector3 MoonDirection { get; init; }
        public float DayLight { get; init; }
        public float SunsetFactor { get; init; }
        public float TwilightFactor { get; init; }
        public Vector3 SkyHorizon { get; init; }
        public Vector3 SkyZenith { get; init; }
        public Vector3 AmbientColor { get; init; }
        public Vector3 SunColor { get; init; }
        public Vector3 MoonColor { get; init; }
        public float FogMultiplier { get; init; }
        public float CloudIntensity { get; init; }
        public float RainIntensity { get; init; }
        public float LightningIntensity { get; init; }
        public float WindIntensity { get; init; }

        public bool SunEnabled => SunDirection.Y > 0.02f;
        public bool MoonEnabled => MoonDirection.Y > 0.02f;

        public static SceneLighting FromTimeOfDay(float timeOfDay, BiomeType biome = BiomeType.Plains, float rainIntensity = 0f, float cloudIntensity = 0f, float lightningIntensity = 0f, float windIntensity = 0f)
        {
            float sunAngle = DayNightCycle.WarpTimeForSun(timeOfDay) * MathF.PI * 2f;
            var sunDir = new Vector3(0f, MathF.Sin(sunAngle), MathF.Cos(sunAngle));
            var moonDir = -sunDir;

            float sunY = sunDir.Y;
            float dayLight = Math.Clamp((sunY + 0.02f) / 0.32f, 0f, 1f);
            float sunIntensity = Math.Clamp((sunY - 0.02f) / 0.50f, 0f, 1f);
            float moonIntensity = Math.Clamp((moonDir.Y - 0.02f) / 0.42f, 0f, 1f);
            // Sunset: narrow band when sun is near the horizon.
            float sunsetFactor = Math.Clamp(1f - Math.Abs(sunY) / 0.22f, 0f, 1f) * MathF.Max(dayLight, moonIntensity * 0.3f);
            float twilightFactor = Math.Clamp(sunsetFactor * (1.2f - dayLight * 0.5f), 0f, 1f);

            sunsetFactor *= (1f - cloudIntensity * 0.8f);
            twilightFactor *= (1f - cloudIntensity * 0.5f);

            // Much darker nights so daylight transition is dramatic and atmospheric.
            var ambNight = new Vector3(0.05f, 0.06f, 0.14f);
            var skyHorizonNight = new Vector3(0.05f, 0.06f, 0.14f);
            var skyZenithNight = new Vector3(0.02f, 0.03f, 0.08f);

            // Custom night colors for biomes
            switch (biome)
            {
                case BiomeType.Swamp:
                    ambNight = new Vector3(0.03f, 0.04f, 0.08f);
                    skyHorizonNight = new Vector3(0.03f, 0.04f, 0.08f);
                    skyZenithNight = new Vector3(0.01f, 0.02f, 0.04f);
                    break;
                case BiomeType.Desert:
                    ambNight = new Vector3(0.03f, 0.04f, 0.10f);
                    skyHorizonNight = new Vector3(0.03f, 0.04f, 0.10f);
                    skyZenithNight = new Vector3(0.01f, 0.02f, 0.06f);
                    break;
                case BiomeType.SnowyPeaks:
                    ambNight = new Vector3(0.08f, 0.10f, 0.22f);
                    skyHorizonNight = new Vector3(0.08f, 0.10f, 0.22f);
                    skyZenithNight = new Vector3(0.03f, 0.05f, 0.12f);
                    break;
            }

            var ambDay = new Vector3(0.25f, 0.28f, 0.34f);
            var skyHorizonDay = new Vector3(0.62f, 0.78f, 0.98f);
            var skyZenithDay = new Vector3(0.18f, 0.42f, 0.86f);

            var skyHorizonSunset = new Vector3(1.0f, 0.48f, 0.16f);
            var skyZenithSunset = new Vector3(0.52f, 0.22f, 0.58f);

            float fogMultiplier = 1.0f;

            switch (biome)
            {
                case BiomeType.Swamp:
                    skyHorizonDay = new Vector3(0.48f, 0.58f, 0.46f);
                    skyZenithDay = new Vector3(0.12f, 0.28f, 0.22f);
                    skyHorizonSunset = new Vector3(0.65f, 0.35f, 0.25f);
                    skyZenithSunset = new Vector3(0.32f, 0.15f, 0.22f);
                    ambDay = new Vector3(0.20f, 0.24f, 0.22f);
                    fogMultiplier = 0.5f;
                    break;
                case BiomeType.Desert:
                    skyHorizonDay = new Vector3(0.72f, 0.75f, 0.62f);
                    skyZenithDay = new Vector3(0.14f, 0.48f, 0.92f);
                    skyHorizonSunset = new Vector3(1.1f, 0.35f, 0.1f);
                    skyZenithSunset = new Vector3(0.6f, 0.15f, 0.45f);
                    ambDay = new Vector3(0.28f, 0.30f, 0.32f);
                    fogMultiplier = 1.25f;
                    break;
                case BiomeType.SnowyPeaks:
                    skyHorizonDay = new Vector3(0.75f, 0.88f, 0.98f);
                    skyZenithDay = new Vector3(0.24f, 0.52f, 0.92f);
                    skyHorizonSunset = new Vector3(0.98f, 0.62f, 0.68f);
                    skyZenithSunset = new Vector3(0.48f, 0.38f, 0.58f);
                    ambDay = new Vector3(0.30f, 0.34f, 0.42f);
                    fogMultiplier = 0.7f;
                    break;
                case BiomeType.Forest:
                    skyHorizonDay = new Vector3(0.55f, 0.74f, 0.88f);
                    skyZenithDay = new Vector3(0.14f, 0.38f, 0.78f);
                    ambDay = new Vector3(0.22f, 0.26f, 0.30f);
                    fogMultiplier = 0.85f;
                    break;
                case BiomeType.Jungle:
                    skyHorizonDay = new Vector3(0.48f, 0.70f, 0.82f);
                    skyZenithDay = new Vector3(0.10f, 0.34f, 0.70f);
                    ambDay = new Vector3(0.18f, 0.24f, 0.22f);
                    fogMultiplier = 0.92f;
                    break;
                case BiomeType.Ocean:
                    skyHorizonDay = new Vector3(0.48f, 0.75f, 0.92f);
                    skyZenithDay = new Vector3(0.12f, 0.36f, 0.82f);
                    fogMultiplier = 1.15f;
                    break;
                case BiomeType.Mountains:
                    skyHorizonDay = new Vector3(0.68f, 0.82f, 0.98f);
                    skyZenithDay = new Vector3(0.12f, 0.32f, 0.76f);
                    fogMultiplier = 1.0f;
                    break;
                case BiomeType.Beach:
                    skyHorizonDay = new Vector3(0.65f, 0.78f, 0.95f);
                    skyZenithDay = new Vector3(0.16f, 0.40f, 0.84f);
                    fogMultiplier = 1.05f;
                    break;
            }

            var ambTwilight = new Vector3(0.20f, 0.16f, 0.24f);
            var ambient = Vector3.Lerp(ambNight, ambDay, dayLight);
            ambient = Vector3.Lerp(ambient, ambTwilight, twilightFactor * 0.65f);

            var sunColor = new Vector3(0.85f, 0.82f, 0.74f) * (0.25f + sunIntensity * 0.55f);
            var moonColor = new Vector3(0.40f, 0.46f, 0.68f) * (0.14f + moonIntensity * 0.55f);

            var skyHorizonBase = Vector3.Lerp(skyHorizonNight, skyHorizonDay, dayLight);
            var skyHorizon = Vector3.Lerp(skyHorizonBase, skyHorizonSunset, sunsetFactor * 0.98f);

            var skyZenithBase = Vector3.Lerp(skyZenithNight, skyZenithDay, dayLight);
            var skyZenith = Vector3.Lerp(skyZenithBase, skyZenithSunset, sunsetFactor * 0.80f);

            // Apply weather effects
            if (cloudIntensity > 0.01f)
            {
                sunColor *= (1f - cloudIntensity * 0.75f);
                moonColor *= (1f - cloudIntensity * 0.60f);

                var stormSkyHorizon = new Vector3(0.22f, 0.24f, 0.28f) * (0.2f + dayLight * 0.8f);
                var stormSkyZenith = new Vector3(0.12f, 0.14f, 0.18f) * (0.2f + dayLight * 0.8f);
                skyHorizon = Vector3.Lerp(skyHorizon, stormSkyHorizon, cloudIntensity);
                skyZenith = Vector3.Lerp(skyZenith, stormSkyZenith, cloudIntensity);

                var stormAmbient = ambient * 0.45f;
                ambient = Vector3.Lerp(ambient, stormAmbient, cloudIntensity);

                fogMultiplier = Microsoft.Xna.Framework.MathHelper.Lerp(fogMultiplier, fogMultiplier * 0.38f, rainIntensity);
                fogMultiplier = Microsoft.Xna.Framework.MathHelper.Lerp(fogMultiplier, fogMultiplier * 0.72f, (1f - rainIntensity) * cloudIntensity);
            }

            if (lightningIntensity > 0.01f)
            {
                ambient += new Vector3(1.0f, 1.0f, 1.1f) * lightningIntensity;
                skyHorizon += new Vector3(1.2f, 1.2f, 1.3f) * lightningIntensity;
                skyZenith += new Vector3(0.8f, 0.8f, 0.9f) * lightningIntensity;
            }

            return new SceneLighting
            {
                SunDirection = sunDir,
                MoonDirection = moonDir,
                DayLight = dayLight,
                SunsetFactor = sunsetFactor,
                TwilightFactor = twilightFactor,
                SkyHorizon = skyHorizon,
                SkyZenith = skyZenith,
                AmbientColor = ambient,
                SunColor = sunColor,
                MoonColor = moonColor,
                FogMultiplier = fogMultiplier,
                CloudIntensity = cloudIntensity,
                RainIntensity = rainIntensity,
                LightningIntensity = lightningIntensity,
                WindIntensity = windIntensity
            };
        }

        public Microsoft.Xna.Framework.Vector3 ToMono(Vector3 v) => new Microsoft.Xna.Framework.Vector3(v.X, v.Y, v.Z);

        public Color SkyHorizonColor => new Color(SkyHorizon.X, SkyHorizon.Y, SkyHorizon.Z);
    }
}

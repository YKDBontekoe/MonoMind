using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;

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

        public bool SunEnabled => SunDirection.Y > 0.02f;
        public bool MoonEnabled => MoonDirection.Y > 0.02f;

        public static SceneLighting FromTimeOfDay(float timeOfDay)
        {
            float sunAngle = timeOfDay * MathF.PI * 2f;
            var sunDir = new Vector3(0f, MathF.Sin(sunAngle), MathF.Cos(sunAngle));
            var moonDir = -sunDir;

            float sunY = sunDir.Y;
            // Start daylight only when sun is above horizon; allow a thin twilight band.
            float dayLight = Math.Clamp((sunY + 0.06f) / 0.36f, 0f, 1f);
            float sunIntensity = Math.Clamp((sunY - 0.02f) / 0.50f, 0f, 1f);
            float moonIntensity = Math.Clamp((moonDir.Y - 0.02f) / 0.42f, 0f, 1f);
            // Sunset: narrow band when sun is near the horizon.
            float sunsetFactor = Math.Clamp(1f - Math.Abs(sunY) / 0.22f, 0f, 1f) * MathF.Max(dayLight, moonIntensity * 0.3f);
            float twilightFactor = Math.Clamp(sunsetFactor * (1.2f - dayLight * 0.5f), 0f, 1f);

            // Much darker nights so daylight transition is dramatic and atmospheric.
            var ambNight = new Vector3(0.04f, 0.05f, 0.12f);
            var ambDay = new Vector3(0.32f, 0.38f, 0.52f);
            var ambTwilight = new Vector3(0.20f, 0.16f, 0.24f);
            var ambient = Vector3.Lerp(ambNight, ambDay, dayLight);
            ambient = Vector3.Lerp(ambient, ambTwilight, twilightFactor * 0.6f);

            // Stronger directional sun to compensate for lower ambient; warm noon, orange dusk.
            var sunColor = new Vector3(1.02f, 0.96f, 0.82f) * (0.35f + sunIntensity * 1.1f);
            var moonColor = new Vector3(0.36f, 0.42f, 0.62f) * (0.12f + moonIntensity * 0.50f);

            // Deep blue zenith at noon, very dark at night, vivid orange-red at sunset.
            var skyHorizonNight = new Vector3(0.04f, 0.05f, 0.12f);
            var skyHorizonDay = new Vector3(0.55f, 0.72f, 0.94f);
            var skyHorizonSunset = new Vector3(1.0f, 0.45f, 0.18f);
            var skyHorizonBase = Vector3.Lerp(skyHorizonNight, skyHorizonDay, dayLight);
            var skyHorizon = Vector3.Lerp(skyHorizonBase, skyHorizonSunset, sunsetFactor * 0.95f);

            var skyZenithNight = new Vector3(0.01f, 0.02f, 0.06f);
            var skyZenithDay = new Vector3(0.14f, 0.34f, 0.78f);
            var skyZenithSunset = new Vector3(0.48f, 0.20f, 0.52f);
            var skyZenithBase = Vector3.Lerp(skyZenithNight, skyZenithDay, dayLight);
            var skyZenith = Vector3.Lerp(skyZenithBase, skyZenithSunset, sunsetFactor * 0.80f);

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
                MoonColor = moonColor
            };
        }

        public Microsoft.Xna.Framework.Vector3 ToMono(Vector3 v) => new Microsoft.Xna.Framework.Vector3(v.X, v.Y, v.Z);

        public Color SkyHorizonColor => new Color(SkyHorizon.X, SkyHorizon.Y, SkyHorizon.Z);
    }
}

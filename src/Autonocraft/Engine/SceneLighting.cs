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
            float dayLight = Math.Clamp((sunY + 0.15f) / 0.45f, 0f, 1f);
            float sunIntensity = Math.Clamp((sunY - 0.05f) / 0.55f, 0f, 1f);
            float moonIntensity = Math.Clamp((moonDir.Y - 0.05f) / 0.45f, 0f, 1f);
            float sunsetFactor = Math.Clamp(1f - Math.Abs(sunY) / 0.28f, 0f, 1f) * dayLight;
            float twilightFactor = Math.Clamp(sunsetFactor * (1.15f - dayLight * 0.35f), 0f, 1f);

            var ambNight = new Vector3(0.12f, 0.14f, 0.26f);
            var ambDay = new Vector3(0.48f, 0.54f, 0.66f);
            var ambTwilight = new Vector3(0.28f, 0.24f, 0.34f);
            var ambient = Vector3.Lerp(ambNight, ambDay, dayLight);
            ambient = Vector3.Lerp(ambient, ambTwilight, twilightFactor * 0.55f);

            var sunColor = new Vector3(1.0f, 0.96f, 0.86f) * (0.55f + sunIntensity * 0.85f);
            var moonColor = new Vector3(0.42f, 0.48f, 0.68f) * (0.28f + moonIntensity * 0.48f);

            var skyHorizonNight = new Vector3(0.10f, 0.12f, 0.24f);
            var skyHorizonDay = new Vector3(0.62f, 0.78f, 0.96f);
            var skyHorizonSunset = new Vector3(0.98f, 0.48f, 0.22f);
            var skyHorizonBase = Vector3.Lerp(skyHorizonNight, skyHorizonDay, dayLight);
            var skyHorizon = Vector3.Lerp(skyHorizonBase, skyHorizonSunset, sunsetFactor * 0.92f);

            var skyZenithNight = new Vector3(0.06f, 0.08f, 0.18f);
            var skyZenithDay = new Vector3(0.22f, 0.44f, 0.86f);
            var skyZenithSunset = new Vector3(0.58f, 0.28f, 0.62f);
            var skyZenithBase = Vector3.Lerp(skyZenithNight, skyZenithDay, dayLight);
            var skyZenith = Vector3.Lerp(skyZenithBase, skyZenithSunset, sunsetFactor * 0.78f);

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

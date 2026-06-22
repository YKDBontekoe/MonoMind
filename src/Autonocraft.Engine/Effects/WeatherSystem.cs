using System;
using Microsoft.Xna.Framework;
using Autonocraft.World;

namespace Autonocraft.Engine
{
    public enum WeatherKind
    {
        Clear,
        Cloudy,
        Rain,
        Thunderstorm,
        Storm
    }

    public sealed class WeatherSystem
    {
        private float _stateTimeRemaining;
        private readonly Random _random = new();

        public WeatherKind CurrentWeather { get; private set; } = WeatherKind.Clear;
        public WeatherKind TargetWeather { get; private set; } = WeatherKind.Clear;

        // Progress of transition from CurrentWeather to TargetWeather (0.0 to 1.0)
        public float TransitionProgress { get; private set; } = 1.0f;

        // Overall intensity: 0.0 (fully clear) to 1.0 (maximum storm/rain)
        public float RainIntensity { get; private set; } = 0.0f;
        public float CloudIntensity { get; private set; } = 0.0f;
        public float WindIntensity { get; private set; } = 0.0f;

        // Lightning parameters
        public bool LightningActive { get; private set; }
        private float _lightningTimer;
        private float _lightningIntensity;

        public float LightningIntensity => LightningActive ? _lightningIntensity : 0f;

        public WeatherSystem()
        {
            CurrentWeather = WeatherKind.Clear;
            TargetWeather = WeatherKind.Clear;
            TransitionProgress = 1.0f;
            _stateTimeRemaining = GetRandomDuration(CurrentWeather);
        }

        public void Update(float deltaTime)
        {
            // Transition progression
            if (TransitionProgress < 1.0f)
            {
                TransitionProgress += deltaTime * 0.05f; // transition takes 20 seconds
                if (TransitionProgress >= 1.0f)
                {
                    TransitionProgress = 1.0f;
                    CurrentWeather = TargetWeather;
                }
            }

            // State duration countdown
            _stateTimeRemaining -= deltaTime;
            if (_stateTimeRemaining <= 0f)
            {
                SelectNextWeatherState();
            }

            // Calculate current intensities by blending current and target states
            float currentRain = GetRainIntensityFactor(CurrentWeather);
            float targetRain = GetRainIntensityFactor(TargetWeather);
            RainIntensity = MathHelper.Lerp(currentRain, targetRain, TransitionProgress);

            float currentCloud = GetCloudIntensityFactor(CurrentWeather);
            float targetCloud = GetCloudIntensityFactor(TargetWeather);
            CloudIntensity = MathHelper.Lerp(currentCloud, targetCloud, TransitionProgress);

            float currentWind = GetWindIntensityFactor(CurrentWeather);
            float targetWind = GetWindIntensityFactor(TargetWeather);
            WindIntensity = MathHelper.Lerp(currentWind, targetWind, TransitionProgress);

            // Handle thunderstorm/storm lightning
            if ((CurrentWeather == WeatherKind.Thunderstorm || TargetWeather == WeatherKind.Thunderstorm ||
                 CurrentWeather == WeatherKind.Storm || TargetWeather == WeatherKind.Storm) && RainIntensity > 0.5f)
            {
                UpdateLightning(deltaTime);
            }
            else
            {
                LightningActive = false;
            }
        }

        private void UpdateLightning(float deltaTime)
        {
            _lightningTimer -= deltaTime;
            if (_lightningTimer <= 0f)
            {
                if (LightningActive)
                {
                    // Turn off lightning
                    LightningActive = false;
                    _lightningTimer = 5f + (float)_random.NextDouble() * 15f; // time between lightning flashes: 5 to 20 seconds
                }
                else
                {
                    // Trigger lightning flash
                    LightningActive = true;
                    _lightningTimer = 0.15f + (float)_random.NextDouble() * 0.25f; // duration of flash
                    _lightningIntensity = 0.6f + (float)_random.NextDouble() * 0.4f;
                }
            }
        }

        private void SelectNextWeatherState()
        {
            // Define transitioning logic
            WeatherKind next;
            double r = _random.NextDouble();
            if (CurrentWeather == WeatherKind.Clear)
            {
                next = r < 0.65 ? WeatherKind.Cloudy : WeatherKind.Clear;
            }
            else if (CurrentWeather == WeatherKind.Cloudy)
            {
                if (r < 0.35) next = WeatherKind.Clear;
                else if (r < 0.75) next = WeatherKind.Rain;
                else if (r < 0.90) next = WeatherKind.Thunderstorm;
                else next = WeatherKind.Storm;
            }
            else if (CurrentWeather == WeatherKind.Rain)
            {
                if (r < 0.5) next = WeatherKind.Cloudy;
                else if (r < 0.8) next = WeatherKind.Thunderstorm;
                else next = WeatherKind.Storm;
            }
            else if (CurrentWeather == WeatherKind.Thunderstorm)
            {
                if (r < 0.6) next = WeatherKind.Rain;
                else next = WeatherKind.Storm;
            }
            else // Storm
            {
                if (r < 0.5) next = WeatherKind.Thunderstorm;
                else next = WeatherKind.Rain;
            }

            TargetWeather = next;
            TransitionProgress = 0.0f;
            _stateTimeRemaining = GetRandomDuration(TargetWeather);
        }

        private float GetRandomDuration(WeatherKind weather)
        {
            return weather switch
            {
                WeatherKind.Clear => 120f + (float)_random.NextDouble() * 180f, // 2-5 minutes
                WeatherKind.Cloudy => 90f + (float)_random.NextDouble() * 120f,  // 1.5-3.5 minutes
                WeatherKind.Rain => 60f + (float)_random.NextDouble() * 120f,    // 1-3 minutes
                WeatherKind.Thunderstorm => 45f + (float)_random.NextDouble() * 60f, // 45s-1.75m
                WeatherKind.Storm => 30f + (float)_random.NextDouble() * 60f,    // 30s-1.5m
                _ => 120f
            };
        }

        private static float GetRainIntensityFactor(WeatherKind weather)
        {
            return weather switch
            {
                WeatherKind.Rain => 0.75f,
                WeatherKind.Thunderstorm => 1.0f,
                WeatherKind.Storm => 1.4f, // Increased rainfall!
                _ => 0.0f
            };
        }

        private static float GetCloudIntensityFactor(WeatherKind weather)
        {
            return weather switch
            {
                WeatherKind.Clear => 0.0f,
                WeatherKind.Cloudy => 0.6f,
                WeatherKind.Rain => 0.85f,
                WeatherKind.Thunderstorm => 1.0f,
                WeatherKind.Storm => 1.0f,
                _ => 0.0f
            };
        }

        private static float GetWindIntensityFactor(WeatherKind weather)
        {
            return weather switch
            {
                WeatherKind.Clear => 0.1f,
                WeatherKind.Cloudy => 0.2f,
                WeatherKind.Rain => 0.4f,
                WeatherKind.Thunderstorm => 0.7f,
                WeatherKind.Storm => 1.6f, // strong wind!
                _ => 0.0f
            };
        }

        // Dev console overrides
        public void ForceWeather(WeatherKind weather, float duration = 120f)
        {
            TargetWeather = weather;
            TransitionProgress = 0.0f;
            _stateTimeRemaining = duration;
        }

        public float TemperatureOffset { get; set; } = 0.0f;
    }
}

using System;
using Autonocraft.Engine;

namespace Autonocraft.Core.DevCommands.Commands
{
    internal sealed class WeatherCommand : IDevCommand
    {
        public string Name => "weather";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var remaining = args.Trim();

            if (!DevCommandParser.TryReadNextToken(ref remaining, out var modeSpan))
            {
                return $"Current weather: {session.Weather.CurrentWeather} (target: {session.Weather.TargetWeather}, progress: {session.Weather.TransitionProgress:F2})";
            }

            WeatherKind weather;
            if (DevCommandParser.EqualsIgnoreCase(modeSpan, "clear"))
            {
                weather = WeatherKind.Clear;
            }
            else if (DevCommandParser.EqualsIgnoreCase(modeSpan, "cloudy"))
            {
                weather = WeatherKind.Cloudy;
            }
            else if (DevCommandParser.EqualsIgnoreCase(modeSpan, "rain"))
            {
                weather = WeatherKind.Rain;
            }
            else if (DevCommandParser.EqualsIgnoreCase(modeSpan, "thunder") ||
                     DevCommandParser.EqualsIgnoreCase(modeSpan, "thunderstorm"))
            {
                weather = WeatherKind.Thunderstorm;
            }
            else if (DevCommandParser.EqualsIgnoreCase(modeSpan, "storm"))
            {
                weather = WeatherKind.Storm;
            }
            else
            {
                return "Unknown weather state. Use: clear, cloudy, rain, thunder, storm";
            }

            session.Weather.ForceWeather(weather);
            return $"Forced weather to {weather}";
        }
    }

    internal sealed class PosCommand : IDevCommand
    {
        public string Name => "pos";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            return $"Position: ({session.Player.Position.X:F1}, {session.Player.Position.Y:F1}, {session.Player.Position.Z:F1})";
        }
    }

    internal sealed class SeedCommand : IDevCommand
    {
        public string Name => "seed";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            return $"World seed: {host.Session.Grid.Seed}";
        }
    }

    internal sealed class AnimalsCommand : IDevCommand
    {
        public string Name => "animals";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            return host.Session.Animals.GetCountSummary();
        }
    }

    internal sealed class TemperatureCommand : IDevCommand
    {
        public string Name => "temperature";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = new[] { "temp" };

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var remaining = args.Trim();

            if (!DevCommandParser.TryReadNextToken(ref remaining, out var actionSpan))
            {
                return $"Current temperature offset: {session.Weather.TemperatureOffset:F1}°C";
            }

            if (DevCommandParser.EqualsIgnoreCase(actionSpan, "set"))
            {
                if (!DevCommandParser.TryReadNextToken(ref remaining, out var valSpan) || !float.TryParse(valSpan.ToString(), out float val))
                {
                    return "Usage: temperature set <value>";
                }
                session.Weather.TemperatureOffset = val;
                return $"Set temperature offset to {val:F1}°C";
            }
            else if (DevCommandParser.EqualsIgnoreCase(actionSpan, "reduce") || DevCommandParser.EqualsIgnoreCase(actionSpan, "sub") || DevCommandParser.EqualsIgnoreCase(actionSpan, "decrease"))
            {
                if (!DevCommandParser.TryReadNextToken(ref remaining, out var valSpan) || !float.TryParse(valSpan.ToString(), out float val))
                {
                    return "Usage: temperature reduce <value>";
                }
                session.Weather.TemperatureOffset -= val;
                return $"Reduced temperature offset by {val:F1}°C (New offset: {session.Weather.TemperatureOffset:F1}°C)";
            }
            else if (DevCommandParser.EqualsIgnoreCase(actionSpan, "add") || DevCommandParser.EqualsIgnoreCase(actionSpan, "increase"))
            {
                if (!DevCommandParser.TryReadNextToken(ref remaining, out var valSpan) || !float.TryParse(valSpan.ToString(), out float val))
                {
                    return "Usage: temperature add <value>";
                }
                session.Weather.TemperatureOffset += val;
                return $"Increased temperature offset by {val:F1}°C (New offset: {session.Weather.TemperatureOffset:F1}°C)";
            }
            else
            {
                if (float.TryParse(actionSpan.ToString(), out float val))
                {
                    session.Weather.TemperatureOffset = val;
                    return $"Set temperature offset to {val:F1}°C";
                }
                return "Unknown sub-command. Use: temperature [set|reduce|add] <value>";
            }
        }
    }

    internal sealed class SnowCommand : IDevCommand
    {
        public string Name => "snow";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var remaining = args.Trim();

            if (DevCommandParser.TryReadNextToken(ref remaining, out var subSpan))
            {
                if (DevCommandParser.EqualsIgnoreCase(subSpan, "clear"))
                {
                    session.Weather.ForceWeather(WeatherKind.Clear);
                    session.Weather.TemperatureOffset = 0.0f;
                    return "Snow cleared (weather set to Clear, temperature offset reset to 0.0)";
                }
                if (DevCommandParser.EqualsIgnoreCase(subSpan, "storm"))
                {
                    session.Weather.ForceWeather(WeatherKind.Storm);
                    session.Weather.TemperatureOffset = -5.0f;
                    return "Forced snow storm (weather set to Storm, temperature offset set to -5.0)";
                }
            }

            session.Weather.ForceWeather(WeatherKind.Thunderstorm);
            session.Weather.TemperatureOffset = -5.0f;
            return "Forced snowing weather (weather set to Thunderstorm, temperature offset set to -5.0)";
        }
    }
}


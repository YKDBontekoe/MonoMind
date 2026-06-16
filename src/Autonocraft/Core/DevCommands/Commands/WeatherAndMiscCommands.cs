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
                     DevCommandParser.EqualsIgnoreCase(modeSpan, "thunderstorm") ||
                     DevCommandParser.EqualsIgnoreCase(modeSpan, "storm"))
            {
                weather = WeatherKind.Thunderstorm;
            }
            else
            {
                return "Unknown weather state. Use: clear, cloudy, rain, thunder";
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
}


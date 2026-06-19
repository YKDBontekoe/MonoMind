using System;
using Autonocraft.Domain.Core;

namespace Autonocraft.Core.DevCommands.Commands
{
    internal sealed class TimeCommand : IDevCommand
    {
        public string Name => "time";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            args = args.Trim();
            if (args.IsEmpty)
            {
                return $"Time: {host.TimeOfDay:F3} ({DevCommandHelpers.GetTimeLabel(host.TimeOfDay)}) | Scale: {host.TimeScale:F4} | Paused: {host.TimePaused}";
            }

            var remaining = args;
            if (!DevCommandParser.TryReadNextToken(ref remaining, out var subSpan))
            {
                // Should not happen, but fall back to usage to match original behavior for malformed input.
                return "Usage: time [set|dawn|noon|dusk|midnight|scale|pause|resume]";
            }

            if (DevCommandParser.EqualsIgnoreCase(subSpan, "set"))
            {
                if (!DevCommandParser.TryReadNextToken(ref remaining, out var valueSpan) ||
                    !DevCommandParser.TryParseFloat(valueSpan, out float value) ||
                    !float.IsFinite(value))
                {
                    return "Usage: time set <0-1>";
                }

                host.SetTimeOfDay(value);
                return $"Time set to {host.TimeOfDay:F3} ({DevCommandHelpers.GetTimeLabel(host.TimeOfDay)})";
            }

            if (DevCommandParser.EqualsIgnoreCase(subSpan, "dawn"))
            {
                host.SetTimeOfDay(DayNightCycle.Sunrise + 0.02f);
                return $"Time set to dawn ({host.TimeOfDay:F3})";
            }

            if (DevCommandParser.EqualsIgnoreCase(subSpan, "noon"))
            {
                host.SetTimeOfDay(DayNightCycle.Noon);
                return $"Time set to noon ({host.TimeOfDay:F3})";
            }

            if (DevCommandParser.EqualsIgnoreCase(subSpan, "dusk"))
            {
                host.SetTimeOfDay(DayNightCycle.Sunset - 0.02f);
                return $"Time set to dusk ({host.TimeOfDay:F3})";
            }

            if (DevCommandParser.EqualsIgnoreCase(subSpan, "midnight"))
            {
                host.SetTimeOfDay(DayNightCycle.Midnight);
                return $"Time set to midnight ({host.TimeOfDay:F3})";
            }

            if (DevCommandParser.EqualsIgnoreCase(subSpan, "scale"))
            {
                if (!DevCommandParser.TryReadNextToken(ref remaining, out var scaleSpan) ||
                    !DevCommandParser.TryParseFloat(scaleSpan, out float scale) ||
                    !float.IsFinite(scale))
                {
                    return "Usage: time scale <number> (0 to pause)";
                }

                host.TimeScale = Math.Max(0f, scale);
                host.TimePaused = scale <= 0f;
                return $"Time scale set to {host.TimeScale:F4}";
            }

            if (DevCommandParser.EqualsIgnoreCase(subSpan, "pause"))
            {
                host.TimePaused = true;
                return "Day cycle paused";
            }

            if (DevCommandParser.EqualsIgnoreCase(subSpan, "resume"))
            {
                host.TimePaused = false;
                if (host.TimeScale <= 0f) host.TimeScale = DayNightCycle.DefaultTimeScale;
                return "Day cycle resumed";
            }

            return "Usage: time [set|dawn|noon|dusk|midnight|scale|pause|resume]";
        }
    }
}


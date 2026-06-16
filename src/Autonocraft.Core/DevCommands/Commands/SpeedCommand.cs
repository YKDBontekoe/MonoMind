using System;

namespace Autonocraft.Core.DevCommands.Commands
{
    internal sealed class SpeedCommand : IDevCommand
    {
        public string Name => "speed";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var remaining = args.Trim();
            if (!DevCommandParser.TryReadNextToken(ref remaining, out var speedSpan) ||
                !DevCommandParser.TryParseFloat(speedSpan, out float speed) ||
                speed <= 0f)
            {
                return "Usage: speed <number>";
            }

            host.SetMoveSpeedOverride?.Invoke(speed);
            return $"Move speed set to {speed:F1}";
        }
    }
}


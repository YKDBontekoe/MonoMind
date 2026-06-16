using System;

namespace Autonocraft.Core.DevCommands.Commands
{
    internal sealed class CreativeCommand : IDevCommand
    {
        public string Name => "creative";

        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = new[] { "fly" };

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var player = session.Player;

            var remaining = args.Trim();
            if (remaining.IsEmpty)
            {
                player.CreativeMode = !player.CreativeMode;
            }
            else
            {
                if (!DevCommandParser.TryReadNextToken(ref remaining, out var argSpan))
                {
                    return "Usage: creative [on|off]";
                }

                if (DevCommandParser.EqualsIgnoreCase(argSpan, "on") ||
                    DevCommandParser.EqualsIgnoreCase(argSpan, "true") ||
                    DevCommandParser.EqualsIgnoreCase(argSpan, "1"))
                {
                    player.CreativeMode = true;
                }
                else if (DevCommandParser.EqualsIgnoreCase(argSpan, "off") ||
                         DevCommandParser.EqualsIgnoreCase(argSpan, "false") ||
                         DevCommandParser.EqualsIgnoreCase(argSpan, "0"))
                {
                    player.CreativeMode = false;
                }
                else
                {
                    return "Usage: creative [on|off]";
                }
            }

            player.Velocity = System.Numerics.Vector3.Zero;
            if (!player.CreativeMode)
            {
                player.ForceAirborne();
            }

            return $"Creative mode: {(player.CreativeMode ? "ON" : "OFF (survival)")}";
        }
    }
}


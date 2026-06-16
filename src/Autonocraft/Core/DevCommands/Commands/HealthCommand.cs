using System;

namespace Autonocraft.Core.DevCommands.Commands
{
    internal sealed class HealthCommand : IDevCommand
    {
        public string Name => "health";

        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = new[] { "heal" };

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var remaining = args.Trim();

            if (DevCommandParser.TryReadNextToken(ref remaining, out var valueSpan) &&
                DevCommandParser.TryParseFloat(valueSpan, out float health))
            {
                session.Player.Health = Math.Clamp(health, 0f, session.Player.MaxHealth);
            }
            else
            {
                session.Player.Health = session.Player.MaxHealth;
            }

            return $"Health: {session.Player.Health:F0}/{session.Player.MaxHealth:F0}";
        }
    }
}


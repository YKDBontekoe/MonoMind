using System;

namespace Autonocraft.Core.DevCommands.Commands
{
    internal sealed class DamageCommand : IDevCommand
    {
        public string Name => "damage";

        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = new[] { "hurt" };

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            float amount = 1f;

            var remaining = args.Trim();
            if (DevCommandParser.TryReadNextToken(ref remaining, out var valueSpan))
            {
                if (!DevCommandParser.TryParseFloat(valueSpan, out amount) || amount <= 0f)
                {
                    return "Usage: damage [amount]";
                }
            }

            bool applied = session.Player.TakeDamage(amount);
            return applied
                ? $"Took {amount:F0} damage. Health: {session.Player.Health:F0}/{session.Player.MaxHealth:F0}"
                : $"No damage applied. Health: {session.Player.Health:F0}/{session.Player.MaxHealth:F0}";
        }
    }
}


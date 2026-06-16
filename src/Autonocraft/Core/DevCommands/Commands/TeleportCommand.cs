using System;
using System.Numerics;

namespace Autonocraft.Core.DevCommands.Commands
{
    internal sealed class TeleportCommand : IDevCommand
    {
        public string Name => "tp";

        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = new[] { "teleport" };

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var remaining = args;

            if (!DevCommandParser.TryReadNextToken(ref remaining, out var xSpan) ||
                !DevCommandParser.TryReadNextToken(ref remaining, out var ySpan) ||
                !DevCommandParser.TryReadNextToken(ref remaining, out var zSpan) ||
                !DevCommandParser.TryParseFloat(xSpan, out float x) ||
                !DevCommandParser.TryParseFloat(ySpan, out float y) ||
                !DevCommandParser.TryParseFloat(zSpan, out float z))
            {
                return "Usage: tp <x> <y> <z>";
            }

            session.Player.Position = new Vector3(x, y, z);
            session.Player.Velocity = Vector3.Zero;
            session.Player.ForceAirborne();
            return $"Teleported to ({x:F1}, {y:F1}, {z:F1})";
        }
    }
}


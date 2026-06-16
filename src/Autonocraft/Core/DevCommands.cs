using System;
using Autonocraft.Engine;
using Autonocraft.Core.DevCommands;

namespace Autonocraft.Core.DevCommands
{
    /// <summary>
    /// Entry point for the in-game dev console.
    /// Parses an input line and dispatches to the strongly-typed command registry.
    /// </summary>
    public static class DevCommandRouter
    {
        public static string Execute(GameHostContext host, string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            ReadOnlySpan<char> span = input.AsSpan().Trim();
            if (span.IsEmpty)
                return string.Empty;

            int spaceIndex = span.IndexOf(' ');
            ReadOnlySpan<char> cmdSpan;
            ReadOnlySpan<char> argsSpan;
            if (spaceIndex < 0)
            {
                cmdSpan = span;
                argsSpan = ReadOnlySpan<char>.Empty;
            }
            else
            {
                cmdSpan = span[..spaceIndex];
                argsSpan = span[(spaceIndex + 1)..];
            }

            // Preserve original command casing in error messages.
            string originalCmd = cmdSpan.ToString();
            string cmdLower = originalCmd.ToLowerInvariant();

            if (cmdLower is "help" or "?")
                return GetHelp();

            if (cmdLower == "clear")
                return "__CLEAR__";

            if (DevCommandRegistry.TryGet(cmdLower, out var command))
            {
                return command.Execute(host, argsSpan);
            }

            return $"Unknown command: {originalCmd}. Type 'help' for commands.";
        }

        private static string GetHelp()
        {
            return string.Join("\n", new[]
            {
                "DEV CONSOLE COMMANDS:",
                "  time              - show time of day",
                "  time set <0-1>    - set cycle (0=midnight, 0.25=dawn, 0.5=noon, 0.75=dusk)",
                "  time dawn|noon|dusk|midnight",
                "  time scale <n>    - cycle speed (0=pause, 0.01=default)",
                "  time pause|resume - pause/resume day cycle",
                "  tp <x> <y> <z>    - teleport",
                "  pos               - show position",
                "  creative [on|off] - toggle/set creative mode (alias: fly)",
                "  give <block> [n]  - add blocks to hotbar",
                "  health [n]        - set health (default: max)",
                "  damage [n]        - apply damage to player (default: 1)",
                "  speed <n>         - set fly/walk speed",
                "  slot <0-8>        - select hotbar slot",
                "  inv / hotbar      - list all hotbar slots",
                "  seed              - show world seed",
                "  perf              - show performance counters",
                "  chunks            - show loaded chunk count and player chunk",
                "  weather [clear|cloudy|rain|thunder] - get or set weather",
                "  spawn <type> [n]  - spawn animals in front of player",
                "  animals           - show animal counts",
                "  recipes           - list unlocked crafting discoveries",
                "  unlock <id>       - unlock sigil/recipe for debugging",
                "  clear             - clear console output",
                "  help              - show this list"
            });
        }
    }
}

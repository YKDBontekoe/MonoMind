using System;
using System.Globalization;
using System.Numerics;
using Autonocraft.Entities;
using Autonocraft.World;

namespace Autonocraft.Core
{
    public static class DevCommands
    {
        public static string Execute(AutonocraftGame game, string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLowerInvariant();

            return cmd switch
            {
                "help" or "?" => GetHelp(),
                "clear" => "__CLEAR__",
                "time" => HandleTime(game, parts),
                "tp" or "teleport" => HandleTeleport(game, parts),
                "pos" => $"Position: ({game.Player.Position.X:F1}, {game.Player.Position.Y:F1}, {game.Player.Position.Z:F1})",
                "fly" => HandleFly(game, parts),
                "give" => HandleGive(game, parts),
                "health" or "heal" => HandleHealth(game, parts),
                "damage" or "hurt" => HandleDamage(game, parts),
                "speed" => HandleSpeed(game, parts),
                "slot" => HandleSlot(game, parts),
                "chunks" => $"Active chunks: {game.Grid.GetActiveChunks().Count}",
                "spawn" => HandleSpawn(game, parts),
                "animals" => game.Animals.GetCountSummary(),
                _ => $"Unknown command: {parts[0]}. Type 'help' for commands."
            };
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
                "  fly [on|off]      - toggle/set flying mode",
                "  give <block> [n]  - add blocks to hotbar",
                "  health [n]        - set health (default: max)",
                "  damage [n]        - apply damage to player (default: 1)",
                "  speed <n>         - set fly/walk speed",
                "  slot <0-8>        - select hotbar slot",
                "  chunks            - show loaded chunk count",
                "  spawn <type> [n]  - spawn animals in front of player",
                "  animals           - show animal counts",
                "  clear             - clear console output",
                "  help              - show this list"
            });
        }

        private static string HandleTime(AutonocraftGame game, string[] parts)
        {
            if (parts.Length == 1)
            {
                return $"Time: {game.TimeOfDay:F3} ({GetTimeLabel(game.TimeOfDay)}) | Scale: {game.TimeScale:F4} | Paused: {game.TimePaused}";
            }

            string sub = parts[1].ToLowerInvariant();
            switch (sub)
            {
                case "set":
                    if (parts.Length < 3 || !TryParseFloat(parts[2], out float value))
                        return "Usage: time set <0-1>";
                    game.SetTimeOfDay(value);
                    return $"Time set to {game.TimeOfDay:F3} ({GetTimeLabel(game.TimeOfDay)})";

                case "dawn":
                    game.SetTimeOfDay(0.25f);
                    return "Time set to dawn (0.25)";

                case "noon":
                    game.SetTimeOfDay(0.5f);
                    return "Time set to noon (0.5)";

                case "dusk":
                    game.SetTimeOfDay(0.75f);
                    return "Time set to dusk (0.75)";

                case "midnight":
                    game.SetTimeOfDay(0.0f);
                    return "Time set to midnight (0.0)";

                case "scale":
                    if (parts.Length < 3 || !TryParseFloat(parts[2], out float scale))
                        return "Usage: time scale <number> (0 to pause)";
                    game.TimeScale = Math.Max(0f, scale);
                    game.TimePaused = scale <= 0f;
                    return $"Time scale set to {game.TimeScale:F4}";

                case "pause":
                    game.TimePaused = true;
                    return "Day cycle paused";

                case "resume":
                    game.TimePaused = false;
                    if (game.TimeScale <= 0f) game.TimeScale = 0.01f;
                    return "Day cycle resumed";

                default:
                    return "Usage: time [set|dawn|noon|dusk|midnight|scale|pause|resume]";
            }
        }

        private static string HandleTeleport(AutonocraftGame game, string[] parts)
        {
            if (parts.Length < 4 ||
                !TryParseFloat(parts[1], out float x) ||
                !TryParseFloat(parts[2], out float y) ||
                !TryParseFloat(parts[3], out float z))
            {
                return "Usage: tp <x> <y> <z>";
            }

            game.Player.Position = new Vector3(x, y, z);
            game.Player.Velocity = Vector3.Zero;
            return $"Teleported to ({x:F1}, {y:F1}, {z:F1})";
        }

        private static string HandleFly(AutonocraftGame game, string[] parts)
        {
            if (parts.Length == 1)
            {
                game.Player.FlyingMode = !game.Player.FlyingMode;
            }
            else
            {
                string arg = parts[1].ToLowerInvariant();
                if (arg is "on" or "true" or "1")
                    game.Player.FlyingMode = true;
                else if (arg is "off" or "false" or "0")
                    game.Player.FlyingMode = false;
                else
                    return "Usage: fly [on|off]";
            }

            game.Player.Velocity = Vector3.Zero;
            return $"Flying mode: {(game.Player.FlyingMode ? "ON (creative)" : "OFF (survival)")}";
        }

        private static string HandleGive(AutonocraftGame game, string[] parts)
        {
            if (parts.Length < 2)
                return "Usage: give <block> [count]";

            if (!Enum.TryParse<BlockType>(parts[1], true, out var blockType) || blockType == BlockType.Air)
                return $"Unknown block: {parts[1]}";

            int count = 64;
            if (parts.Length >= 3 && (!int.TryParse(parts[2], out count) || count <= 0))
                return "Invalid count";

            game.Player.GiveBlocks(blockType, count);
            return $"Gave {count}x {blockType}";
        }

        private static string HandleHealth(AutonocraftGame game, string[] parts)
        {
            if (parts.Length >= 2 && TryParseFloat(parts[1], out float health))
            {
                game.Player.Health = Math.Clamp(health, 0f, game.Player.MaxHealth);
            }
            else
            {
                game.Player.Health = game.Player.MaxHealth;
            }

            return $"Health: {game.Player.Health:F0}/{game.Player.MaxHealth:F0}";
        }

        private static string HandleDamage(AutonocraftGame game, string[] parts)
        {
            float amount = 1f;
            if (parts.Length >= 2 && (!TryParseFloat(parts[1], out amount) || amount <= 0f))
            {
                return "Usage: damage [amount]";
            }

            bool applied = game.Player.TakeDamage(amount);
            return applied
                ? $"Took {amount:F0} damage. Health: {game.Player.Health:F0}/{game.Player.MaxHealth:F0}"
                : $"No damage applied. Health: {game.Player.Health:F0}/{game.Player.MaxHealth:F0}";
        }

        private static string HandleSpeed(AutonocraftGame game, string[] parts)
        {
            if (parts.Length < 2 || !TryParseFloat(parts[1], out float speed) || speed <= 0f)
                return "Usage: speed <number>";

            game.MoveSpeedOverride = speed;
            return $"Move speed set to {speed:F1}";
        }

        private static string HandleSlot(AutonocraftGame game, string[] parts)
        {
            if (parts.Length < 2 || !int.TryParse(parts[1], out int slot) || slot < 0 || slot > 8)
                return "Usage: slot <0-8>";

            game.Player.SelectedSlot = slot;
            var item = game.Player.Hotbar[slot];
            return item.Type == BlockType.Air
                ? $"Selected slot {slot + 1} (empty)"
                : $"Selected slot {slot + 1}: {item.Type} x{item.Count}";
        }

        private static string HandleSpawn(AutonocraftGame game, string[] parts)
        {
            if (parts.Length < 2 ||
                !Enum.TryParse<AnimalType>(parts[1], true, out var type))
            {
                return "Usage: spawn <sheep|pig|chicken> [count]";
            }

            int count = 1;
            if (parts.Length >= 3 && (!int.TryParse(parts[2], out count) || count <= 0))
            {
                return "Invalid count";
            }

            int spawned = game.Animals.SpawnInFrontOfPlayer(game.Player, game.Grid, type, count);
            return spawned == 0
                ? $"Failed to spawn {type}"
                : $"Spawned {spawned}x {type}";
        }

        private static string GetTimeLabel(float time)
        {
            if (time < 0.2f || time > 0.9f) return "NIGHT";
            if (time < 0.3f) return "DAWN";
            if (time < 0.7f) return "DAY";
            if (time < 0.8f) return "DUSK";
            return "NIGHT";
        }

        private static bool TryParseFloat(string s, out float value)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}

using System;
using System.Globalization;
using System.Numerics;
using Autonocraft.Crafting;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.Village;
using Autonocraft.Domain.Village;
using Autonocraft.World;

namespace Autonocraft.Core
{
    public static class DevCommands
    {
        public static string Execute(GameHostContext host, string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLowerInvariant();
            var session = host.Session;

            return cmd switch
            {
                "help" or "?" => GetHelp(),
                "clear" => "__CLEAR__",
                "time" => HandleTime(host, parts),
                "tp" or "teleport" => HandleTeleport(session, parts),
                "pos" => $"Position: ({session.Player.Position.X:F1}, {session.Player.Position.Y:F1}, {session.Player.Position.Z:F1})",
                "fly" => HandleFly(session, parts),
                "give" => HandleGive(session, parts),
                "health" or "heal" => HandleHealth(session, parts),
                "damage" or "hurt" => HandleDamage(session, parts),
                "speed" => HandleSpeed(host, parts),
                "slot" => HandleSlot(session, parts),
                "inv" or "hotbar" => HandleInventory(session),
                "seed" => $"World seed: {session.Grid.Seed}",
                "perf" => HandlePerf(session),
                "chunks" => HandleChunks(session),
                "spawn" => HandleSpawn(session, parts),
                "animals" => session.Animals.GetCountSummary(),
                "recipes" => HandleRecipes(session),
                "unlock" => HandleUnlock(session, parts),
                "village" => HandleVillage(session),
                "recruit" => HandleRecruit(session),
                "assign" => HandleAssignJob(session, parts),
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
                "  inv / hotbar      - list all hotbar slots",
                "  seed              - show world seed",
                "  perf              - show performance counters",
                "  chunks            - show loaded chunk count and player chunk",
                "  spawn <type> [n]  - spawn animals in front of player",
                "  animals           - show animal counts",
                "  recipes           - list unlocked crafting discoveries",
                "  unlock <id>       - unlock sigil/recipe for debugging",
                "  clear             - clear console output",
                "  help              - show this list"
            });
        }

        private static string HandleTime(GameHostContext host, string[] parts)
        {
            if (parts.Length == 1)
            {
                return $"Time: {host.TimeOfDay:F3} ({GetTimeLabel(host.TimeOfDay)}) | Scale: {host.TimeScale:F4} | Paused: {host.TimePaused}";
            }

            string sub = parts[1].ToLowerInvariant();
            switch (sub)
            {
                case "set":
                    if (parts.Length < 3 || !TryParseFloat(parts[2], out float value))
                        return "Usage: time set <0-1>";
                    host.SetTimeOfDay(value);
                    return $"Time set to {host.TimeOfDay:F3} ({GetTimeLabel(host.TimeOfDay)})";

                case "dawn":
                    host.SetTimeOfDay(0.25f);
                    return "Time set to dawn (0.25)";

                case "noon":
                    host.SetTimeOfDay(0.5f);
                    return "Time set to noon (0.5)";

                case "dusk":
                    host.SetTimeOfDay(0.75f);
                    return "Time set to dusk (0.75)";

                case "midnight":
                    host.SetTimeOfDay(0.0f);
                    return "Time set to midnight (0.0)";

                case "scale":
                    if (parts.Length < 3 || !TryParseFloat(parts[2], out float scale))
                        return "Usage: time scale <number> (0 to pause)";
                    host.TimeScale = Math.Max(0f, scale);
                    host.TimePaused = scale <= 0f;
                    return $"Time scale set to {host.TimeScale:F4}";

                case "pause":
                    host.TimePaused = true;
                    return "Day cycle paused";

                case "resume":
                    host.TimePaused = false;
                    if (host.TimeScale <= 0f) host.TimeScale = 0.01f;
                    return "Day cycle resumed";

                default:
                    return "Usage: time [set|dawn|noon|dusk|midnight|scale|pause|resume]";
            }
        }

        private static string HandleTeleport(GameSession session, string[] parts)
        {
            if (parts.Length < 4 ||
                !TryParseFloat(parts[1], out float x) ||
                !TryParseFloat(parts[2], out float y) ||
                !TryParseFloat(parts[3], out float z))
            {
                return "Usage: tp <x> <y> <z>";
            }

            session.Player.Position = new Vector3(x, y, z);
            session.Player.Velocity = Vector3.Zero;
            return $"Teleported to ({x:F1}, {y:F1}, {z:F1})";
        }

        private static string HandleFly(GameSession session, string[] parts)
        {
            if (parts.Length == 1)
            {
                session.Player.FlyingMode = !session.Player.FlyingMode;
            }
            else
            {
                string arg = parts[1].ToLowerInvariant();
                if (arg is "on" or "true" or "1")
                    session.Player.FlyingMode = true;
                else if (arg is "off" or "false" or "0")
                    session.Player.FlyingMode = false;
                else
                    return "Usage: fly [on|off]";
            }

            session.Player.Velocity = Vector3.Zero;
            return $"Flying mode: {(session.Player.FlyingMode ? "ON (creative)" : "OFF (survival)")}";
        }

        private static string HandleGive(GameSession session, string[] parts)
        {
            if (parts.Length < 2)
                return "Usage: give <block|tool> [count]";

            if (parts[1].Equals("bucket", StringComparison.OrdinalIgnoreCase))
            {
                bool filled = parts.Length >= 3 &&
                    parts[2].Equals("water", StringComparison.OrdinalIgnoreCase);
                session.Player.AddItem(ItemStack.CreateFluidContainer(
                    filled ? ItemId.WaterBucket : ItemId.EmptyBucket));
                return filled ? "Gave Water Bucket" : "Gave Empty Bucket";
            }

            if (parts[1].Equals("tool", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length < 3 || !Enum.TryParse<ToolType>(parts[2], true, out var toolType))
                {
                    return "Usage: give tool <pickaxe|axe|shovel|sword> [wood|stone|iron|gold]";
                }

                var tier = ToolTier.Wood;
                if (parts.Length >= 4 && !Enum.TryParse<ToolTier>(parts[3], true, out tier))
                {
                    return "Invalid tier";
                }

                var itemId = ToolRegistry.GetItemId(toolType, tier);
                if (itemId == ItemId.None)
                {
                    return $"Tool {toolType}/{tier} is not available";
                }

                session.Player.AddItem(ToolRegistry.CreateStack(itemId));
                return $"Gave {ToolRegistry.Get(itemId).DisplayName}";
            }

            if (!Enum.TryParse<BlockType>(parts[1], true, out var blockType) || blockType == BlockType.Air)
                return $"Unknown block: {parts[1]}";

            int count = 64;
            if (parts.Length >= 3 && (!int.TryParse(parts[2], out count) || count <= 0))
                return "Invalid count";

            session.Player.GiveBlocks(blockType, count);
            return $"Gave {count}x {blockType}";
        }

        private static string HandleHealth(GameSession session, string[] parts)
        {
            if (parts.Length >= 2 && TryParseFloat(parts[1], out float health))
            {
                session.Player.Health = Math.Clamp(health, 0f, session.Player.MaxHealth);
            }
            else
            {
                session.Player.Health = session.Player.MaxHealth;
            }

            return $"Health: {session.Player.Health:F0}/{session.Player.MaxHealth:F0}";
        }

        private static string HandleDamage(GameSession session, string[] parts)
        {
            float amount = 1f;
            if (parts.Length >= 2 && (!TryParseFloat(parts[1], out amount) || amount <= 0f))
            {
                return "Usage: damage [amount]";
            }

            bool applied = session.Player.TakeDamage(amount);
            return applied
                ? $"Took {amount:F0} damage. Health: {session.Player.Health:F0}/{session.Player.MaxHealth:F0}"
                : $"No damage applied. Health: {session.Player.Health:F0}/{session.Player.MaxHealth:F0}";
        }

        private static string HandleSpeed(GameHostContext host, string[] parts)
        {
            if (parts.Length < 2 || !TryParseFloat(parts[1], out float speed) || speed <= 0f)
                return "Usage: speed <number>";

            host.SetMoveSpeedOverride?.Invoke(speed);
            return $"Move speed set to {speed:F1}";
        }

        private static string HandleInventory(GameSession session)
        {
            var lines = new string[9];
            for (int i = 0; i < 9; i++)
            {
                var item = session.Player.Hotbar[i];
                string marker = i == session.Player.SelectedSlot ? "*" : " ";
                lines[i] = item.IsEmpty
                    ? $"{marker} [{i}] empty"
                    : $"{marker} [{i}] {FormatStack(item)}";
            }

            return "Hotbar:\n" + string.Join("\n", lines);
        }

        private static string HandleChunks(GameSession session)
        {
            var pos = session.Player.Position;
            int cx = (int)MathF.Floor(pos.X) >> 4;
            int cy = (int)MathF.Floor(pos.Y) >> 4;
            int cz = (int)MathF.Floor(pos.Z) >> 4;
            return $"Active chunks: {session.Grid.ActiveChunkCount} | player chunk: ({cx}, {cy}, {cz}) | seed: {session.Grid.Seed}";
        }

        private static string HandlePerf(GameSession session)
        {
            return string.Join("\n", new[]
            {
                "PERF COUNTERS:",
                $"  FPS (rolling): {RuntimeMetrics.RollingFps:F1}",
                $"  UpdateMs: {PerfCounters.LastUpdateMs:F2}",
                $"  DrawMs: {PerfCounters.LastDrawMs:F2}",
                $"  PeakUpdateMs: {PerfCounters.PeakUpdateMs:F2}",
                $"  PeakDrawMs: {PerfCounters.PeakDrawMs:F2}",
                $"  GetBlockCalls: {PerfCounters.GetBlockCalls}",
                $"  RaycastBlockVisits: {PerfCounters.RaycastBlockVisits}",
                $"  TerrainDrawCalls: {PerfCounters.TerrainDrawCalls}",
                $"  TerrainOpaqueDrawCalls: {PerfCounters.TerrainOpaqueDrawCalls}",
                $"  TerrainWaterDrawCalls: {PerfCounters.TerrainWaterDrawCalls}",
                $"  TerrainCutoutDrawCalls: {PerfCounters.TerrainCutoutDrawCalls}",
                $"  FloraDrawCalls: {PerfCounters.FloraDrawCalls}",
                $"  FloraVertexCount: {PerfCounters.FloraVertexCount}",
                $"  FloraDrawMs: {PerfCounters.FloraDrawMs:F2}",
                $"  PendingMeshCount: {PerfCounters.PendingMeshCount}",
                $"  ChunksMeshedThisFrame: {PerfCounters.ChunksMeshedThisFrame}",
                $"  MeshBuildMs: {PerfCounters.MeshBuildMs:F2}",
                $"  LastFrameMeshBuildMs: {PerfCounters.LastFrameMeshBuildMs:F2}",
                $"  PeakMeshBuildMs: {PerfCounters.PeakMeshBuildMs:F2}",
                $"  ActiveChunks: {session.Grid.ActiveChunkCount}"
            });
        }

        private static string HandleSlot(GameSession session, string[] parts)
        {
            if (parts.Length < 2 || !int.TryParse(parts[1], out int slot) || slot < 0 || slot > 8)
                return "Usage: slot <0-8>";

            session.Player.SelectedSlot = slot;
            var item = session.Player.Hotbar[slot];
            return item.IsEmpty
                ? $"Selected slot {slot + 1} (empty)"
                : $"Selected slot {slot + 1}: {FormatStack(item)}";
        }

        private static string FormatStack(ItemStack stack)
        {
            if (stack.IsTool())
            {
                return $"{stack.GetDisplayName()} ({stack.Durability}/{stack.MaxDurability})";
            }

            return $"{stack.BlockType} x{stack.Count}";
        }

        private static string HandleSpawn(GameSession session, string[] parts)
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

            int spawned = session.Animals.SpawnInFrontOfPlayer(session.Player, session.Grid, type, count);
            return spawned == 0
                ? $"Failed to spawn {type}"
                : $"Spawned {spawned}x {type}";
        }

        private static string HandleRecipes(GameSession session)
        {
            var ids = session.Crafting.Journal.Export();
            if (ids.Count == 0)
            {
                return "No crafting discoveries yet.";
            }

            return "Unlocked:\n" + string.Join("\n", ids);
        }

        private static string HandleUnlock(GameSession session, string[] parts)
        {
            if (parts.Length < 2)
            {
                return "Usage: unlock <sigil:bench|recipe:plank|...>";
            }

            session.Crafting.Journal.Unlock(parts[1]);
            return $"Unlocked '{parts[1]}'.";
        }

        private static string HandleVillage(GameSession session)
        {
            var village = session.Villages.GetPrimaryVillage();
            if (village == null)
            {
                return "No village. Use 'recruit' after founding (press V in-game).";
            }

            return $"Village '{village.Name}' pop {village.Population}/{village.PopulationCap} tier {village.Tier} happiness {village.Happiness:F2}";
        }

        private static string HandleRecruit(GameSession session)
        {
            var village = session.Villages.GetPrimaryVillage();
            if (village == null)
            {
                int ax = (int)MathF.Floor(session.Player.Position.X);
                int az = (int)MathF.Floor(session.Player.Position.Z);
                if (!session.Villages.TryFoundVillage(session.Grid, "Dev Village", ax, az, out village))
                {
                    return "Could not found village.";
                }
            }

            return session.Villages.TryRecruit(village!) ? "Recruited villager." : "Recruit failed (need 4 oak planks, under cap).";
        }

        private static string HandleAssignJob(GameSession session, string[] parts)
        {
            if (parts.Length < 3)
            {
                return "Usage: assign <villager_id> <Idle|Gather|Build|Haul>";
            }

            if (!int.TryParse(parts[1], out int vid) || !Enum.TryParse<JobType>(parts[2], true, out var job))
            {
                return "Invalid villager id or job.";
            }

            var village = session.Villages.GetPrimaryVillage();
            if (village == null || !session.Villagers.TryGet(vid, out var villager))
            {
                return "Village or villager not found.";
            }

            return session.Villages.TryAssignJob(village, villager, job) ? $"Assigned {job}." : "Assign failed.";
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

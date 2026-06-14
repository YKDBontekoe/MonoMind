using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Autonocraft.Core;
using Autonocraft.Items;

namespace Autonocraft.World
{
    public static class WorldSaveManager
    {
        private const string WorldFileName = "world.json";
        private static string? _overrideSavesDirectory;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static void SetSavesDirectoryForTests(string? directory)
        {
            _overrideSavesDirectory = directory;
        }

        public static string GetSavesDirectory()
        {
            if (_overrideSavesDirectory != null)
            {
                return _overrideSavesDirectory;
            }

            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "Autonocraft", "saves");
        }

        public static string GetSlotDirectory(string slotId)
        {
            return Path.Combine(GetSavesDirectory(), slotId);
        }

        public static string GetWorldFilePath(string slotId)
        {
            return Path.Combine(GetSlotDirectory(slotId), WorldFileName);
        }

        public static List<SaveSlotInfo> ListSlots()
        {
            string savesDir = GetSavesDirectory();
            if (!Directory.Exists(savesDir))
            {
                return new List<SaveSlotInfo>();
            }

            var slots = new List<SaveSlotInfo>();
            foreach (string slotDir in Directory.GetDirectories(savesDir))
            {
                string slotId = Path.GetFileName(slotDir);
                string worldPath = Path.Combine(slotDir, WorldFileName);
                if (!File.Exists(worldPath))
                {
                    continue;
                }

                try
                {
                    var data = Load(slotId);
                    slots.Add(new SaveSlotInfo
                    {
                        SlotId = slotId,
                        SlotName = data.SlotName,
                        Seed = data.Seed,
                        SavedAt = data.SavedAt
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Save] Corrupt save slot '{slotId}': {ex.Message}");
                    slots.Add(new SaveSlotInfo
                    {
                        SlotId = slotId,
                        SlotName = $"{slotId} (corrupt)",
                        SavedAt = File.GetLastWriteTimeUtc(worldPath),
                        IsCorrupt = true
                    });
                }
            }

            return slots.OrderByDescending(s => s.SavedAt).ToList();
        }

        public static string CreateSlotId(string slotName)
        {
            string slug = Slugify(slotName);
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = "world";
            }

            string baseId = slug;
            int suffix = 1;
            while (Directory.Exists(GetSlotDirectory(baseId)))
            {
                baseId = $"{slug}-{suffix}";
                suffix++;
            }

            return baseId;
        }

        public static string GenerateDefaultSlotName()
        {
            var existing = ListSlots();
            int index = 1;
            while (existing.Any(s => string.Equals(s.SlotName, $"World {index}", StringComparison.OrdinalIgnoreCase)))
            {
                index++;
            }

            return $"World {index}";
        }

        public static WorldSaveData CreateNewWorldSaveData(string slotId, string slotName, int seed, int spawnX, int spawnZ)
        {
            return new WorldSaveData
            {
                Version = 4,
                SlotId = slotId,
                SlotName = slotName,
                Seed = seed,
                SavedAt = DateTime.UtcNow,
                Spawn = new SpawnSaveData { X = spawnX, Z = spawnZ },
                Player = CreateDefaultPlayerSaveData(spawnX, spawnZ),
                Time = new TimeSaveData(),
                Modifications = new List<BlockModification>(),
                UnlockedCraftingIds = new List<string>()
            };
        }

        public static WorldSaveData BuildFromSnapshot(SaveSnapshot snapshot)
        {
            return new WorldSaveData
            {
                Version = 6,
                SlotId = snapshot.SlotId,
                SlotName = snapshot.SlotName,
                Seed = snapshot.Seed,
                SavedAt = DateTime.UtcNow,
                Spawn = new SpawnSaveData { X = snapshot.SpawnX, Z = snapshot.SpawnZ },
                Time = snapshot.Time,
                Modifications = snapshot.Modifications,
                FluidModifications = snapshot.FluidModifications,
                UnlockedCraftingIds = snapshot.UnlockedCraftingIds,
                Villages = snapshot.Villages,
                Villagers = snapshot.Villagers,
                ClaimedAnchors = snapshot.ClaimedAnchors,
                Player = snapshot.Player
            };
        }

        [Obsolete("Use BuildFromSnapshot")]
        public static WorldSaveData BuildFromGame(string slotId, string slotName, AutonocraftGame game, VoxelWorld world)
        {
            var snapshot = game.Session.BuildSaveSnapshot(
                slotId, slotName, game.TimeOfDay, game.TimeScale, game.TimePaused,
                GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);
            return BuildFromSnapshot(snapshot);
        }

        public static void Save(WorldSaveData data)
        {
            string slotDir = GetSlotDirectory(data.SlotId);
            Directory.CreateDirectory(slotDir);
            data.SavedAt = DateTime.UtcNow;

            string json = JsonSerializer.Serialize(data, JsonOptions);
            string worldPath = GetWorldFilePath(data.SlotId);
            string tempPath = worldPath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, worldPath, overwrite: true);
        }

        public static WorldSaveData Load(string slotId)
        {
            string worldPath = GetWorldFilePath(slotId);
            if (!File.Exists(worldPath))
            {
                throw new FileNotFoundException($"Save slot not found: {slotId}", worldPath);
            }

            string json = File.ReadAllText(worldPath);
            var data = JsonSerializer.Deserialize<WorldSaveData>(json, JsonOptions)
                ?? throw new InvalidDataException($"Failed to deserialize save slot: {slotId}");

            data.SlotId = slotId;
            data.Modifications ??= new List<BlockModification>();
            data.FluidModifications ??= new List<FluidModification>();
            data.UnlockedCraftingIds ??= new List<string>();
            data.Villages ??= new List<VillageSaveData>();
            data.Villagers ??= new List<VillagerSaveData>();
            data.ClaimedAnchors ??= new List<ClaimedAnchorSaveData>();
            data.Player ??= CreateDefaultPlayerSaveData(data.Spawn.X, data.Spawn.Z);
            data.Time ??= new TimeSaveData();
            data.Spawn ??= new SpawnSaveData();
            MigrateSaveData(data);
            ValidateAndSanitize(data);
            return data;
        }

        internal static void ValidateAndSanitize(WorldSaveData data)
        {
            data.Player ??= CreateDefaultPlayerSaveData(data.Spawn.X, data.Spawn.Z);

            data.Player.MaxHealth = Math.Max(1, data.Player.MaxHealth);
            data.Player.Health = Math.Clamp(data.Player.Health, 0, data.Player.MaxHealth);
            data.Player.SelectedSlot = Math.Clamp(data.Player.SelectedSlot, 0, 8);

            if (data.Player.Hotbar == null)
            {
                data.Player.Hotbar = new List<InventorySlotSaveData>();
            }

            while (data.Player.Hotbar.Count < 9)
            {
                data.Player.Hotbar.Add(new InventorySlotSaveData());
            }

            if (data.Player.Hotbar.Count > 9)
            {
                data.Player.Hotbar = data.Player.Hotbar.GetRange(0, 9);
            }

            for (int i = 0; i < data.Player.Hotbar.Count; i++)
            {
                data.Player.Hotbar[i] = SanitizeHotbarSlot(data.Player.Hotbar[i]);
            }

            var validMods = new List<BlockModification>();
            foreach (var mod in data.Modifications)
            {
                if (mod.Y < 0 || mod.Y >= Chunk.Height)
                {
                    continue;
                }

                if (!Enum.IsDefined(typeof(BlockType), mod.Block))
                {
                    continue;
                }

                validMods.Add(mod);
            }

            data.Modifications = validMods;
        }

        private static InventorySlotSaveData SanitizeHotbarSlot(InventorySlotSaveData data)
        {
            var kind = (ItemKind)data.Kind;
            if (kind == ItemKind.Tool && data.ToolId != 0)
            {
                if (!ToolRegistry.TryGet((ItemId)data.ToolId, out _))
                {
                    return new InventorySlotSaveData();
                }
            }
            else if (kind == ItemKind.FluidContainer && data.ToolId != 0)
            {
                if (!ToolRegistry.TryGet((ItemId)data.ToolId, out _))
                {
                    return new InventorySlotSaveData();
                }
            }
            else if (kind == ItemKind.Block || (kind == ItemKind.Empty && data.Count > 0))
            {
                if (data.Count <= 0 || !Enum.IsDefined(typeof(BlockType), data.Block) || data.Block == (byte)BlockType.Air)
                {
                    return new InventorySlotSaveData();
                }
            }
            else if (data.Count > 0)
            {
                return new InventorySlotSaveData();
            }

            return data;
        }

        public static void DeleteSlot(string slotId)
        {
            string slotDir = GetSlotDirectory(slotId);
            if (Directory.Exists(slotDir))
            {
                Directory.Delete(slotDir, recursive: true);
            }
        }

        public static PlayerSaveData BuildPlayerSaveData(Player player)
        {
            return new PlayerSaveData
            {
                PosX = player.Position.X,
                PosY = player.Position.Y,
                PosZ = player.Position.Z,
                VelX = player.Velocity.X,
                VelY = player.Velocity.Y,
                VelZ = player.Velocity.Z,
                Yaw = player.Yaw,
                Pitch = player.Pitch,
                Health = player.Health,
                MaxHealth = player.MaxHealth,
                FlyingMode = player.FlyingMode,
                SelectedSlot = player.SelectedSlot,
                Hotbar = SerializeHotbar(player),
                MiningLevel = player.Skills.Mining.Level,
                MiningXp = player.Skills.Mining.Xp,
                WoodcuttingLevel = player.Skills.Woodcutting.Level,
                WoodcuttingXp = player.Skills.Woodcutting.Xp,
                CombatLevel = player.Skills.Combat.Level,
                CombatXp = player.Skills.Combat.Xp
            };
        }

        public static void ApplyPlayerSaveData(Player player, PlayerSaveData data)
        {
            player.Position = new System.Numerics.Vector3(data.PosX, data.PosY, data.PosZ);
            player.Velocity = new System.Numerics.Vector3(data.VelX, data.VelY, data.VelZ);
            player.Yaw = data.Yaw;
            player.Pitch = data.Pitch;
            player.MaxHealth = Math.Max(1, data.MaxHealth);
            player.Health = Math.Clamp(data.Health, 0, player.MaxHealth);
            player.FlyingMode = data.FlyingMode;
            player.SelectedSlot = Math.Clamp(data.SelectedSlot, 0, 8);

            for (int i = 0; i < player.Hotbar.Length; i++)
            {
                player.Hotbar[i] = i < data.Hotbar.Count
                    ? DeserializeHotbarSlot(data.Hotbar[i])
                    : ItemStack.Empty;
            }

            player.Skills.Mining = new SkillProgress { Level = data.MiningLevel > 0 ? data.MiningLevel : 1, Xp = data.MiningXp };
            player.Skills.Woodcutting = new SkillProgress { Level = data.WoodcuttingLevel > 0 ? data.WoodcuttingLevel : 1, Xp = data.WoodcuttingXp };
            player.Skills.Combat = new SkillProgress { Level = data.CombatLevel > 0 ? data.CombatLevel : 1, Xp = data.CombatXp };
        }

        private static List<InventorySlotSaveData> SerializeHotbar(Player player)
        {
            var hotbar = new List<InventorySlotSaveData>();
            for (int i = 0; i < player.Hotbar.Length; i++)
            {
                hotbar.Add(SerializeHotbarSlot(player.Hotbar[i]));
            }

            return hotbar;
        }

        private static InventorySlotSaveData SerializeHotbarSlot(ItemStack stack)
        {
            if (stack.IsEmpty)
            {
                return new InventorySlotSaveData();
            }

            if (stack.IsTool())
            {
                return new InventorySlotSaveData
                {
                    Kind = (byte)ItemKind.Tool,
                    ToolId = (ushort)stack.ToolId,
                    Count = stack.Count,
                    Durability = stack.Durability,
                    MaxDurability = stack.MaxDurability
                };
            }

            if (stack.IsFluidContainer())
            {
                return new InventorySlotSaveData
                {
                    Kind = (byte)ItemKind.FluidContainer,
                    ToolId = (ushort)stack.ToolId,
                    Count = 1
                };
            }

            return new InventorySlotSaveData
            {
                Kind = (byte)ItemKind.Block,
                Block = (byte)stack.BlockType,
                Count = stack.Count
            };
        }

        private static ItemStack DeserializeHotbarSlot(InventorySlotSaveData data)
        {
            var kind = (ItemKind)data.Kind;
            if (kind == ItemKind.FluidContainer && data.ToolId != 0)
            {
                return ItemStack.CreateFluidContainer((ItemId)data.ToolId);
            }

            if (kind == ItemKind.Tool && data.ToolId != 0)
            {
                var toolId = (ItemId)data.ToolId;
                if (!ToolRegistry.TryGet(toolId, out _))
                {
                    return ItemStack.Empty;
                }

                int maxDurability = data.MaxDurability > 0 ? data.MaxDurability : ToolRegistry.Get(toolId).MaxDurability;
                int durability = data.Durability > 0 ? data.Durability : maxDurability;
                return new ItemStack
                {
                    Kind = ItemKind.Tool,
                    ToolId = toolId,
                    Count = Math.Max(1, data.Count),
                    Durability = durability,
                    MaxDurability = maxDurability
                };
            }

            if (data.Count <= 0 || data.Block == (byte)BlockType.Air || !Enum.IsDefined(typeof(BlockType), data.Block))
            {
                return ItemStack.Empty;
            }

            return ItemStack.CreateBlock((BlockType)data.Block, data.Count);
        }

        private static void MigrateSaveData(WorldSaveData data)
        {
            if (data.Version < 3)
            {
                foreach (var slot in data.Player.Hotbar)
                {
                    if (slot.Kind == 0 && slot.Count > 0)
                    {
                        slot.Kind = (byte)ItemKind.Block;
                    }
                }

                data.Version = 3;
            }

            if (data.Version < 5)
            {
                data.Villages ??= new List<VillageSaveData>();
                data.Villagers ??= new List<VillagerSaveData>();
                data.Version = 5;
            }

            if (data.Version < 6)
            {
                data.ClaimedAnchors ??= new List<ClaimedAnchorSaveData>();
                foreach (var village in data.Villages)
                {
                    village.Buildings ??= new List<BuildingSaveData>();
                    village.BuildingSites ??= new List<BuildingSiteSaveData>();
                }

                foreach (var villager in data.Villagers)
                {
                    villager.Inventory ??= new List<InventorySlotSaveData>();
                }

                data.Version = 6;
            }
        }

        public static InventorySlotSaveData SerializeItemStack(ItemStack stack) => SerializeHotbarSlot(stack);

        public static ItemStack DeserializeItemStack(InventorySlotSaveData data) => DeserializeHotbarSlot(data);

        private static PlayerSaveData CreateDefaultPlayerSaveData(int spawnX, int spawnZ)
        {
            var player = new Player(new System.Numerics.Vector3(spawnX + 0.5f, 64f, spawnZ + 0.5f));
            return new PlayerSaveData
            {
                PosX = player.Position.X,
                PosY = player.Position.Y,
                PosZ = player.Position.Z,
                Yaw = player.Yaw,
                Pitch = player.Pitch,
                Health = player.Health,
                MaxHealth = player.MaxHealth,
                Hotbar = SerializeHotbar(player)
            };
        }

        private static string Slugify(string value)
        {
            var chars = value.Trim().ToLowerInvariant()
                .Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-')
                .ToArray();

            string slug = new string(chars).Replace(' ', '-');
            while (slug.Contains("--", StringComparison.Ordinal))
            {
                slug = slug.Replace("--", "-", StringComparison.Ordinal);
            }

            return slug.Trim('-');
        }
    }
}

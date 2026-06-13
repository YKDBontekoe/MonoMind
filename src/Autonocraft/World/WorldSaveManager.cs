using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Autonocraft.Core;

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
                        SavedAt = data.SavedAt
                    });
                }
                catch
                {
                    slots.Add(new SaveSlotInfo
                    {
                        SlotId = slotId,
                        SlotName = slotId,
                        SavedAt = File.GetLastWriteTimeUtc(worldPath)
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
                Version = 1,
                SlotId = slotId,
                SlotName = slotName,
                Seed = seed,
                SavedAt = DateTime.UtcNow,
                Spawn = new SpawnSaveData { X = spawnX, Z = spawnZ },
                Player = CreateDefaultPlayerSaveData(spawnX, spawnZ),
                Time = new TimeSaveData(),
                Modifications = new List<BlockModification>()
            };
        }

        public static WorldSaveData BuildFromGame(string slotId, string slotName, AutonocraftGame game, VoxelWorld world)
        {
            var player = game.Player;
            var save = new WorldSaveData
            {
                Version = 1,
                SlotId = slotId,
                SlotName = slotName,
                Seed = world.Seed,
                SavedAt = DateTime.UtcNow,
                Spawn = new SpawnSaveData { X = AutonocraftGame.DefaultSpawnX, Z = AutonocraftGame.DefaultSpawnZ },
                Time = new TimeSaveData
                {
                    TimeOfDay = game.TimeOfDay,
                    TimeScale = game.TimeScale,
                    TimePaused = game.TimePaused
                },
                Modifications = world.ExportModifications()
            };

            save.Player = new PlayerSaveData
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
                Hotbar = new List<InventorySlotSaveData>()
            };

            for (int i = 0; i < player.Hotbar.Length; i++)
            {
                save.Player.Hotbar.Add(new InventorySlotSaveData
                {
                    Block = (byte)player.Hotbar[i].Type,
                    Count = player.Hotbar[i].Count
                });
            }

            return save;
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
            data.Player ??= CreateDefaultPlayerSaveData(data.Spawn.X, data.Spawn.Z);
            data.Time ??= new TimeSaveData();
            data.Spawn ??= new SpawnSaveData();
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

        public static void ApplyPlayerSaveData(Player player, PlayerSaveData data)
        {
            player.Position = new System.Numerics.Vector3(data.PosX, data.PosY, data.PosZ);
            player.Velocity = new System.Numerics.Vector3(data.VelX, data.VelY, data.VelZ);
            player.Yaw = data.Yaw;
            player.Pitch = data.Pitch;
            player.Health = data.Health;
            player.MaxHealth = data.MaxHealth;
            player.FlyingMode = data.FlyingMode;
            player.SelectedSlot = data.SelectedSlot;

            for (int i = 0; i < player.Hotbar.Length; i++)
            {
                if (i < data.Hotbar.Count)
                {
                    player.Hotbar[i] = new Player.InventorySlot
                    {
                        Type = (BlockType)data.Hotbar[i].Block,
                        Count = data.Hotbar[i].Count
                    };
                }
                else
                {
                    player.Hotbar[i] = new Player.InventorySlot { Type = BlockType.Air, Count = 0 };
                }
            }
        }

        private static PlayerSaveData CreateDefaultPlayerSaveData(int spawnX, int spawnZ)
        {
            var player = new Player(new System.Numerics.Vector3(spawnX + 0.5f, 64f, spawnZ + 0.5f));
            var data = new PlayerSaveData
            {
                PosX = player.Position.X,
                PosY = player.Position.Y,
                PosZ = player.Position.Z,
                Yaw = player.Yaw,
                Pitch = player.Pitch,
                Health = player.Health,
                MaxHealth = player.MaxHealth,
                Hotbar = new List<InventorySlotSaveData>()
            };

            for (int i = 0; i < player.Hotbar.Length; i++)
            {
                data.Hotbar.Add(new InventorySlotSaveData
                {
                    Block = (byte)player.Hotbar[i].Type,
                    Count = player.Hotbar[i].Count
                });
            }

            return data;
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

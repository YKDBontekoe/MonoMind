using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Domain.World;
using Autonocraft.Village;
using Autonocraft.World;

namespace Autonocraft.Entities
{
    public sealed class VillagerManager
    {
        private readonly List<Villager> _villagers = new();
        private readonly Dictionary<int, Villager> _villagerIndex = new();
        private readonly List<Villager> _rangeScratch = new();

        public VoxelWorld? World { get; private set; }

        public IReadOnlyList<Villager> All => _villagers;

        public Villager Spawn(int villageId, Vector3 position, int seed)
        {
            var villager = new Villager(villageId, position, seed);
            _villagers.Add(villager);
            _villagerIndex[villager.Id] = villager;
            return villager;
        }

        public void Despawn(int villagerId)
        {
            if (_villagerIndex.TryGetValue(villagerId, out var villager))
            {
                _villagers.Remove(villager);
                _villagerIndex.Remove(villagerId);
            }
        }

        public bool TryGet(int id, out Villager villager) =>
            _villagerIndex.TryGetValue(id, out villager!);

        public List<Villager> GetVillagersInRange(Vector3 position, float range)
        {
            _rangeScratch.Clear();
            float rangeSq = range * range;
            foreach (var villager in _villagers)
            {
                if (Vector3.DistanceSquared(villager.Position, position) <= rangeSq)
                {
                    _rangeScratch.Add(villager);
                }
            }

            return _rangeScratch;
        }

        public Villager? GetNearest(Vector3 position, float maxRange)
        {
            Villager? nearest = null;
            float best = maxRange * maxRange;
            foreach (var villager in _villagers)
            {
                float dist = Vector3.DistanceSquared(villager.Position, position);
                if (dist <= best)
                {
                    best = dist;
                    nearest = villager;
                }
            }

            return nearest;
        }

        public void Update(float deltaTime, VoxelWorld world, IReadOnlyList<Village.Village> villages)
        {
            World = world;
            foreach (var villager in _villagers)
            {
                RecoverIfOutOfWorld(villager, world, villages);
            }
        }

        private static void RecoverIfOutOfWorld(Villager villager, VoxelWorld world, IReadOnlyList<Village.Village> villages)
        {
            if (float.IsFinite(villager.Position.X) &&
                float.IsFinite(villager.Position.Y) &&
                float.IsFinite(villager.Position.Z) &&
                villager.Position.Y >= WorldConstants.BedrockLevel - 8f)
            {
                return;
            }

            var fallback = villager.Position;
            foreach (var village in villages)
            {
                if (village.Id == villager.VillageId)
                {
                    fallback = village.Center;
                    break;
                }
            }

            int x = (int)MathF.Floor(fallback.X);
            int z = (int)MathF.Floor(fallback.Z);
            int surfaceY = world.GetHighestSolidY(x, z);
            if (surfaceY < WorldConstants.BedrockLevel)
            {
                surfaceY = WorldConstants.SeaLevel;
            }

            villager.Position = new Vector3(x + 0.5f, surfaceY + 1f, z + 0.5f);
            villager.Velocity = Vector3.Zero;
            villager.IsGrounded = false;
            villager.OnBlocked();
        }

        public void LoadVillagers(IEnumerable<VillagerSaveData> data)
        {
            _villagers.Clear();
            _villagerIndex.Clear();
            foreach (var entry in data)
            {
                var villager = new Villager(
                    entry.VillageId,
                    new Vector3(entry.PosX, entry.PosY, entry.PosZ),
                    entry.Id,
                    entry.Name,
                    entry.Id);
                villager.Role = (VillagerRole)entry.Role;
                var job = (JobType)entry.Job;
                if (job == JobType.Gather)
                {
                    job = JobType.Lumber;
                }

                villager.AssignJob(job, null, entry.BuildingSiteId, entry.AssignedBuildingId);
                villager.RestoreHaulState(entry.HaulSourceChestId, entry.HaulSourceVillagerId, entry.HaulIsDelivering);
                villager.RestoreAiPhase((VillagerAiPhase)entry.AiPhase);
                villager.Yaw = entry.Yaw;
                villager.HomeBuildingId = entry.HomeBuildingId;
                if (entry.MarkedResourceX.HasValue && entry.MarkedResourceY.HasValue && entry.MarkedResourceZ.HasValue)
                {
                    villager.MarkedResource = new Vector3(
                        entry.MarkedResourceX.Value,
                        entry.MarkedResourceY.Value,
                        entry.MarkedResourceZ.Value);
                }

                if (entry.EquippedTool != null)
                {
                    villager.SetEquippedTool(WorldSaveManager.DeserializeItemStack(entry.EquippedTool));
                }

                villager.Happiness = entry.Happiness;
                villager.RestoreNeeds(entry.NeedFood, entry.NeedRest, entry.NeedSocial);
                villager.RestorePersona(entry.Trait);
                villager.RestoreSkills(
                    entry.MiningLevel,
                    entry.MiningXp,
                    entry.WoodcuttingLevel,
                    entry.WoodcuttingXp,
                    entry.FarmingLevel,
                    entry.FarmingXp);

                if (entry.Inventory != null)
                {
                    for (int i = 0; i < entry.Inventory.Count && i < villager.Inventory.SlotCount; i++)
                    {
                        villager.Inventory.SetSlot(i, WorldSaveManager.DeserializeItemStack(entry.Inventory[i]));
                    }
                }

                _villagers.Add(villager);
                _villagerIndex[villager.Id] = villager;
            }
        }

        public List<VillagerSaveData> ExportVillagers()
        {
            var result = new List<VillagerSaveData>();
            foreach (var villager in _villagers)
            {
                var inventory = new List<InventorySlotSaveData>();
                for (int i = 0; i < villager.Inventory.SlotCount; i++)
                {
                    inventory.Add(WorldSaveManager.SerializeItemStack(villager.Inventory.GetSlot(i)));
                }

                result.Add(new VillagerSaveData
                {
                    Id = villager.Id,
                    VillageId = villager.VillageId,
                    Name = villager.Name,
                    Role = (int)villager.Role,
                    Job = (int)villager.CurrentJob,
                    PosX = villager.Position.X,
                    PosY = villager.Position.Y,
                    PosZ = villager.Position.Z,
                    Happiness = villager.Happiness,
                    NeedFood = villager.Needs.Food,
                    NeedRest = villager.Needs.Rest,
                    NeedSocial = villager.Needs.Social,
                    Trait = villager.Persona.Trait,
                    MiningLevel = villager.Skills.Mining.Level,
                    MiningXp = villager.Skills.Mining.Xp,
                    WoodcuttingLevel = villager.Skills.Woodcutting.Level,
                    WoodcuttingXp = villager.Skills.Woodcutting.Xp,
                    FarmingLevel = villager.Skills.Farming.Level,
                    FarmingXp = villager.Skills.Farming.Xp,
                    BuildingSiteId = villager.AssignedBuildingSiteId,
                    AssignedBuildingId = villager.AssignedBuildingId,
                    HaulSourceChestId = villager.HaulSourceChestId,
                    HaulSourceVillagerId = villager.HaulSourceVillagerId,
                    HaulIsDelivering = villager.HaulIsDelivering,
                    MarkedResourceX = villager.MarkedResource?.X,
                    MarkedResourceY = villager.MarkedResource?.Y,
                    MarkedResourceZ = villager.MarkedResource?.Z,
                    HomeBuildingId = villager.HomeBuildingId,
                    Yaw = villager.Yaw,
                    AiPhase = (int)villager.AiPhase,
                    EquippedTool = villager.EquippedTool.IsEmpty
                        ? null
                        : WorldSaveManager.SerializeItemStack(villager.EquippedTool),
                    Inventory = inventory
                });
            }

            return result;
        }
    }
}

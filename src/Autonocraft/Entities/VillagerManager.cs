using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Village;
using Autonocraft.World;

namespace Autonocraft.Entities
{
    public sealed class VillagerManager
    {
        private readonly List<Villager> _villagers = new();
        private readonly List<Villager> _rangeScratch = new();

        public VoxelWorld? World { get; private set; }

        public IReadOnlyList<Villager> All => _villagers;

        public Villager Spawn(int villageId, Vector3 position, int seed)
        {
            var villager = new Villager(villageId, position, seed);
            _villagers.Add(villager);
            return villager;
        }

        public bool TryGet(int id, out Villager villager)
        {
            foreach (var entry in _villagers)
            {
                if (entry.Id == id)
                {
                    villager = entry;
                    return true;
                }
            }

            villager = null!;
            return false;
        }

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
            for (int i = _villagers.Count - 1; i >= 0; i--)
            {
                var villager = _villagers[i];
                var prevX = villager.Position.X;
                var prevZ = villager.Position.Z;

                if (!villager.IsGrounded && villager.Velocity.X == 0f && villager.Velocity.Z == 0f &&
                    (MathF.Abs(villager.Position.X - prevX) < 0.001f && MathF.Abs(villager.Position.Z - prevZ) < 0.001f) &&
                    villager.WanderDirection != Vector3.Zero)
                {
                    villager.OnBlocked();
                }
            }
        }

        public void LoadVillagers(IEnumerable<VillagerSaveData> data)
        {
            _villagers.Clear();
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
                villager.Happiness = entry.Happiness;
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
                    Trait = villager.Persona.Trait,
                    MiningLevel = villager.Skills.Mining.Level,
                    MiningXp = villager.Skills.Mining.Xp,
                    WoodcuttingLevel = villager.Skills.Woodcutting.Level,
                    WoodcuttingXp = villager.Skills.Woodcutting.Xp,
                    FarmingLevel = villager.Skills.Farming.Level,
                    FarmingXp = villager.Skills.Farming.Xp,
                    BuildingSiteId = villager.AssignedBuildingSiteId,
                    AssignedBuildingId = villager.AssignedBuildingId,
                    Inventory = inventory
                });
            }

            return result;
        }
    }
}

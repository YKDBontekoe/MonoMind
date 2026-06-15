using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.Core;
using Autonocraft.Domain.Core;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public sealed class VillageBuilding
    {
        public int Id { get; set; }
        public string BlueprintId { get; set; } = string.Empty;
        public BuildingKind Kind { get; set; }
        public int AnchorX { get; set; }
        public int AnchorY { get; set; }
        public int AnchorZ { get; set; }
        public bool IsComplete { get; set; } = true;
    }

    public sealed class Village
    {
        private static int _nextId = 1;

        public int Id { get; private set; }
        public string Name { get; set; }
        public int AnchorX { get; }
        public int AnchorY { get; }
        public int AnchorZ { get; }
        public float Radius { get; set; } = 32f;
        public VillageTier Tier { get; set; } = VillageTier.Hamlet;
        public VillageStorage Storage { get; }
        public JobScheduler Scheduler { get; } = new();

        public int HousingCapacity { get; set; }
        public int PopulationCap { get; set; } = 2;
        public int Population => _villagerIds.Count;
        public float FoodStock { get; set; }
        public float FoodConsumptionPerDay { get; set; } = 1f;
        public float Happiness { get; set; } = 1f;
        public float DayAccumulator { get; set; }
        public float FarmProductionTimer { get; set; }
        public int LowFoodDayStreak { get; set; }

        private readonly List<int> _villagerIds = new();
        private readonly List<VillageBuilding> _buildings = new();
        private readonly List<BuildingSite> _buildingSites = new();
        private readonly Dictionary<int, BuildingSite> _sitesById = new();

        public IReadOnlyList<VillageBuilding> Buildings => _buildings;
        public IReadOnlyList<BuildingSite> BuildingSites => _buildingSites;
        public IReadOnlyList<int> VillagerIds => _villagerIds;

        public Vector3 Center => new Vector3(AnchorX + 0.5f, AnchorY, AnchorZ + 0.5f);
        public Vector3 StoragePosition => Center;

        public const int RecruitFoodCost = 4;
        public static readonly BlockType RationBlock = BlockType.OakPlank;
        public const float FarmFoodPerMinute = 1f;
        public const float FarmProductionInterval = 60f;

        public Village(string name, int anchorX, int anchorY, int anchorZ, int storageSlots = 9, int? explicitId = null)
        {
            Id = explicitId ?? _nextId++;
            if (explicitId.HasValue && explicitId.Value >= _nextId)
            {
                _nextId = explicitId.Value + 1;
            }

            Name = name;
            AnchorX = anchorX;
            AnchorY = anchorY;
            AnchorZ = anchorZ;
            Storage = new VillageStorage(storageSlots);
            PopulationCap = 2;
        }

        public static void ResetIdCounter(int nextId) => _nextId = Math.Max(1, nextId);

        public bool Contains(Vector3 position)
        {
            var offset = position - Center;
            offset.Y = 0f;
            return offset.Length() <= Radius;
        }

        public bool CanRecruit()
        {
            return Population < PopulationCap && Storage.CountBlock(RationBlock) >= RecruitFoodCost;
        }

        public bool TryRecruitCost()
        {
            if (!CanRecruit())
            {
                return false;
            }

            return Storage.TryConsumeBlock(RationBlock, RecruitFoodCost);
        }

        public void RegisterVillager(int villagerId)
        {
            if (!_villagerIds.Contains(villagerId))
            {
                _villagerIds.Add(villagerId);
            }
        }

        public void UnregisterVillager(int villagerId) => _villagerIds.Remove(villagerId);

        public BuildingSite? QueueBuild(BuildingBlueprint blueprint, int anchorX, int anchorY, int anchorZ)
        {
            var site = new BuildingSite(Id, blueprint, anchorX, anchorY, anchorZ);
            _buildingSites.Add(site);
            _sitesById[site.Id] = site;
            return site;
        }

        public BuildingSite? RestoreBuildingSite(BuildingSiteSaveData entry, BuildingBlueprint blueprint)
        {
            var site = BuildingSite.Restore(entry, blueprint);
            _buildingSites.Add(site);
            _sitesById[site.Id] = site;
            return site;
        }

        public void RestoreBuilding(BuildingSaveData entry)
        {
            var building = new VillageBuilding
            {
                Id = entry.Id,
                BlueprintId = entry.BlueprintId,
                Kind = (BuildingKind)entry.Kind,
                AnchorX = entry.AnchorX,
                AnchorY = entry.AnchorY,
                AnchorZ = entry.AnchorZ,
                IsComplete = entry.IsComplete
            };
            _buildings.Add(building);
        }

        public bool TryGetBuildingSite(int siteId, out BuildingSite site)
            => _sitesById.TryGetValue(siteId, out site!);

        public BuildingSite? GetNearestPendingSite(Vector3 from)
        {
            BuildingSite? nearest = null;
            float best = float.MaxValue;
            foreach (var site in _buildingSites)
            {
                if (site.IsComplete)
                {
                    continue;
                }

                var pos = new Vector3(site.AnchorX + 0.5f, site.AnchorY, site.AnchorZ + 0.5f);
                float dist = Vector3.DistanceSquared(from, pos);
                if (dist < best)
                {
                    best = dist;
                    nearest = site;
                }
            }

            return nearest;
        }

        public int CountBuildings(BuildingKind kind)
        {
            int count = 0;
            foreach (var building in _buildings)
            {
                if (building.Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        public void CompleteBuilding(BuildingBlueprint blueprint, BuildingSite site)
        {
            var building = new VillageBuilding
            {
                Id = _buildings.Count + 1,
                BlueprintId = blueprint.Id,
                Kind = blueprint.Kind,
                AnchorX = site.AnchorX,
                AnchorY = site.AnchorY,
                AnchorZ = site.AnchorZ,
                IsComplete = true
            };
            _buildings.Add(building);
            HousingCapacity += blueprint.HousingProvided;
            PopulationCap += blueprint.PopulationCapBonus;
            if (blueprint.StorageSlots > 0)
            {
                Storage.ExpandSlots(blueprint.StorageSlots);
            }

            UpdateTier();
        }

        public void RegisterClaimedBuilding(BuildingBlueprint blueprint, int anchorX, int anchorY, int anchorZ)
        {
            var building = new VillageBuilding
            {
                Id = _buildings.Count + 1,
                BlueprintId = blueprint.Id,
                Kind = blueprint.Kind,
                AnchorX = anchorX,
                AnchorY = anchorY,
                AnchorZ = anchorZ,
                IsComplete = true
            };
            _buildings.Add(building);
            HousingCapacity += blueprint.HousingProvided;
            PopulationCap += blueprint.PopulationCapBonus;
            if (blueprint.StorageSlots > 0)
            {
                Storage.ExpandSlots(blueprint.StorageSlots);
            }

            UpdateTier();
        }

        public void UpdateSimulation(float deltaTime, float timeOfDay)
        {
            UpdateFarmProduction(deltaTime);

            DayAccumulator += deltaTime;
            if (DayAccumulator >= 120f)
            {
                DayAccumulator = 0f;
                SimulateDailyNeeds();
            }

            bool isNight = DayNightCycle.IsNight(timeOfDay);
            if (isNight && FoodStock <= 0f)
            {
                Happiness = MathF.Max(0.2f, Happiness - 0.05f);
            }
            else if (FoodStock > Population)
            {
                Happiness = MathF.Min(1f, Happiness + 0.02f);
            }
        }

        private void UpdateFarmProduction(float deltaTime)
        {
            int farmPlots = CountBuildings(BuildingKind.FarmPlot);
            if (farmPlots <= 0)
            {
                return;
            }

            FarmProductionTimer += deltaTime;
            if (FarmProductionTimer < FarmProductionInterval)
            {
                return;
            }

            FarmProductionTimer = 0f;
            FoodStock += farmPlots * FarmFoodPerMinute;
            Storage.AddItem(ItemStack.CreateConsumable(ItemId.Bread, farmPlots));
        }

        private void SimulateDailyNeeds()
        {
            float need = Population * FoodConsumptionPerDay;
            if (FoodStock >= need)
            {
                FoodStock -= need;
                LowFoodDayStreak = 0;
            }
            else
            {
                FoodStock = 0f;
                Happiness = MathF.Max(0.1f, Happiness - 0.1f);
                LowFoodDayStreak++;
            }
        }

        public bool TryTakeRation(Player player)
        {
            if (FoodStock < 1f)
            {
                return false;
            }

            FoodStock -= 1f;
            player.AddItem(ItemStack.CreateConsumable(ItemId.VillageRation, 1));
            return true;
        }

        public float GetWorkSpeedMultiplier()
        {
            float mult = Happiness;
            if (FoodStock <= 0f)
            {
                mult *= 0.5f;
            }

            if (Population > HousingCapacity && HousingCapacity > 0)
            {
                mult *= 0.75f;
            }

            return Math.Clamp(mult, 0.25f, 1.5f);
        }

        public void UpdateTier()
        {
            VillageTier target = Population switch
            {
                >= 13 => VillageTier.Town,
                >= 5 => VillageTier.Village,
                _ => VillageTier.Hamlet
            };

            while (target > VillageTier.Hamlet && !MeetsTierRequirements(target))
            {
                target = target switch
                {
                    VillageTier.Town => VillageTier.Village,
                    VillageTier.Village => VillageTier.Hamlet,
                    _ => VillageTier.Hamlet
                };
            }

            Tier = target;
        }

        public void AddFarmFood(float amount) => FoodStock += amount;

        public bool MeetsTierRequirements(VillageTier target)
        {
            int houses = CountBuildings(BuildingKind.House);
            return target switch
            {
                VillageTier.Village => houses >= 2 && Storage.SlotCount >= 9,
                VillageTier.Town => houses >= 4 && _buildings.Exists(b => b.Kind == BuildingKind.Workshop),
                _ => true
            };
        }
    }
}

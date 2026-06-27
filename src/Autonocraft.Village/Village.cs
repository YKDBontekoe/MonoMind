using System;
using System.Collections.Generic;
using System.Numerics;
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
        public GatherWorkQueue WorkQueue { get; } = new();
        public VillageEconomy Economy { get; } = new VillageEconomy();

        public int HousingCapacity { get; set; }
        public int PopulationCap { get; set; } = 2;
        public int Population => _villagerIds.Count;
        public float FoodStock { get; set; }
        public float FoodConsumptionPerDay { get; set; } = 1f;
        public float Happiness { get; set; } = 1f;
        public int Favor { get; set; }
        public float FamilyGrowthProgress { get; set; }
        public float DayAccumulator { get; set; }
        public float FarmGrowthAccumulator { get; set; }
        public int ConsecutiveDaysWithoutFood { get; set; }
        public bool DailyNeedsSimulatedThisFrame { get; private set; }

        private readonly List<int> _villagerIds = new();
        private readonly List<VillageBuilding> _buildings = new();
        private readonly List<BuildingSite> _buildingSites = new();
        private readonly Dictionary<int, BuildingSite> _sitesById = new();
        private readonly List<OutputChest> _outputChests = new();
        private readonly Dictionary<int, OutputChest> _outputChestsByBuildingId = new();

        public IReadOnlyList<VillageBuilding> Buildings => _buildings;
        public IReadOnlyList<BuildingSite> BuildingSites => _buildingSites;
        public IReadOnlyList<int> VillagerIds => _villagerIds;
        public IReadOnlyList<OutputChest> OutputChests => _outputChests;

        public Vector3 Center => new Vector3(AnchorX + 0.5f, AnchorY, AnchorZ + 0.5f);
        public Vector3 StoragePosition => Center;

        public const int RecruitFoodCost = 4;
        public const float FamilyArrivalFoodCost = 3f;
        public static readonly BlockType RationBlock = BlockType.OakPlank;

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
            Favor = 12; // Start with 12 Favor so the Steward can buy the first building commissions!
        }

        public static void ResetIdCounter(int nextId) => _nextId = Math.Max(1, nextId);

        public bool Contains(Vector3 position)
        {
            var offset = position - Center;
            offset.Y = 0f;
            return offset.Length() <= Radius;
        }

        public bool CanRecruit(bool creative = false)
        {
            if (Population == 0)
            {
                return false;
            }

            if (Population >= EffectiveRecruitmentCap)
            {
                return false;
            }

            return creative || Storage.CountBlock(RationBlock) >= RecruitFoodCost;
        }

        public bool CanRecruit(VillagerManager villagers, bool creative = false)
        {
            VillageSettlementHealth.SyncPopulationRegistry(this, villagers);
            if (VillageSettlementHealth.GetLivePopulation(this, villagers) == 0)
            {
                return false;
            }

            return CanRecruit(creative);
        }

        public int EffectiveRecruitmentCap => HousingCapacity > 0
            ? Math.Min(PopulationCap, HousingCapacity)
            : PopulationCap;

        public bool TryRecruitCost(bool creative = false)
        {
            if (!CanRecruit(creative))
            {
                return false;
            }

            if (creative)
            {
                return true;
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

        public void ReconcileVillagerRegistry(IReadOnlyList<Villager> villagers)
        {
            for (int i = _villagerIds.Count - 1; i >= 0; i--)
            {
                if (!ContainsVillagerId(villagers, _villagerIds[i]))
                {
                    _villagerIds.RemoveAt(i);
                }
            }

            foreach (var villager in villagers)
            {
                if (villager.VillageId == Id)
                {
                    RegisterVillager(villager.Id);
                }
            }
        }

        private static bool ContainsVillagerId(IReadOnlyList<Villager> villagers, int villagerId)
        {
            foreach (var villager in villagers)
            {
                if (villager.Id == villagerId)
                {
                    return true;
                }
            }

            return false;
        }

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

        public bool RemoveBuildingSite(int siteId)
        {
            if (!_sitesById.Remove(siteId, out var site))
            {
                return false;
            }

            _buildingSites.Remove(site);
            return true;
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
            if (entry.IsComplete && PlayerStructureRegistry.TryGet(entry.BlueprintId, out var blueprint))
            {
                RegisterOutputChestIfNeeded(building, blueprint);
            }
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

        public BuildingSite? GetLatestPendingSite(string? blueprintId = null)
        {
            for (int i = _buildingSites.Count - 1; i >= 0; i--)
            {
                var site = _buildingSites[i];
                if (site.IsComplete)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(blueprintId) &&
                    !string.Equals(site.BlueprintId, blueprintId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return site;
            }

            return null;
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

        public bool HasBuilding(BuildingKind kind) => CountBuildings(kind) > 0;

        public bool HasCompletedBuilding(string blueprintId)
        {
            foreach (var building in _buildings)
            {
                if (building.IsComplete &&
                    string.Equals(building.BlueprintId, blueprintId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public int CountCompletedBuildings(string blueprintId)
        {
            int count = 0;
            foreach (var building in _buildings)
            {
                if (building.IsComplete &&
                    string.Equals(building.BlueprintId, blueprintId, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        public int CountPendingSites(string blueprintId)
        {
            int count = 0;
            foreach (var site in _buildingSites)
            {
                if (string.Equals(site.BlueprintId, blueprintId, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        public bool HasCompletedBuildingAt(string blueprintId, int anchorX, int anchorY, int anchorZ)
        {
            foreach (var building in _buildings)
            {
                if (building.IsComplete &&
                    string.Equals(building.BlueprintId, blueprintId, StringComparison.OrdinalIgnoreCase) &&
                    building.AnchorX == anchorX &&
                    building.AnchorY == anchorY &&
                    building.AnchorZ == anchorZ)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasPendingOrCompleteBuilding(string blueprintId)
        {
            if (HasCompletedBuilding(blueprintId))
            {
                return true;
            }

            foreach (var site in _buildingSites)
            {
                if (string.Equals(site.BlueprintId, blueprintId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsProtectedStructureBlock(int x, int y, int z, BlockType blockType)
        {
            if (blockType == BlockType.Air)
            {
                return false;
            }

            foreach (var building in _buildings)
            {
                if (MatchesProtectedBlueprintBlock(building.BlueprintId, building.AnchorX, building.AnchorY, building.AnchorZ, x, y, z, blockType))
                {
                    return true;
                }
            }

            foreach (var site in _buildingSites)
            {
                if (MatchesProtectedBlueprintBlock(site.BlueprintId, site.AnchorX, site.AnchorY, site.AnchorZ, x, y, z, blockType))
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetBuilding(int buildingId, out VillageBuilding building)
        {
            foreach (var entry in _buildings)
            {
                if (entry.Id == buildingId)
                {
                    building = entry;
                    return true;
                }
            }

            building = null!;
            return false;
        }

        public VillageBuilding? GetNearestBuilding(BuildingKind kind, Vector3 from)
        {
            VillageBuilding? nearest = null;
            float best = float.MaxValue;
            foreach (var building in _buildings)
            {
                if (building.Kind != kind)
                {
                    continue;
                }

                float dist = Vector3.DistanceSquared(from, BuildingEffects.GetWorkPosition(building));
                if (dist < best)
                {
                    best = dist;
                    nearest = building;
                }
            }

            return nearest;
        }

        public VillageBuilding? GetPreferredFarmPlot(Vector3 from, IReadOnlyList<Villager> villagers)
        {
            VillageBuilding? best = null;
            int bestWorkers = int.MaxValue;
            float bestDist = float.MaxValue;

            foreach (var building in _buildings)
            {
                if (building.Kind != BuildingKind.FarmPlot)
                {
                    continue;
                }

                int workers = 0;
                foreach (var villager in villagers)
                {
                    if (villager.VillageId == Id &&
                        villager.AssignedBuildingId == building.Id &&
                        (villager.CurrentJob == JobType.Farm || villager.CurrentJob == JobType.Craft))
                    {
                        workers++;
                    }
                }

                float dist = Vector3.DistanceSquared(from, BuildingEffects.GetWorkPosition(building));
                if (workers < bestWorkers || (workers == bestWorkers && dist < bestDist))
                {
                    bestWorkers = workers;
                    bestDist = dist;
                    best = building;
                }
            }

            return best;
        }

        public Vector3 GetBuildingWorkPosition(VillageBuilding building)
            => BuildingEffects.GetWorkPosition(building);

        private static bool MatchesProtectedBlueprintBlock(
            string blueprintId,
            int anchorX,
            int anchorY,
            int anchorZ,
            int x,
            int y,
            int z,
            BlockType blockType)
        {
            if (!PlayerStructureRegistry.TryGet(blueprintId, out var blueprint))
            {
                return false;
            }

            foreach (var block in blueprint.Template.Blocks)
            {
                if (block.Type == BlockType.Air)
                {
                    continue;
                }

                if (block.Type != blockType)
                {
                    continue;
                }

                if (anchorX + block.Dx == x &&
                    anchorY + block.Dy == y &&
                    anchorZ + block.Dz == z)
                {
                    return true;
                }
            }

            return false;
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
            RegisterOutputChestIfNeeded(building, blueprint);
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
            RegisterOutputChestIfNeeded(building, blueprint);
            HousingCapacity += blueprint.HousingProvided;
            PopulationCap += blueprint.PopulationCapBonus;
            if (blueprint.StorageSlots > 0)
            {
                Storage.ExpandSlots(blueprint.StorageSlots);
            }

            UpdateTier();
        }

        public bool TryGetOutputChest(int chestId, out OutputChest chest)
        {
            foreach (var entry in _outputChests)
            {
                if (entry.Id == chestId)
                {
                    chest = entry;
                    return true;
                }
            }

            chest = null!;
            return false;
        }

        public bool TryGetOutputChestForBuilding(int buildingId, out OutputChest chest)
            => _outputChestsByBuildingId.TryGetValue(buildingId, out chest!);

        public OutputChest? FindFullestOutputChest()
        {
            OutputChest? best = null;
            int bestCount = 0;
            foreach (var chest in _outputChests)
            {
                if (!chest.HasItems)
                {
                    continue;
                }

                int count = chest.ItemCount;
                if (count > bestCount)
                {
                    bestCount = count;
                    best = chest;
                }
            }

            return best;
        }

        public OutputChest? GetNearestOutputChest(BuildingKind kind, Vector3 from)
        {
            OutputChest? nearest = null;
            float best = float.MaxValue;
            foreach (var chest in _outputChests)
            {
                if (chest.BuildingKind != kind)
                {
                    continue;
                }

                float dist = Vector3.DistanceSquared(from, chest.Position);
                if (dist < best)
                {
                    best = dist;
                    nearest = chest;
                }
            }

            return nearest;
        }

        private void RegisterOutputChestIfNeeded(VillageBuilding building, BuildingBlueprint blueprint)
        {
            if (blueprint.Kind is not (BuildingKind.LumberCamp or BuildingKind.Quarry or BuildingKind.FarmPlot))
            {
                return;
            }

            if (_outputChestsByBuildingId.ContainsKey(building.Id))
            {
                return;
            }

            var position = HaulLogistics.GetOutputChestPosition(
                blueprint,
                building.AnchorX,
                building.AnchorY,
                building.AnchorZ);
            var chest = new OutputChest(building.Id, blueprint.Kind, position);
            _outputChests.Add(chest);
            _outputChestsByBuildingId[building.Id] = chest;
        }

        public void UpdateSimulation(float deltaTime, float timeOfDay)
        {
            DailyNeedsSimulatedThisFrame = false;
            DayAccumulator += deltaTime;
            if (DayAccumulator >= 120f)
            {
                DayAccumulator = 0f;
                SimulateDailyNeeds();
                DailyNeedsSimulatedThisFrame = true;
            }

            float maxHappiness = HasBuilding(BuildingKind.Market) ? 1.1f : 1.0f;
            bool isNight = DayNightCycle.IsNight(timeOfDay);
            if (isNight && FoodStock <= 0f)
            {
                Happiness = MathF.Max(0.2f, Happiness - 0.05f);
            }
            else if (FoodStock > Population)
            {
                Happiness = MathF.Min(maxHappiness, Happiness + 0.02f);
            }
        }

        private void SimulateDailyNeeds()
        {
            float need = Population * FoodConsumptionPerDay;
            if (FoodStock >= need)
            {
                FoodStock -= need;
                ConsecutiveDaysWithoutFood = 0;
                AwardMarketFavor();
            }
            else
            {
                FoodStock = 0f;
                Happiness = MathF.Max(0.1f, Happiness - 0.1f);
                ConsecutiveDaysWithoutFood++;
            }
        }

        public bool TrySpendFavor(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (Favor < amount)
            {
                return false;
            }

            Favor -= amount;
            return true;
        }

        public void AddFavor(int amount)
        {
            if (amount > 0)
            {
                Favor += amount;
            }
        }

        public bool TryAdvanceFamilyGrowth(int livePopulation)
        {
            if (livePopulation <= 0 || livePopulation >= PopulationCap)
            {
                FamilyGrowthProgress = MathF.Max(0f, FamilyGrowthProgress - 0.15f);
                return false;
            }

            if (ConsecutiveDaysWithoutFood > 0 || Happiness < 0.72f)
            {
                FamilyGrowthProgress = MathF.Max(0f, FamilyGrowthProgress - 0.12f);
                return false;
            }

            float comfortFood = MathF.Max(4f, livePopulation * 1.5f);
            if (FoodStock < comfortFood + FamilyArrivalFoodCost)
            {
                FamilyGrowthProgress = MathF.Max(0f, FamilyGrowthProgress - 0.08f);
                return false;
            }

            float surplus = MathF.Max(0f, FoodStock - comfortFood);
            float progress = 0.35f + MathF.Min(0.35f, surplus / 10f);
            if (HasBuilding(BuildingKind.House))
            {
                progress += 0.08f;
            }

            if (HasBuilding(BuildingKind.FarmPlot))
            {
                progress += 0.06f;
            }

            if (HasBuilding(BuildingKind.Market))
            {
                progress += 0.05f;
            }

            if (Happiness >= 0.95f)
            {
                progress += 0.04f;
            }

            FamilyGrowthProgress += progress;
            if (FamilyGrowthProgress < 1f)
            {
                return false;
            }

            FamilyGrowthProgress = 0f;
            FoodStock = MathF.Max(0f, FoodStock - FamilyArrivalFoodCost);
            return true;
        }

        private void AwardMarketFavor()
        {
            if (Population <= 0)
            {
                return;
            }

            int dailyFavor = 0;
            if (HasBuilding(BuildingKind.Market))
            {
                int foodSurplus = Math.Max(0, (int)MathF.Floor(FoodStock - Population));
                dailyFavor = Math.Clamp(1 + foodSurplus / 4, 1, 4);
                if (Happiness >= 0.85f)
                {
                    dailyFavor++;
                }
            }
            else
            {
                // Baseline gratitude: citizens generate 1 favor per day if they are fed and happy
                if (Happiness >= 0.72f)
                {
                    dailyFavor = 1;
                }
            }

            if (dailyFavor > 0)
            {
                AddFavor(dailyFavor);
            }
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
            Tier = VillageTierProgression.EvaluateTier(this);
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

        public List<OutputChestSaveData> ExportOutputChests()
        {
            var result = new List<OutputChestSaveData>();
            foreach (var chest in _outputChests)
            {
                var buffer = new List<InventorySlotSaveData>();
                for (int i = 0; i < chest.Buffer.SlotCount; i++)
                {
                    buffer.Add(ItemStackSaveCodec.Serialize(chest.Buffer.GetSlot(i)));
                }

                result.Add(new OutputChestSaveData
                {
                    Id = chest.Id,
                    BuildingId = chest.BuildingId,
                    Kind = (int)chest.BuildingKind,
                    PosX = chest.Position.X,
                    PosY = chest.Position.Y,
                    PosZ = chest.Position.Z,
                    Buffer = buffer
                });
            }

            return result;
        }

        public void RestoreOutputChests(List<OutputChestSaveData>? entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            int maxId = 0;
            foreach (var entry in entries)
            {
                maxId = Math.Max(maxId, entry.Id);
                var chest = new OutputChest(
                    entry.BuildingId,
                    (BuildingKind)entry.Kind,
                    new Vector3(entry.PosX, entry.PosY, entry.PosZ),
                    entry.Id);
                for (int i = 0; i < entry.Buffer.Count && i < chest.Buffer.SlotCount; i++)
                {
                    chest.Buffer.SetSlot(i, ItemStackSaveCodec.Deserialize(entry.Buffer[i]));
                }

                _outputChests.Add(chest);
                _outputChestsByBuildingId[chest.BuildingId] = chest;
            }

            OutputChest.ResetIdCounter(maxId + 1);
        }
    }
}

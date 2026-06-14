using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Village
{
    public sealed class VillageManager
    {
        private readonly List<Village> _villages = new();
        private readonly VillagerManager _villagers;
        private readonly HashSet<int> _finalizedSites = new();
        private readonly HashSet<long> _claimedAnchors = new();
        private int _worldSeed;
        private bool _wasNight;

        public Action<string>? ShowToast { get; set; }
        public IReadOnlyList<Village> Villages => _villages;
        public IReadOnlyCollection<long> ClaimedAnchors => _claimedAnchors;

        public VillageManager(VillagerManager villagers)
        {
            _villagers = villagers;
        }

        public void SetWorldSeed(int seed) => _worldSeed = seed;

        public Village? GetPrimaryVillage() => _villages.Count > 0 ? _villages[0] : null;

        public Village? GetVillage(int id)
        {
            foreach (var village in _villages)
            {
                if (village.Id == id)
                {
                    return village;
                }
            }

            return null;
        }

        public Village? GetVillageAt(Vector3 position)
        {
            foreach (var village in _villages)
            {
                if (village.Contains(position))
                {
                    return village;
                }
            }

            return null;
        }

        public (int spawnX, int spawnZ) InitializeStarterSettlement(VoxelWorld world, int nearX, int nearZ)
        {
            const string villageName = "Founder's Hamlet";
            int heartX = nearX + 4;
            int heartZ = nearZ;
            int heartY = StructureFingerprint.FindSurfaceAnchorY(world, heartX, heartZ);

            if (!PlayerStructureRegistry.TryGet("town_heart", out var blueprint))
            {
                return (nearX, nearZ);
            }

            ClearFootprint(world, blueprint, heartX, heartY, heartZ);
            PlaceBlueprintBlocks(world, blueprint, heartX, heartY, heartZ);

            var village = new Village(villageName, heartX, heartY, heartZ, blueprint.StorageSlots);
            _villages.Add(village);
            village.RegisterClaimedBuilding(blueprint, heartX, heartY, heartZ);
            village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 16));
            village.Storage.AddItem(ItemStack.CreateBlock(BlockType.Cobblestone, 8));
            village.FoodStock = 6f;

            var spawnPos = village.Center + new Vector3(-2.5f, 0f, 0.5f);
            var lumberjack = _villagers.Spawn(village.Id, spawnPos, _worldSeed ^ 1);
            lumberjack.Role = VillagerRole.Lumberjack;
            village.RegisterVillager(lumberjack.Id);

            var peasant = _villagers.Spawn(village.Id, spawnPos + new Vector3(1.5f, 0f, 1f), _worldSeed ^ 2);
            peasant.Role = VillagerRole.Peasant;
            village.RegisterVillager(peasant.Id);

            var gatherTarget = FindNearbyGatherTarget(world, heartX, heartZ, 20);
            if (gatherTarget.HasValue)
            {
                TryAssignJob(village, lumberjack, JobType.Gather, gatherTarget);
            }

            RecordClaimedAnchor(heartX, heartZ);
            ShowToast?.Invoke($"Welcome to {villageName}!");
            return (nearX, nearZ);
        }

        public bool TryFoundVillage(VoxelWorld world, string name, int anchorX, int anchorZ, out Village? village)
        {
            village = null;
            int anchorY = StructureFingerprint.FindSurfaceAnchorY(world, anchorX, anchorZ);
            if (!PlayerStructureRegistry.TryGet("town_heart", out var blueprint))
            {
                return false;
            }

            foreach (var block in blueprint.Template.Blocks)
            {
                int wx = anchorX + block.Dx;
                int wy = anchorY + block.Dy;
                int wz = anchorZ + block.Dz;
                if (world.GetBlock(wx, wy, wz) != BlockType.Air)
                {
                    ShowToast?.Invoke("Not enough space for Town Heart.");
                    return false;
                }
            }

            village = new Village(name, anchorX, anchorY, anchorZ, blueprint.StorageSlots);
            _villages.Add(village);
            village.QueueBuild(blueprint, anchorX, anchorY, anchorZ);
            ShowToast?.Invoke($"Founded village '{name}'. Build the Town Heart!");
            return true;
        }

        public bool TryFindClaimableStructure(VoxelWorld world, Vector3 playerPos, float radius, out int anchorX, out int anchorZ, out StructureDefinition? matched)
        {
            anchorX = 0;
            anchorZ = 0;
            matched = null;
            int px = (int)MathF.Floor(playerPos.X);
            int pz = (int)MathF.Floor(playerPos.Z);
            int scanRadius = (int)MathF.Ceiling(radius);
            float bestDist = float.MaxValue;

            for (int dx = -scanRadius; dx <= scanRadius; dx += 2)
            {
                for (int dz = -scanRadius; dz <= scanRadius; dz += 2)
                {
                    int ax = px + dx;
                    int az = pz + dz;
                    if (IsAnchorClaimed(ax, az))
                    {
                        continue;
                    }

                    if (!TryResolveClaimableMatch(world, ax, az, out var definition, out _, out int anchorY))
                    {
                        continue;
                    }

                    if (!ClaimableStructureMap.IsClaimable(definition.Id))
                    {
                        continue;
                    }

                    float dist = Vector2.DistanceSquared(
                        new Vector2(playerPos.X, playerPos.Z),
                        new Vector2(ax + 0.5f, az + 0.5f));
                    if (dist > radius * radius || dist >= bestDist)
                    {
                        continue;
                    }

                    bestDist = dist;
                    anchorX = ax;
                    anchorZ = az;
                    matched = definition;
                }
            }

            return matched != null;
        }

        public bool TryClaimStructure(VoxelWorld world, int anchorX, int anchorZ, out Village? village)
        {
            village = null;
            if (IsAnchorClaimed(anchorX, anchorZ))
            {
                ShowToast?.Invoke("This outpost is already claimed.");
                return false;
            }

            if (!TryResolveClaimableMatch(world, anchorX, anchorZ, out var matched, out float ratio, out int anchorY))
            {
                ShowToast?.Invoke("No recognizable structure to claim.");
                return false;
            }

            string blueprintId = ClaimableStructureMap.GetBlueprintId(matched.Id);
            if (!PlayerStructureRegistry.TryGet(blueprintId, out var blueprint))
            {
                return false;
            }

            string name = ClaimableStructureMap.GetDefaultVillageName(matched.Id);
            village = new Village(name, anchorX, anchorY, anchorZ);
            _villages.Add(village);
            village.RegisterClaimedBuilding(blueprint, anchorX, anchorY, anchorZ);
            village.FoodStock = 3f;
            village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 8));

            var spawn = village.Center + new Vector3(0.5f, 0f, 0.5f);
            var villager = _villagers.Spawn(village.Id, spawn, _worldSeed ^ (anchorX * 92821 + anchorZ));
            villager.Role = VillagerRole.Peasant;
            village.RegisterVillager(villager.Id);

            RecordClaimedAnchor(anchorX, anchorZ);
            ShowToast?.Invoke($"Claimed {matched.Id} ({ratio:P0} intact) as '{name}'.");
            return true;
        }

        public bool TryClaimAtBlock(VoxelWorld world, int blockX, int blockY, int blockZ)
        {
            for (int dx = -6; dx <= 6; dx += 2)
            {
                for (int dz = -6; dz <= 6; dz += 2)
                {
                    int ax = blockX + dx;
                    int az = blockZ + dz;
                    if (TryClaimStructure(world, ax, az, out _))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool TryQueueBlueprint(VoxelWorld world, Village village, string blueprintId, int anchorX, int anchorZ, IItemContainer payer)
        {
            if (!PlayerStructureRegistry.TryGet(blueprintId, out var blueprint))
            {
                ShowToast?.Invoke("Unknown blueprint.");
                return false;
            }

            if (!blueprint.CanAfford(payer))
            {
                ShowToast?.Invoke($"Cannot afford {blueprint.DisplayName}.");
                return false;
            }

            int anchorY = StructureFingerprint.FindSurfaceAnchorY(world, anchorX, anchorZ);
            blueprint.TryConsumeCosts(payer);
            village.QueueBuild(blueprint, anchorX, anchorY, anchorZ);
            ShowToast?.Invoke($"Queued {blueprint.DisplayName} for construction.");
            return true;
        }

        public bool TryRecruit(Village village)
        {
            if (!village.CanRecruit())
            {
                ShowToast?.Invoke("Cannot recruit: at cap or need 4 oak planks.");
                return false;
            }

            if (!village.TryRecruitCost())
            {
                return false;
            }

            var spawn = village.Center + new Vector3(0.5f, 0f, 0.5f);
            var villager = _villagers.Spawn(village.Id, spawn, _worldSeed ^ village.Population);
            village.RegisterVillager(villager.Id);
            ShowToast?.Invoke($"{villager.Name} joined the village!");
            return true;
        }

        public bool TryAssignJob(Village village, Villager villager, JobType job, Vector3? target = null, int? buildingSiteId = null)
        {
            if (villager.VillageId != village.Id)
            {
                return false;
            }

            villager.Role = job switch
            {
                JobType.Build => VillagerRole.Builder,
                JobType.Gather => VillagerRole.Lumberjack,
                JobType.Craft when village.CountBuildings(BuildingKind.FarmPlot) > 0 => VillagerRole.Farmer,
                _ => villager.Role
            };

            if (job == JobType.Build && !buildingSiteId.HasValue)
            {
                var site = village.GetNearestPendingSite(villager.Position);
                buildingSiteId = site?.Id;
            }

            village.Scheduler.AssignJob(villager, job, target, buildingSiteId);
            return true;
        }

        public void AutoAssignIdleWorkers(Village village, VoxelWorld world)
        {
            var goal = village.Scheduler.GetTopOpenGoal();
            if (goal != null)
            {
                village.Scheduler.TryApplyGoal(village, world, this, goal);
            }

            foreach (var villagerId in village.VillagerIds)
            {
                if (!_villagers.TryGet(villagerId, out var villager) || villager.CurrentJob != JobType.Idle)
                {
                    continue;
                }

                var site = village.GetNearestPendingSite(villager.Position);
                if (site != null)
                {
                    TryAssignJob(village, villager, JobType.Build, null, site.Id);
                    continue;
                }

                if (village.CountBuildings(BuildingKind.FarmPlot) > 0 && villager.Role == VillagerRole.Farmer)
                {
                    TryAssignJob(village, villager, JobType.Craft, village.Center, null);
                    continue;
                }

                var gatherTarget = FindNearbyGatherTarget(world, village.AnchorX, village.AnchorZ, 24);
                if (gatherTarget.HasValue)
                {
                    TryAssignJob(village, villager, JobType.Gather, gatherTarget);
                }
            }
        }

        public void Update(float deltaTime, VoxelWorld world, float timeOfDay)
        {
            bool isNight = timeOfDay < 0.2f || timeOfDay > 0.8f;
            bool morning = _wasNight && !isNight;
            _wasNight = isNight;

            foreach (var village in _villages)
            {
                village.UpdateSimulation(deltaTime, timeOfDay);
                FinalizeCompletedSites(village, world);

                if (morning)
                {
                    AutoAssignIdleWorkers(village, world);
                }

                var context = BuildContext(village);
                float workMult = village.GetWorkSpeedMultiplier();

                foreach (var villagerId in village.VillagerIds)
                {
                    if (!_villagers.TryGet(villagerId, out var villager))
                    {
                        continue;
                    }

                    villager.WorkSpeedMultiplier = workMult;
                    if (isNight && villager.CurrentJob != JobType.Build)
                    {
                        if (villager.CurrentJob != JobType.Sleep)
                        {
                            villager.AssignJob(JobType.Sleep, villager.Position, null);
                        }
                    }
                    else if (villager.CurrentJob == JobType.Sleep && !isNight)
                    {
                        villager.AssignJob(JobType.Idle, null, null);
                    }

                    villager.Update(deltaTime, world, context);
                }
            }

            _villagers.Update(deltaTime, world, _villages);
        }

        private void FinalizeCompletedSites(Village village, VoxelWorld world)
        {
            foreach (var site in village.BuildingSites)
            {
                site.SyncWithWorld(world);
                if (!site.IsComplete || _finalizedSites.Contains(site.Id))
                {
                    continue;
                }

                if (PlayerStructureRegistry.TryGet(site.BlueprintId, out var blueprint))
                {
                    village.CompleteBuilding(blueprint, site);
                    _finalizedSites.Add(site.Id);
                }
            }
        }

        private VillageContext BuildContext(Village village)
        {
            return new VillageContext
            {
                Village = village,
                VillageCenter = village.Center,
                VillageRadius = village.Radius,
                StoragePosition = village.StoragePosition,
                Storage = village.Storage,
                ResolveBuildingSite = id => village.TryGetBuildingSite(id, out var site) ? site : null
            };
        }

        public void LoadFromSave(
            IEnumerable<VillageSaveData> villageData,
            IEnumerable<VillagerSaveData> villagerData,
            IEnumerable<ClaimedAnchorSaveData>? claimedAnchors = null)
        {
            _villages.Clear();
            _finalizedSites.Clear();
            _claimedAnchors.Clear();

            var villageList = new List<VillageSaveData>(villageData);
            var villagerList = new List<VillagerSaveData>(villagerData);

            int maxVillageId = 0;
            foreach (var entry in villageList)
            {
                maxVillageId = Math.Max(maxVillageId, entry.Id);
            }

            int maxSiteId = 0;
            foreach (var entry in villageList)
            {
                foreach (var site in entry.BuildingSites)
                {
                    maxSiteId = Math.Max(maxSiteId, site.Id);
                }
            }

            int maxVillagerId = 0;
            foreach (var entry in villagerList)
            {
                maxVillagerId = Math.Max(maxVillagerId, entry.Id);
            }

            Village.ResetIdCounter(maxVillageId + 1);
            BuildingSite.ResetIdCounter(maxSiteId + 1);
            Villager.ResetIdCounter(maxVillagerId + 1);

            _villagers.LoadVillagers(villagerList);

            foreach (var entry in villageList)
            {
                var village = new Village(entry.Name, entry.AnchorX, entry.AnchorY, entry.AnchorZ, entry.StorageSlots, entry.Id);
                village.Tier = (VillageTier)entry.Tier;
                village.FoodStock = entry.FoodStock;
                village.Happiness = entry.Happiness;
                village.PopulationCap = entry.PopulationCap > 0 ? entry.PopulationCap : village.PopulationCap;
                village.HousingCapacity = entry.HousingCapacity;

                for (int i = 0; i < entry.Storage.Count && i < village.Storage.SlotCount; i++)
                {
                    village.Storage.SetSlot(i, WorldSaveManager.DeserializeItemStack(entry.Storage[i]));
                }

                foreach (var building in entry.Buildings)
                {
                    village.RestoreBuilding(building);
                }

                foreach (var siteEntry in entry.BuildingSites)
                {
                    if (PlayerStructureRegistry.TryGet(siteEntry.BlueprintId, out var blueprint))
                    {
                        village.RestoreBuildingSite(siteEntry, blueprint);
                        if (siteEntry.IsComplete)
                        {
                            _finalizedSites.Add(siteEntry.Id);
                        }
                    }
                }

                foreach (int vid in entry.VillagerIds)
                {
                    village.RegisterVillager(vid);
                }

                village.UpdateTier();
                _villages.Add(village);
            }

            if (claimedAnchors != null)
            {
                foreach (var anchor in claimedAnchors)
                {
                    RecordClaimedAnchor(anchor.X, anchor.Z);
                }
            }
        }

        public List<VillageSaveData> ExportVillages()
        {
            var result = new List<VillageSaveData>();
            foreach (var village in _villages)
            {
                var storage = new List<InventorySlotSaveData>();
                for (int i = 0; i < village.Storage.SlotCount; i++)
                {
                    storage.Add(WorldSaveManager.SerializeItemStack(village.Storage.GetSlot(i)));
                }

                var buildings = new List<BuildingSaveData>();
                foreach (var building in village.Buildings)
                {
                    buildings.Add(new BuildingSaveData
                    {
                        Id = building.Id,
                        BlueprintId = building.BlueprintId,
                        Kind = (int)building.Kind,
                        AnchorX = building.AnchorX,
                        AnchorY = building.AnchorY,
                        AnchorZ = building.AnchorZ,
                        IsComplete = building.IsComplete
                    });
                }

                var sites = new List<BuildingSiteSaveData>();
                foreach (var site in village.BuildingSites)
                {
                    sites.Add(new BuildingSiteSaveData
                    {
                        Id = site.Id,
                        VillageId = site.VillageId,
                        BlueprintId = site.BlueprintId,
                        AnchorX = site.AnchorX,
                        AnchorY = site.AnchorY,
                        AnchorZ = site.AnchorZ,
                        IsComplete = site.IsComplete
                    });
                }

                result.Add(new VillageSaveData
                {
                    Id = village.Id,
                    Name = village.Name,
                    AnchorX = village.AnchorX,
                    AnchorY = village.AnchorY,
                    AnchorZ = village.AnchorZ,
                    Tier = (int)village.Tier,
                    FoodStock = village.FoodStock,
                    Happiness = village.Happiness,
                    StorageSlots = village.Storage.SlotCount,
                    PopulationCap = village.PopulationCap,
                    HousingCapacity = village.HousingCapacity,
                    Storage = storage,
                    VillagerIds = new List<int>(village.VillagerIds),
                    Buildings = buildings,
                    BuildingSites = sites
                });
            }

            return result;
        }

        public List<ClaimedAnchorSaveData> ExportClaimedAnchors()
        {
            var result = new List<ClaimedAnchorSaveData>();
            foreach (long packed in _claimedAnchors)
            {
                result.Add(new ClaimedAnchorSaveData
                {
                    X = (int)(packed >> 32),
                    Z = (int)(packed & 0xFFFFFFFF)
                });
            }

            return result;
        }

        private static void ClearFootprint(VoxelWorld world, BuildingBlueprint blueprint, int anchorX, int anchorY, int anchorZ)
        {
            foreach (var block in blueprint.Template.Blocks)
            {
                int wx = anchorX + block.Dx;
                int wy = anchorY + block.Dy;
                int wz = anchorZ + block.Dz;
                world.SetBlock(wx, wy, wz, BlockType.Air);
            }
        }

        private static void PlaceBlueprintBlocks(VoxelWorld world, BuildingBlueprint blueprint, int anchorX, int anchorY, int anchorZ)
        {
            foreach (var block in blueprint.Template.Blocks)
            {
                int wx = anchorX + block.Dx;
                int wy = anchorY + block.Dy;
                int wz = anchorZ + block.Dz;
                world.SetBlock(wx, wy, wz, block.Type);
            }
        }

        private static Vector3? FindNearbyGatherTarget(VoxelWorld world, int centerX, int centerZ, int radius)
        {
            for (int r = 1; r <= radius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dz) != r)
                        {
                            continue;
                        }

                        int x = centerX + dx;
                        int z = centerZ + dz;
                        int topY = world.GetHighestSolidY(x, z);
                        for (int y = topY; y >= topY - 8 && y > 0; y--)
                        {
                            var block = world.GetBlock(x, y, z);
                            if (block == BlockType.OakLog || block == BlockType.OakLeaves)
                            {
                                return new Vector3(x + 0.5f, y, z + 0.5f);
                            }
                        }
                    }
                }
            }

            return null;
        }

        private void RecordClaimedAnchor(int anchorX, int anchorZ)
            => _claimedAnchors.Add(PackAnchor(anchorX, anchorZ));

        private bool IsAnchorClaimed(int anchorX, int anchorZ)
            => _claimedAnchors.Contains(PackAnchor(anchorX, anchorZ));

        private static bool TryResolveClaimableMatch(
            VoxelWorld world,
            int anchorX,
            int anchorZ,
            out StructureDefinition matched,
            out float matchRatio,
            out int anchorY)
        {
            matched = StructureRegistry.All[0];
            matchRatio = 0f;
            anchorY = 0;
            int surfaceY = StructureFingerprint.FindSurfaceAnchorY(world, anchorX, anchorZ);
            float bestRatio = 0f;
            StructureDefinition? best = null;
            int bestY = surfaceY - 1;

            for (int y = surfaceY - 4; y <= surfaceY + 1; y++)
            {
                if (!StructureFingerprint.TryMatchWorldStructure(world, anchorX, y, anchorZ, out var candidate, out float ratio))
                {
                    continue;
                }

                if (!ClaimableStructureMap.IsClaimable(candidate.Id) || ratio <= bestRatio)
                {
                    continue;
                }

                bestRatio = ratio;
                best = candidate;
                bestY = y;
            }

            if (best == null)
            {
                return false;
            }

            matched = best;
            matchRatio = bestRatio;
            anchorY = bestY;
            return true;
        }

        private static long PackAnchor(int anchorX, int anchorZ)
            => ((long)anchorX << 32) | (uint)anchorZ;
    }
}

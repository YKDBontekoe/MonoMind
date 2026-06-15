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
    public sealed class VillageManager : IJobAssignment
    {
        private readonly List<Village> _villages = new();
        private readonly Dictionary<int, Village> _villageIndex = new();
        private readonly VillagerManager _villagers;
        private readonly HashSet<int> _finalizedSites = new();
        private readonly VillageFoundingService _founding;
        private readonly JobDispatcher _dispatcher;
        private readonly HaulCoordinator _haulCoordinator;
        private readonly VillageSimulation _simulation;
        private readonly VillagePersistence _persistence;
        private VillageEvents? _events;
        private int _worldSeed;
        private float _repairScanCooldown;

        public Action<string>? ShowToast
        {
            get => _founding.ShowToast;
            set
            {
                _founding.ShowToast = value;
                _dispatcher.ShowToast = value;
            }
        }

        public bool CreativeMode
        {
            get => _dispatcher.CreativeMode;
            set
            {
                _dispatcher.CreativeMode = value;
                _simulation.CreativeMode = value;
            }
        }

        public IReadOnlyList<Village> Villages => _villages;
        public IReadOnlyCollection<long> ClaimedAnchors => throw new NotSupportedException("Use ExportClaimedAnchors");

        public VillageManager(VillagerManager villagers)
        {
            _villagers = villagers;
            _haulCoordinator = new HaulCoordinator(villagers);
            _dispatcher = new JobDispatcher(villagers, _haulCoordinator);
            _founding = new VillageFoundingService(villagers, new HashSet<long>());
            _simulation = new VillageSimulation(villagers, _dispatcher, _haulCoordinator);
            _persistence = new VillagePersistence(villagers);
        }

        public void SetWorldSeed(int seed)
        {
            _worldSeed = seed;
            _founding.SetWorldSeed(seed);
        }

        public Village? GetPrimaryVillage() => _villages.Count > 0 ? _villages[0] : null;

        public Village? GetActiveVillage(Vector3 playerPos)
        {
            var atPlayer = GetVillageAt(playerPos);
            if (atPlayer != null)
            {
                VillageSettlementHealth.AdoptNearbyOrphanedCitizens(atPlayer, _villagers, _villages);
                VillageSettlementHealth.SyncPopulationRegistry(atPlayer, _villagers);
                return atPlayer;
            }

            Village? best = null;
            int bestPopulation = -1;
            float bestDist = float.MaxValue;
            foreach (var village in _villages)
            {
                if (!VillageSettlementHealth.HasEstablishedSettlement(village))
                {
                    continue;
                }

                VillageSettlementHealth.AdoptNearbyOrphanedCitizens(village, _villagers, _villages);
                int population = VillageSettlementHealth.GetLivePopulation(village, _villagers);
                float dist = HorizontalDistanceSquared(playerPos, village);
                if (population > bestPopulation || (population == bestPopulation && dist < bestDist))
                {
                    bestPopulation = population;
                    bestDist = dist;
                    best = village;
                }
            }

            if (best != null && bestPopulation > 0 && bestDist <= 48f * 48f)
            {
                VillageSettlementHealth.SyncPopulationRegistry(best, _villagers);
                return best;
            }

            Village? nearestHeart = null;
            bestDist = float.MaxValue;
            foreach (var village in _villages)
            {
                if (!VillageSettlementHealth.HasEstablishedSettlement(village))
                {
                    continue;
                }

                float dist = HorizontalDistanceSquared(playerPos, village);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearestHeart = village;
                }
            }

            if (nearestHeart != null && bestDist <= 48f * 48f)
            {
                VillageSettlementHealth.AdoptNearbyOrphanedCitizens(nearestHeart, _villagers, _villages);
                VillageSettlementHealth.SyncPopulationRegistry(nearestHeart, _villagers);
                return nearestHeart;
            }

            var primary = GetPrimaryVillage();
            if (primary != null)
            {
                VillageSettlementHealth.AdoptNearbyOrphanedCitizens(primary, _villagers, _villages);
                VillageSettlementHealth.SyncPopulationRegistry(primary, _villagers);
            }

            return primary;
        }

        public void SyncCitizensForVillage(Village village)
        {
            VillageSettlementHealth.AdoptNearbyOrphanedCitizens(village, _villagers, _villages);
            VillageSettlementHealth.SyncPopulationRegistry(village, _villagers);
        }

        public Village? GetVillage(int id) =>
            _villageIndex.TryGetValue(id, out var village) ? village : null;

        public Village? GetVillageAt(Vector3 position)
        {
            Village? best = null;
            int bestPopulation = -1;
            float bestDist = float.MaxValue;

            foreach (var village in _villages)
            {
                if (!village.Contains(position))
                {
                    continue;
                }

                int population = VillageSettlementHealth.GetLivePopulation(village, _villagers);
                float dist = HorizontalDistanceSquared(position, village);
                if (population > bestPopulation || (population == bestPopulation && dist < bestDist))
                {
                    bestPopulation = population;
                    bestDist = dist;
                    best = village;
                }
            }

            return best;
        }

        private static float HorizontalDistanceSquared(Vector3 position, Village village)
        {
            float dx = position.X - (village.AnchorX + 0.5f);
            float dz = position.Z - (village.AnchorZ + 0.5f);
            return dx * dx + dz * dz;
        }

        public (int spawnX, int spawnZ) InitializeStarterSettlement(VoxelWorld world, int nearX, int nearZ)
        {
            var result = _founding.InitializeStarterSettlement(_villages, world, nearX, nearZ, _dispatcher);
            RebuildIndex();
            return result;
        }

        public void EnsureStarterSettlement(VoxelWorld world, int nearX, int nearZ)
        {
            var primary = GetPrimaryVillage();
            if (primary == null)
            {
                InitializeStarterSettlement(world, nearX, nearZ);
                return;
            }

            RepairVillageCitizens(primary, world);
        }

        public bool RepairVillageCitizens(Village village, VoxelWorld world)
        {
            VillageSettlementHealth.SyncPopulationRegistry(village, _villagers);
            if (!VillageSettlementHealth.NeedsCitizenRepair(village, _villagers))
            {
                return false;
            }

            int before = VillageSettlementHealth.GetLivePopulation(village, _villagers);
            if (before > 0)
            {
                return false;
            }

            VillageSettlementHealth.EnsureVillageChunksLoaded(world, village);
            int after = _founding.SpawnStarterCitizens(village, world, _dispatcher);
            VillageSettlementHealth.SyncPopulationRegistry(village, _villagers);
            after = VillageSettlementHealth.GetLivePopulation(village, _villagers);
            if (after > before)
            {
                ShowToast?.Invoke($"{after} settler(s) arrived at {village.Name}. Open PEOPLE tab to assign jobs.");
                return true;
            }

            return false;
        }

        public void RepairNearbySettlements(VoxelWorld world, Vector3 playerPos, float deltaTime)
        {
            _repairScanCooldown -= deltaTime;
            if (_repairScanCooldown > 0f)
            {
                return;
            }

            _repairScanCooldown = 1f;
            foreach (var village in _villages)
            {
                if (!VillageSettlementHealth.IsPlayerNearTownHeart(village, playerPos))
                {
                    continue;
                }

                RepairVillageCitizens(village, world);
            }
        }

        public void RepairAllVillages(VoxelWorld world)
        {
            foreach (var village in _villages)
            {
                RepairVillageCitizens(village, world);
            }
        }

        public bool CanPlaceTownHeart(
            VoxelWorld world,
            int anchorX,
            int anchorY,
            int anchorZ,
            IItemContainer payer) =>
            _founding.CanPlaceTownHeart(world, anchorX, anchorY, anchorZ, payer, CreativeMode);

        public bool TryFoundVillage(
            VoxelWorld world,
            string name,
            int anchorX,
            int anchorZ,
            out Village? village,
            int anchorY = -1)
        {
            var ok = _founding.TryFoundVillage(_villages, world, name, anchorX, anchorZ, out village, anchorY);
            if (ok && village != null)
            {
                RebuildIndex();
                Villager? founder = null;
                foreach (var citizen in VillageSettlementHealth.EnumerateLiveCitizens(village, _villagers))
                {
                    founder = citizen;
                    break;
                }

                if (founder != null)
                {
                    foreach (var site in village.BuildingSites)
                    {
                        if (!site.IsComplete)
                        {
                            _dispatcher.TryAssignJob(village, founder, JobType.Build, null, site.Id);
                            break;
                        }
                    }
                }
            }

            return ok;
        }

        public bool TryFindClaimableStructure(
            VoxelWorld world,
            Vector3 playerPos,
            float radius,
            out int anchorX,
            out int anchorZ,
            out StructureDefinition? matched,
            bool quickScan = false) =>
            _founding.TryFindClaimableStructure(world, playerPos, radius, out anchorX, out anchorZ, out matched, quickScan);

        public bool TryClaimStructure(VoxelWorld world, int anchorX, int anchorZ, out Village? village)
        {
            var ok = _founding.TryClaimStructure(_villages, world, anchorX, anchorZ, out village);
            if (ok)
            {
                RebuildIndex();
            }

            return ok;
        }

        public bool TryClaimAtBlock(VoxelWorld world, int blockX, int blockY, int blockZ) =>
            _founding.TryClaimAtBlock(_villages, world, blockX, blockY, blockZ);

        public bool TryQueueBlueprint(
            VoxelWorld world,
            Village village,
            string blueprintId,
            int anchorX,
            int anchorZ,
            IItemContainer payer,
            int anchorY = -1)
        {
            if (!_dispatcher.TryQueueBlueprint(world, village, blueprintId, anchorX, anchorZ, payer, anchorY))
            {
                return false;
            }

            var site = village.BuildingSites[^1];
            if (!site.IsComplete)
            {
                _dispatcher.TryAssignBuilderToSite(village, site);
            }

            return true;
        }

        public bool CanPlaceBlueprint(
            VoxelWorld world,
            Village village,
            BuildingBlueprint blueprint,
            int anchorX,
            int anchorZ,
            IItemContainer payer,
            int anchorY = -1) =>
            _dispatcher.CanPlaceBlueprint(world, village, blueprint, anchorX, anchorZ, payer, anchorY);

        public Village? GetVillageForBlock(int x, int y, int z) =>
            GetVillageAt(new Vector3(x + 0.5f, y, z + 0.5f));

        public bool TryMarkWorkBlock(VoxelWorld world, int x, int y, int z, out string message)
        {
            var village = GetVillageForBlock(x, y, z);
            if (village == null)
            {
                message = "No village nearby.";
                return false;
            }

            return _dispatcher.TryMarkWorkBlock(world, village, x, y, z, out message);
        }

        public bool TryMarkWorkZone(
            VoxelWorld world,
            Village village,
            int ax,
            int ay,
            int az,
            int bx,
            int by,
            int bz,
            out string message)
        {
            var bounds = GatherWorkQueue.NormalizeBounds(ax, ay, az, bx, by, bz);
            int added = village.WorkQueue.EnqueueZone(
                world,
                bounds.minX,
                bounds.minY,
                bounds.minZ,
                bounds.maxX,
                bounds.maxY,
                bounds.maxZ);

            if (added == 0)
            {
                message = "No gatherable blocks in that zone.";
                return false;
            }

            _dispatcher.AssignIdleWorkersFromQueue(village, world);
            message = $"Queued {added} block(s) for workers.";
            return true;
        }

        public bool CanMarkWorkZone(Village village, int ax, int ay, int az, int bx, int by, int bz)
        {
            var bounds = GatherWorkQueue.NormalizeBounds(ax, ay, az, bx, by, bz);
            var center = new Vector3(
                (bounds.minX + bounds.maxX + 1) * 0.5f,
                (bounds.minY + bounds.maxY) * 0.5f,
                (bounds.minZ + bounds.maxZ + 1) * 0.5f);
            return village.Contains(center);
        }

        public bool TryRecruit(Village village, VoxelWorld world)
        {
            VillageSettlementHealth.SyncPopulationRegistry(village, _villagers);
            if (VillageSettlementHealth.GetLivePopulation(village, _villagers) == 0)
            {
                if (RepairVillageCitizens(village, world))
                {
                    return true;
                }

                ShowToast?.Invoke("Stand at the Town Heart and click SUMMON SETTLERS, or found a new settlement.");
                return false;
            }

            if (!village.CanRecruit(CreativeMode))
            {
                if (village.Population >= village.PopulationCap)
                {
                    ShowToast?.Invoke("Cannot recruit: at housing cap. Queue peasant house in BUILDINGS tab.");
                }
                else
                {
                    ShowToast?.Invoke($"Cannot recruit: need {Village.RecruitFoodCost} oak planks in village storage.");
                }

                return false;
            }

            if (!village.TryRecruitCost(CreativeMode))
            {
                return false;
            }

            var spawn = VillageSpawnHelper.FindSpawnPosition(world, village, _worldSeed ^ village.Population);
            var villager = _villagers.Spawn(village.Id, spawn, _worldSeed ^ village.Population);
            villager.IsGrounded = true;
            village.RegisterVillager(villager.Id);
            _events?.OnRecruit(villager);
            ShowToast?.Invoke($"{villager.Name} joined! Assign a job on the Villagers tab.");
            return true;
        }

        public bool TryAssignJob(
            Village village,
            Villager villager,
            JobType job,
            Vector3? target = null,
            int? buildingSiteId = null,
            int? buildingId = null) =>
            _dispatcher.TryAssignJob(village, villager, job, target, buildingSiteId, buildingId);

        public bool TryAssignStockGoalWorker(Village village, VoxelWorld world, Villager villager, BlockType blockType) =>
            _dispatcher.TryAssignStockGoalWorker(village, world, villager, blockType);

        public void AutoAssignIdleWorkers(Village village, VoxelWorld world) =>
            _dispatcher.AutoAssignIdleWorkers(village, world);

        public void Update(float deltaTime, VoxelWorld world, float timeOfDay, AnimalManager animalManager)
        {
            _villagers.Update(deltaTime, world, _villages);
            _simulation.Update(_villages, _finalizedSites, deltaTime, world, timeOfDay, animalManager);
        }

        public void SetVillageEvents(VillageEvents events)
        {
            _events = events;
            _simulation.SetVillageEvents(events);
        }

        public void LoadFromSave(
            IEnumerable<VillageSaveData> villageData,
            IEnumerable<VillagerSaveData> villagerData,
            IEnumerable<ClaimedAnchorSaveData>? claimedAnchors = null)
        {
            _persistence.LoadFromSave(_villages, _finalizedSites, villageData, villagerData);
            _founding.LoadClaimedAnchors(claimedAnchors);
            RebuildIndex();
        }

        public List<VillageSaveData> ExportVillages() => _persistence.ExportVillages(_villages);

        public List<ClaimedAnchorSaveData> ExportClaimedAnchors() => _founding.ExportClaimedAnchors();

        public void RegisterVillageForTest(Village village)
        {
            _villages.Add(village);
            RebuildIndex();
        }

        private void RebuildIndex()
        {
            _villageIndex.Clear();
            foreach (var village in _villages)
            {
                _villageIndex[village.Id] = village;
            }
        }
    }
}

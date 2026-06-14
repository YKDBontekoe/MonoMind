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

        public Village? GetVillage(int id) =>
            _villageIndex.TryGetValue(id, out var village) ? village : null;

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
            var result = _founding.InitializeStarterSettlement(_villages, world, nearX, nearZ, _dispatcher);
            RebuildIndex();
            return result;
        }

        public bool TryFoundVillage(VoxelWorld world, string name, int anchorX, int anchorZ, out Village? village)
        {
            var ok = _founding.TryFoundVillage(_villages, world, name, anchorX, anchorZ, out village);
            if (ok)
            {
                RebuildIndex();
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

        public bool TryQueueBlueprint(VoxelWorld world, Village village, string blueprintId, int anchorX, int anchorZ, IItemContainer payer) =>
            _dispatcher.TryQueueBlueprint(world, village, blueprintId, anchorX, anchorZ, payer);

        public bool CanPlaceBlueprint(
            VoxelWorld world,
            Village village,
            BuildingBlueprint blueprint,
            int anchorX,
            int anchorZ,
            IItemContainer payer) =>
            _dispatcher.CanPlaceBlueprint(world, village, blueprint, anchorX, anchorZ, payer);

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

        public bool TryRecruit(Village village)
        {
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

            var spawn = village.Center + new Vector3(0.5f, 0f, 0.5f);
            var villager = _villagers.Spawn(village.Id, spawn, _worldSeed ^ village.Population);
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

        public void Update(float deltaTime, VoxelWorld world, float timeOfDay) =>
            _simulation.Update(_villages, _finalizedSites, deltaTime, world, timeOfDay);

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

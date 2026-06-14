using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.Domain.Core;
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
        public bool CreativeMode { get; set; }
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
            village.Storage.AddItem(ToolRegistry.CreateStack(ToolType.Axe, ToolTier.Wood));
            village.Storage.AddItem(ToolRegistry.CreateStack(ToolType.Pickaxe, ToolTier.Wood));
            village.FoodStock = 6f;

            var spawnPos = village.Center + new Vector3(-2.5f, 0f, 0.5f);
            var lumberjack = _villagers.Spawn(village.Id, spawnPos, _worldSeed ^ 1);
            lumberjack.Role = VillagerRole.Lumberjack;
            village.RegisterVillager(lumberjack.Id);

            var peasant = _villagers.Spawn(village.Id, spawnPos + new Vector3(1.5f, 0f, 1f), _worldSeed ^ 2);
            peasant.Role = VillagerRole.Hauler;
            village.RegisterVillager(peasant.Id);

            var gatherTarget = FindNearbyLumberTarget(world, village, null, spawnPos);
            if (gatherTarget.HasValue)
            {
                TryAssignJob(village, lumberjack, JobType.Lumber, gatherTarget);
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

        public bool TryFindClaimableStructure(
            VoxelWorld world,
            Vector3 playerPos,
            float radius,
            out int anchorX,
            out int anchorZ,
            out StructureDefinition? matched,
            bool quickScan = false)
        {
            anchorX = 0;
            anchorZ = 0;
            matched = null;
            int px = (int)MathF.Floor(playerPos.X);
            int pz = (int)MathF.Floor(playerPos.Z);
            float effectiveRadius = quickScan ? Math.Min(radius, 16f) : radius;
            int scanRadius = (int)MathF.Ceiling(effectiveRadius);
            int step = quickScan ? 4 : 2;
            float bestDist = float.MaxValue;

            for (int dx = -scanRadius; dx <= scanRadius; dx += step)
            {
                for (int dz = -scanRadius; dz <= scanRadius; dz += step)
                {
                    int ax = px + dx;
                    int az = pz + dz;
                    if (IsAnchorClaimed(ax, az))
                    {
                        continue;
                    }

                    if (!TryResolveClaimableMatch(world, ax, az, out var definition, out _, out int anchorY, quickScan))
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

            if (!CanPlaceBlueprint(world, village, blueprint, anchorX, anchorZ, payer))
            {
                ShowToast?.Invoke($"Cannot place {blueprint.DisplayName} here.");
                return false;
            }

            int anchorY = StructureFingerprint.FindSurfaceAnchorY(world, anchorX, anchorZ);
            if (!CreativeMode)
            {
                blueprint.TryConsumeCosts(payer);
            }

            village.QueueBuild(blueprint, anchorX, anchorY, anchorZ);
            ShowToast?.Invoke($"Queued {blueprint.DisplayName} for construction.");
            return true;
        }

        public bool CanPlaceBlueprint(
            VoxelWorld world,
            Village village,
            BuildingBlueprint blueprint,
            int anchorX,
            int anchorZ,
            IItemContainer payer)
        {
            if (!CreativeMode && !blueprint.CanAfford(payer))
            {
                return false;
            }

            float dx = anchorX + 0.5f - village.Center.X;
            float dz = anchorZ + 0.5f - village.Center.Z;
            if (dx * dx + dz * dz > village.Radius * village.Radius)
            {
                return false;
            }

            int anchorY = StructureFingerprint.FindSurfaceAnchorY(world, anchorX, anchorZ);
            foreach (var block in blueprint.Template.Blocks)
            {
                int wx = anchorX + block.Dx;
                int wy = anchorY + block.Dy;
                int wz = anchorZ + block.Dz;
                if (wy <= 0 || wy >= Chunk.Height)
                {
                    return false;
                }

                var current = world.GetBlock(wx, wy, wz);
                if (!CanAcceptBlueprintBlock(current))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool CanAcceptBlueprintBlock(BlockType current)
        {
            if (current == BlockType.Air)
            {
                return true;
            }

            if (current.IsTransparent())
            {
                return true;
            }

            return current is BlockType.Grass
                or BlockType.Dirt
                or BlockType.Sand
                or BlockType.Snow
                or BlockType.Gravel
                or BlockType.Mud;
        }

        public Village? GetVillageForBlock(int x, int y, int z)
        {
            var pos = new Vector3(x + 0.5f, y, z + 0.5f);
            foreach (var village in _villages)
            {
                if (village.Contains(pos))
                {
                    return village;
                }
            }

            return null;
        }

        public bool TryMarkWorkBlock(VoxelWorld world, int x, int y, int z, out string message)
        {
            message = string.Empty;
            var block = world.GetBlock(x, y, z);
            if (!GatherBlockClassifier.IsGatherable(block))
            {
                message = "That block can't be marked for workers.";
                return false;
            }

            var village = GetVillageForBlock(x, y, z);
            if (village == null)
            {
                message = "No village nearby.";
                return false;
            }

            if (!village.WorkQueue.Enqueue(x, y, z))
            {
                message = "Block already queued.";
                return false;
            }

            var role = GatherBlockClassifier.GetPreferredRole(block);
            var target = new Vector3(x + 0.5f, y, z + 0.5f);
            var worker = FindNearestIdleWorker(village, role, target);
            if (worker != null)
            {
                var job = role == VillagerRole.Miner ? JobType.Mine : JobType.Lumber;
                TryAssignJob(village, worker, job, target);
                message = $"Marked for {worker.Name}.";
            }
            else
            {
                message = "Block queued for workers.";
            }

            return true;
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

            AssignIdleWorkersFromQueue(village, world);
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

        private void AssignIdleWorkersFromQueue(Village village, VoxelWorld world)
        {
            foreach (var villagerId in village.VillagerIds)
            {
                if (!_villagers.TryGet(villagerId, out var villager) || villager.CurrentJob != JobType.Idle)
                {
                    continue;
                }

                TryAssignFromWorkQueue(village, world, villager);
            }
        }

        private bool TryAssignFromWorkQueue(Village village, VoxelWorld world, Villager villager)
        {
            if (village.WorkQueue.Count == 0)
            {
                return false;
            }

            if (villager.Role is VillagerRole.Lumberjack or VillagerRole.Peasant
                && village.WorkQueue.TryGetNextForRole(VillagerRole.Lumberjack, world, out int x, out int y, out int z))
            {
                TryAssignJob(village, villager, JobType.Lumber, new Vector3(x + 0.5f, y, z + 0.5f));
                return true;
            }

            if (villager.Role is VillagerRole.Miner or VillagerRole.Peasant
                && village.WorkQueue.TryGetNextForRole(VillagerRole.Miner, world, out x, out y, out z))
            {
                TryAssignJob(village, villager, JobType.Mine, new Vector3(x + 0.5f, y, z + 0.5f));
                return true;
            }

            return false;
        }

        private Villager? FindNearestIdleWorker(Village village, VillagerRole preferredRole, Vector3 near)
        {
            Villager? bestSpecialist = null;
            Villager? bestPeasant = null;
            float bestSpecialistDist = float.MaxValue;
            float bestPeasantDist = float.MaxValue;

            foreach (var villagerId in village.VillagerIds)
            {
                if (!_villagers.TryGet(villagerId, out var villager) || villager.CurrentJob != JobType.Idle)
                {
                    continue;
                }

                float dist = Vector3.DistanceSquared(villager.Position, near);
                if (villager.Role == preferredRole && dist < bestSpecialistDist)
                {
                    bestSpecialist = villager;
                    bestSpecialistDist = dist;
                }
                else if (villager.Role == VillagerRole.Peasant && dist < bestPeasantDist)
                {
                    bestPeasant = villager;
                    bestPeasantDist = dist;
                }
            }

            return bestSpecialist ?? bestPeasant;
        }

        public bool TryRecruit(Village village)
        {
            if (!village.CanRecruit(CreativeMode))
            {
                ShowToast?.Invoke(CreativeMode
                    ? "Cannot recruit: at population cap."
                    : "Cannot recruit: at cap or need 4 oak planks.");
                return false;
            }

            if (!village.TryRecruitCost(CreativeMode))
            {
                return false;
            }

            var spawn = village.Center + new Vector3(0.5f, 0f, 0.5f);
            var villager = _villagers.Spawn(village.Id, spawn, _worldSeed ^ village.Population);
            village.RegisterVillager(villager.Id);
            ShowToast?.Invoke($"{villager.Name} joined the village!");
            return true;
        }

        public bool TryAssignJob(
            Village village,
            Villager villager,
            JobType job,
            Vector3? target = null,
            int? buildingSiteId = null,
            int? buildingId = null)
        {
            if (villager.VillageId != village.Id)
            {
                return false;
            }

            job = NormalizeJob(job);
            int? assignedBuildingId = buildingId;

            switch (job)
            {
                case JobType.Build:
                    villager.Role = VillagerRole.Builder;
                    if (!buildingSiteId.HasValue)
                    {
                        buildingSiteId = village.GetNearestPendingSite(villager.Position)?.Id;
                    }

                    break;
                case JobType.Lumber:
                    villager.Role = VillagerRole.Lumberjack;
                    var lumberCamp = ResolveAssignedBuilding(village, buildingId, BuildingKind.LumberCamp, villager.Position);
                    if (lumberCamp != null)
                    {
                        assignedBuildingId = lumberCamp.Id;
                    }

                    target ??= _villagers.World != null
                        ? FindNearbyLumberTarget(_villagers.World, village, lumberCamp, villager.Position)
                        : null;
                    break;
                case JobType.Mine:
                    villager.Role = VillagerRole.Miner;
                    var quarry = ResolveAssignedBuilding(village, buildingId, BuildingKind.Quarry, villager.Position);
                    if (target == null)
                    {
                        if (quarry == null)
                        {
                            return false;
                        }

                        assignedBuildingId = quarry.Id;
                        target = _villagers.World != null ? FindNearbyMineTarget(_villagers.World, quarry) : null;
                    }
                    else if (quarry != null)
                    {
                        assignedBuildingId = quarry.Id;
                    }

                    break;
                case JobType.Farm:
                    if (!village.HasBuilding(BuildingKind.FarmPlot))
                    {
                        return false;
                    }

                    villager.Role = VillagerRole.Farmer;
                    var farmPlot = ResolveAssignedBuilding(village, buildingId, BuildingKind.FarmPlot, villager.Position)
                        ?? village.GetPreferredFarmPlot(villager.Position, _villagers.All);
                    if (farmPlot == null)
                    {
                        return false;
                    }

                    assignedBuildingId = farmPlot.Id;
                    target ??= village.GetBuildingWorkPosition(farmPlot);
                    if (_villagers.World != null)
                    {
                        target = FindNearbyFarmTarget(_villagers.World, village, villager.Position, farmPlot)
                            ?? target;
                    }

                    break;
                case JobType.Craft:
                    if (!village.HasBuilding(BuildingKind.Workshop) ||
                        !VillageWorkshopCrafting.NeedsSmithWork(village.Storage, CreativeMode))
                    {
                        return false;
                    }

                    villager.Role = VillagerRole.Smith;
                    var workshop = ResolveAssignedBuilding(village, buildingId, BuildingKind.Workshop, villager.Position);
                    if (workshop == null)
                    {
                        return false;
                    }

                    assignedBuildingId = workshop.Id;
                    target ??= village.GetBuildingWorkPosition(workshop);
                    break;
                default:
                    break;
            }

            if (target == null && job is JobType.Lumber or JobType.Mine or JobType.Farm)
            {
                return false;
            }

            village.Scheduler.AssignJob(villager, job, target, buildingSiteId, assignedBuildingId);
            if (job == JobType.Farm && target.HasValue && _villagers.World != null)
            {
                int bx = (int)MathF.Floor(target.Value.X);
                int by = (int)MathF.Floor(target.Value.Y);
                int bz = (int)MathF.Floor(target.Value.Z);
                var approach = FarmCropHelper.GetApproachPosition(_villagers.World, bx, by, bz, villager.Position);
                if (VoxelPathfinder.TryFindPath(_villagers.World, villager.Position, approach, 24, out var waypoints))
                {
                    villager.SetPath(waypoints);
                }
            }

            return true;
        }

        private static VillageBuilding? ResolveAssignedBuilding(
            Village village,
            int? buildingId,
            BuildingKind kind,
            Vector3 from)
        {
            if (buildingId.HasValue &&
                village.TryGetBuilding(buildingId.Value, out var specified) &&
                specified.Kind == kind)
            {
                return specified;
            }

            return village.GetNearestBuilding(kind, from);
        }

        private static JobType NormalizeJob(JobType job) =>
            job == JobType.Gather ? JobType.Lumber : job;

        public bool TryAssignStockGoalWorker(Village village, VoxelWorld world, Villager villager, BlockType blockType)
        {
            var category = GatherBlockClassifier.GetCategory(blockType);
            if (!category.HasValue)
            {
                return false;
            }

            if (category == GatherCategory.Mine &&
                villager.Role is not (VillagerRole.Miner or VillagerRole.Peasant))
            {
                return false;
            }

            if (category == GatherCategory.Lumber &&
                villager.Role is not (VillagerRole.Lumberjack or VillagerRole.Peasant))
            {
                return false;
            }

            Vector3? target = FindNearbyStockTarget(world, village, blockType);
            if (!target.HasValue)
            {
                return false;
            }

            JobType job = category == GatherCategory.Mine ? JobType.Mine : JobType.Lumber;
            int? buildingId = null;
            if (category == GatherCategory.Mine && village.HasBuilding(BuildingKind.Quarry))
            {
                buildingId = village.GetNearestBuilding(BuildingKind.Quarry, villager.Position)?.Id;
            }
            else if (category == GatherCategory.Lumber && village.HasBuilding(BuildingKind.LumberCamp))
            {
                buildingId = village.GetNearestBuilding(BuildingKind.LumberCamp, villager.Position)?.Id;
            }

            return TryAssignJob(village, villager, job, target, buildingId: buildingId);
        }

        private static Vector3? FindNearbyStockTarget(VoxelWorld world, Village village, BlockType blockType)
        {
            int centerX = village.AnchorX;
            int centerZ = village.AnchorZ;
            int radius = BuildingEffects.QuarryScanRadius;
            int minY = Math.Max(1, village.AnchorY - BuildingEffects.QuarryDepthLimit);
            int maxY = village.AnchorY + 4;

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
                        int topY = Math.Min(maxY, world.GetHighestSolidY(x, z));
                        for (int y = topY; y >= minY; y--)
                        {
                            if (world.GetBlock(x, y, z) == blockType)
                            {
                                return new Vector3(x + 0.5f, y, z + 0.5f);
                            }
                        }
                    }
                }
            }

            var category = GatherBlockClassifier.GetCategory(blockType);
            if (category == GatherCategory.Mine)
            {
                var quarry = village.GetNearestBuilding(BuildingKind.Quarry, village.Center);
                if (quarry != null)
                {
                    return FindNearbyMineTarget(world, quarry);
                }
            }
            else if (category == GatherCategory.Lumber)
            {
                var lumberCamp = village.GetNearestBuilding(BuildingKind.LumberCamp, village.Center);
                return FindNearbyLumberTarget(world, village, lumberCamp, village.Center);
            }

            return null;
        }

        public void AutoAssignIdleWorkers(Village village, VoxelWorld world)
        {
            village.Scheduler.CheckGoalProgress(village);
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

                if (goal != null && village.Scheduler.TryAssignForGoal(village, world, this, goal, villager))
                {
                    continue;
                }

                if (villager.Role == VillagerRole.Hauler && TryAssignHaulWork(village, villager))
                {
                    continue;
                }

                var site = village.GetNearestPendingSite(villager.Position);
                if (site != null)
                {
                    TryAssignJob(village, villager, JobType.Build, null, site.Id);
                    continue;
                }

                if (village.HasBuilding(BuildingKind.FarmPlot) &&
                    (villager.Role == VillagerRole.Farmer || villager.Role == VillagerRole.Peasant))
                {
                    var plot = village.GetPreferredFarmPlot(villager.Position, _villagers.All);
                    if (plot != null)
                    {
                        var farmTarget = FindNearbyFarmTarget(world, village, villager.Position, plot);
                        if (farmTarget.HasValue &&
                            TryAssignJob(village, villager, JobType.Farm, farmTarget, buildingId: plot.Id))
                        {
                            continue;
                        }
                    }
                }

                if (village.HasBuilding(BuildingKind.Workshop) &&
                    VillageWorkshopCrafting.NeedsSmithWork(village.Storage, CreativeMode) &&
                    (villager.Role == VillagerRole.Smith || villager.Role == VillagerRole.Peasant))
                {
                    if (TryAssignJob(village, villager, JobType.Craft))
                    {
                        continue;
                    }
                }

                if (TryAssignFromWorkQueue(village, world, villager))
                {
                    continue;
                }

                if (TryAssignHaulWork(village, villager))
                {
                    continue;
                }

                if (village.HasBuilding(BuildingKind.LumberCamp) &&
                    (villager.Role == VillagerRole.Lumberjack || villager.Role == VillagerRole.Peasant))
                {
                    var lumberCamp = village.GetNearestBuilding(BuildingKind.LumberCamp, villager.Position);
                    if (lumberCamp != null)
                    {
                        var lumberTarget = FindNearbyLumberTarget(world, village, lumberCamp, villager.Position);
                        if (lumberTarget.HasValue &&
                            TryAssignJob(village, villager, JobType.Lumber, lumberTarget, buildingId: lumberCamp.Id))
                        {
                            continue;
                        }
                    }
                }

                if (village.HasBuilding(BuildingKind.Quarry) &&
                    (villager.Role == VillagerRole.Miner || villager.Role == VillagerRole.Peasant))
                {
                    var quarry = village.GetNearestBuilding(BuildingKind.Quarry, villager.Position);
                    if (quarry != null)
                    {
                        var mineTarget = FindNearbyMineTarget(world, quarry);
                        if (mineTarget.HasValue &&
                            TryAssignJob(village, villager, JobType.Mine, mineTarget, buildingId: quarry.Id))
                        {
                            continue;
                        }
                    }
                }

                var fallbackLumber = FindNearbyLumberTarget(world, village, null, villager.Position);
                if (fallbackLumber.HasValue)
                {
                    TryAssignJob(village, villager, JobType.Lumber, fallbackLumber);
                }
            }
        }

        public void Update(float deltaTime, VoxelWorld world, float timeOfDay)
        {
            bool isNight = DayNightCycle.IsNight(timeOfDay);
            bool morning = _wasNight && !isNight;
            _wasNight = isNight;

            foreach (var village in _villages)
            {
                village.UpdateSimulation(deltaTime, timeOfDay);
                FarmCropGrowth.Advance(world, village, deltaTime, timeOfDay);
                village.WorkQueue.SyncWithWorld(world);
                FinalizeCompletedSites(village, world);
                TryAssignHaulers(village);
                village.Scheduler.CheckGoalProgress(village);

                if (morning || village.Scheduler.HasActiveNumericGoal())
                {
                    AutoAssignIdleWorkers(village, world);
                }

                var context = BuildContext(village);

                foreach (var villagerId in village.VillagerIds)
                {
                    if (!_villagers.TryGet(villagerId, out var villager))
                    {
                        continue;
                    }

                    villager.DriftHappinessToward(village.Happiness, deltaTime);
                    villager.RefreshWorkSpeed(village.GetWorkSpeedMultiplier());
                    ApplyBuildingWorkBonuses(village, villager);
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
                CreativeMode = CreativeMode,
                VillageCenter = village.Center,
                VillageRadius = village.Radius,
                StoragePosition = village.StoragePosition,
                Storage = village.Storage,
                ResolveBuildingSite = id => village.TryGetBuildingSite(id, out var site) ? site : null,
                ResolveBuilding = id => village.TryGetBuilding(id, out var building) ? building : null,
                ResolveVillager = id => _villagers.TryGet(id, out var villager) ? villager : null
            };
        }

        private void TryAssignHaulers(Village village)
        {
            foreach (var villagerId in village.VillagerIds)
            {
                if (!_villagers.TryGet(villagerId, out var hauler) || hauler.CurrentJob != JobType.Idle)
                {
                    continue;
                }

                if (hauler.Role != VillagerRole.Hauler && hauler.Role != VillagerRole.Peasant)
                {
                    continue;
                }

                if (!TryFindHaulWork(village, out var chest, out var sourceVillager, out var pickupPos, hauler.Id))
                {
                    continue;
                }

                if (chest != null)
                {
                    hauler.AssignHaulJob(pickupPos, chest.Id, null);
                }
                else if (sourceVillager != null)
                {
                    hauler.AssignHaulJob(pickupPos, null, sourceVillager.Id);
                }
            }
        }

        private bool TryAssignHaulWork(Village village, Villager hauler)
        {
            if (!TryFindHaulWork(village, out var chest, out var sourceVillager, out var pickupPos, hauler.Id))
            {
                return false;
            }

            if (chest != null)
            {
                hauler.AssignHaulJob(pickupPos, chest.Id, null);
                return true;
            }

            if (sourceVillager != null)
            {
                hauler.AssignHaulJob(pickupPos, null, sourceVillager.Id);
                return true;
            }

            return false;
        }

        private bool TryFindHaulWork(
            Village village,
            out OutputChest? chest,
            out Villager? sourceVillager,
            out Vector3 pickupPos,
            int? excludeVillagerId = null)
        {
            chest = village.FindFullestOutputChest();
            if (chest != null)
            {
                sourceVillager = null;
                pickupPos = chest.Position;
                return true;
            }

            foreach (var villagerId in village.VillagerIds)
            {
                if (!_villagers.TryGet(villagerId, out var worker))
                {
                    continue;
                }

                if (excludeVillagerId.HasValue && worker.Id == excludeVillagerId.Value)
                {
                    continue;
                }

                if (worker.CurrentJob is not (JobType.Lumber or JobType.Mine or JobType.Farm))
                {
                    continue;
                }

                if (!HaulLogistics.IsCarryFull(worker.Inventory))
                {
                    continue;
                }

                if (IsVillagerReservedForHaul(worker.Id))
                {
                    continue;
                }

                sourceVillager = worker;
                pickupPos = worker.Position;
                chest = null;
                return true;
            }

            sourceVillager = null;
            pickupPos = default;
            chest = null;
            return false;
        }

        private bool IsVillagerReservedForHaul(int villagerId)
        {
            foreach (var villager in _villagers.All)
            {
                if (villager.CurrentJob == JobType.Haul && villager.HaulSourceVillagerId == villagerId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyBuildingWorkBonuses(Village village, Villager villager)
        {
            if (!villager.AssignedBuildingId.HasValue ||
                !village.TryGetBuilding(villager.AssignedBuildingId.Value, out var building))
            {
                return;
            }

            if (building.Kind == BuildingKind.LumberCamp && villager.CurrentJob == JobType.Lumber)
            {
                villager.WorkSpeedMultiplier *= BuildingEffects.LumberCampWorkSpeedBonus;
            }
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

            int maxGoalId = 0;
            foreach (var entry in villageList)
            {
                foreach (var goal in entry.Goals)
                {
                    maxGoalId = Math.Max(maxGoalId, goal.Id);
                }
            }

            Village.ResetIdCounter(maxVillageId + 1);
            BuildingSite.ResetIdCounter(maxSiteId + 1);
            Villager.ResetIdCounter(maxVillagerId + 1);
            JobScheduler.ResetGoalIdCounter(maxGoalId + 1);

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

                village.WorkQueue.Restore(entry.WorkQueue);
                foreach (var goalEntry in entry.Goals)
                {
                    village.Scheduler.RestoreGoal(new VillageGoal
                    {
                        Id = goalEntry.Id,
                        Description = goalEntry.Description,
                        Priority = goalEntry.Priority,
                        Completed = goalEntry.Completed,
                        Kind = (VillageGoalKind)goalEntry.Kind,
                        StockBlock = goalEntry.StockBlock.HasValue ? (BlockType)goalEntry.StockBlock.Value : null,
                        TargetCount = goalEntry.TargetCount,
                        BlueprintId = goalEntry.BlueprintId,
                        BuildQueued = goalEntry.BuildQueued
                    });
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

                var goals = new List<VillageGoalSaveData>();
                foreach (var goal in village.Scheduler.Goals)
                {
                    goals.Add(new VillageGoalSaveData
                    {
                        Id = goal.Id,
                        Description = goal.Description,
                        Priority = goal.Priority,
                        Completed = goal.Completed,
                        Kind = (int)goal.Kind,
                        StockBlock = goal.StockBlock.HasValue ? (int)goal.StockBlock.Value : null,
                        TargetCount = goal.TargetCount,
                        BlueprintId = goal.BlueprintId,
                        BuildQueued = goal.BuildQueued
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
                    BuildingSites = sites,
                    WorkQueue = village.WorkQueue.Export(),
                    Goals = goals
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

        private static Vector3? FindNearbyLumberTarget(
            VoxelWorld world,
            Village village,
            VillageBuilding? lumberCamp,
            Vector3 from)
        {
            if (lumberCamp != null)
            {
                int radius = BuildingEffects.GetGatherScanRadius(BuildingKind.LumberCamp);
                var target = ScanForLumber(world, lumberCamp.AnchorX, lumberCamp.AnchorZ, radius);
                if (target.HasValue)
                {
                    return target;
                }
            }

            return ScanForLumber(world, village.AnchorX, village.AnchorZ, BuildingEffects.BaseGatherScanRadius);
        }

        private static Vector3? ScanForLumber(VoxelWorld world, int centerX, int centerZ, int radius)
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
                            if (block is BlockType.OakLog or BlockType.OakLeaves
                                or BlockType.BirchLog or BlockType.BirchLeaves
                                or BlockType.PineLog or BlockType.PineLeaves
                                or BlockType.WillowLog or BlockType.WillowLeaves
                                or BlockType.PalmLog or BlockType.PalmLeaves)
                            {
                                return new Vector3(x + 0.5f, y, z + 0.5f);
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static Vector3? FindNearbyMineTarget(VoxelWorld world, VillageBuilding quarry)
        {
            int centerX = quarry.AnchorX;
            int centerZ = quarry.AnchorZ;
            int radius = BuildingEffects.QuarryScanRadius;
            int minY = Math.Max(1, quarry.AnchorY - BuildingEffects.QuarryDepthLimit);
            int maxY = quarry.AnchorY;

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
                        int topY = Math.Min(maxY, world.GetHighestSolidY(x, z));
                        for (int y = topY; y >= minY; y--)
                        {
                            var block = world.GetBlock(x, y, z);
                            if (block is BlockType.Stone or BlockType.Cobblestone or BlockType.CoalOre
                                or BlockType.IronOre or BlockType.GoldOre or BlockType.Gravel or BlockType.MossStone)
                            {
                                return new Vector3(x + 0.5f, y, z + 0.5f);
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static Vector3? FindNearbyFarmTarget(
            VoxelWorld world,
            Village village,
            Vector3 from,
            VillageBuilding? farmPlot = null)
        {
            farmPlot ??= village.GetNearestBuilding(BuildingKind.FarmPlot, from);
            if (farmPlot == null)
            {
                return null;
            }

            return FarmCropHelper.FindBestFarmCell(world, village, farmPlot, from)
                ?? village.GetBuildingWorkPosition(farmPlot);
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
            out int anchorY,
            bool quickScan = false)
        {
            matched = StructureRegistry.All[0];
            matchRatio = 0f;
            anchorY = 0;
            int surfaceSearch = quickScan ? 1 : 8;
            int surfaceY = StructureFingerprint.FindSurfaceAnchorY(world, anchorX, anchorZ, surfaceSearch);
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

using System;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public sealed class JobDispatcher : IJobAssignment
    {
        private readonly VillagerManager _villagers;
        private readonly HaulCoordinator _haulCoordinator;

        public bool CreativeMode { get; set; }
        public Action<string>? ShowToast { get; set; }

        public JobDispatcher(VillagerManager villagers, HaulCoordinator haulCoordinator)
        {
            _villagers = villagers;
            _haulCoordinator = haulCoordinator;
        }

        public JobAssignmentResult TryAssignJob(
            Village village,
            Villager villager,
            JobType job,
            Vector3? target = null,
            int? buildingSiteId = null,
            int? buildingId = null)
        {
            if (villager.VillageId != village.Id)
            {
                return JobAssignmentResult.Failed(
                    JobAssignmentReasonCodes.WrongVillage,
                    "That villager does not belong to this settlement.",
                    "Select a citizen from your active settlement.");
            }

            job = NormalizeJob(job);
            int? assignedBuildingId = buildingId;

            switch (job)
            {
                case JobType.Build:
                    villager.Role = VillagerRole.Builder;
                    buildingSiteId ??= village.GetNearestPendingSite(villager.Position)?.Id;
                    if (!buildingSiteId.HasValue)
                    {
                        return JobAssignmentResult.Failed(
                            JobAssignmentReasonCodes.NoPendingSite,
                            "No construction site is queued.",
                            "Queue a building on the Build tab, then assign Build.");
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
                        ? JobTargetScanner.FindNearbyLumberTarget(_villagers.World, village, lumberCamp, villager.Position)
                        : null;
                    break;
                case JobType.Mine:
                    villager.Role = VillagerRole.Miner;
                    var quarry = ResolveAssignedBuilding(village, buildingId, BuildingKind.Quarry, villager.Position);
                    if (target == null)
                    {
                        if (quarry == null)
                        {
                            return JobAssignmentResult.Failed(
                                JobAssignmentReasonCodes.NoQuarry,
                                "No quarry — miners need a quarry building.",
                                "Queue a quarry on the Build tab, then assign Mine.");
                        }

                        assignedBuildingId = quarry.Id;
                        target = _villagers.World != null ? JobTargetScanner.FindNearbyMineTarget(_villagers.World, quarry) : null;
                    }
                    else if (quarry != null)
                    {
                        assignedBuildingId = quarry.Id;
                    }

                    break;
                case JobType.Farm:
                    if (!village.HasBuilding(BuildingKind.FarmPlot))
                    {
                        return JobAssignmentResult.Failed(
                            JobAssignmentReasonCodes.NoFarmPlot,
                            "No farm plot — farmers need a farm building.",
                            "Queue a farm plot on the Build tab, then assign Farm.");
                    }

                    villager.Role = VillagerRole.Farmer;
                    var farmPlot = ResolveAssignedBuilding(village, buildingId, BuildingKind.FarmPlot, villager.Position)
                        ?? village.GetPreferredFarmPlot(villager.Position, _villagers.All);
                    if (farmPlot == null)
                    {
                        return JobAssignmentResult.Failed(
                            JobAssignmentReasonCodes.NoFarmPlot,
                            "No farm plot available for this villager.",
                            "Queue a farm plot on the Build tab, then assign Farm.");
                    }

                    assignedBuildingId = farmPlot.Id;
                    target ??= village.GetBuildingWorkPosition(farmPlot);
                    if (_villagers.World != null)
                    {
                        target = JobTargetScanner.FindNearbyFarmTarget(_villagers.World, village, villager.Position, farmPlot)
                            ?? target;
                    }

                    break;
                case JobType.Craft:
                    if (!village.HasBuilding(BuildingKind.Workshop))
                    {
                        return JobAssignmentResult.Failed(
                            JobAssignmentReasonCodes.NoWorkshop,
                            "No workshop — smiths need a workshop building.",
                            "Queue a workshop on the Build tab, then assign Craft.");
                    }

                    if (!VillageWorkshopCrafting.NeedsSmithWork(village.Storage, CreativeMode))
                    {
                        return JobAssignmentResult.Failed(
                            JobAssignmentReasonCodes.NoSmithWork,
                            "Workshop has no smithing work right now.",
                            "Wait for tool repairs or queued crafts, or assign another job.");
                    }

                    villager.Role = VillagerRole.Smith;
                    var workshop = ResolveAssignedBuilding(village, buildingId, BuildingKind.Workshop, villager.Position);
                    if (workshop == null)
                    {
                        return JobAssignmentResult.Failed(
                            JobAssignmentReasonCodes.NoWorkshop,
                            "No workshop found near this villager.",
                            "Queue a workshop on the Build tab, then assign Craft.");
                    }

                    assignedBuildingId = workshop.Id;
                    target ??= village.GetBuildingWorkPosition(workshop);
                    break;
                case JobType.Hunt:
                    villager.Role = VillagerRole.Hunter;
                    target ??= villager.Position + new Vector3(2f, 0f, 0f);
                    break;
                case JobType.Mason:
                    villager.Role = VillagerRole.Mason;
                    var masonQuarry = ResolveAssignedBuilding(village, buildingId, BuildingKind.Quarry, villager.Position);
                    if (masonQuarry != null)
                    {
                        assignedBuildingId = masonQuarry.Id;
                    }

                    target ??= _villagers.World != null && masonQuarry != null
                        ? JobTargetScanner.FindNearbyMineTarget(_villagers.World, masonQuarry)
                        : null;
                    break;
                case JobType.Cook:
                    if (!village.HasBuilding(BuildingKind.Kitchen))
                    {
                        return JobAssignmentResult.Failed(
                            JobAssignmentReasonCodes.NoKitchen,
                            "No kitchen — cooks need a kitchen building.",
                            "Queue a kitchen on the Build tab, then assign Cook.");
                    }

                    villager.Role = VillagerRole.Cook;
                    var kitchen = ResolveAssignedBuilding(village, buildingId, BuildingKind.Kitchen, villager.Position);
                    if (kitchen == null)
                    {
                        return JobAssignmentResult.Failed(
                            JobAssignmentReasonCodes.NoKitchen,
                            "No kitchen found near this villager.",
                            "Queue a kitchen on the Build tab, then assign Cook.");
                    }

                    assignedBuildingId = kitchen.Id;
                    target ??= village.GetBuildingWorkPosition(kitchen);
                    break;
            }

            if (target == null && job is JobType.Lumber or JobType.Mine or JobType.Farm)
            {
                string reasonCode = job switch
                {
                    JobType.Lumber => JobAssignmentReasonCodes.NoLumberTarget,
                    JobType.Mine => JobAssignmentReasonCodes.NoMineTarget,
                    _ => JobAssignmentReasonCodes.NoTarget
                };
                string message = job switch
                {
                    JobType.Lumber => "No marked trees or lumber targets nearby.",
                    JobType.Mine => "No mineable stone near the quarry.",
                    JobType.Farm => "No farm work available right now.",
                    _ => "No work target found."
                };
                string remediation = job switch
                {
                    JobType.Lumber => "Shift+click trees to mark them, or paint a lumber zone.",
                    JobType.Mine => "Ensure the quarry has reachable stone nearby.",
                    JobType.Farm => "Wait for crops to grow or queue a farm plot.",
                    _ => "Try another job or queue the required building."
                };
                return JobAssignmentResult.Failed(reasonCode, message, remediation);
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

            return JobAssignmentResult.Succeeded();
        }

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

            Vector3? target = JobTargetScanner.FindNearbyStockTarget(world, village, blockType);
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

            return TryAssignJob(village, villager, job, target, buildingId: buildingId).Success;
        }

        public void AutoAssignIdleWorkers(Village village, VoxelWorld world)
        {
            village.Scheduler.CheckGoalProgress(village);
            var goal = village.Scheduler.GetTopOpenGoal();
            if (goal != null)
            {
                village.Scheduler.TryApplyGoal(village, world, this, goal);
            }

            foreach (var villager in VillageSettlementHealth.EnumerateLiveCitizens(village, _villagers))
            {
                if (villager.CurrentJob != JobType.Idle)
                {
                    continue;
                }

                if (goal != null && village.Scheduler.TryAssignForGoal(village, world, this, goal, villager))
                {
                    continue;
                }

                if (villager.Role == VillagerRole.Hauler && _haulCoordinator.TryAssignHaulWork(village, villager))
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
                        var farmTarget = JobTargetScanner.FindNearbyFarmTarget(world, village, villager.Position, plot);
                        if (farmTarget.HasValue &&
                            TryAssignJob(village, villager, JobType.Farm, farmTarget, buildingId: plot.Id).Success)
                        {
                            continue;
                        }
                    }
                }

                if (village.HasBuilding(BuildingKind.Workshop) &&
                    VillageWorkshopCrafting.NeedsSmithWork(village.Storage, CreativeMode) &&
                    (villager.Role == VillagerRole.Smith || villager.Role == VillagerRole.Peasant))
                {
                    if (TryAssignJob(village, villager, JobType.Craft).Success)
                    {
                        continue;
                    }
                }

                if (TryAssignFromWorkQueue(village, world, villager))
                {
                    continue;
                }

                if (_haulCoordinator.TryAssignHaulWork(village, villager))
                {
                    continue;
                }

                var highestDemandBlock = village.Economy.GetHighestDemandBlock();
                if (highestDemandBlock.HasValue && GatherBlockClassifier.CanGather(villager.Role, highestDemandBlock.Value))
                {
                    var stockTarget = JobTargetScanner.FindNearbyStockTarget(world, village, highestDemandBlock.Value);
                    if (stockTarget.HasValue)
                    {
                        var category = GatherBlockClassifier.GetCategory(highestDemandBlock.Value);
                        var job = category == GatherCategory.Mine ? JobType.Mine : JobType.Lumber;
                        int? buildingId = null;
                        if (category == GatherCategory.Mine && village.HasBuilding(BuildingKind.Quarry))
                        {
                            buildingId = village.GetNearestBuilding(BuildingKind.Quarry, villager.Position)?.Id;
                        }
                        else if (category == GatherCategory.Lumber && village.HasBuilding(BuildingKind.LumberCamp))
                        {
                            buildingId = village.GetNearestBuilding(BuildingKind.LumberCamp, villager.Position)?.Id;
                        }
                        if (TryAssignJob(village, villager, job, stockTarget, buildingId: buildingId).Success)
                        {
                            continue;
                        }
                    }
                }

                if (village.HasBuilding(BuildingKind.LumberCamp) &&
                    (villager.Role == VillagerRole.Lumberjack || villager.Role == VillagerRole.Peasant))
                {
                    var lumberCamp = village.GetNearestBuilding(BuildingKind.LumberCamp, villager.Position);
                    if (lumberCamp != null)
                    {
                        var lumberTarget = JobTargetScanner.FindNearbyLumberTarget(world, village, lumberCamp, villager.Position);
                        if (lumberTarget.HasValue &&
                            TryAssignJob(village, villager, JobType.Lumber, lumberTarget, buildingId: lumberCamp.Id).Success)
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
                        var mineTarget = JobTargetScanner.FindNearbyMineTarget(world, quarry);
                        if (mineTarget.HasValue &&
                            TryAssignJob(village, villager, JobType.Mine, mineTarget, buildingId: quarry.Id).Success)
                        {
                            continue;
                        }
                    }
                }

                var fallbackLumber = JobTargetScanner.FindNearbyLumberTarget(world, village, null, villager.Position);
                if (fallbackLumber.HasValue)
                {
                    TryAssignJob(village, villager, JobType.Lumber, fallbackLumber);
                }
            }
        }

        public bool TryMarkWorkBlock(VoxelWorld world, Village village, int x, int y, int z, out string message)
        {
            message = string.Empty;
            var block = world.GetBlock(x, y, z);
            if (!GatherBlockClassifier.IsGatherable(block))
            {
                message = "That block can't be marked for workers.";
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

        public void AssignIdleWorkersFromQueue(Village village, VoxelWorld world)
        {
            foreach (var villager in VillageSettlementHealth.EnumerateLiveCitizens(village, _villagers))
            {
                if (villager.CurrentJob != JobType.Idle)
                {
                    continue;
                }

                TryAssignFromWorkQueue(village, world, villager);
            }
        }

        public bool TryQueueBlueprint(
            VoxelWorld world,
            Village village,
            string blueprintId,
            int anchorX,
            int anchorZ,
            IItemContainer payer,
            int anchorY = -1)
        {
            if (!PlayerStructureRegistry.TryGet(blueprintId, out var blueprint))
            {
                ShowToast?.Invoke("Unknown blueprint.");
                return false;
            }

            int resolvedY = anchorY >= 0
                ? anchorY
                : StructureFingerprint.FindSurfaceAnchorY(world, anchorX, anchorZ);

            if (!CanPlaceBlueprint(world, village, blueprint, anchorX, anchorZ, payer, resolvedY))
            {
                ShowToast?.Invoke($"Cannot place {blueprint.DisplayName} here.");
                return false;
            }

            if (!CreativeMode)
            {
                blueprint.TryConsumeCosts(payer);
            }

            village.QueueBuild(blueprint, anchorX, resolvedY, anchorZ);
            ShowToast?.Invoke($"Queued {blueprint.DisplayName} for construction.");
            return true;
        }

        public bool TryAssignBuilderToSite(Village village, BuildingSite site)
        {
            foreach (var villager in VillageSettlementHealth.EnumerateLiveCitizens(village, _villagers))
            {
                if (villager.CurrentJob != JobType.Idle)
                {
                    continue;
                }

                if (TryAssignJob(village, villager, JobType.Build, null, site.Id).Success)
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanPlaceBlueprint(
            VoxelWorld world,
            Village village,
            BuildingBlueprint blueprint,
            int anchorX,
            int anchorZ,
            IItemContainer payer,
            int anchorY = -1)
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

            int resolvedY = anchorY >= 0
                ? anchorY
                : StructureFingerprint.FindSurfaceAnchorY(world, anchorX, anchorZ);
            foreach (var block in blueprint.Template.Blocks)
            {
                int wx = anchorX + block.Dx;
                int wy = resolvedY + block.Dy;
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

            foreach (var villager in VillageSettlementHealth.EnumerateLiveCitizens(village, _villagers))
            {
                if (villager.CurrentJob != JobType.Idle)
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

        private static bool CanAcceptBlueprintBlock(BlockType current) =>
            current == BlockType.Air || BlueprintPlacementHelper.IsNaturalTerrainBlock(current);
    }
}

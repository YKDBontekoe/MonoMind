using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public sealed class VillageGoal
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; }
        public bool Completed { get; set; }
        public VillageGoalKind Kind { get; set; } = VillageGoalKind.Generic;
        public BlockType? StockBlock { get; set; }
        public int TargetCount { get; set; }
        public string? BlueprintId { get; set; }
        public bool BuildQueued { get; set; }
    }

    public sealed class JobScheduler
    {
        private static int _nextGoalId = 1;
        private readonly List<VillageGoal> _goals = new();

        public IReadOnlyList<VillageGoal> Goals => _goals;

        public static void ResetGoalIdCounter(int nextId) => _nextGoalId = Math.Max(1, nextId);

        public int AddGoal(string description, int priority = 0)
        {
            if (VillageGoalParser.TryParseDescription(description, out var parsed))
            {
                return parsed.Kind switch
                {
                    VillageGoalKind.Stock when parsed.StockBlock.HasValue =>
                        AddStockGoal(parsed.StockBlock.Value, parsed.TargetCount, priority, parsed.Description),
                    VillageGoalKind.Build when !string.IsNullOrEmpty(parsed.BlueprintId) =>
                        AddBuildGoal(parsed.BlueprintId, priority, parsed.Description),
                    _ => AddGenericGoal(description, priority)
                };
            }

            return AddGenericGoal(description, priority);
        }

        public int AddStockGoal(BlockType blockType, int targetCount, int priority = 0, string? description = null)
        {
            var goal = new VillageGoal
            {
                Id = _nextGoalId++,
                Kind = VillageGoalKind.Stock,
                Description = description ?? $"Stock {targetCount} {blockType}",
                Priority = priority,
                StockBlock = blockType,
                TargetCount = targetCount
            };
            InsertGoal(goal);
            return goal.Id;
        }

        public int AddBuildGoal(string blueprintId, int priority = 0, string? description = null)
        {
            string label = description ?? $"Build {blueprintId}";
            if (PlayerStructureRegistry.TryGet(blueprintId, out var blueprint))
            {
                label = description ?? $"Build {blueprint.DisplayName}";
            }

            var goal = new VillageGoal
            {
                Id = _nextGoalId++,
                Kind = VillageGoalKind.Build,
                Description = label,
                Priority = priority,
                BlueprintId = blueprintId
            };
            InsertGoal(goal);
            return goal.Id;
        }

        public void RestoreGoal(VillageGoal goal)
        {
            goal.Id = goal.Id > 0 ? goal.Id : _nextGoalId++;
            _nextGoalId = Math.Max(_nextGoalId, goal.Id + 1);
            InsertGoal(goal);
        }

        public void CompleteGoal(int goalId)
        {
            foreach (var goal in _goals)
            {
                if (goal.Id == goalId)
                {
                    goal.Completed = true;
                    break;
                }
            }
        }

        public VillageGoal? GetTopOpenGoal()
        {
            foreach (var goal in _goals)
            {
                if (!goal.Completed)
                {
                    return goal;
                }
            }

            return null;
        }

        public bool HasActiveNumericGoal()
        {
            foreach (var goal in _goals)
            {
                if (!goal.Completed && goal.Kind is VillageGoalKind.Stock or VillageGoalKind.Build)
                {
                    return true;
                }
            }

            return false;
        }

        public void CheckGoalProgress(Village village)
        {
            foreach (var goal in _goals)
            {
                if (goal.Completed)
                {
                    continue;
                }

                switch (goal.Kind)
                {
                    case VillageGoalKind.Stock when goal.StockBlock.HasValue:
                        if (village.Storage.CountBlock(goal.StockBlock.Value) >= goal.TargetCount)
                        {
                            goal.Completed = true;
                        }

                        break;
                    case VillageGoalKind.Build when !string.IsNullOrEmpty(goal.BlueprintId):
                        if (village.HasCompletedBuilding(goal.BlueprintId))
                        {
                            goal.Completed = true;
                        }

                        break;
                }
            }
        }

        public int GetStockProgress(VillageGoal goal, Village village)
        {
            if (goal.Kind != VillageGoalKind.Stock || !goal.StockBlock.HasValue)
            {
                return 0;
            }

            return village.Storage.CountBlock(goal.StockBlock.Value);
        }

        public bool AssignJob(
            Villager villager,
            JobType job,
            Vector3? target = null,
            int? buildingSiteId = null,
            int? assignedBuildingId = null)
        {
            if (villager == null)
            {
                return false;
            }

            villager.AssignJob(job, target, buildingSiteId, assignedBuildingId);
            return true;
        }

        public bool AssignRole(Villager villager, VillagerRole role)
        {
            if (villager == null)
            {
                return false;
            }

            villager.Role = role;
            return true;
        }

        public bool CancelJob(Villager villager)
        {
            villager?.AssignJob(JobType.Idle, null, null);
            return true;
        }

        public bool TryApplyGoal(Village village, VoxelWorld world, IJobAssignment assignment, VillageGoal goal)
        {
            switch (goal.Kind)
            {
                case VillageGoalKind.Stock:
                    return false;
                case VillageGoalKind.Build:
                    return TryApplyBuildGoal(village, world, assignment, goal);
                default:
                    return TryApplyGenericGoal(village, world, assignment, goal);
            }
        }

        public bool TryAssignForGoal(
            Village village,
            VoxelWorld world,
            IJobAssignment assignment,
            VillageGoal goal,
            Villager villager)
        {
            if (goal.Completed || villager.CurrentJob != JobType.Idle)
            {
                return false;
            }

            switch (goal.Kind)
            {
                case VillageGoalKind.Stock when goal.StockBlock.HasValue:
                    if (village.Storage.CountBlock(goal.StockBlock.Value) >= goal.TargetCount)
                    {
                        goal.Completed = true;
                        return false;
                    }

                    return assignment.TryAssignStockGoalWorker(village, world, villager, goal.StockBlock.Value);
                case VillageGoalKind.Build:
                    var site = village.GetNearestPendingSite(villager.Position);
                    if (site != null &&
                        !string.IsNullOrEmpty(goal.BlueprintId) &&
                        string.Equals(site.BlueprintId, goal.BlueprintId, StringComparison.OrdinalIgnoreCase))
                    {
                        return assignment.TryAssignJob(village, villager, JobType.Build, null, site.Id);
                    }

                    if (site != null && string.IsNullOrEmpty(goal.BlueprintId))
                    {
                        return assignment.TryAssignJob(village, villager, JobType.Build, null, site.Id);
                    }

                    return false;
                default:
                    return false;
            }
        }

        private bool TryApplyBuildGoal(Village village, VoxelWorld world, IJobAssignment assignment, VillageGoal goal)
        {
            if (goal.BuildQueued || string.IsNullOrEmpty(goal.BlueprintId))
            {
                return false;
            }

            if (village.HasPendingOrCompleteBuilding(goal.BlueprintId))
            {
                goal.BuildQueued = true;
                return false;
            }

            if (!PlayerStructureRegistry.TryGet(goal.BlueprintId, out var blueprint))
            {
                return false;
            }

            int[] offsets = { 3, 6, -3, -6, 9, -9, 12, -12 };
            foreach (int dx in offsets)
            {
                foreach (int dz in offsets)
                {
                    int ax = village.AnchorX + dx;
                    int az = village.AnchorZ + dz;
                    const int anchoredOffsetMax = 9;
                    int anchorY =
                        Math.Abs(dx) <= anchoredOffsetMax && Math.Abs(dz) <= anchoredOffsetMax
                            ? village.AnchorY
                            : -1;
                    if (assignment.TryQueueBlueprint(world, village, goal.BlueprintId, ax, az, village.Storage, anchorY))
                    {
                        goal.BuildQueued = true;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryApplyGenericGoal(Village village, VoxelWorld world, IJobAssignment assignment, VillageGoal goal)
        {
            string description = goal.Description.ToLowerInvariant();
            if (description.Contains("storage") || description.Contains("expand_storage"))
            {
                if (PlayerStructureRegistry.TryGet("storage_crate", out var blueprint) &&
                    (assignment.CreativeMode || blueprint.CanAfford(village.Storage)))
                {
                    int ax = village.AnchorX + 6;
                    int az = village.AnchorZ;
                    return assignment.TryQueueBlueprint(world, village, "storage_crate", ax, az, village.Storage);
                }
            }

            if (description.Contains("farm") || description.Contains("food"))
            {
                if (PlayerStructureRegistry.TryGet("farm_plot", out var farm) &&
                    (assignment.CreativeMode || farm.CanAfford(village.Storage)))
                {
                    int ax = village.AnchorX - 6;
                    int az = village.AnchorZ;
                    return assignment.TryQueueBlueprint(world, village, "farm_plot", ax, az, village.Storage);
                }
            }

            if (description.Contains("mine") || description.Contains("quarry") || description.Contains("ore"))
            {
                if (PlayerStructureRegistry.TryGet("quarry", out var quarry) &&
                    (assignment.CreativeMode || quarry.CanAfford(village.Storage)))
                {
                    int ax = village.AnchorX + 6;
                    int az = village.AnchorZ + 6;
                    return assignment.TryQueueBlueprint(world, village, "quarry", ax, az, village.Storage);
                }
            }

            if (description.Contains("house") || description.Contains("peasant"))
            {
                if (PlayerStructureRegistry.TryGet("peasant_house", out _))
                {
                    return assignment.TryQueueBlueprint(world, village, "peasant_house", village.AnchorX + 3, village.AnchorZ + 3, village.Storage);
                }
            }

            return false;
        }

        private int AddGenericGoal(string description, int priority)
        {
            var goal = new VillageGoal
            {
                Id = _nextGoalId++,
                Kind = VillageGoalKind.Generic,
                Description = description,
                Priority = priority
            };
            InsertGoal(goal);
            return goal.Id;
        }

        public void ClearGoals()
        {
            _goals.Clear();
        }

        public void RemoveGoal(int id)
        {
            _goals.RemoveAll(g => g.Id == id);
        }

        private void InsertGoal(VillageGoal goal)
        {
            _goals.Add(goal);
            _goals.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }
}

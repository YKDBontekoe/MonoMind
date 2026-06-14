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
    }

    public sealed class JobScheduler
    {
        private static int _nextGoalId = 1;
        private readonly List<VillageGoal> _goals = new();

        public IReadOnlyList<VillageGoal> Goals => _goals;

        public int AddGoal(string description, int priority = 0)
        {
            var goal = new VillageGoal
            {
                Id = _nextGoalId++,
                Description = description,
                Priority = priority
            };
            _goals.Add(goal);
            _goals.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            return goal.Id;
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

        public bool AssignJob(Villager villager, JobType job, Vector3? target = null, int? buildingSiteId = null)
        {
            if (villager == null)
            {
                return false;
            }

            villager.AssignJob(job, target, buildingSiteId);
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

        public bool TryApplyGoal(Village village, VoxelWorld world, VillageManager manager, VillageGoal goal)
        {
            string description = goal.Description.ToLowerInvariant();
            if (description.Contains("storage") || description.Contains("expand_storage"))
            {
                if (PlayerStructureRegistry.TryGet("storage_crate", out var blueprint) &&
                    blueprint.CanAfford(village.Storage))
                {
                    int ax = village.AnchorX + 6;
                    int az = village.AnchorZ;
                    return manager.TryQueueBlueprint(world, village, "storage_crate", ax, az, village.Storage);
                }
            }

            if (description.Contains("farm") || description.Contains("food"))
            {
                if (PlayerStructureRegistry.TryGet("farm_plot", out var farm) && farm.CanAfford(village.Storage))
                {
                    int ax = village.AnchorX - 6;
                    int az = village.AnchorZ;
                    return manager.TryQueueBlueprint(world, village, "farm_plot", ax, az, village.Storage);
                }
            }

            return false;
        }
    }
}

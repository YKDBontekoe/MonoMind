using System.Collections.Generic;
using Autonocraft.Domain.Core;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public sealed class VillageSimulation
    {
        private readonly VillagerManager _villagers;
        private readonly JobDispatcher _dispatcher;
        private readonly HaulCoordinator _haulCoordinator;
        private VillageEvents? _events;
        private bool _wasNight;

        public bool CreativeMode { get; set; }

        public VillageSimulation(
            VillagerManager villagers,
            JobDispatcher dispatcher,
            HaulCoordinator haulCoordinator)
        {
            _villagers = villagers;
            _dispatcher = dispatcher;
            _haulCoordinator = haulCoordinator;
        }

        public void SetVillageEvents(VillageEvents events) => _events = events;

        public void Update(
            IReadOnlyList<Village> villages,
            HashSet<int> finalizedSites,
            float deltaTime,
            VoxelWorld world,
            float timeOfDay,
            AnimalManager animalManager)
        {
            bool isNight = DayNightCycle.IsNight(timeOfDay);
            bool morning = _wasNight && !isNight;
            _wasNight = isNight;

            foreach (var village in villages)
            {
                VillageSettlementHealth.SyncPopulationRegistry(village, _villagers);
                village.Economy.Clear();
                village.Economy.SyncFromStorage(village.Storage);
                foreach (var site in village.BuildingSites)
                {
                    if (!site.IsComplete)
                    {
                        foreach (var block in site.PendingBlocks)
                        {
                            village.Economy.RecordDemand(block.Type, 1);
                        }
                    }
                }

                village.UpdateSimulation(deltaTime, timeOfDay);
                if (village.DailyNeedsSimulatedThisFrame)
                {
                    if (village.ConsecutiveDaysWithoutFood >= 4 && VillageSettlementHealth.GetLivePopulation(village, _villagers) > 0)
                    {
                        Villager? starveling = null;
                        foreach (var citizen in VillageSettlementHealth.EnumerateLiveCitizens(village, _villagers))
                        {
                            starveling = citizen;
                            break;
                        }

                        if (starveling != null)
                        {
                            _events?.ShowToast?.Invoke($"{starveling.Name} left the village (starving)");
                            _events?.PlaySfx?.Invoke("food");
                            village.UnregisterVillager(starveling.Id);
                            _villagers.Despawn(starveling.Id);
                        }
                    }
                }
                FarmCropGrowth.Advance(world, village, deltaTime, timeOfDay);
                village.WorkQueue.SyncWithWorld(world);
                FinalizeCompletedSites(village, world, finalizedSites);
                _haulCoordinator.TryAssignHaulers(village);
                village.Scheduler.CheckGoalProgress(village);

                if (morning || village.Scheduler.HasActiveNumericGoal())
                {
                    _dispatcher.AutoAssignIdleWorkers(village, world);
                }

                var context = BuildContext(village, animalManager);

                foreach (var villager in VillageSettlementHealth.EnumerateLiveCitizens(village, _villagers))
                {
                    VillagerNeedsTracker.ApplyNeeds(villager, villager.Needs, deltaTime, isNight);
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

            _villagers.Update(deltaTime, world, villages);
        }

        private void FinalizeCompletedSites(Village village, VoxelWorld world, HashSet<int> finalizedSites)
        {
            foreach (var site in village.BuildingSites)
            {
                site.SyncWithWorld(world);
                if (!site.IsComplete || finalizedSites.Contains(site.Id))
                {
                    continue;
                }

                if (PlayerStructureRegistry.TryGet(site.BlueprintId, out var blueprint))
                {
                    village.CompleteBuilding(blueprint, site);
                    finalizedSites.Add(site.Id);
                    _events?.OnBuildingCompleted(blueprint.DisplayName);
                    _events?.CheckTierChange(village);
                }
            }
        }

        private VillageContext BuildContext(Village village, AnimalManager animalManager)
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
                ResolveVillager = id => _villagers.TryGet(id, out var villager) ? villager : null,
                Animals = animalManager,
                Events = _events
            };
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
    }
}

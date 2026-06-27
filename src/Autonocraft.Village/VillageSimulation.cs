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
        public bool IsTestMode { get; set; } = false;

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
            AnimalManager animalManager,
            float timeScale = DayNightCycle.DefaultTimeScale)
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

                    TryGrowFamily(village, world);
                    AutoDelegate(village);
                }
                FarmCropGrowth.Advance(world, village, deltaTime, timeOfDay, timeScale);
                village.WorkQueue.SyncWithWorld(world, village);
                FinalizeCompletedSites(village, world, finalizedSites);
                _haulCoordinator.TryAssignHaulers(village);
                village.Scheduler.CheckGoalProgress(village);
                VillageGrowthPlanner.EnsureOrganicGrowthPlan(village, _villagers);

                // Wake up or put to sleep BEFORE auto-assigning jobs
                foreach (var villager in VillageSettlementHealth.EnumerateLiveCitizens(village, _villagers))
                {
                    if (isNight && villager.CurrentJob != JobType.Build)
                    {
                        if (villager.CurrentJob != JobType.Sleep)
                        {
                            villager.AssignJob(JobType.Sleep, villager.Position, null);
                        }
                    }
                    else if (villager.CurrentJob == JobType.Sleep && !isNight)
                    {
                        villager.WakeFromSleep();
                    }
                }

                // Auto-assign ALL idle workers every tick — not just on morning
                _dispatcher.AutoAssignIdleWorkers(village, world);

                var context = BuildContext(village, animalManager);

                foreach (var villager in VillageSettlementHealth.EnumerateLiveCitizens(village, _villagers))
                {
                    VillagerNeedsTracker.ApplyNeeds(villager, villager.Needs, deltaTime, isNight);
                    villager.DriftHappinessToward(village.Happiness, deltaTime);
                    villager.RefreshWorkSpeed(village.GetWorkSpeedMultiplier());
                    ApplyBuildingWorkBonuses(village, villager);
                    villager.Update(deltaTime, world, context);
                }
            }

            _villagers.Update(deltaTime, world, villages);
        }

        private void FinalizeCompletedSites(Village village, VoxelWorld world, HashSet<int> finalizedSites)
        {
            var completedSiteIds = new List<int>();
            foreach (var site in village.BuildingSites)
            {
                if (village.HasCompletedBuildingAt(site.BlueprintId, site.AnchorX, site.AnchorY, site.AnchorZ))
                {
                    completedSiteIds.Add(site.Id);
                    finalizedSites.Add(site.Id);
                    continue;
                }

                site.SyncWithWorld(world);
                if (!site.IsComplete || finalizedSites.Contains(site.Id))
                {
                    continue;
                }

                if (PlayerStructureRegistry.TryGet(site.BlueprintId, out var blueprint))
                {
                    village.CompleteBuilding(blueprint, site);
                    finalizedSites.Add(site.Id);
                    completedSiteIds.Add(site.Id);
                    _events?.OnBuildingCompleted(blueprint.DisplayName);
                    _events?.CheckTierChange(village);
                }
            }

            foreach (int siteId in completedSiteIds)
            {
                village.RemoveBuildingSite(siteId);
            }
        }

        private VillageContext BuildContext(Village village, AnimalManager animalManager)
        {
            return new VillageContext
            {
                Village = village,
                CreativeMode = CreativeMode,
                IsTestMode = IsTestMode,
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

        private void TryGrowFamily(Village village, VoxelWorld world)
        {
            int livePopulation = VillageSettlementHealth.GetLivePopulation(village, _villagers);
            if (!village.TryAdvanceFamilyGrowth(livePopulation))
            {
                return;
            }

            var spawn = VillageSpawnHelper.FindSpawnPosition(
                world,
                village,
                village.Id * 486187739 ^ livePopulation * 16777619 ^ village.Favor);
            var villager = _villagers.Spawn(village.Id, spawn, village.Id ^ livePopulation ^ village.PopulationCap);
            villager.IsGrounded = true;
            village.RegisterVillager(villager.Id);
            _events?.OnFamilyArrival(villager);
        }

        private void AutoDelegate(Village village)
        {
            var contracts = VillageAgentContracts.Suggest(village, _villagers);

            // First, automatically accept all profitable Trade contracts (cost 0)
            foreach (var contract in contracts)
            {
                if (!contract.AlreadyActive && contract.CanAfford && contract.FavorCost == 0)
                {
                    contract.Apply?.Invoke(village);
                    _events?.ShowToast?.Invoke($"Steward completed contract: {contract.Label}");
                }
            }

            // Then, if we have a lot of favor, accept ONE building/stock contract per day
            foreach (var contract in contracts)
            {
                if (!contract.AlreadyActive && contract.CanAfford && contract.FavorCost > 0)
                {
                    if (village.TrySpendFavor(contract.FavorCost))
                    {
                        contract.Apply?.Invoke(village);
                        _events?.ShowToast?.Invoke($"Steward commissioned: {contract.Label}");
                        break;
                    }
                }
            }
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

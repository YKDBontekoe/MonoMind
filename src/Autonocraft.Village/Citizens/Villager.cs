using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Items;
using Autonocraft.Entities;
using Autonocraft.Village;
using Autonocraft.Village.Jobs;
using Autonocraft.World;

namespace Autonocraft.Entities
{
    public enum VillagerAiPhase
    {
        Idle,
        PathTo,
        Working,
        Hauling,
        Sleeping
    }

    public sealed class Villager
    {
        private static int _nextId = 1;

        public int Id { get; }
        public int VillageId { get; set; }
        public string Name { get; set; }
        public VillagerRole Role { get; set; } = VillagerRole.Peasant;
        public JobType CurrentJob { get; private set; } = JobType.Idle;
        public VillagerAiPhase AiPhase { get; private set; } = VillagerAiPhase.Idle;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw;
        public bool IsGrounded { get; set; }
        public float Happiness { get; set; } = 1f;
        public float WorkSpeedMultiplier { get; set; } = 1f;
        public VillagerNeeds Needs { get; } = new VillagerNeeds();

        public Vector3? JobTarget { get; private set; }
        public int? AssignedBuildingSiteId { get; private set; }
        public int? AssignedBuildingId { get; private set; }
        public int? HaulSourceChestId { get; private set; }
        public int? HaulSourceVillagerId { get; private set; }
        public bool HaulIsDelivering { get; private set; }
        public Vector3? MarkedResource { get; set; }
        public int? HomeBuildingId { get; set; }
        public JobType PreSleepJob { get; set; } = JobType.Idle;
        public Vector3? PreSleepJobTarget { get; set; }
        public int? PreSleepBuildingSiteId { get; set; }
        public int? PreSleepBuildingId { get; set; }
        public int? PreSleepHaulSourceChestId { get; set; }
        public int? PreSleepHaulSourceVillagerId { get; set; }
        public bool PreSleepHaulIsDelivering { get; set; }
        public Vector3? PreSleepMarkedResource { get; set; }

        public Inventory Inventory { get; } = new Inventory(8);
        public ItemStack EquippedTool { get; private set; }
        public VillagerPersonaData Persona { get; private set; }
        public VillagerSkills Skills { get; } = new();

        public float IdleTime { get; set; }
        public Vector3 WanderDirection;
        public float WanderDistanceRemaining;
        public float WorkTimer { get; set; }
        public float BreakProgress { get; set; }
        public Vector3 LastMovePosition { get; set; }
        public float StuckTimer { get; set; }
        public Vector3? LastPathGoal { get; set; }

        internal Random JobRandom => _rng;

        private readonly Random _rng;
        private readonly List<Vector3> _path = new();
        private int _pathIndex;

        public const float Width = 0.5f;
        public const float Height = 1.7f;
        public const float WalkSpeed = 3.5f;
        public const float WorkInterval = 0.8f;

        public Villager(int villageId, Vector3 position, int seed, string? name = null, int? explicitId = null)
        {
            Id = explicitId ?? _nextId++;
            if (explicitId.HasValue && explicitId.Value >= _nextId)
            {
                _nextId = explicitId.Value + 1;
            }

            VillageId = villageId;
            _rng = new Random(seed ^ (Id * 397));
            Name = name ?? GenerateName(_rng);
            Position = position;
            Velocity = Vector3.Zero;
            Persona = VillagerPersonaData.Generate(_rng, Role);
            Happiness = 0.9f + (float)_rng.NextDouble() * 0.1f;
            IdleTime = 1f + (float)_rng.NextDouble() * 2f;
        }

        public void RestoreSkills(
            int miningLevel,
            float miningXp,
            int woodcuttingLevel,
            float woodcuttingXp,
            int farmingLevel,
            float farmingXp)
        {
            Skills.Mining = new SkillProgress { Level = miningLevel > 0 ? miningLevel : 1, Xp = miningXp };
            Skills.Woodcutting = new SkillProgress { Level = woodcuttingLevel > 0 ? woodcuttingLevel : 1, Xp = woodcuttingXp };
            Skills.Farming = new SkillProgress { Level = farmingLevel > 0 ? farmingLevel : 1, Xp = farmingXp };
        }

        public void RestoreNeeds(float food, float rest, float social)
        {
            Needs.Food = food;
            Needs.Rest = rest;
            Needs.Social = social;
        }

        public void DriftHappinessToward(float villageHappiness, float deltaTime)
        {
            float t = Math.Clamp(deltaTime * 0.02f, 0f, 1f);
            Happiness = Math.Clamp(
                Happiness + (villageHappiness - Happiness) * t,
                0.1f,
                1f);
        }

        public void RefreshWorkSpeed(float villageMultiplier)
        {
            float personal = Math.Clamp(Happiness, 0.5f, 1.25f);
            WorkSpeedMultiplier = villageMultiplier * personal;
            if (CurrentJob == JobType.Mine)
            {
                WorkSpeedMultiplier *= VillagerTraits.GetMineSpeedMultiplier(Persona.Trait);
            }
        }

        public static void ResetIdCounter(int nextId) => _nextId = Math.Max(1, nextId);

        public void RestorePersona(string trait)
        {
            if (!string.IsNullOrWhiteSpace(trait))
            {
                Persona.RestoreTrait(trait);
            }
        }

        public void AssignJob(JobType job, Vector3? target, int? buildingSiteId, int? assignedBuildingId = null)
        {
            if (job == JobType.Sleep)
            {
                // Remember the previous job and state so we can restore it after sleeping
                if (CurrentJob != JobType.Sleep && CurrentJob != JobType.Idle)
                {
                    PreSleepJob = CurrentJob;
                    PreSleepJobTarget = JobTarget;
                    PreSleepBuildingSiteId = AssignedBuildingSiteId;
                    PreSleepBuildingId = AssignedBuildingId;
                    PreSleepHaulSourceChestId = HaulSourceChestId;
                    PreSleepHaulSourceVillagerId = HaulSourceVillagerId;
                    PreSleepHaulIsDelivering = HaulIsDelivering;
                    PreSleepMarkedResource = MarkedResource;
                }
            }
            else if (job != JobType.Idle)
            {
                // When actively assigned a real job, clear the pre-sleep reminder
                ClearPreSleepState();
            }

            CurrentJob = job;
            JobTarget = target;
            AssignedBuildingSiteId = buildingSiteId;
            AssignedBuildingId = assignedBuildingId;
            HaulSourceChestId = null;
            HaulSourceVillagerId = null;
            HaulIsDelivering = false;
            AiPhase = job == JobType.Idle ? VillagerAiPhase.Idle : VillagerAiPhase.PathTo;
            WorkTimer = 0f;
            BreakProgress = 0f;
            _path.Clear();
            _pathIndex = 0;
        }

        public void AssignHaulJob(Vector3? pickupTarget, int? sourceChestId, int? sourceVillagerId)
        {
            ClearPreSleepState();

            CurrentJob = JobType.Haul;
            JobTarget = pickupTarget;
            AssignedBuildingSiteId = null;
            HaulSourceChestId = sourceChestId;
            HaulSourceVillagerId = sourceVillagerId;
            HaulIsDelivering = false;
            AiPhase = VillagerAiPhase.PathTo;
            WorkTimer = 0f;
            BreakProgress = 0f;
            _path.Clear();
            _pathIndex = 0;
        }

        public void WakeFromSleep()
        {
            if (PreSleepJob != JobType.Idle && PreSleepJob != JobType.Sleep)
            {
                CurrentJob = PreSleepJob;
                JobTarget = PreSleepJobTarget;
                AssignedBuildingSiteId = PreSleepBuildingSiteId;
                AssignedBuildingId = PreSleepBuildingId;
                HaulSourceChestId = PreSleepHaulSourceChestId;
                HaulSourceVillagerId = PreSleepHaulSourceVillagerId;
                HaulIsDelivering = PreSleepHaulIsDelivering;
                MarkedResource = PreSleepMarkedResource;

                AiPhase = CurrentJob == JobType.Idle ? VillagerAiPhase.Idle : VillagerAiPhase.PathTo;
                WorkTimer = 0f;
                BreakProgress = 0f;
                _path.Clear();
                _pathIndex = 0;
            }
            else
            {
                AssignJob(JobType.Idle, null, null);
            }

            ClearPreSleepState();
        }

        private void ClearPreSleepState()
        {
            PreSleepJob = JobType.Idle;
            PreSleepJobTarget = null;
            PreSleepBuildingSiteId = null;
            PreSleepBuildingId = null;
            PreSleepHaulSourceChestId = null;
            PreSleepHaulSourceVillagerId = null;
            PreSleepHaulIsDelivering = false;
            PreSleepMarkedResource = null;
        }

        public void SetPath(IReadOnlyList<Vector3> waypoints)
        {
            _path.Clear();
            foreach (var wp in waypoints)
            {
                _path.Add(wp);
            }

            _pathIndex = 0;
            AiPhase = _path.Count > 0 ? VillagerAiPhase.PathTo : VillagerAiPhase.Working;
        }

        public void ClearPath()
        {
            _path.Clear();
            _pathIndex = 0;
            LastPathGoal = null;
            StuckTimer = 0f;
        }

        public bool HasPath => _pathIndex < _path.Count;

        public Vector3? GetCurrentPathTarget()
        {
            if (_pathIndex >= _path.Count)
            {
                return JobTarget;
            }

            return _path[_pathIndex];
        }

        public void AdvancePath()
        {
            if (_pathIndex < _path.Count)
            {
                _pathIndex++;
            }
        }

        public bool HasReachedPathEnd() => _pathIndex >= _path.Count;

        public void OnBlocked()
        {
            WanderDirection = Vector3.Zero;
            WanderDistanceRemaining = 0f;
            IdleTime = 1f;
            _path.Clear();
            _pathIndex = 0;
            AiPhase = VillagerAiPhase.Idle;
        }

        public void SetAiPhase(VillagerAiPhase phase) => AiPhase = phase;

        public void SetJobTarget(Vector3? target) => JobTarget = target;

        public void ClearJobTarget() => JobTarget = null;

        public void SetEquippedTool(ItemStack tool) => EquippedTool = tool;

        public void SetHaulDelivering(bool delivering) => HaulIsDelivering = delivering;

        public void ClearHaulSources()
        {
            HaulSourceChestId = null;
            HaulSourceVillagerId = null;
        }

        public void RestoreHaulState(int? haulSourceChestId, int? haulSourceVillagerId, bool haulIsDelivering)
        {
            HaulSourceChestId = haulSourceChestId;
            HaulSourceVillagerId = haulSourceVillagerId;
            HaulIsDelivering = haulIsDelivering;
        }

        public void RestoreAiPhase(VillagerAiPhase phase) => AiPhase = phase;

        public void Update(float deltaTime, VoxelWorld world, VillageContext context)
        {
            JobRegistry.Tick(this, deltaTime, world, context);
        }

        private static string GenerateName(Random rng)
        {
            string[] firsts = {
                "Aldric", "Bryn", "Cedric", "Dara", "Eldon", "Faye", "Greta", "Hale", "Iris", "Joren",
                "Kael", "Lyra", "Merrick", "Nia", "Orion", "Pippa", "Quinn", "Rowan", "Silas", "Talia",
                "Ulric", "Vesper", "Wren", "Xander", "Yara", "Zephyr", "Aria", "Bram", "Cora", "Dane"
            };
            string[] lasts = {
                "Tanner", "Miller", "Smith", "Fletcher", "Cooper", "Weaver", "Carter", "Thatcher", "Baker", "Mason",
                "Wright", "Fisher", "Hunter", "Glover", "Potter", "Dyer", "Archer", "Brewer", "Chandler", "Cook",
                "Farmer", "Gardener", "Herder", "Miner", "Shepherd", "Slater", "Tailor", "Tucker", "Turner", "Ward"
            };
            return $"{firsts[rng.Next(firsts.Length)]} {lasts[rng.Next(lasts.Length)]}";
        }
    }

    public sealed class VillagerPersonaData
    {
        public string Trait { get; private set; } = "cheerful";
        public string SpeechStyle { get; init; } = "plain";

        public void RestoreTrait(string trait)
        {
            if (!string.IsNullOrWhiteSpace(trait))
            {
                Trait = trait;
            }
        }

        public static VillagerPersonaData Generate(Random rng, VillagerRole role)
        {
            string[] traits = { "cheerful", "grumpy", "quiet", "eager", "wise", "strong", "green_thumb" };
            return new VillagerPersonaData
            {
                Trait = traits[rng.Next(traits.Length)],
                SpeechStyle = role switch
                {
                    VillagerRole.Builder => "practical",
                    VillagerRole.Lumberjack => "blunt",
                    VillagerRole.Miner => "gruff",
                    VillagerRole.Farmer => "gentle",
                    VillagerRole.Smith => "terse",
                    VillagerRole.Hauler => "steady",
                    _ => "plain"
                }
            };
        }
    }

    public sealed class VillageContext
    {
        public Village.Village? Village { get; init; }
        public bool CreativeMode { get; init; }
        public bool IsTestMode { get; init; }
        public Vector3 VillageCenter { get; init; }
        public float VillageRadius { get; init; } = 32f;
        public Vector3 StoragePosition { get; init; }
        public Village.VillageStorage Storage { get; init; } = null!;
        public Func<int, Village.BuildingSite?>? ResolveBuildingSite { get; init; }
        public Func<int, Village.VillageBuilding?>? ResolveBuilding { get; init; }
        public Func<int, Villager?>? ResolveVillager { get; init; }
        public AnimalManager? Animals { get; init; }
        public VillageEvents? Events { get; init; }

        public bool TryGetBuildingSite(int id, out Village.BuildingSite site)
        {
            site = null!;
            var resolved = ResolveBuildingSite?.Invoke(id);
            if (resolved == null)
            {
                return false;
            }

            site = resolved;
            return true;
        }

        public bool TryGetVillager(int id, out Villager villager)
        {
            villager = null!;
            var resolved = ResolveVillager?.Invoke(id);
            if (resolved == null)
            {
                return false;
            }

            villager = resolved;
            return true;
        }
    }
}

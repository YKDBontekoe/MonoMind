using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Items;
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
        public bool IsGrounded { get; private set; }
        public float Happiness { get; set; } = 1f;
        public float WorkSpeedMultiplier { get; set; } = 1f;

        public Vector3? JobTarget { get; private set; }
        public int? AssignedBuildingSiteId { get; private set; }
        public Vector3? MarkedResource { get; set; }
        public int? HomeBuildingId { get; set; }

        public Inventory Inventory { get; } = new Inventory(8);
        public VillagerPersonaData Persona { get; private set; }

        public float IdleTime { get; private set; }
        public Vector3 WanderDirection;
        public float WanderDistanceRemaining;
        public float WorkTimer;
        public float BreakProgress;

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
            _rng = new Random(seed ^ Id);
            Name = name ?? GenerateName(_rng);
            Position = position;
            Velocity = Vector3.Zero;
            Persona = VillagerPersonaData.Generate(_rng, Role);
            IdleTime = 1f + (float)_rng.NextDouble() * 2f;
        }

        public static void ResetIdCounter(int nextId) => _nextId = Math.Max(1, nextId);

        public void RestorePersona(string trait)
        {
            if (!string.IsNullOrWhiteSpace(trait))
            {
                Persona.RestoreTrait(trait);
            }
        }

        public void AssignJob(JobType job, Vector3? target, int? buildingSiteId)
        {
            CurrentJob = job;
            JobTarget = target;
            AssignedBuildingSiteId = buildingSiteId;
            AiPhase = job == JobType.Idle ? VillagerAiPhase.Idle : VillagerAiPhase.PathTo;
            WorkTimer = 0f;
            BreakProgress = 0f;
            _path.Clear();
            _pathIndex = 0;
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

        public bool HasReachedPathEnd()
        {
            return _pathIndex >= _path.Count;
        }

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

        public void Update(float deltaTime, VoxelWorld world, VillageContext context)
        {
            switch (CurrentJob)
            {
                case JobType.Sleep:
                    UpdateSleep(deltaTime);
                    break;
                case JobType.Gather:
                    UpdateGather(deltaTime, world, context);
                    break;
                case JobType.Build:
                    UpdateBuild(deltaTime, world, context);
                    break;
                case JobType.Haul:
                    UpdateHaul(deltaTime, world, context);
                    break;
                case JobType.Craft:
                    UpdateCraft(deltaTime, context);
                    break;
                default:
                    UpdateIdle(deltaTime, world, context);
                    break;
            }
        }

        private void UpdateSleep(float deltaTime)
        {
            AiPhase = VillagerAiPhase.Sleeping;
            Velocity = Vector3.Zero;
            WanderDirection = Vector3.Zero;
        }

        private void UpdateIdle(float deltaTime, VoxelWorld world, VillageContext context)
        {
            if (AiPhase == VillagerAiPhase.PathTo && TryMoveAlongPath(deltaTime, world))
            {
                return;
            }

            UpdateWander(deltaTime, world, context.VillageRadius, context.VillageCenter);
        }

        private void UpdateGather(float deltaTime, VoxelWorld world, VillageContext context)
        {
            var target = MarkedResource ?? JobTarget;
            if (!target.HasValue)
            {
                AssignJob(JobType.Idle, null, null);
                return;
            }

            if (AiPhase == VillagerAiPhase.PathTo)
            {
                if (TryMoveToward(deltaTime, world, target.Value))
                {
                    return;
                }

                AiPhase = VillagerAiPhase.Working;
            }

            if (AiPhase == VillagerAiPhase.Working)
            {
                int bx = (int)MathF.Floor(target.Value.X);
                int by = (int)MathF.Floor(target.Value.Y);
                int bz = (int)MathF.Floor(target.Value.Z);
                var block = world.GetBlock(bx, by, bz);
                if (block == BlockType.Air || !block.IsCollidable())
                {
                    AssignJob(JobType.Haul, context.StoragePosition, null);
                    return;
                }

                WorkTimer += deltaTime * WorkSpeedMultiplier;
                if (WorkTimer >= WorkInterval)
                {
                    WorkTimer = 0f;
                    world.SetBlock(bx, by, bz, BlockType.Air);
                    Inventory.AddItem(ItemStack.CreateBlock(block, 1));
                    AssignJob(JobType.Haul, context.StoragePosition, null);
                }
            }
        }

        private void UpdateBuild(float deltaTime, VoxelWorld world, VillageContext context)
        {
            if (!AssignedBuildingSiteId.HasValue || !context.TryGetBuildingSite(AssignedBuildingSiteId.Value, out var site))
            {
                AssignJob(JobType.Idle, null, null);
                return;
            }

            if (!site.TryGetNextBlock(out var nextBlock))
            {
                AssignJob(JobType.Idle, null, null);
                return;
            }

            var targetPos = new Vector3(site.AnchorX + nextBlock.Dx + 0.5f, site.AnchorY + nextBlock.Dy, site.AnchorZ + nextBlock.Dz + 0.5f);
            if (AiPhase == VillagerAiPhase.PathTo)
            {
                if (TryMoveToward(deltaTime, world, targetPos))
                {
                    return;
                }

                AiPhase = VillagerAiPhase.Working;
            }

            WorkTimer += deltaTime * WorkSpeedMultiplier;
            if (WorkTimer >= WorkInterval * 0.5f)
            {
                WorkTimer = 0f;
                site.TryPlaceNextBlock(world, context.Storage, Width, Height, Position);
                AiPhase = VillagerAiPhase.PathTo;
            }
        }

        private void UpdateHaul(float deltaTime, VoxelWorld world, VillageContext context)
        {
            if (Inventory.CountBlock(BlockType.Air) == 0 && !Inventory.GetSlot(0).IsEmpty)
            {
                // has items
            }
            else if (IsInventoryEmpty())
            {
                AssignJob(JobType.Idle, null, null);
                return;
            }

            if (AiPhase == VillagerAiPhase.PathTo)
            {
                if (TryMoveToward(deltaTime, world, context.StoragePosition))
                {
                    return;
                }

                AiPhase = VillagerAiPhase.Working;
            }

            DepositAllToStorage(context.Storage);
            AssignJob(JobType.Idle, null, null);
        }

        private void UpdateCraft(float deltaTime, VillageContext context)
        {
            WorkTimer += deltaTime * WorkSpeedMultiplier;
            if (WorkTimer >= WorkInterval * 2f)
            {
                WorkTimer = 0f;
                if (Role == VillagerRole.Farmer)
                {
                    context.Village?.AddFarmFood(0.5f);
                }

                AssignJob(JobType.Idle, null, null);
            }
        }

        private void DepositAllToStorage(Village.VillageStorage storage)
        {
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                var stack = Inventory.GetSlot(i);
                if (stack.IsEmpty)
                {
                    continue;
                }

                if (storage.AddItem(stack))
                {
                    Inventory.SetSlot(i, ItemStack.Empty);
                }
            }
        }

        private bool IsInventoryEmpty()
        {
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                if (!Inventory.GetSlot(i).IsEmpty)
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryMoveAlongPath(float deltaTime, VoxelWorld world)
        {
            var target = GetCurrentPathTarget();
            if (!target.HasValue)
            {
                AiPhase = VillagerAiPhase.Idle;
                return false;
            }

            if (TryMoveToward(deltaTime, world, target.Value))
            {
                return true;
            }

            AdvancePath();
            return !HasReachedPathEnd();
        }

        private bool TryMoveToward(float deltaTime, VoxelWorld world, Vector3 target)
        {
            var flatTarget = new Vector3(target.X, Position.Y, target.Z);
            var toTarget = flatTarget - Position;
            toTarget.Y = 0f;
            float dist = toTarget.Length();
            if (dist < 0.6f)
            {
                Velocity = Vector3.Zero;
                WanderDirection = Vector3.Zero;
                return false;
            }

            WanderDirection = Vector3.Normalize(toTarget);
            Yaw = MathF.Atan2(WanderDirection.X, WanderDirection.Z);
            ApplyMovement(deltaTime, world, WanderDirection * WalkSpeed);
            return true;
        }

        private void UpdateWander(float deltaTime, VoxelWorld world, float radius, Vector3 center)
        {
            if (WanderDistanceRemaining > 0f)
            {
                ApplyMovement(deltaTime, world, WanderDirection * WalkSpeed * 0.5f);
                WanderDistanceRemaining -= MathF.Abs(Velocity.X) * deltaTime + MathF.Abs(Velocity.Z) * deltaTime;
                if (WanderDistanceRemaining <= 0f)
                {
                    WanderDirection = Vector3.Zero;
                    IdleTime = 1f + (float)_rng.NextDouble();
                }

                return;
            }

            IdleTime -= deltaTime;
            if (IdleTime > 0f)
            {
                WanderDirection = Vector3.Zero;
                Velocity = new Vector3(0f, Velocity.Y, 0f);
                return;
            }

            if (!IsGrounded)
            {
                ApplyMovement(deltaTime, world, Vector3.Zero);
                return;
            }

            float angle = (float)(_rng.NextDouble() * MathF.PI * 2f);
            WanderDirection = new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle));
            WanderDistanceRemaining = 1.5f + (float)_rng.NextDouble() * 3f;

            var offset = Position - center;
            offset.Y = 0f;
            if (offset.Length() > radius)
            {
                WanderDirection = Vector3.Normalize(center - Position);
                WanderDirection.Y = 0f;
            }
        }

        private void ApplyMovement(float deltaTime, VoxelWorld world, Vector3 horizontal)
        {
            var state = new EntityCollisionState
            {
                Position = Position,
                Velocity = Velocity,
                IsGrounded = IsGrounded
            };

            EntityCollision.ApplyGravityAndMove(
                ref state,
                world,
                deltaTime,
                Width,
                Height,
                Height * 0.85f,
                horizontal,
                swimUp: false,
                swimDown: false);

            Position = state.Position;
            Velocity = state.Velocity;
            IsGrounded = state.IsGrounded;
        }

        private static string GenerateName(Random rng)
        {
            string[] first = { "Aldric", "Bryn", "Cedric", "Dara", "Eldon", "Faye", "Greta", "Hale", "Iris", "Joren" };
            return first[rng.Next(first.Length)];
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
            string[] traits = { "cheerful", "grumpy", "quiet", "eager", "wise" };
            return new VillagerPersonaData
            {
                Trait = traits[rng.Next(traits.Length)],
                SpeechStyle = role switch
                {
                    VillagerRole.Builder => "practical",
                    VillagerRole.Lumberjack => "blunt",
                    VillagerRole.Farmer => "gentle",
                    VillagerRole.Smith => "terse",
                    _ => "plain"
                }
            };
        }
    }

    public sealed class VillageContext
    {
        public Village.Village? Village { get; init; }
        public Vector3 VillageCenter { get; init; }
        public float VillageRadius { get; init; } = 32f;
        public Vector3 StoragePosition { get; init; }
        public Village.VillageStorage Storage { get; init; } = null!;
        public Func<int, Village.BuildingSite?>? ResolveBuildingSite { get; init; }

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
    }
}

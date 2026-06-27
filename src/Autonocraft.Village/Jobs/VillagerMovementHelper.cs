using System;
using System.Numerics;
using Autonocraft.Entities;
using Autonocraft.World;

namespace Autonocraft.Village.Jobs
{
    internal static class VillagerMovementHelper
    {
        public static bool TryMoveAlongPath(Villager villager, float deltaTime, VoxelWorld world)
        {
            var target = villager.GetCurrentPathTarget();
            if (!target.HasValue)
            {
                villager.SetAiPhase(VillagerAiPhase.Idle);
                return false;
            }

            if (TryMoveDirectToward(villager, deltaTime, world, target.Value))
            {
                return true;
            }

            villager.AdvancePath();
            return !villager.HasReachedPathEnd();
        }

        public static bool TryMoveToward(Villager villager, float deltaTime, VoxelWorld world, Vector3 target)
        {
            var flatGoal = new Vector3(target.X, villager.Position.Y, target.Z);
            float goalDistSq = Vector3.DistanceSquared(
                new Vector3(villager.Position.X, 0f, villager.Position.Z),
                new Vector3(target.X, 0f, target.Z));
            bool pathGoalChanged = !villager.LastPathGoal.HasValue ||
                Vector3.DistanceSquared(villager.LastPathGoal.Value, target) > 1.5f;

            if ((villager.HasPath && !pathGoalChanged) ||
                (goalDistSq > 9f && TryBeginPath(villager, world, target)))
            {
                if (TryMoveAlongPath(villager, deltaTime, world))
                {
                    TrackStuck(villager, deltaTime, world, target);
                    return true;
                }
            }

            bool moved = TryMoveDirectToward(villager, deltaTime, world, flatGoal);
            TrackStuck(villager, deltaTime, world, target);
            return moved;
        }

        private static bool TryBeginPath(Villager villager, VoxelWorld world, Vector3 target)
        {
            if (villager.LastPathGoal.HasValue &&
                Vector3.DistanceSquared(villager.LastPathGoal.Value, target) <= 1.5f &&
                villager.HasPath)
            {
                return true;
            }

            int range = Math.Clamp((int)MathF.Ceiling(Vector3.Distance(villager.Position, target)) + 4, 12, 48);
            if (!VoxelPathfinder.TryFindPath(world, villager.Position, target, range, out var waypoints) || waypoints.Count == 0)
            {
                // Fallback: Try pathing to a closer block
                var dir = Vector3.Normalize(villager.Position - target);
                var fallbackTarget = target + dir * 2f;
                
                if (!VoxelPathfinder.TryFindPath(world, villager.Position, fallbackTarget, range, out waypoints) || waypoints.Count == 0)
                {
                    return false;
                }
            }

            if (waypoints.Count > 1)
            {
                waypoints.RemoveAt(0);
            }

            villager.SetPath(waypoints);
            villager.LastPathGoal = target;
            return true;
        }

        private static bool TryMoveDirectToward(Villager villager, float deltaTime, VoxelWorld world, Vector3 target)
        {
            var flatTarget = new Vector3(target.X, villager.Position.Y, target.Z);
            var toTarget = flatTarget - villager.Position;
            toTarget.Y = 0f;
            float dist = toTarget.Length();
            if (dist < 0.6f)
            {
                villager.Velocity = Vector3.Zero;
                villager.WanderDirection = Vector3.Zero;
                return false;
            }

            villager.WanderDirection = Vector3.Normalize(toTarget);
            villager.Yaw = MathF.Atan2(villager.WanderDirection.X, villager.WanderDirection.Z);
            ApplyMovement(villager, deltaTime, world, villager.WanderDirection * Villager.WalkSpeed);
            return true;
        }

        private static void TrackStuck(Villager villager, float deltaTime, VoxelWorld world, Vector3 target)
        {
            var before = villager.LastMovePosition;
            float movedSq = Vector3.DistanceSquared(
                new Vector3(before.X, 0f, before.Z),
                new Vector3(villager.Position.X, 0f, villager.Position.Z));
            villager.LastMovePosition = villager.Position;

            if (movedSq > 0.0025f || villager.WanderDirection.LengthSquared() <= 0.01f)
            {
                villager.StuckTimer = 0f;
                return;
            }

            // Clamp deltaTime so a single lag spike (e.g. 2s frame) doesn't instantly trigger stuck
            float clampedDt = MathF.Min(deltaTime, 0.1f);
            villager.StuckTimer += clampedDt;
            if (villager.StuckTimer < 0.55f)
            {
                return;
            }

            villager.ClearPath();
            villager.StuckTimer = 0f;
            if (VoxelPathfinder.TryFindPath(world, villager.Position, target, 48, out var waypoints) && waypoints.Count > 0)
            {
                if (waypoints.Count > 1)
                {
                    waypoints.RemoveAt(0);
                }

                villager.SetPath(waypoints);
                villager.LastPathGoal = target;
            }
        }

        public static void UpdateWander(
            Villager villager,
            float deltaTime,
            VoxelWorld world,
            float radius,
            Vector3 center,
            Random rng)
        {
            if (villager.WanderDistanceRemaining > 0f)
            {
                ApplyMovement(villager, deltaTime, world, villager.WanderDirection * Villager.WalkSpeed * 0.5f);
                villager.WanderDistanceRemaining -= MathF.Abs(villager.Velocity.X) * deltaTime + MathF.Abs(villager.Velocity.Z) * deltaTime;
                if (villager.WanderDistanceRemaining <= 0f)
                {
                    villager.WanderDirection = Vector3.Zero;
                    villager.IdleTime = 1f + (float)rng.NextDouble();
                }

                return;
            }

            villager.IdleTime -= deltaTime;
            if (villager.IdleTime > 0f)
            {
                villager.WanderDirection = Vector3.Zero;
                villager.Velocity = new Vector3(0f, villager.Velocity.Y, 0f);
                return;
            }

            if (!villager.IsGrounded)
            {
                ApplyMovement(villager, deltaTime, world, Vector3.Zero);
                return;
            }

            float angle = (float)(rng.NextDouble() * MathF.PI * 2f);
            villager.WanderDirection = new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle));
            villager.WanderDistanceRemaining = 1.5f + (float)rng.NextDouble() * 3f;

            var offset = villager.Position - center;
            offset.Y = 0f;
            if (offset.Length() > radius)
            {
                villager.WanderDirection = Vector3.Normalize(center - villager.Position);
                villager.WanderDirection = new Vector3(villager.WanderDirection.X, 0f, villager.WanderDirection.Z);
            }
        }

        public static void ApplyMovement(Villager villager, float deltaTime, VoxelWorld world, Vector3 horizontal)
        {
            var state = new EntityCollisionState
            {
                Position = villager.Position,
                Velocity = villager.Velocity,
                IsGrounded = villager.IsGrounded
            };

            EntityCollision.ApplyGravityAndMove(
                ref state,
                world,
                deltaTime,
                Villager.Width,
                Villager.Height,
                Villager.Height * 0.85f,
                horizontal,
                swimUp: false,
                swimDown: false);

            villager.Position = state.Position;
            villager.Velocity = state.Velocity;
            villager.IsGrounded = state.IsGrounded;
        }
    }
}

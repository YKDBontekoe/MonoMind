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

            if (TryMoveToward(villager, deltaTime, world, target.Value))
            {
                return true;
            }

            villager.AdvancePath();
            return !villager.HasReachedPathEnd();
        }

        public static bool TryMoveToward(Villager villager, float deltaTime, VoxelWorld world, Vector3 target)
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

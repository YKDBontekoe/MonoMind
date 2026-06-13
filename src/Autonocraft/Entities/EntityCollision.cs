using System;
using System.Numerics;
using Autonocraft.World;

namespace Autonocraft.Entities
{
    public ref struct EntityCollisionState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public bool IsGrounded;
    }

    public static class EntityCollision
    {
        public const float Gravity = -32f;
        public const float TerminalVelocity = -50f;
        public const float Damping = 0.15f;

        public static void ResolveAxis(
            ref EntityCollisionState state,
            VoxelWorld world,
            int axis,
            float width,
            float height)
        {
            float minX = state.Position.X - width / 2f;
            float maxX = state.Position.X + width / 2f;
            float minY = state.Position.Y;
            float maxY = state.Position.Y + height;
            float minZ = state.Position.Z - width / 2f;
            float maxZ = state.Position.Z + width / 2f;

            int startX = (int)MathF.Floor(minX);
            int endX = (int)MathF.Floor(maxX);
            int startY = (int)MathF.Floor(minY);
            int endY = (int)MathF.Floor(maxY);
            int startZ = (int)MathF.Floor(minZ);
            int endZ = (int)MathF.Floor(maxZ);

            float epsilon = 0.001f;

            for (int y = startY; y <= endY; y++)
            {
                for (int z = startZ; z <= endZ; z++)
                {
                    for (int x = startX; x <= endX; x++)
                    {
                        if (world.GetBlock(x, y, z) == BlockType.Air)
                        {
                            continue;
                        }

                        if (axis == 0)
                        {
                            if (state.Velocity.X > 0)
                            {
                                state.Position.X = x - width / 2f - epsilon;
                            }
                            else if (state.Velocity.X < 0)
                            {
                                state.Position.X = x + 1f + width / 2f + epsilon;
                            }
                            state.Velocity.X = 0;
                        }
                        else if (axis == 1)
                        {
                            if (state.Velocity.Y > 0)
                            {
                                state.Position.Y = y - height - epsilon;
                            }
                            else if (state.Velocity.Y < 0)
                            {
                                state.Position.Y = y + 1f + epsilon;
                                state.IsGrounded = true;
                            }
                            state.Velocity.Y = 0;
                        }
                        else
                        {
                            if (state.Velocity.Z > 0)
                            {
                                state.Position.Z = z - width / 2f - epsilon;
                            }
                            else if (state.Velocity.Z < 0)
                            {
                                state.Position.Z = z + 1f + width / 2f + epsilon;
                            }
                            state.Velocity.Z = 0;
                        }

                        minX = state.Position.X - width / 2f;
                        maxX = state.Position.X + width / 2f;
                        minY = state.Position.Y;
                        maxY = state.Position.Y + height;
                        minZ = state.Position.Z - width / 2f;
                        maxZ = state.Position.Z + width / 2f;
                        startX = (int)MathF.Floor(minX);
                        endX = (int)MathF.Floor(maxX);
                        startY = (int)MathF.Floor(minY);
                        endY = (int)MathF.Floor(maxY);
                        startZ = (int)MathF.Floor(minZ);
                        endZ = (int)MathF.Floor(maxZ);
                    }
                }
            }
        }

        public static void ApplyGravityAndMove(
            ref EntityCollisionState state,
            VoxelWorld world,
            float deltaTime,
            float width,
            float height,
            Vector3 horizontalVelocity)
        {
            state.Velocity.Y += Gravity * deltaTime;
            if (state.Velocity.Y < TerminalVelocity)
            {
                state.Velocity.Y = TerminalVelocity;
            }

            state.Velocity.X = horizontalVelocity.X;
            state.Velocity.Z = horizontalVelocity.Z;

            state.Position.X += state.Velocity.X * deltaTime;
            ResolveAxis(ref state, world, 0, width, height);

            state.Position.Y += state.Velocity.Y * deltaTime;
            state.IsGrounded = false;
            ResolveAxis(ref state, world, 1, width, height);

            state.Position.Z += state.Velocity.Z * deltaTime;
            ResolveAxis(ref state, world, 2, width, height);
        }

        public static bool IsSpaceClearAt(VoxelWorld world, Vector3 position, float width, float height)
        {
            float minX = position.X - width / 2f;
            float maxX = position.X + width / 2f;
            float minY = position.Y;
            float maxY = position.Y + height;
            float minZ = position.Z - width / 2f;
            float maxZ = position.Z + width / 2f;

            int startX = (int)MathF.Floor(minX);
            int endX = (int)MathF.Floor(maxX);
            int startY = (int)MathF.Floor(minY);
            int endY = (int)MathF.Floor(maxY);
            int startZ = (int)MathF.Floor(minZ);
            int endZ = (int)MathF.Floor(maxZ);

            for (int by = startY; by <= endY; by++)
            {
                for (int bz = startZ; bz <= endZ; bz++)
                {
                    for (int bx = startX; bx <= endX; bx++)
                    {
                        if (world.GetBlock(bx, by, bz) != BlockType.Air)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}

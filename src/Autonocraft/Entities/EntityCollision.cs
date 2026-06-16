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
        public bool InWater;
        public bool HeadUnderwater;
        public bool OnWaterSurface;
    }

    public static class EntityCollision
    {
        public const float Gravity = -38f;
        public const float TerminalVelocity = -50f;
        public const float Damping = 0.15f;

        public const float SwimSpeedScale = 1.4f;
        public const float SwimUpForce = 6f;
        public const float SwimDownForce = 5f;
        public const float WaterGravityScale = 0.15f;
        public const float WaterTerminalVelocity = -8f;
        public const float WaterHorizontalDamping = 0.35f;

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

            int yStart = startY;
            int yEnd = endY;
            int yStep = 1;
            if (axis == 1 && state.Velocity.Y < 0)
            {
                yStart = endY;
                yEnd = startY;
                yStep = -1;
            }

            int zStart = startZ;
            int zEnd = endZ;
            int zStep = 1;
            if (axis == 2 && state.Velocity.Z < 0)
            {
                zStart = endZ;
                zEnd = startZ;
                zStep = -1;
            }

            int xStart = startX;
            int xEnd = endX;
            int xStep = 1;
            if (axis == 0 && state.Velocity.X < 0)
            {
                xStart = endX;
                xEnd = startX;
                xStep = -1;
            }

            for (int y = yStart; yStep > 0 ? y <= yEnd : y >= yEnd; y += yStep)
            {
                for (int z = zStart; zStep > 0 ? z <= zEnd : z >= zEnd; z += zStep)
                {
                    for (int x = xStart; xStep > 0 ? x <= xEnd : x >= xEnd; x += xStep)
                    {
                        if (!world.GetBlock(x, y, z).IsCollidable())
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

                        yStart = startY; yEnd = endY;
                        if (axis == 1 && state.Velocity.Y < 0) { yStart = endY; yEnd = startY; }

                        zStart = startZ; zEnd = endZ;
                        if (axis == 2 && state.Velocity.Z < 0) { zStart = endZ; zEnd = startZ; }

                        xStart = startX; xEnd = endX;
                        if (axis == 0 && state.Velocity.X < 0) { xStart = endX; xEnd = startX; }
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
            float eyeHeight,
            Vector3 horizontalVelocity,
            bool swimUp,
            bool swimDown)
        {
            var water = WaterQuery.GetBodyState(world, state.Position, width, height, eyeHeight);
            state.InWater = water.InWater;
            state.HeadUnderwater = water.HeadUnderwater;
            state.OnWaterSurface = water.OnSurface;

            float gravity = Gravity;
            float terminalVelocity = TerminalVelocity;

            if (water.InWater)
            {
                gravity *= WaterGravityScale;
                terminalVelocity = WaterTerminalVelocity;
                horizontalVelocity *= SwimSpeedScale;

                if (swimUp)
                {
                    state.Velocity.Y += SwimUpForce * deltaTime;
                }

                if (swimDown)
                {
                    state.Velocity.Y -= SwimDownForce * deltaTime;
                }

                if (horizontalVelocity == Vector3.Zero)
                {
                    state.Velocity.X *= MathF.Pow(WaterHorizontalDamping, deltaTime * 10f);
                    state.Velocity.Z *= MathF.Pow(WaterHorizontalDamping, deltaTime * 10f);
                    if (MathF.Abs(state.Velocity.X) < 0.01f) state.Velocity.X = 0;
                    if (MathF.Abs(state.Velocity.Z) < 0.01f) state.Velocity.Z = 0;
                }
            }

            state.Velocity.Y += gravity * deltaTime;
            if (state.Velocity.Y < terminalVelocity)
            {
                state.Velocity.Y = terminalVelocity;
            }

            if (horizontalVelocity != Vector3.Zero)
            {
                state.Velocity.X = horizontalVelocity.X;
                state.Velocity.Z = horizontalVelocity.Z;
            }

            state.Position.X += state.Velocity.X * deltaTime;
            ResolveAxis(ref state, world, 0, width, height);

            state.Position.Y += state.Velocity.Y * deltaTime;
            state.IsGrounded = false;
            ResolveAxis(ref state, world, 1, width, height);

            // Surface float: resist sinking only — never push upward or you can walk up water columns.
            if (water.InWater && !water.HeadUnderwater && !swimDown)
            {
                if (state.Velocity.Y < 0f)
                {
                    state.Velocity.Y = MathF.Min(state.Velocity.Y + 10f * deltaTime, -0.05f);
                }
                else if (state.Velocity.Y > 0.25f)
                {
                    state.Velocity.Y *= MathF.Pow(0.35f, deltaTime * 10f);
                }
            }

            state.Position.Z += state.Velocity.Z * deltaTime;
            ResolveAxis(ref state, world, 2, width, height);

            // Grounded only on solid blocks, never on water surface.
            state.OnWaterSurface = water.InWater && !water.HeadUnderwater;
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
                        if (world.GetBlock(bx, by, bz).IsCollidable())
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

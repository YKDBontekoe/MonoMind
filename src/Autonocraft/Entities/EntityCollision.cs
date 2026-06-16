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
        public bool InLava;
        public bool HeadInLava;
        public bool OnLavaSurface;
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

        private const float CollisionEpsilon = 0.001f;

        private static float GetColliderTopY(int blockY, BlockType block)
        {
            return blockY + (block.IsSlab() ? 0.5f : 1f);
        }

        private static bool IntersectsCollider(
            float minX, float minY, float minZ,
            float maxX, float maxY, float maxZ,
            int blockX, int blockY, int blockZ,
            BlockType block)
        {
            float blockMaxX = blockX + 1f;
            float blockMaxY = GetColliderTopY(blockY, block);
            float blockMaxZ = blockZ + 1f;

            if (minX >= blockMaxX - CollisionEpsilon || maxX <= blockX + CollisionEpsilon)
            {
                return false;
            }

            if (minY >= blockMaxY - CollisionEpsilon || maxY <= blockY + CollisionEpsilon)
            {
                return false;
            }

            if (minZ >= blockMaxZ - CollisionEpsilon || maxZ <= blockZ + CollisionEpsilon)
            {
                return false;
            }

            return true;
        }

        private static float ProbeHighestGround(
            VoxelWorld world,
            float posX,
            float posY,
            float posZ,
            float width,
            float probeDepth = 2f)
        {
            float minX = posX - width / 2f;
            float maxX = posX + width / 2f;
            float minZ = posZ - width / 2f;
            float maxZ = posZ + width / 2f;
            int startX = (int)MathF.Floor(minX);
            int endX = (int)MathF.Floor(maxX);
            int startZ = (int)MathF.Floor(minZ);
            int endZ = (int)MathF.Floor(maxZ);
            int topY = (int)MathF.Floor(posY);
            int bottomY = Math.Max(0, topY - (int)MathF.Ceiling(probeDepth));

            float highest = float.NegativeInfinity;
            for (int y = topY; y >= bottomY; y--)
            {
                for (int z = startZ; z <= endZ; z++)
                {
                    for (int x = startX; x <= endX; x++)
                    {
                        var block = world.GetBlock(x, y, z);
                        if (!block.IsCollidable())
                        {
                            continue;
                        }

                        float top = GetColliderTopY(y, block);
                        if (top <= posY + 0.15f)
                        {
                            highest = MathF.Max(highest, top);
                        }
                    }
                }
            }

            return highest;
        }

        private static float ProbeSupportBelow(
            VoxelWorld world,
            float posX,
            float posY,
            float posZ,
            float width,
            float tolerance = 0.25f)
        {
            float minX = posX - width / 2f;
            float maxX = posX + width / 2f;
            float minZ = posZ - width / 2f;
            float maxZ = posZ + width / 2f;
            int startX = (int)MathF.Floor(minX);
            int endX = (int)MathF.Floor(maxX);
            int startZ = (int)MathF.Floor(minZ);
            int endZ = (int)MathF.Floor(maxZ);
            int topY = (int)MathF.Floor(posY + tolerance);
            int bottomY = Math.Max(0, topY - 4);

            float highest = float.NegativeInfinity;
            for (int y = topY; y >= bottomY; y--)
            {
                for (int z = startZ; z <= endZ; z++)
                {
                    for (int x = startX; x <= endX; x++)
                    {
                        var block = world.GetBlock(x, y, z);
                        if (!block.IsCollidable())
                        {
                            continue;
                        }

                        float top = GetColliderTopY(y, block);
                        if (top <= posY + tolerance)
                        {
                            highest = MathF.Max(highest, top);
                        }
                    }
                }
            }

            return highest;
        }

        private static void SnapToGround(ref EntityCollisionState state, VoxelWorld world, float width)
        {
            float ground = ProbeHighestGround(world, state.Position.X, state.Position.Y, state.Position.Z, width);
            if (ground > float.NegativeInfinity)
            {
                float targetY = ground + CollisionEpsilon;
                if (state.Position.Y > targetY && state.Position.Y - targetY < 0.15f)
                {
                    state.Position.Y = targetY;
                }
            }
        }

        private static float ProbeGroundTopAt(
            VoxelWorld world,
            float posX,
            float posZ,
            float width,
            int maxBlockY,
            int depth = 3)
        {
            float minX = posX - width / 2f;
            float maxX = posX + width / 2f;
            float minZ = posZ - width / 2f;
            float maxZ = posZ + width / 2f;
            int startX = (int)MathF.Floor(minX);
            int endX = (int)MathF.Floor(maxX);
            int startZ = (int)MathF.Floor(minZ);
            int endZ = (int)MathF.Floor(maxZ);
            int bottomY = Math.Max(0, maxBlockY - depth + 1);

            float highest = float.NegativeInfinity;
            for (int y = maxBlockY; y >= bottomY; y--)
            {
                for (int z = startZ; z <= endZ; z++)
                {
                    for (int x = startX; x <= endX; x++)
                    {
                        var block = world.GetBlock(x, y, z);
                        if (!block.IsCollidable())
                        {
                            continue;
                        }

                        highest = MathF.Max(highest, GetColliderTopY(y, block));
                    }
                }
            }

            return highest;
        }

        private static bool TryStepUp(
            ref EntityCollisionState state,
            VoxelWorld world,
            float deltaTime,
            float width,
            float height,
            Vector3 horizontalVelocity)
        {
            if (horizontalVelocity == Vector3.Zero)
            {
                return false;
            }

            float currentX = state.Position.X;
            float currentY = state.Position.Y;
            float currentZ = state.Position.Z;
            float testX = currentX + horizontalVelocity.X * deltaTime;
            float testZ = currentZ + horizontalVelocity.Z * deltaTime;
            int maxBlockY = (int)MathF.Floor(currentY + 1.05f);
            float forwardGround = ProbeGroundTopAt(world, testX, testZ, width, maxBlockY);
            if (forwardGround <= float.NegativeInfinity)
            {
                return false;
            }

            float neededStep = forwardGround + CollisionEpsilon - currentY;
            if (neededStep <= CollisionEpsilon || neededStep > 1.05f)
            {
                return false;
            }

            Vector3 stepUpPos = new Vector3(currentX, currentY + neededStep, currentZ);
            if (!IsStepUpClearAt(world, stepUpPos, width, height))
            {
                return false;
            }

            EntityCollisionState testState = new EntityCollisionState
            {
                Position = new Vector3(testX, stepUpPos.Y, testZ),
                Velocity = state.Velocity
            };
            ResolveAxis(ref testState, world, 0, width, height);
            ResolveAxis(ref testState, world, 2, width, height);

            bool testCollidedX = MathF.Abs(testState.Position.X - testX) > 0.001f;
            bool testCollidedZ = MathF.Abs(testState.Position.Z - testZ) > 0.001f;
            if (testCollidedX || testCollidedZ)
            {
                return false;
            }

            state.Position.X = testState.Position.X;
            state.Position.Z = testState.Position.Z;
            state.Position.Y = testState.Position.Y;
            return true;
        }

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

            float epsilon = CollisionEpsilon;

            if (axis == 1 && state.Velocity.Y < 0f)
            {
                float landY = float.NegativeInfinity;
                for (int y = endY; y >= startY; y--)
                {
                    for (int z = startZ; z <= endZ; z++)
                    {
                        for (int x = startX; x <= endX; x++)
                        {
                            var block = world.GetBlock(x, y, z);
                            if (!block.IsCollidable())
                            {
                                continue;
                            }

                            if (!IntersectsCollider(minX, minY, minZ, maxX, maxY, maxZ, x, y, z, block))
                            {
                                continue;
                            }

                            landY = MathF.Max(landY, GetColliderTopY(y, block));
                        }
                    }
                }

                if (landY > float.NegativeInfinity)
                {
                    state.Position.Y = landY + epsilon;
                    state.IsGrounded = true;
                    state.Velocity.Y = 0f;
                }

                return;
            }

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
                        var block = world.GetBlock(x, y, z);
                        if (!block.IsCollidable())
                        {
                            continue;
                        }

                        float blockMinY = y;
                        float blockMaxY = GetColliderTopY(y, block);

                        if (axis != 1)
                        {
                            if (minY >= blockMaxY - epsilon || maxY <= blockMinY + epsilon)
                            {
                                continue;
                            }

                            // Walk up onto nearby slab tops without snagging on the side collider.
                            if (block.IsSlab() && blockMaxY > minY && blockMaxY - minY <= 0.55f + epsilon)
                            {
                                continue;
                            }
                        }
                        else if (!IntersectsCollider(minX, minY, minZ, maxX, maxY, maxZ, x, y, z, block))
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
                                state.Position.Y = blockMinY - height - epsilon;
                            }
                            else if (state.Velocity.Y < 0)
                            {
                                state.Position.Y = blockMaxY + epsilon;
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
            // Check if player is in a climbable block (Rope/Vine)
            bool inClimbable = false;
            // Check if player is in quicksand
            bool inQuicksand = false;

            {
                int startX = (int)MathF.Floor(state.Position.X - width / 2f);
                int endX = (int)MathF.Floor(state.Position.X + width / 2f);
                int startY = (int)MathF.Floor(state.Position.Y);
                int endY = (int)MathF.Floor(state.Position.Y + height);
                int startZ = (int)MathF.Floor(state.Position.Z - width / 2f);
                int endZ = (int)MathF.Floor(state.Position.Z + width / 2f);

                for (int y = startY; y <= endY; y++)
                {
                    for (int z = startZ; z <= endZ; z++)
                    {
                        for (int x = startX; x <= endX; x++)
                        {
                            var b = world.GetBlock(x, y, z);
                            if (b.IsClimbable())
                            {
                                inClimbable = true;
                            }
                            if (b == BlockType.Quicksand)
                            {
                                inQuicksand = true;
                            }
                        }
                    }
                }
            }

            var water = WaterQuery.GetBodyState(world, state.Position, width, height, eyeHeight);
            state.InWater = water.InWater;
            state.HeadUnderwater = water.HeadUnderwater;
            state.OnWaterSurface = water.OnSurface;

            var lava = LavaQuery.GetBodyState(world, state.Position, width, height, eyeHeight);
            state.InLava = lava.InLava;
            state.HeadInLava = lava.HeadInLava;
            state.OnLavaSurface = lava.OnSurface;

            float gravity = Gravity;
            float terminalVelocity = TerminalVelocity;

            if (inClimbable)
            {
                gravity = 0f;
                state.Velocity.Y = 0f;
                if (swimUp)
                {
                    state.Velocity.Y = 4f;
                }
                else if (swimDown)
                {
                    state.Velocity.Y = -4f;
                }
            }
            else if (inQuicksand)
            {
                gravity = 0f;
                state.Velocity.Y = -0.5f;
                if (swimUp)
                {
                    state.Velocity.Y = 0.1f;
                }
                horizontalVelocity *= 0.15f;
            }
            else if (water.InWater)
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
            else if (lava.InLava)
            {
                gravity *= WaterGravityScale; // lava is viscous, so low gravity scale
                terminalVelocity = -4f; // slower sinking than water
                horizontalVelocity *= 0.7f; // slower horizontal swimming

                if (swimUp)
                {
                    state.Velocity.Y += 3f * deltaTime;
                }

                if (swimDown)
                {
                    state.Velocity.Y -= 2.5f * deltaTime;
                }

                if (horizontalVelocity == Vector3.Zero)
                {
                    state.Velocity.X *= MathF.Pow(0.5f, deltaTime * 10f);
                    state.Velocity.Z *= MathF.Pow(0.5f, deltaTime * 10f);
                    if (MathF.Abs(state.Velocity.X) < 0.01f) state.Velocity.X = 0;
                    if (MathF.Abs(state.Velocity.Z) < 0.01f) state.Velocity.Z = 0;
                }
            }

            if (!inClimbable && !inQuicksand)
            {
                state.Velocity.Y += gravity * deltaTime;
                if (state.Velocity.Y < terminalVelocity)
                {
                    state.Velocity.Y = terminalVelocity;
                }
            }

            if (horizontalVelocity != Vector3.Zero)
            {
                state.Velocity.X = horizontalVelocity.X;
                state.Velocity.Z = horizontalVelocity.Z;
            }

            bool wasGrounded = state.IsGrounded;
            float originalX = state.Position.X;
            float originalY = state.Position.Y;
            float originalZ = state.Position.Z;
            Vector3 planarVelocity = new Vector3(state.Velocity.X, 0f, state.Velocity.Z);

            // X movement
            state.Position.X += state.Velocity.X * deltaTime;
            ResolveAxis(ref state, world, 0, width, height);
            bool collidedX = MathF.Abs(state.Position.X - (originalX + state.Velocity.X * deltaTime)) > 0.001f;

            // Z movement
            state.Position.Z += state.Velocity.Z * deltaTime;
            ResolveAxis(ref state, world, 2, width, height);
            bool collidedZ = MathF.Abs(state.Position.Z - (originalZ + state.Velocity.Z * deltaTime)) > 0.001f;

            if (wasGrounded)
            {
                float testX = state.Position.X + planarVelocity.X * deltaTime;
                float testZ = state.Position.Z + planarVelocity.Z * deltaTime;
                int maxBlockY = (int)MathF.Floor(state.Position.Y + 1.05f);
                float forwardGround = ProbeGroundTopAt(world, testX, testZ, width, maxBlockY);
                bool needsStep = forwardGround > state.Position.Y + CollisionEpsilon;
                if (needsStep || collidedX || collidedZ)
                {
                    TryStepUp(ref state, world, deltaTime, width, height, planarVelocity);
                }
            }

            // Y movement (Gravity and jump resolution)
            state.Position.Y += state.Velocity.Y * deltaTime;
            state.IsGrounded = false;
            ResolveAxis(ref state, world, 1, width, height);

            if (!state.IsGrounded && state.Velocity.Y < 0f)
            {
                float ground = ProbeSupportBelow(world, state.Position.X, state.Position.Y, state.Position.Z, width, tolerance: 0.08f);
                if (ground > float.NegativeInfinity && state.Position.Y - ground < 0.12f)
                {
                    state.Position.Y = ground + CollisionEpsilon;
                    state.IsGrounded = true;
                    state.Velocity.Y = 0f;
                }
            }

            if (state.IsGrounded)
            {
                SnapToGround(ref state, world, width);
            }

            // Surface float: resist sinking only — never push upward or you can walk up water columns.
            if (water.InWater && !water.HeadUnderwater && !swimDown && !inClimbable && !inQuicksand)
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
            else if (lava.InLava && !lava.HeadInLava && !swimDown && !inClimbable && !inQuicksand)
            {
                if (state.Velocity.Y < 0f)
                {
                    state.Velocity.Y = MathF.Min(state.Velocity.Y + 8f * deltaTime, -0.05f);
                }
                else if (state.Velocity.Y > 0.2f)
                {
                    state.Velocity.Y *= MathF.Pow(0.4f, deltaTime * 10f);
                }
            }

            // Grounded only on solid blocks, never on water/lava surface.
            state.OnWaterSurface = water.InWater && !water.HeadUnderwater;
            state.OnLavaSurface = lava.InLava && !lava.HeadInLava;
        }

        public static bool IsSpaceClearAt(VoxelWorld world, Vector3 position, float width, float height)
        {
            return IsBodyClearAt(world, position, width, height, ignoreGroundBelowFeet: false);
        }

        private static bool IsStepUpClearAt(VoxelWorld world, Vector3 position, float width, float height)
        {
            return IsBodyClearAt(world, position, width, height, ignoreGroundBelowFeet: true);
        }

        private static bool IsBodyClearAt(
            VoxelWorld world,
            Vector3 position,
            float width,
            float height,
            bool ignoreGroundBelowFeet)
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
                        var block = world.GetBlock(bx, by, bz);
                        if (!block.IsCollidable())
                        {
                            continue;
                        }

                        if (IntersectsCollider(minX, minY, minZ, maxX, maxY, maxZ, bx, by, bz, block))
                        {
                            if (ignoreGroundBelowFeet)
                            {
                                float top = GetColliderTopY(by, block);
                                if (top <= position.Y + CollisionEpsilon)
                                {
                                    continue;
                                }
                            }

                            return false;
                        }
                    }
                }
            }

            return true;
        }
    }
}

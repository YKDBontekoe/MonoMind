using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Village.Jobs
{
    internal sealed class BuildJob : IVillagerJob
    {
        private const float BuildReach = 2.4f;

        public void Tick(Villager villager, float deltaTime, VoxelWorld world, VillageContext context)
        {
            if (!villager.AssignedBuildingSiteId.HasValue ||
                !context.TryGetBuildingSite(villager.AssignedBuildingSiteId.Value, out var site))
            {
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            if (!site.TryGetNextBlock(out var nextBlock))
            {
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            var blockCenter = new Vector3(
                site.AnchorX + nextBlock.Dx + 0.5f,
                villager.Position.Y,
                site.AnchorZ + nextBlock.Dz + 0.5f);
            var targetPos = GetBuildStandPosition(villager, site, nextBlock);
            if (villager.AiPhase == VillagerAiPhase.PathTo)
            {
                if (context.CreativeMode)
                {
                    villager.SetAiPhase(VillagerAiPhase.Working);
                }
                else
                {
                    var toBlock = blockCenter - villager.Position;
                    toBlock.Y = 0f;
                    if (toBlock.LengthSquared() > BuildReach * BuildReach &&
                        VillagerMovementHelper.TryMoveToward(villager, deltaTime, world, targetPos))
                    {
                        return;
                    }

                    villager.SetAiPhase(VillagerAiPhase.Working);
                }
            }

            villager.WorkTimer += deltaTime * villager.WorkSpeedMultiplier;
            if (villager.WorkTimer >= Villager.WorkInterval * 0.5f)
            {
                villager.WorkTimer = 0f;
                site.TryPlaceNextBlock(
                    world,
                    context.Storage,
                    Villager.Width,
                    Villager.Height,
                    villager.Position,
                    context.CreativeMode,
                    checkBuilderCollision: false);
                villager.SetAiPhase(VillagerAiPhase.PathTo);
            }
        }

        private static Vector3 GetBuildStandPosition(Villager villager, BuildingSite site, StructureBlock nextBlock)
        {
            float blockCenterX = site.AnchorX + nextBlock.Dx + 0.5f;
            float blockCenterZ = site.AnchorZ + nextBlock.Dz + 0.5f;
            float standDistance = 0.9f;

            Vector3 best = new(blockCenterX + standDistance, villager.Position.Y, blockCenterZ);
            float bestDist = Vector3.DistanceSquared(villager.Position, best);
            ReadOnlySpan<Vector2> offsets =
            [
                new(1f, 0f),
                new(-1f, 0f),
                new(0f, 1f),
                new(0f, -1f),
                new(1f, 1f),
                new(-1f, 1f),
                new(1f, -1f),
                new(-1f, -1f)
            ];

            foreach (var offset in offsets)
            {
                var dir = Vector2.Normalize(offset);
                var candidate = new Vector3(
                    blockCenterX + dir.X * standDistance,
                    villager.Position.Y,
                    blockCenterZ + dir.Y * standDistance);
                float dist = Vector3.DistanceSquared(villager.Position, candidate);
                if (dist < bestDist)
                {
                    best = candidate;
                    bestDist = dist;
                }
            }

            return best;
        }
    }
}

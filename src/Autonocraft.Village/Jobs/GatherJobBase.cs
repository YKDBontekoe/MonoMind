using System;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Village.Jobs
{
    internal abstract class GatherJobBase : IVillagerJob
    {
        protected abstract ToolType RequiredTool { get; }
        protected abstract Func<BlockType, bool> IsTargetBlock { get; }

        public void Tick(Villager villager, float deltaTime, VoxelWorld world, VillageContext context)
        {
            if (!TryResolveBreakTarget(villager, world, context, out var target))
            {
                if (context.CreativeMode && context.IsTestMode)
                {
                    TickCreativeFallback(villager, deltaTime);
                    return;
                }

                ReturnEquippedTool(villager, context.Storage);
                villager.AssignJob(JobType.Idle, null, null);
                return;
            }

            if (context.Village != null)
            {
                VillageSettlementHealth.EnsureVillageChunksLoaded(world, context.Village);
            }
            else
            {
                world.EnsureChunksLoaded(target, chunkRadius: 1);
            }

            if (villager.AiPhase == VillagerAiPhase.PathTo)
            {
                if (!(context.CreativeMode && context.IsTestMode) && !IsWithinGatherReach(villager, target))
                {
                    if (VillagerMovementHelper.TryMoveToward(villager, deltaTime, world, target))
                    {
                        return;
                    }
                }

                villager.SetAiPhase(VillagerAiPhase.Working);
            }

            if (villager.AiPhase == VillagerAiPhase.Working)
            {
                TryEquipPreferredTool(villager, context.Storage, RequiredTool, context.CreativeMode);

                int bx = (int)MathF.Floor(target.X);
                int by = (int)MathF.Floor(target.Y);
                int bz = (int)MathF.Floor(target.Z);
                if (!(context.CreativeMode && context.IsTestMode) && !IsWithinGatherReach(villager, target))
                {
                    villager.SetAiPhase(VillagerAiPhase.PathTo);
                    return;
                }

                var block = world.GetBlock(bx, by, bz);
                if (block == BlockType.Air || !IsTargetBlock(block))
                {
                    context.Village?.WorkQueue.Complete(bx, by, bz);
                    villager.MarkedResource = null;
                    villager.ClearJobTarget();
                    villager.SetAiPhase(VillagerAiPhase.PathTo);
                    return;
                }

                float breakTime = MiningCalculator.GetEffectiveBreakTime(block, villager.EquippedTool, villager.Skills);
                if (breakTime <= 0f)
                {
                    breakTime = block.GetBreakTime();
                }

                villager.BreakProgress += deltaTime * villager.WorkSpeedMultiplier / Math.Max(0.01f, breakTime);
                if (villager.BreakProgress < 1f)
                {
                    return;
                }

                villager.BreakProgress = 0f;
                world.SetBlock(bx, by, bz, BlockType.Air);
                villager.Inventory.AddItem(ItemStack.CreateBlock(block, 1));
                var workSkill = MiningCalculator.ToVillagerSkill(MiningCalculator.GetSkillForBlock(block));
                villager.Skills.AddXp(workSkill, MiningCalculator.GetXpForBlock(block));
                DamageEquippedTool(villager, 1, context.CreativeMode);
                context.Village?.WorkQueue.Complete(bx, by, bz);
                villager.MarkedResource = null;
                villager.ClearJobTarget();
                if (HaulLogistics.IsCarryFull(villager.Inventory))
                {
                    VillagerCarryHelper.TryOffloadCarryToOutputChest(villager, context);
                }

                villager.SetAiPhase(VillagerAiPhase.PathTo);
            }
        }

        private void TickCreativeFallback(Villager villager, float deltaTime)
        {
            villager.SetAiPhase(VillagerAiPhase.Working);
            if (!villager.EquippedTool.IsTool() ||
                !ToolRegistry.TryGet(villager.EquippedTool.ToolId, out var equippedDef) ||
                equippedDef.ToolType != RequiredTool)
            {
                villager.SetEquippedTool(ToolRegistry.CreateStack(RequiredTool, ToolTier.Stone));
            }

            villager.WorkTimer += deltaTime * villager.WorkSpeedMultiplier;
            if (villager.WorkTimer < Villager.WorkInterval)
            {
                return;
            }

            villager.WorkTimer = 0f;
            var block = RequiredTool == ToolType.Axe ? BlockType.OakLog : BlockType.Cobblestone;
            villager.Inventory.AddItem(ItemStack.CreateBlock(block, 1));
        }

        private static void TryEquipPreferredTool(Villager villager, VillageStorage storage, ToolType preferredTool, bool creative)
        {
            if (villager.EquippedTool.IsTool() &&
                ToolRegistry.TryGet(villager.EquippedTool.ToolId, out var equippedDef) &&
                equippedDef.ToolType == preferredTool &&
                (creative || villager.EquippedTool.Durability > 0))
            {
                return;
            }

            ReturnEquippedTool(villager, storage);
            if (storage.TryWithdrawTool(preferredTool, out var tool))
            {
                villager.SetEquippedTool(tool);
                return;
            }

            if (creative)
            {
                villager.SetEquippedTool(ToolRegistry.CreateStack(preferredTool, ToolTier.Stone));
            }
        }

        private static void ReturnEquippedTool(Villager villager, VillageStorage storage)
        {
            if (villager.EquippedTool.IsEmpty)
            {
                return;
            }

            storage.TryReturnTool(villager.EquippedTool);
            villager.SetEquippedTool(ItemStack.Empty);
        }

        private static bool DamageEquippedTool(Villager villager, int amount, bool creative)
        {
            if (!villager.EquippedTool.IsTool() || amount <= 0 || creative)
            {
                return false;
            }

            var tool = villager.EquippedTool;
            tool.Durability -= amount;
            if (tool.Durability <= 0)
            {
                villager.SetEquippedTool(ItemStack.Empty);
                return true;
            }

            villager.SetEquippedTool(tool);
            return false;
        }

        private bool TryResolveBreakTarget(
            Villager villager,
            VoxelWorld world,
            VillageContext context,
            out Vector3 target)
        {
            if (villager.MarkedResource.HasValue)
            {
                target = villager.MarkedResource.Value;
                return true;
            }

            if (villager.JobTarget.HasValue)
            {
                int bx = (int)MathF.Floor(villager.JobTarget.Value.X);
                int by = (int)MathF.Floor(villager.JobTarget.Value.Y);
                int bz = (int)MathF.Floor(villager.JobTarget.Value.Z);
                var block = world.GetBlock(bx, by, bz);
                if (block != BlockType.Air &&
                    IsTargetBlock(block) &&
                    (context.Village == null || !context.Village.IsProtectedStructureBlock(bx, by, bz, block)))
                {
                    target = villager.JobTarget.Value;
                    return true;
                }

                context.Village?.WorkQueue.Complete(bx, by, bz);
                villager.ClearJobTarget();
            }

            if (context.Village != null)
            {
                VillagerRole queueRole = villager.Role switch
                {
                    VillagerRole.Miner => VillagerRole.Miner,
                    VillagerRole.Lumberjack => VillagerRole.Lumberjack,
                    _ => VillagerRole.Peasant
                };

                if (context.Village.WorkQueue.TryGetNextForRole(queueRole, world, context.Village, out int x, out int y, out int z)
                    || (queueRole == VillagerRole.Peasant && context.Village.WorkQueue.TryGetNextAny(world, context.Village, out x, out y, out z)))
                {
                    var queuedBlock = world.GetBlock(x, y, z);
                    if (IsTargetBlock(queuedBlock) &&
                        (context.Village == null || !context.Village.IsProtectedStructureBlock(x, y, z, queuedBlock)))
                    {
                        target = new Vector3(x + 0.5f, y, z + 0.5f);
                        villager.SetJobTarget(target);
                        return true;
                    }

                    context.Village.WorkQueue.Complete(x, y, z);
                }
            }

            target = default;
            return false;
        }

        private static bool IsWithinGatherReach(Villager villager, Vector3 target)
        {
            int bx = (int)MathF.Floor(target.X);
            int bz = (int)MathF.Floor(target.Z);
            float reachX = villager.Position.X - (bx + 0.5f);
            float reachZ = villager.Position.Z - (bz + 0.5f);
            return reachX * reachX + reachZ * reachZ <= 2.5f * 2.5f;
        }
    }

    internal sealed class LumberJob : GatherJobBase
    {
        protected override ToolType RequiredTool => ToolType.Axe;

        protected override Func<BlockType, bool> IsTargetBlock => block =>
            block is BlockType.OakLog
                or BlockType.BirchLog
                or BlockType.PineLog
                or BlockType.WillowLog
                or BlockType.PalmLog;
    }

    internal sealed class MineJob : GatherJobBase
    {
        protected override ToolType RequiredTool => ToolType.Pickaxe;

        protected override Func<BlockType, bool> IsTargetBlock => block =>
            block is BlockType.Stone or BlockType.Cobblestone or BlockType.CoalOre
                or BlockType.IronOre or BlockType.GoldOre or BlockType.Gravel or BlockType.MossStone;
    }
}

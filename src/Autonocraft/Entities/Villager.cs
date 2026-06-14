using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.Domain.Village;
using Autonocraft.Items;
using Autonocraft.Village;
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
        public int? AssignedBuildingId { get; private set; }
        public int? HaulSourceChestId { get; private set; }
        public int? HaulSourceVillagerId { get; private set; }
        public bool HaulIsDelivering { get; private set; }
        public Vector3? MarkedResource { get; set; }
        public int? HomeBuildingId { get; set; }

        public Inventory Inventory { get; } = new Inventory(8);
        public ItemStack EquippedTool { get; private set; }
        public VillagerPersonaData Persona { get; private set; }
        public VillagerSkills Skills { get; } = new();

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
                case JobType.Lumber:
                    UpdateLumber(deltaTime, world, context);
                    break;
                case JobType.Mine:
                    UpdateMine(deltaTime, world, context);
                    break;
                case JobType.Farm:
                    UpdateFarm(deltaTime, world, context);
                    break;
                case JobType.Build:
                    UpdateBuild(deltaTime, world, context);
                    break;
                case JobType.Haul:
                    UpdateHaul(deltaTime, world, context);
                    break;
                case JobType.Craft:
                    UpdateCraft(deltaTime, world, context);
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

        private void UpdateLumber(float deltaTime, VoxelWorld world, VillageContext context)
        {
            UpdateBreakBlockJob(deltaTime, world, context, ToolType.Axe, IsLumberBlock);
        }

        private void UpdateMine(float deltaTime, VoxelWorld world, VillageContext context)
        {
            UpdateBreakBlockJob(deltaTime, world, context, ToolType.Pickaxe, IsMineableBlock);
        }

        private void UpdateFarm(float deltaTime, VoxelWorld world, VillageContext context)
        {
            if (context.Village == null)
            {
                AssignJob(JobType.Idle, null, null);
                return;
            }

            if (!TryResolveFarmTarget(world, context, out var workCell, out var approach))
            {
                AssignJob(JobType.Idle, null, null);
                return;
            }

            JobTarget = workCell;
            if (AiPhase == VillagerAiPhase.PathTo)
            {
                if (TryMoveAlongPath(deltaTime, world) || TryMoveToward(deltaTime, world, approach))
                {
                    return;
                }

                AiPhase = VillagerAiPhase.Working;
            }

            if (AiPhase != VillagerAiPhase.Working)
            {
                return;
            }

            int bx = (int)MathF.Floor(workCell.X);
            int by = (int)MathF.Floor(workCell.Y);
            int bz = (int)MathF.Floor(workCell.Z);
            var block = world.GetBlock(bx, by, bz);
            var work = FarmCropHelper.ClassifyWork(block);
            if (work == FarmWorkKind.None)
            {
                MarkedResource = null;
                TryAdvanceFarmTarget(world, context);
                return;
            }

            WorkTimer += deltaTime * WorkSpeedMultiplier * Skills.GetBonus(VillagerSkill.Farming);
            float workDuration = WorkInterval * (work == FarmWorkKind.Harvest ? 1f : 1.2f);
            if (WorkTimer < workDuration)
            {
                return;
            }

            WorkTimer = 0f;
            if (work == FarmWorkKind.Harvest)
            {
                var harvest = FarmCropHelper.GetHarvestProduct(block);
                world.SetBlock(bx, by, bz, BlockType.Dirt);
                GrantFarmYield(context, FarmCropHelper.GetFoodValue(harvest));
                Skills.AddXp(VillagerSkill.Farming, 1f);
                Inventory.AddItem(ItemStack.CreateBlock(harvest, 1));
                TryOffloadCarryToOutputChest(context);
            }
            else
            {
                var crop = FarmCropHelper.PickPlantCrop(bx, bz);
                world.SetBlock(bx, by, bz, FarmCropHelper.GetSproutBlock(crop));
                Skills.AddXp(VillagerSkill.Farming, 0.5f);
            }

            MarkedResource = null;
            TryAdvanceFarmTarget(world, context);
        }

        private bool TryResolveFarmTarget(
            VoxelWorld world,
            VillageContext context,
            out Vector3 workCell,
            out Vector3 approach)
        {
            workCell = default;
            approach = default;
            if (context.Village == null)
            {
                return false;
            }

            if (MarkedResource.HasValue || JobTarget.HasValue)
            {
                workCell = MarkedResource ?? JobTarget!.Value;
                int bx = (int)MathF.Floor(workCell.X);
                int by = (int)MathF.Floor(workCell.Y);
                int bz = (int)MathF.Floor(workCell.Z);
                var block = world.GetBlock(bx, by, bz);
                if (FarmCropHelper.ClassifyWork(block) != FarmWorkKind.None)
                {
                    approach = FarmCropHelper.GetApproachPosition(world, bx, by, bz, Position);
                    return true;
                }

                MarkedResource = null;
                JobTarget = null;
            }

            VillageBuilding? plot = null;
            if (AssignedBuildingId.HasValue &&
                context.Village.TryGetBuilding(AssignedBuildingId.Value, out var assigned) &&
                assigned.Kind == BuildingKind.FarmPlot)
            {
                plot = assigned;
            }

            var next = plot != null
                ? FarmCropHelper.FindBestFarmCell(world, context.Village, plot, Position)
                : FarmCropHelper.FindBestFarmCellAnyPlot(world, context.Village, Position);
            if (!next.HasValue)
            {
                return false;
            }

            workCell = next.Value;
            MarkedResource = workCell;
            JobTarget = workCell;
            int wx = (int)MathF.Floor(workCell.X);
            int wy = (int)MathF.Floor(workCell.Y);
            int wz = (int)MathF.Floor(workCell.Z);
            approach = FarmCropHelper.GetApproachPosition(world, wx, wy, wz, Position);
            TryBeginFarmPath(world, approach);
            return true;
        }

        private void TryAdvanceFarmTarget(VoxelWorld world, VillageContext context)
        {
            if (!TryResolveFarmTarget(world, context, out _, out _))
            {
                AssignJob(JobType.Idle, null, null);
                return;
            }

            AiPhase = VillagerAiPhase.PathTo;
        }

        private void TryBeginFarmPath(VoxelWorld world, Vector3 approach)
        {
            if (VoxelPathfinder.TryFindPath(world, Position, approach, 24, out var waypoints))
            {
                SetPath(waypoints);
            }
            else
            {
                _path.Clear();
                _pathIndex = 0;
            }
        }

        private void UpdateBreakBlockJob(
            float deltaTime,
            VoxelWorld world,
            VillageContext context,
            ToolType requiredTool,
            Func<BlockType, bool> isTargetBlock)
        {
            if (!TryResolveBreakTarget(world, context, isTargetBlock, out var target))
            {
                ReturnEquippedTool(context.Storage);
                AssignJob(JobType.Idle, null, null);
                return;
            }

            if (AiPhase == VillagerAiPhase.PathTo)
            {
                if (TryMoveToward(deltaTime, world, target))
                {
                    return;
                }

                AiPhase = VillagerAiPhase.Working;
            }

            if (AiPhase == VillagerAiPhase.Working)
            {
                if (!EnsureEquippedTool(context.Storage, requiredTool, context.CreativeMode))
                {
                    ReturnEquippedTool(context.Storage);
                    AssignJob(JobType.Idle, null, null);
                    return;
                }

                int bx = (int)MathF.Floor(target.X);
                int by = (int)MathF.Floor(target.Y);
                int bz = (int)MathF.Floor(target.Z);
                var block = world.GetBlock(bx, by, bz);
                if (block == BlockType.Air || !block.IsCollidable() || !isTargetBlock(block))
                {
                    context.Village?.WorkQueue.Complete(bx, by, bz);
                    MarkedResource = null;
                    JobTarget = null;
                    AiPhase = VillagerAiPhase.PathTo;
                    return;
                }

                float breakTime = MiningCalculator.GetEffectiveBreakTime(block, EquippedTool, Skills);
                if (breakTime <= 0f)
                {
                    breakTime = block.GetBreakTime();
                }

                BreakProgress += deltaTime * WorkSpeedMultiplier / Math.Max(0.01f, breakTime);
                if (BreakProgress < 1f)
                {
                    return;
                }

                BreakProgress = 0f;
                world.SetBlock(bx, by, bz, BlockType.Air);
                Inventory.AddItem(ItemStack.CreateBlock(block, 1));
                var workSkill = MiningCalculator.ToVillagerSkill(MiningCalculator.GetSkillForBlock(block));
                Skills.AddXp(workSkill, MiningCalculator.GetXpForBlock(block));
                DamageEquippedTool(1, context.CreativeMode);
                context.Village?.WorkQueue.Complete(bx, by, bz);
                MarkedResource = null;
                JobTarget = null;
                if (HaulLogistics.IsCarryFull(Inventory))
                {
                    TryOffloadCarryToOutputChest(context);
                }

                AiPhase = VillagerAiPhase.PathTo;
            }
        }

        private bool EnsureEquippedTool(VillageStorage storage, ToolType requiredTool, bool creative = false)
        {
            if (EquippedTool.IsTool() &&
                ToolRegistry.TryGet(EquippedTool.ToolId, out var equippedDef) &&
                equippedDef.ToolType == requiredTool &&
                (creative || EquippedTool.Durability > 0))
            {
                return true;
            }

            ReturnEquippedTool(storage);
            if (storage.TryWithdrawTool(requiredTool, out var tool))
            {
                EquippedTool = tool;
                return true;
            }

            if (creative && TryEquipCreativeTool(requiredTool))
            {
                return true;
            }

            return false;
        }

        private bool TryEquipCreativeTool(ToolType requiredTool)
        {
            EquippedTool = ToolRegistry.CreateStack(requiredTool, ToolTier.Stone);
            return EquippedTool.IsTool();
        }

        private void ReturnEquippedTool(VillageStorage storage)
        {
            if (EquippedTool.IsEmpty)
            {
                return;
            }

            storage.TryReturnTool(EquippedTool);
            EquippedTool = ItemStack.Empty;
        }

        private bool DamageEquippedTool(int amount, bool creative = false)
        {
            if (!EquippedTool.IsTool() || amount <= 0 || creative)
            {
                return false;
            }

            var tool = EquippedTool;
            tool.Durability -= amount;
            if (tool.Durability <= 0)
            {
                EquippedTool = ItemStack.Empty;
                return true;
            }

            EquippedTool = tool;
            return false;
        }

        private bool TryResolveBreakTarget(
            VoxelWorld world,
            VillageContext context,
            Func<BlockType, bool> isTargetBlock,
            out Vector3 target)
        {
            if (MarkedResource.HasValue)
            {
                target = MarkedResource.Value;
                return true;
            }

            if (JobTarget.HasValue)
            {
                int bx = (int)MathF.Floor(JobTarget.Value.X);
                int by = (int)MathF.Floor(JobTarget.Value.Y);
                int bz = (int)MathF.Floor(JobTarget.Value.Z);
                var block = world.GetBlock(bx, by, bz);
                if (block != BlockType.Air && block.IsCollidable() && isTargetBlock(block))
                {
                    target = JobTarget.Value;
                    return true;
                }

                context.Village?.WorkQueue.Complete(bx, by, bz);
                JobTarget = null;
            }

            if (context.Village != null)
            {
                VillagerRole queueRole = Role switch
                {
                    VillagerRole.Miner => VillagerRole.Miner,
                    VillagerRole.Lumberjack => VillagerRole.Lumberjack,
                    _ => VillagerRole.Peasant
                };

                if (context.Village.WorkQueue.TryGetNextForRole(queueRole, world, out int x, out int y, out int z)
                    || (queueRole == VillagerRole.Peasant && context.Village.WorkQueue.TryGetNextAny(world, out x, out y, out z)))
                {
                    var queuedBlock = world.GetBlock(x, y, z);
                    if (isTargetBlock(queuedBlock))
                    {
                        target = new Vector3(x + 0.5f, y, z + 0.5f);
                        JobTarget = target;
                        return true;
                    }

                    context.Village.WorkQueue.Complete(x, y, z);
                }
            }

            target = default;
            return false;
        }

        private static bool IsLumberBlock(BlockType block) =>
            block is BlockType.OakLog or BlockType.OakLeaves
                or BlockType.BirchLog or BlockType.BirchLeaves
                or BlockType.PineLog or BlockType.PineLeaves
                or BlockType.WillowLog or BlockType.WillowLeaves
                or BlockType.PalmLog or BlockType.PalmLeaves;

        private static bool IsMineableBlock(BlockType block) =>
            block is BlockType.Stone or BlockType.Cobblestone or BlockType.CoalOre
                or BlockType.IronOre or BlockType.GoldOre or BlockType.Gravel or BlockType.MossStone;

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
                site.TryPlaceNextBlock(world, context.Storage, Width, Height, Position, context.CreativeMode);
                AiPhase = VillagerAiPhase.PathTo;
            }
        }

        private void UpdateHaul(float deltaTime, VoxelWorld world, VillageContext context)
        {
            if (context.Village == null)
            {
                AssignJob(JobType.Idle, null, null);
                return;
            }

            if (!HaulIsDelivering)
            {
                if (!IsInventoryEmpty())
                {
                    HaulIsDelivering = true;
                    HaulSourceChestId = null;
                    HaulSourceVillagerId = null;
                    PrepareDeliveryTarget(context);
                    AiPhase = VillagerAiPhase.PathTo;
                }
                else if (HaulSourceChestId.HasValue || HaulSourceVillagerId.HasValue)
                {
                    var pickupPos = GetHaulPickupPosition(context);
                    if (!pickupPos.HasValue)
                    {
                        AssignJob(JobType.Idle, null, null);
                        return;
                    }

                    if (AiPhase == VillagerAiPhase.PathTo)
                    {
                        if (TryMoveToward(deltaTime, world, pickupPos.Value))
                        {
                            return;
                        }

                        AiPhase = VillagerAiPhase.Working;
                    }

                    TryExecuteHaulPickup(context);
                    if (IsInventoryEmpty())
                    {
                        AssignJob(JobType.Idle, null, null);
                        return;
                    }

                    HaulIsDelivering = true;
                    HaulSourceChestId = null;
                    HaulSourceVillagerId = null;
                    PrepareDeliveryTarget(context);
                    AiPhase = VillagerAiPhase.PathTo;
                }
                else
                {
                    AssignJob(JobType.Idle, null, null);
                }

                return;
            }

            if (IsInventoryEmpty())
            {
                AssignJob(JobType.Idle, null, null);
                return;
            }

            var destination = JobTarget ?? context.StoragePosition;
            if (AiPhase == VillagerAiPhase.PathTo)
            {
                if (TryMoveToward(deltaTime, world, destination))
                {
                    return;
                }

                AiPhase = VillagerAiPhase.Working;
            }

            DepositAtDeliveryTarget(context);
            if (IsInventoryEmpty())
            {
                AssignJob(JobType.Idle, null, null);
                return;
            }

            PrepareDeliveryTarget(context);
            AiPhase = VillagerAiPhase.PathTo;
        }

        private Vector3? GetHaulPickupPosition(VillageContext context)
        {
            if (HaulSourceChestId.HasValue &&
                context.Village != null &&
                context.Village.TryGetOutputChest(HaulSourceChestId.Value, out var chest))
            {
                return chest.Position;
            }

            if (HaulSourceVillagerId.HasValue &&
                context.TryGetVillager(HaulSourceVillagerId.Value, out var source))
            {
                return source.Position;
            }

            return JobTarget;
        }

        private void TryExecuteHaulPickup(VillageContext context)
        {
            if (context.Village == null)
            {
                return;
            }

            if (HaulSourceChestId.HasValue &&
                context.Village.TryGetOutputChest(HaulSourceChestId.Value, out var chest))
            {
                HaulLogistics.TryPickupChestToHauler(chest, this);
                return;
            }

            if (HaulSourceVillagerId.HasValue &&
                context.TryGetVillager(HaulSourceVillagerId.Value, out var source))
            {
                HaulLogistics.TryPickupVillagerToHauler(source, this);
            }
        }

        private void PrepareDeliveryTarget(VillageContext context)
        {
            if (context.Village == null ||
                !HaulLogistics.TryGetHighestPriorityStack(Inventory, out _, out var stack))
            {
                JobTarget = context.StoragePosition;
                return;
            }

            JobTarget = HaulLogistics.ResolveDeliveryTarget(
                context.Village,
                stack.BlockType,
                Position,
                out _);
        }

        private void DepositAtDeliveryTarget(VillageContext context)
        {
            if (context.Village == null ||
                !HaulLogistics.TryGetHighestPriorityStack(Inventory, out int slot, out var stack))
            {
                return;
            }

            HaulLogistics.ResolveDeliveryTarget(
                context.Village,
                stack.BlockType,
                Position,
                out bool toFoodStock);

            if (toFoodStock && stack.IsBlock() && FarmCropHelper.IsFoodCrop(stack.BlockType))
            {
                context.Village.AddFarmFood(FarmCropHelper.GetFoodValue(stack.BlockType) * stack.Count);
                Inventory.SetSlot(slot, ItemStack.Empty);
                return;
            }

            if (context.Storage.AddItem(stack))
            {
                Inventory.SetSlot(slot, ItemStack.Empty);
            }
        }

        private void TryOffloadCarryToOutputChest(VillageContext context)
        {
            if (context.Village == null)
            {
                return;
            }

            BuildingKind? kind = CurrentJob switch
            {
                JobType.Lumber => BuildingKind.LumberCamp,
                JobType.Mine => BuildingKind.Quarry,
                JobType.Farm => BuildingKind.FarmPlot,
                _ => null
            };

            if (!kind.HasValue)
            {
                return;
            }

            OutputChest? chest = null;
            if (AssignedBuildingId.HasValue &&
                context.Village.TryGetOutputChestForBuilding(AssignedBuildingId.Value, out var buildingChest))
            {
                chest = buildingChest;
            }
            else
            {
                chest = context.Village.GetNearestOutputChest(kind.Value, Position);
            }

            if (chest != null)
            {
                HaulLogistics.OffloadInventoryToChest(this, chest);
            }
        }

        private void UpdateCraft(float deltaTime, VoxelWorld world, VillageContext context)
        {
            var target = JobTarget ?? context.VillageCenter;
            if (AiPhase == VillagerAiPhase.PathTo)
            {
                if (TryMoveToward(deltaTime, world, target))
                {
                    return;
                }

                AiPhase = VillagerAiPhase.Working;
            }

            if (AiPhase != VillagerAiPhase.Working)
            {
                return;
            }

            WorkTimer += deltaTime * WorkSpeedMultiplier;
            if (WorkTimer < WorkInterval * 2f)
            {
                return;
            }

            WorkTimer = 0f;
            if (Role == VillagerRole.Farmer)
            {
                GrantFarmYield(context, 0.5f);
                Skills.AddXp(VillagerSkill.Farming, 1f);
            }
            else if (Role == VillagerRole.Smith && !VillageWorkshopCrafting.TrySmithWork(context.Storage, context.CreativeMode))
            {
                AssignJob(JobType.Idle, null, null);
                return;
            }

            AssignJob(JobType.Idle, null, null);
        }

        private void GrantFarmYield(VillageContext context, float baseAmount)
        {
            if (context.Village == null)
            {
                return;
            }

            float yield = baseAmount
                * Skills.GetBonus(VillagerSkill.Farming)
                * VillagerTraits.GetFarmYieldMultiplier(Persona.Trait);
            context.Village.AddFarmFood(yield);
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
        public Vector3 VillageCenter { get; init; }
        public float VillageRadius { get; init; } = 32f;
        public Vector3 StoragePosition { get; init; }
        public Village.VillageStorage Storage { get; init; } = null!;
        public Func<int, Village.BuildingSite?>? ResolveBuildingSite { get; init; }
        public Func<int, Village.VillageBuilding?>? ResolveBuilding { get; init; }
        public Func<int, Villager?>? ResolveVillager { get; init; }

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

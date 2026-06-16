using System;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Crafting;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.Engine.Audio;
using Autonocraft.Items;
using Autonocraft.World;
using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.Core
{
    public enum CrosshairState
    {
        Neutral,
        Mining,
        ValidPlace,
        InvalidPlace,
        InteractStation,
        Flash,
        Melee
    }

    public struct PlacePopEffect
    {
        public Vector3 Position;
        public BlockType BlockType;
        public float Timer;
        public float Duration;
        public bool Active;
    }

    public class BlockInteractionSystem
    {
        public const float RaycastRange = 5f;

        private PlacePopEffect _placePop;
        private Vector3? _miningBlockPos;
        private BlockType _miningBlockType;
        private float _breakProgress;
        private float _animTime;
        private int _prevSelectedSlot = -1;
        private float _hotbarPulseTimer;
        private float _crosshairFlashTimer;
        private float _meleeCrosshairTimer;
        private InteractionAnimator? _animator;
        private float _sigilAnimTimer;
        private SigilPattern? _pendingSigilPattern;
        private Vector3? _pendingSigilCenter;
        private Vector3? _pendingSigilNormal;
        private int _pendingSigilCx;
        private int _pendingSigilCy;
        private int _pendingSigilCz;
        private float _sigilLastHintProgress;

        public Vector3? TargetBlockPos { get; private set; }
        public Vector3? TargetNormal { get; private set; }
        public BlockType TargetBlockType { get; private set; } = BlockType.Air;

        public Vector3? GhostBlockPos { get; private set; }
        public BlockType GhostBlockType { get; private set; } = BlockType.Air;
        public bool GhostValid { get; private set; }

        public float BreakProgress => _breakProgress;
        public bool IsMining => _breakProgress > 0f && _miningBlockPos.HasValue;
        public CrosshairState Crosshair { get; private set; } = CrosshairState.Neutral;
        public float HotbarPulseScale { get; private set; } = 1f;
        public float CrosshairFlashAlpha { get; private set; }
        public float AnimTime => _animTime;
        public PlacePopEffect PlacePop => _placePop;

        public Action<string>? ShowToast { get; set; }
        public Action<SfxKind, BlockType>? PlaySfx { get; set; }
        public Func<VoxelWorld, int, int, int, bool>? TryClaimStructureAt { get; set; }
        public Action<ItemStack, Vector3>? OnSpawnItemDrop { get; set; }

        public Vector3? PendingStationOpen { get; private set; }
        public BlockType PendingStationType { get; private set; } = BlockType.Air;

        public void BindAnimator(InteractionAnimator animator) => _animator = animator;

        public static (Vector3? blockPos, Vector3? normal, BlockType blockType, float distance) Raycast(
            VoxelWorld world,
            Vector3 origin,
            Vector3 direction,
            float maxDistance)
        {
            var hit = RaycastHit(world, origin, direction, maxDistance, solidOnly: false);
            return ToTuple(hit);
        }

        public static (Vector3? blockPos, Vector3? normal, BlockType blockType, float distance) RaycastSolid(
            VoxelWorld world,
            Vector3 origin,
            Vector3 direction,
            float maxDistance)
        {
            var hit = RaycastHit(world, origin, direction, maxDistance, solidOnly: true);
            return ToTuple(hit);
        }

        public static BlockRaycastHit RaycastSolidHit(
            VoxelWorld world,
            Vector3 origin,
            Vector3 direction,
            float maxDistance)
        {
            return RaycastHit(world, origin, direction, maxDistance, solidOnly: true);
        }

        public static BlockRaycastHit RaycastHit(
            VoxelWorld world,
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            bool solidOnly)
        {
            if (direction.LengthSquared() < 1e-8f)
            {
                return BlockRaycastHit.Miss;
            }

            direction = Vector3.Normalize(direction);

            int x = (int)MathF.Floor(origin.X);
            int y = (int)MathF.Floor(origin.Y);
            int z = (int)MathF.Floor(origin.Z);

            int stepX = direction.X > 0f ? 1 : (direction.X < 0f ? -1 : 0);
            int stepY = direction.Y > 0f ? 1 : (direction.Y < 0f ? -1 : 0);
            int stepZ = direction.Z > 0f ? 1 : (direction.Z < 0f ? -1 : 0);

            float tDeltaX = stepX != 0 ? MathF.Abs(1f / direction.X) : float.PositiveInfinity;
            float tDeltaY = stepY != 0 ? MathF.Abs(1f / direction.Y) : float.PositiveInfinity;
            float tDeltaZ = stepZ != 0 ? MathF.Abs(1f / direction.Z) : float.PositiveInfinity;

            float fracX = origin.X - MathF.Floor(origin.X);
            float fracY = origin.Y - MathF.Floor(origin.Y);
            float fracZ = origin.Z - MathF.Floor(origin.Z);

            float tMaxX = stepX > 0 ? (1f - fracX) * tDeltaX : (stepX < 0 ? fracX * tDeltaX : float.PositiveInfinity);
            float tMaxY = stepY > 0 ? (1f - fracY) * tDeltaY : (stepY < 0 ? fracY * tDeltaY : float.PositiveInfinity);
            float tMaxZ = stepZ > 0 ? (1f - fracZ) * tDeltaZ : (stepZ < 0 ? fracZ * tDeltaZ : float.PositiveInfinity);

            Vector3? lastAirPos = null;
            float traveled = 0f;
            const int maxSteps = 256;

            for (int step = 0; step < maxSteps && traveled <= maxDistance; step++)
            {
                PerfCounters.RaycastBlockVisits++;
                var block = world.GetBlock(x, y, z);

                if (block == BlockType.Air || (solidOnly && block.IsFluid()))
                {
                    lastAirPos = new Vector3(x, y, z);
                }
                else if (block.IsFluid() && !solidOnly)
                {
                    var hitPos = new Vector3(x, y, z);
                    var normal = ComputeHitNormal(lastAirPos, hitPos);
                    float distance = traveled + Vector3.Distance(origin, hitPos + new Vector3(0.5f, 0.5f, 0.5f)) * 0.001f;
                    return new BlockRaycastHit(true, hitPos, normal, block, distance);
                }
                else if (!block.IsFluid())
                {
                    var hitPos = new Vector3(x, y, z);
                    var normal = ComputeHitNormal(lastAirPos, hitPos);
                    float distance = traveled + Vector3.Distance(origin, hitPos + new Vector3(0.5f, 0.5f, 0.5f)) * 0.001f;
                    return new BlockRaycastHit(true, hitPos, normal, block, distance);
                }
                else
                {
                    lastAirPos = new Vector3(x, y, z);
                }

                if (tMaxX < tMaxY)
                {
                    if (tMaxX < tMaxZ)
                    {
                        x += stepX;
                        traveled = tMaxX;
                        tMaxX += tDeltaX;
                    }
                    else
                    {
                        z += stepZ;
                        traveled = tMaxZ;
                        tMaxZ += tDeltaZ;
                    }
                }
                else
                {
                    if (tMaxY < tMaxZ)
                    {
                        y += stepY;
                        traveled = tMaxY;
                        tMaxY += tDeltaY;
                    }
                    else
                    {
                        z += stepZ;
                        traveled = tMaxZ;
                        tMaxZ += tDeltaZ;
                    }
                }
            }

            return BlockRaycastHit.Miss;
        }

        private static Vector3 ComputeHitNormal(Vector3? lastAirPos, Vector3 hitPos)
        {
            if (!lastAirPos.HasValue)
            {
                return Vector3.Zero;
            }

            Vector3 diff = lastAirPos.Value - hitPos;
            if (MathF.Abs(diff.X) > MathF.Abs(diff.Y) && MathF.Abs(diff.X) > MathF.Abs(diff.Z))
            {
                return new Vector3(MathF.Sign(diff.X), 0, 0);
            }

            if (MathF.Abs(diff.Y) > MathF.Abs(diff.X) && MathF.Abs(diff.Y) > MathF.Abs(diff.Z))
            {
                return new Vector3(0, MathF.Sign(diff.Y), 0);
            }

            return new Vector3(0, 0, MathF.Sign(diff.Z));
        }

        private static (Vector3? blockPos, Vector3? normal, BlockType blockType, float distance) ToTuple(BlockRaycastHit hit)
        {
            if (!hit.HasHit)
            {
                return (null, null, BlockType.Air, float.MaxValue);
            }

            return (hit.BlockPos, hit.Normal, hit.BlockType, hit.Distance);
        }

        public void Update(
            float deltaTime,
            VoxelWorld world,
            Player player,
            Vector3 cameraPos,
            Vector3 cameraFront,
            bool leftHeld,
            bool rightPressed,
            bool shiftRightPressed,
            CraftingSystem? crafting,
            ParticleSystem particles,
            GraphicsDevice? device,
            BlockRaycastHit? solidRayHit = null,
            bool suppressBlockGhost = false)
        {
            _animTime += deltaTime;
            PendingStationOpen = null;
            PendingStationType = BlockType.Air;

            UpdateHotbarPulse(player.SelectedSlot, deltaTime);
            UpdatePlacePop(deltaTime);
            UpdateCrosshairFlash(deltaTime);
            UpdateMeleeCrosshair(deltaTime);

            var rayHit = solidRayHit ?? RaycastSolidHit(world, cameraPos, cameraFront, RaycastRange);
            Vector3? hitBlockPos = rayHit.HasHit ? rayHit.BlockPos : null;
            Vector3? normal = rayHit.HasHit ? rayHit.Normal : null;
            var blockType = rayHit.HasHit ? rayHit.BlockType : BlockType.Air;
            UpdateSigilAnimation(deltaTime, world, crafting, particles, device, normal);
            TargetBlockPos = hitBlockPos;
            TargetNormal = normal;
            TargetBlockType = blockType;

            if (!suppressBlockGhost)
            {
                UpdateGhostPreview(world, player, hitBlockPos, normal, blockType);
            }
            else
            {
                GhostBlockPos = null;
                GhostValid = false;
                GhostBlockType = BlockType.Air;
            }

            if (leftHeld && hitBlockPos.HasValue && blockType != BlockType.Air)
            {
                _animator?.SetMining(true);

                if (!_miningBlockPos.HasValue || _miningBlockPos.Value != hitBlockPos.Value)
                {
                    _miningBlockPos = hitBlockPos;
                    _miningBlockType = blockType;
                    _breakProgress = 0f;
                }

                float breakTime = MiningCalculator.GetEffectiveBreakTime(blockType, player.GetSelectedStack(), player.Skills);
                if (breakTime > 0f)
                {
                    _breakProgress += deltaTime / breakTime;
                    Crosshair = CrosshairState.Mining;
                    UpdateMiningEffects(deltaTime, player, particles, hitBlockPos.Value, blockType, normal);

                    if (_breakProgress >= 1f)
                    {
                        CompleteBreak(world, player, particles, device);
                    }
                }
            }
            else
            {
                _animator?.SetMining(false);
                _miningBlockPos = null;
                _breakProgress = 0f;
            }

            if (shiftRightPressed && hitBlockPos.HasValue && blockType != BlockType.Air)
            {
                int cx = (int)hitBlockPos.Value.X;
                int cy = (int)hitBlockPos.Value.Y;
                int cz = (int)hitBlockPos.Value.Z;
                if (TryClaimStructureAt?.Invoke(world, cx, cy, cz) == true)
                {
                    return;
                }

                if (crafting != null && _sigilAnimTimer <= 0f)
                {
                    var result = crafting.PreviewSigil(world, cx, cy, cz);
                    if (result.Success && result.Pattern != null)
                    {
                        BeginSigilAnimation(result.Pattern, hitBlockPos.Value, normal, cx, cy, cz);
                    }
                    else if (result.PartialMatch)
                    {
                        particles.SpawnHint(hitBlockPos.Value + new Vector3(0.5f, 0.5f, 0.5f));
                    }
                }
            }
            else if (rightPressed && hitBlockPos.HasValue && blockType.IsStation())
            {
                PendingStationOpen = hitBlockPos.Value;
                PendingStationType = blockType;
            }
            else if (rightPressed)
            {
                if (FoodConsumption.TryEatFromHotbar(player))
                {
                    TriggerCrosshairFlash();
                }
                else if (!TryUseBucket(world, player, cameraPos, cameraFront, particles, device))
                {
                    TryPlaceBlock(world, player, particles, device);
                }
            }

            UpdateCrosshairState();
        }

        public void InstantMine(VoxelWorld world, Player player, Vector3 cameraPos, Vector3 cameraFront, ParticleSystem particles, GraphicsDevice? device)
        {
            var (hitBlockPos, _, blockType, _) = RaycastSolid(world, cameraPos, cameraFront, RaycastRange);
            if (!hitBlockPos.HasValue || blockType == BlockType.Air)
            {
                return;
            }

            int bx = (int)hitBlockPos.Value.X;
            int by = (int)hitBlockPos.Value.Y;
            int bz = (int)hitBlockPos.Value.Z;

            Console.WriteLine($"[Mining] Mined block {blockType} at ({bx}, {by}, {bz}).");
            world.SetBlock(bx, by, bz, BlockType.Air, device);
            if (OnSpawnItemDrop != null)
            {
                OnSpawnItemDrop(ItemStack.CreateBlock(blockType, 1), new Vector3(bx + 0.5f, by + 0.5f, bz + 0.5f));
            }
            else
            {
                player.AddToInventory(blockType);
            }
            player.Stats.RecordBlockBroken();
            player.DamageSelectedTool(1);
            GrantSkillXp(player, MiningCalculator.GetSkillForBlock(blockType), MiningCalculator.GetXpForBlock(blockType));
            SpawnBreakEffects(particles, hitBlockPos.Value + new Vector3(0.5f, 0.5f, 0.5f), blockType, player.GetSelectedStack(), null);
            TriggerCrosshairFlash();
            ResetMining();
        }

        public void InstantPlace(VoxelWorld world, Player player, Vector3 cameraPos, Vector3 cameraFront, ParticleSystem particles, GraphicsDevice? device)
        {
            var (hitBlockPos, normal, _, _) = RaycastSolid(world, cameraPos, cameraFront, RaycastRange);
            if (!hitBlockPos.HasValue || !normal.HasValue)
            {
                return;
            }

            Vector3 placePos = hitBlockPos.Value + normal.Value;
            int px = (int)placePos.X;
            int py = (int)placePos.Y;
            int pz = (int)placePos.Z;

            if (player.Intersects(px, py, pz))
            {
                Console.WriteLine("[Building] Cannot place block inside yourself!");
                return;
            }

            BlockType toPlace = player.GetSelectedBlockType();
            if (toPlace == BlockType.Air)
            {
                return;
            }

            if (!player.UseSelectedBlock())
            {
                Console.WriteLine("[Building] Out of blocks!");
                ShowToast?.Invoke("Out of blocks!");
                return;
            }

            Console.WriteLine($"[Building] Placed {toPlace} at ({px}, {py}, {pz}).");
            world.SetBlock(px, py, pz, toPlace, device);
            player.Stats.RecordBlockPlaced();
            TriggerPlacePop(placePos, toPlace);
            particles.SpawnBlockPlace(placePos + new Vector3(0.5f, 0.5f, 0.5f), toPlace);
            TriggerCrosshairFlash();
        }

        private void CompleteBreak(VoxelWorld world, Player player, ParticleSystem particles, GraphicsDevice? device)
        {
            if (!_miningBlockPos.HasValue)
            {
                return;
            }

            int bx = (int)_miningBlockPos.Value.X;
            int by = (int)_miningBlockPos.Value.Y;
            int bz = (int)_miningBlockPos.Value.Z;

            Console.WriteLine($"[Mining] Mined block {_miningBlockType} at ({bx}, {by}, {bz}).");
            world.SetBlock(bx, by, bz, BlockType.Air, device);
            if (OnSpawnItemDrop != null)
            {
                OnSpawnItemDrop(ItemStack.CreateBlock(_miningBlockType, 1), new Vector3(bx + 0.5f, by + 0.5f, bz + 0.5f));
            }
            else
            {
                player.AddToInventory(_miningBlockType);
            }
            player.Stats.RecordBlockBroken();
            bool toolBroke = player.DamageSelectedTool(1);
            GrantSkillXp(player, MiningCalculator.GetSkillForBlock(_miningBlockType), MiningCalculator.GetXpForBlock(_miningBlockType));
            SpawnBreakEffects(
                particles,
                _miningBlockPos.Value + new Vector3(0.5f, 0.5f, 0.5f),
                _miningBlockType,
                player.GetSelectedStack(),
                TargetNormal);
            PlaySfx?.Invoke(SfxKind.Mine, _miningBlockType);
            _animator?.TriggerSwing(SwingKind.Mine);
            if (toolBroke)
            {
                _animator?.TriggerInvalidAction();
                PlaySfx?.Invoke(SfxKind.ToolBreak, BlockType.Air);
                if (TargetBlockPos.HasValue)
                {
                    particles.SpawnToolBreak(_miningBlockPos.Value + new Vector3(0.5f, 0.5f, 0.5f));
                }
            }
            TriggerCrosshairFlash();
            ResetMining();
        }

        private bool TryUseBucket(
            VoxelWorld world,
            Player player,
            Vector3 cameraPos,
            Vector3 cameraFront,
            ParticleSystem particles,
            GraphicsDevice? device)
        {
            var stack = player.GetSelectedStack();
            if (stack.IsEmptyBucket())
            {
                var (fluidPos, _, fluidType, _) = Raycast(world, cameraPos, cameraFront, RaycastRange);
                if (!fluidPos.HasValue || !fluidType.IsWater())
                {
                    return false;
                }

                int x = (int)fluidPos.Value.X;
                int y = (int)fluidPos.Value.Y;
                int z = (int)fluidPos.Value.Z;
                if (!world.Fluids.TryPickup(world, x, y, z, device))
                {
                    return false;
                }

                player.Hotbar[player.SelectedSlot] = ItemStack.CreateFluidContainer(ItemId.WaterBucket);
                particles.SpawnWaterSplash(fluidPos.Value + new Vector3(0.5f, 0.5f, 0.5f), 1.1f);
                PlaySfx?.Invoke(SfxKind.WaterSplash, BlockType.Water);
                TriggerCrosshairFlash();
                return true;
            }

            if (stack.IsWaterBucket() && GhostBlockPos.HasValue && GhostValid)
            {
                int px = (int)GhostBlockPos.Value.X;
                int py = (int)GhostBlockPos.Value.Y;
                int pz = (int)GhostBlockPos.Value.Z;
                if (world.GetBlock(px, py, pz) != BlockType.Air)
                {
                    return false;
                }

                world.Fluids.PlaceSource(world, px, py, pz, device);
                player.Hotbar[player.SelectedSlot] = ItemStack.CreateFluidContainer(ItemId.EmptyBucket);
                particles.SpawnWaterSplash(GhostBlockPos.Value + new Vector3(0.5f, 0.5f, 0.5f), 1.2f);
                PlaySfx?.Invoke(SfxKind.WaterSplash, BlockType.Water);
                TriggerCrosshairFlash();
                return true;
            }

            return false;
        }

        private void TryPlaceBlock(VoxelWorld world, Player player, ParticleSystem particles, GraphicsDevice? device)
        {
            if (!GhostBlockPos.HasValue)
            {
                return;
            }

            if (!GhostValid)
            {
                _animator?.TriggerInvalidAction();
                PlaySfx?.Invoke(SfxKind.Invalid, BlockType.Air);
                return;
            }

            int px = (int)GhostBlockPos.Value.X;
            int py = (int)GhostBlockPos.Value.Y;
            int pz = (int)GhostBlockPos.Value.Z;
            BlockType toPlace = GhostBlockType;

            if (!player.UseSelectedBlock())
            {
                Console.WriteLine("[Building] Out of blocks!");
                ShowToast?.Invoke("Out of blocks!");
                return;
            }

            Console.WriteLine($"[Building] Placed {toPlace} at ({px}, {py}, {pz}).");
            world.SetBlock(px, py, pz, toPlace, device);
            player.Stats.RecordBlockPlaced();
            TriggerPlacePop(GhostBlockPos.Value, toPlace);
            particles.SpawnBlockPlace(GhostBlockPos.Value + new Vector3(0.5f, 0.5f, 0.5f), toPlace);
            PlaySfx?.Invoke(SfxKind.Place, toPlace);
            _animator?.TriggerSwing(SwingKind.Place);
            TriggerCrosshairFlash();
        }

        private void UpdateGhostPreview(VoxelWorld world, Player player, Vector3? hitBlockPos, Vector3? normal, BlockType hitBlockType)
        {
            GhostBlockPos = null;
            GhostValid = false;
            GhostBlockType = BlockType.Air;

            if (!hitBlockPos.HasValue || !normal.HasValue)
            {
                return;
            }

            Vector3 placePos = hitBlockPos.Value + normal.Value;
            if (hitBlockType.IsSlab() && normal.Value.Y > 0.5f)
            {
                placePos = hitBlockPos.Value;
            }

            int px = (int)placePos.X;
            int py = (int)placePos.Y;
            int pz = (int)placePos.Z;

            if (player.GetSelectedStack().IsWaterBucket())
            {
                if (world.GetBlock(px, py, pz) != BlockType.Air || player.Intersects(px, py, pz))
                {
                    return;
                }

                GhostBlockPos = placePos;
                GhostBlockType = BlockType.Water;
                GhostValid = true;
                return;
            }

            BlockType toPlace = player.GetSelectedBlockType();
            if (toPlace == BlockType.Air)
            {
                return;
            }

            GhostBlockPos = placePos;
            GhostBlockType = toPlace;
            BlockType existing = world.GetBlock(px, py, pz);
            bool targetClear = existing == BlockType.Air || existing.IsSlab();
            bool holdingFluid = player.GetSelectedStack().IsFluidContainer();
            bool playerOverlaps = PlayerOverlapsPlacement(player, px, py, pz, existing);
            GhostValid = !playerOverlaps
                && (targetClear || (holdingFluid && existing.IsFluid()));
        }

        private static bool PlayerOverlapsPlacement(Player player, int px, int py, int pz, BlockType existing)
        {
            if (!player.Intersects(px, py, pz))
            {
                return false;
            }

            if (existing.IsSlab())
            {
                return player.Position.Y < py + 0.5f;
            }

            return true;
        }

        private void UpdateCrosshairState()
        {
            if (_crosshairFlashTimer > 0f)
            {
                Crosshair = CrosshairState.Flash;
                return;
            }

            if (_meleeCrosshairTimer > 0f)
            {
                Crosshair = CrosshairState.Melee;
                return;
            }

            if (IsMining)
            {
                Crosshair = CrosshairState.Mining;
                return;
            }

            if (GhostBlockPos.HasValue)
            {
                Crosshair = GhostValid ? CrosshairState.ValidPlace : CrosshairState.InvalidPlace;
                return;
            }

            if (TargetBlockPos.HasValue && TargetBlockType.IsStation())
            {
                Crosshair = CrosshairState.InteractStation;
                return;
            }

            Crosshair = CrosshairState.Neutral;
        }

        private void UpdateHotbarPulse(int selectedSlot, float dt)
        {
            if (_prevSelectedSlot != selectedSlot)
            {
                _hotbarPulseTimer = 0.2f;
                _prevSelectedSlot = selectedSlot;
            }

            if (_hotbarPulseTimer > 0f)
            {
                _hotbarPulseTimer -= dt;
                float t = 1f - Math.Clamp(_hotbarPulseTimer / 0.2f, 0f, 1f);
                HotbarPulseScale = 1f + 0.08f * MathF.Sin(t * MathF.PI);
            }
            else
            {
                HotbarPulseScale = 1f;
            }
        }

        public void TriggerMeleeCrosshair()
        {
            _meleeCrosshairTimer = 0.15f;
        }

        private void UpdateMeleeCrosshair(float dt)
        {
            if (_meleeCrosshairTimer > 0f)
            {
                _meleeCrosshairTimer -= dt;
            }
        }

        public void TriggerCrosshairFlash()
        {
            _crosshairFlashTimer = 0.1f;
            CrosshairFlashAlpha = 1f;
        }

        private void UpdateCrosshairFlash(float dt)
        {
            if (_crosshairFlashTimer > 0f)
            {
                _crosshairFlashTimer -= dt;
                CrosshairFlashAlpha = Math.Clamp(_crosshairFlashTimer / 0.1f, 0f, 1f);
            }
            else
            {
                CrosshairFlashAlpha = 0f;
            }
        }

        private void ResetMining()
        {
            _miningBlockPos = null;
            _breakProgress = 0f;
            _miningBlockType = BlockType.Air;
        }

        private void UpdateMiningEffects(
            float deltaTime,
            Player player,
            ParticleSystem particles,
            Vector3 blockPos,
            BlockType blockType,
            Vector3? faceNormal)
        {
            if (!faceNormal.HasValue)
            {
                return;
            }

            if (_animator != null && _animator.SwingPeakCrossed)
            {
                var center = blockPos + new Vector3(0.5f, 0.5f, 0.5f);
                particles.SpawnMiningDust(center, blockType, faceNormal.Value, _breakProgress);

                var tool = player.GetSelectedStack();
                if (tool.IsTool() && _breakProgress > 0.15f)
                {
                    particles.SpawnToolSparks(center, tool, faceNormal.Value);
                }
            }
        }

        private static void SpawnBreakEffects(
            ParticleSystem particles,
            Vector3 center,
            BlockType blockType,
            ItemStack tool,
            Vector3? faceNormal)
        {
            particles.SpawnBlockBreak(center, blockType, faceNormal);
            if (tool.IsTool() && faceNormal.HasValue)
            {
                particles.SpawnToolSparks(center, tool, faceNormal.Value);
            }
        }

        private void TriggerPlacePop(Vector3 pos, BlockType blockType)
        {
            _placePop = new PlacePopEffect
            {
                Position = pos,
                BlockType = blockType,
                Timer = 0.22f,
                Duration = 0.22f,
                Active = true
            };
        }

        private void BeginSigilAnimation(SigilPattern pattern, Vector3 blockPos, Vector3? normal, int cx, int cy, int cz)
        {
            _sigilAnimTimer = 0.4f;
            _sigilLastHintProgress = 0f;
            _pendingSigilPattern = pattern;
            _pendingSigilCenter = blockPos + new Vector3(0.5f, 0.5f, 0.5f);
            _pendingSigilNormal = normal;
            _pendingSigilCx = cx;
            _pendingSigilCy = cy;
            _pendingSigilCz = cz;
        }

        private void UpdateSigilAnimation(
            float deltaTime,
            VoxelWorld world,
            CraftingSystem? crafting,
            ParticleSystem particles,
            GraphicsDevice? device,
            Vector3? normal)
        {
            if (_sigilAnimTimer <= 0f || _pendingSigilPattern == null || !_pendingSigilCenter.HasValue)
            {
                return;
            }

            _sigilAnimTimer -= deltaTime;
            float progress = 1f - Math.Clamp(_sigilAnimTimer / 0.4f, 0f, 1f);

            if (progress - _sigilLastHintProgress >= 0.15f)
            {
                particles.SpawnHint(_pendingSigilCenter.Value);
                _sigilLastHintProgress = progress;
            }

            if (_sigilAnimTimer > 0f)
            {
                return;
            }

            var pattern = _pendingSigilPattern;
            var center = _pendingSigilCenter.Value;
            crafting?.ApplySigilActivation(world, _pendingSigilCx, _pendingSigilCy, _pendingSigilCz, pattern, device);
            particles.SpawnBlockBreak(center, pattern.OutputStation, _pendingSigilNormal ?? normal);
            particles.SpawnHint(center);
            TriggerCrosshairFlash();

            _pendingSigilPattern = null;
            _pendingSigilCenter = null;
            _pendingSigilNormal = null;
        }

        private void UpdatePlacePop(float dt)
        {
            if (!_placePop.Active)
            {
                return;
            }

            _placePop.Timer -= dt;
            if (_placePop.Timer <= 0f)
            {
                _placePop.Active = false;
            }
        }

        private void GrantSkillXp(Player player, PlayerSkill skill, float xp)
        {
            if (player.Skills.AddXp(skill, xp))
            {
                ShowToast?.Invoke($"{skill} level {player.Skills.GetLevel(skill)}!");
            }
        }
    }
}

using System;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
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
        Flash
    }

    public struct BlockParticle
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Lifetime;
        public float MaxLifetime;
        public bool Active;
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

        private readonly BlockParticle[] _particles = new BlockParticle[32];
        private PlacePopEffect _placePop;
        private Vector3? _miningBlockPos;
        private BlockType _miningBlockType;
        private float _breakProgress;
        private float _animTime;
        private int _prevSelectedSlot = -1;
        private float _hotbarPulseTimer;
        private float _crosshairFlashTimer;

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
        public ReadOnlySpan<BlockParticle> Particles => _particles;
        public PlacePopEffect PlacePop => _placePop;

        public static (Vector3? blockPos, Vector3? normal, BlockType blockType, float distance) Raycast(
            VoxelWorld world,
            Vector3 origin,
            Vector3 direction,
            float maxDistance)
        {
            float step = 0.02f;
            int steps = (int)(maxDistance / step);
            Vector3 currentPos = origin;
            Vector3? lastIntPos = null;
            direction = Vector3.Normalize(direction);

            for (int i = 0; i < steps; i++)
            {
                currentPos += direction * step;
                int bx = (int)MathF.Floor(currentPos.X);
                int by = (int)MathF.Floor(currentPos.Y);
                int bz = (int)MathF.Floor(currentPos.Z);

                var block = world.GetBlock(bx, by, bz);
                if (block != BlockType.Air)
                {
                    Vector3 hitBlockPos = new Vector3(bx, by, bz);
                    Vector3 normal = Vector3.Zero;
                    if (lastIntPos.HasValue)
                    {
                        Vector3 diff = lastIntPos.Value - hitBlockPos;
                        if (MathF.Abs(diff.X) > MathF.Abs(diff.Y) && MathF.Abs(diff.X) > MathF.Abs(diff.Z))
                            normal = new Vector3(MathF.Sign(diff.X), 0, 0);
                        else if (MathF.Abs(diff.Y) > MathF.Abs(diff.X) && MathF.Abs(diff.Y) > MathF.Abs(diff.Z))
                            normal = new Vector3(0, MathF.Sign(diff.Y), 0);
                        else
                            normal = new Vector3(0, 0, MathF.Sign(diff.Z));
                    }

                    float distance = Vector3.Distance(origin, currentPos);
                    return (hitBlockPos, normal, block, distance);
                }

                lastIntPos = new Vector3(bx, by, bz);
            }

            return (null, null, BlockType.Air, float.MaxValue);
        }

        public void Update(
            float deltaTime,
            VoxelWorld world,
            Player player,
            Vector3 cameraPos,
            Vector3 cameraFront,
            bool leftHeld,
            bool rightPressed,
            GraphicsDevice device)
        {
            _animTime += deltaTime;

            UpdateHotbarPulse(player.SelectedSlot, deltaTime);
            UpdateParticles(deltaTime);
            UpdatePlacePop(deltaTime);
            UpdateCrosshairFlash(deltaTime);

            var (hitBlockPos, normal, blockType, _) = Raycast(world, cameraPos, cameraFront, RaycastRange);
            TargetBlockPos = hitBlockPos;
            TargetNormal = normal;
            TargetBlockType = blockType;

            UpdateGhostPreview(player, hitBlockPos, normal);

            if (leftHeld && hitBlockPos.HasValue && blockType != BlockType.Air)
            {
                if (!_miningBlockPos.HasValue || _miningBlockPos.Value != hitBlockPos.Value)
                {
                    _miningBlockPos = hitBlockPos;
                    _miningBlockType = blockType;
                    _breakProgress = 0f;
                }

                float breakTime = blockType.GetBreakTime();
                if (breakTime > 0f)
                {
                    _breakProgress += deltaTime / breakTime;
                    Crosshair = CrosshairState.Mining;

                    if (_breakProgress >= 1f)
                    {
                        CompleteBreak(world, player, device);
                    }
                }
            }
            else
            {
                _miningBlockPos = null;
                _breakProgress = 0f;
            }

            if (rightPressed)
            {
                TryPlaceBlock(world, player, device);
            }

            UpdateCrosshairState();
        }

        public void InstantMine(VoxelWorld world, Player player, Vector3 cameraPos, Vector3 cameraFront, GraphicsDevice device)
        {
            var (hitBlockPos, _, blockType, _) = Raycast(world, cameraPos, cameraFront, RaycastRange);
            if (!hitBlockPos.HasValue || blockType == BlockType.Air)
            {
                return;
            }

            int bx = (int)hitBlockPos.Value.X;
            int by = (int)hitBlockPos.Value.Y;
            int bz = (int)hitBlockPos.Value.Z;

            Console.WriteLine($"[Mining] Mined block {blockType} at ({bx}, {by}, {bz}).");
            world.SetBlock(bx, by, bz, BlockType.Air, device);
            player.AddToInventory(blockType);
            SpawnBreakParticles(hitBlockPos.Value + new Vector3(0.5f, 0.5f, 0.5f), blockType);
            TriggerCrosshairFlash();
            ResetMining();
        }

        public void InstantPlace(VoxelWorld world, Player player, Vector3 cameraPos, Vector3 cameraFront, GraphicsDevice device)
        {
            var (hitBlockPos, normal, _, _) = Raycast(world, cameraPos, cameraFront, RaycastRange);
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
                return;
            }

            Console.WriteLine($"[Building] Placed {toPlace} at ({px}, {py}, {pz}).");
            world.SetBlock(px, py, pz, toPlace, device);
            TriggerPlacePop(placePos, toPlace);
            SpawnPlaceParticles(placePos + new Vector3(0.5f, 0.5f, 0.5f), toPlace);
            TriggerCrosshairFlash();
        }

        private void CompleteBreak(VoxelWorld world, Player player, GraphicsDevice device)
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
            player.AddToInventory(_miningBlockType);
            SpawnBreakParticles(_miningBlockPos.Value + new Vector3(0.5f, 0.5f, 0.5f), _miningBlockType);
            TriggerCrosshairFlash();
            ResetMining();
        }

        private void TryPlaceBlock(VoxelWorld world, Player player, GraphicsDevice device)
        {
            if (!GhostBlockPos.HasValue || !GhostValid)
            {
                return;
            }

            int px = (int)GhostBlockPos.Value.X;
            int py = (int)GhostBlockPos.Value.Y;
            int pz = (int)GhostBlockPos.Value.Z;
            BlockType toPlace = GhostBlockType;

            if (!player.UseSelectedBlock())
            {
                Console.WriteLine("[Building] Out of blocks!");
                return;
            }

            Console.WriteLine($"[Building] Placed {toPlace} at ({px}, {py}, {pz}).");
            world.SetBlock(px, py, pz, toPlace, device);
            TriggerPlacePop(GhostBlockPos.Value, toPlace);
            SpawnPlaceParticles(GhostBlockPos.Value + new Vector3(0.5f, 0.5f, 0.5f), toPlace);
            TriggerCrosshairFlash();
        }

        private void UpdateGhostPreview(Player player, Vector3? hitBlockPos, Vector3? normal)
        {
            GhostBlockPos = null;
            GhostValid = false;
            GhostBlockType = BlockType.Air;

            if (!hitBlockPos.HasValue || !normal.HasValue)
            {
                return;
            }

            Vector3 placePos = hitBlockPos.Value + normal.Value;
            BlockType toPlace = player.GetSelectedBlockType();
            if (toPlace == BlockType.Air)
            {
                return;
            }

            int px = (int)placePos.X;
            int py = (int)placePos.Y;
            int pz = (int)placePos.Z;

            GhostBlockPos = placePos;
            GhostBlockType = toPlace;
            GhostValid = !player.Intersects(px, py, pz);
        }

        private void UpdateCrosshairState()
        {
            if (_crosshairFlashTimer > 0f)
            {
                Crosshair = CrosshairState.Flash;
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

        private void SpawnBreakParticles(Vector3 center, BlockType blockType)
        {
            var rng = new Random((int)(center.X * 73 + center.Y * 37 + center.Z * 19));
            int count = rng.Next(6, 11);

            for (int i = 0; i < count; i++)
            {
                for (int slot = 0; slot < _particles.Length; slot++)
                {
                    if (_particles[slot].Active)
                    {
                        continue;
                    }

                    float angle = (float)rng.NextDouble() * MathF.PI * 2f;
                    float speed = 1.5f + (float)rng.NextDouble() * 2f;
                    _particles[slot] = new BlockParticle
                    {
                        Position = center,
                        Velocity = new Vector3(MathF.Cos(angle) * speed, 1.5f + (float)rng.NextDouble(), MathF.Sin(angle) * speed),
                        Lifetime = 0.3f,
                        MaxLifetime = 0.3f,
                        Active = true
                    };
                    break;
                }
            }
        }

        private void SpawnPlaceParticles(Vector3 center, BlockType blockType)
        {
            var rng = new Random((int)(center.X * 53 + center.Y * 29 + center.Z * 11 + (int)blockType));
            for (int i = 0; i < 4; i++)
            {
                for (int slot = 0; slot < _particles.Length; slot++)
                {
                    if (_particles[slot].Active)
                    {
                        continue;
                    }

                    float angle = (float)rng.NextDouble() * MathF.PI * 2f;
                    float speed = 0.8f + (float)rng.NextDouble();
                    _particles[slot] = new BlockParticle
                    {
                        Position = center,
                        Velocity = new Vector3(MathF.Cos(angle) * speed, 0.8f + (float)rng.NextDouble() * 0.5f, MathF.Sin(angle) * speed),
                        Lifetime = 0.2f,
                        MaxLifetime = 0.2f,
                        Active = true
                    };
                    break;
                }
            }
        }

        private void UpdateParticles(float dt)
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].Active)
                {
                    continue;
                }

                _particles[i].Lifetime -= dt;
                if (_particles[i].Lifetime <= 0f)
                {
                    _particles[i].Active = false;
                    continue;
                }

                _particles[i].Velocity += new Vector3(0f, -6f, 0f) * dt;
                _particles[i].Position += _particles[i].Velocity * dt;
            }
        }

        private void TriggerPlacePop(Vector3 pos, BlockType blockType)
        {
            _placePop = new PlacePopEffect
            {
                Position = pos,
                BlockType = blockType,
                Timer = 0.15f,
                Duration = 0.15f,
                Active = true
            };
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
    }
}

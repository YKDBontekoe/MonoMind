using System;
using System.Numerics;
using Autonocraft.Domain.Entities;
using Autonocraft.Domain.Rendering;
using Autonocraft.Engine.Animation;
using Autonocraft.Items.Rendering;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Core
{
    public partial class Player : ICraftingPlayer, INightThreatPlayer, IPlayerHudView, IPlayerMotionView, IPlayerAmbientView
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw { get; set; } = -90f;
        public float Pitch { get; set; } = 0f;

        public float Health { get; set; } = 20f;
        public float MaxHealth { get; set; } = 20f;
        public float Hunger { get; set; } = SurvivalConstants.MaxHunger;
        public float MaxHunger { get; set; } = SurvivalConstants.MaxHunger;
        public DeathCause LastDeathCause { get; set; } = DeathCause.Unknown;
        public bool DeathConsequencesApplied { get; set; }

        public const float Width = 0.6f;
        public const float Height = 1.8f;
        public const float EyeHeight = PlayerConstants.EyeHeight;

        public const float Gravity = -38f;
        public const float WalkSpeed = PlayerConstants.WalkSpeed;
        public const float FlySpeed = 15.0f;
        public const float JumpForce = 9.8f;
        public const float Damping = 0.15f;

        public const float SwimSpeed = 4.5f;
        public const float MaxOxygen = PlayerConstants.MaxOxygen;
        public const float OxygenDamagePerSecond = 2f;

        public bool IsGrounded { get; private set; }
        public bool InWater { get; private set; }
        public bool HeadUnderwater { get; private set; }
        public bool OnWaterSurface { get; private set; }
        public bool InLava { get; private set; }
        public bool HeadInLava { get; private set; }
        public bool OnLavaSurface { get; private set; }
        public float Oxygen { get; private set; } = MaxOxygen;
        public bool CreativeMode { get; set; } = false;
        public bool IsSprinting { get; set; } = false;
        public float CustomMoveSpeed { get; set; } = 0f;
        public bool IsAlive => Health > 0f;
        public bool JustLanded { get; private set; }
        public float FallDistance { get; private set; }

        public Action<string>? ShowToast { get; set; }
        public Action<ItemStack>? OnItemAdded { get; set; }

        public PlayerSkills Skills { get; } = new();
        public PlayerStatistics Stats { get; } = new();

        private void Notify(string message) => ShowToast?.Invoke(message);

        private bool _wasGrounded = true;
        private bool _wasInWater;
        private bool _wasInLava;
        private float _fallStartY;
        private float _invulnerabilityTimer;
        private float _starvationTimer;
        public const float InvulnerabilityDuration = 0.5f;

        public void ResetFallTracking()
        {
            _wasGrounded = IsGrounded;
            _wasInWater = InWater;
            _wasInLava = InLava;
            _fallStartY = Position.Y;
            JustLanded = false;
            FallDistance = 0f;
        }

        public void ForceAirborne()
        {
            IsGrounded = false;
            _wasGrounded = false;
            _wasInWater = InWater;
            _wasInLava = InLava;
            _fallStartY = Position.Y;
            JustLanded = false;
            FallDistance = 0f;
        }

        public Player(Vector3 spawnPosition)
        {
            Position = spawnPosition;
            Velocity = Vector3.Zero;

            for (int i = 0; i < Hotbar.Length; i++)
            {
                Hotbar[i] = ItemStack.Empty;
            }
        }

        public void RestoreHunger(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            Hunger = MathF.Min(MaxHunger, Hunger + amount);
        }

        public void UpdateHunger(float deltaTime, InteractionAnimator? animator = null)
        {
            if (CreativeMode || !IsAlive)
            {
                return;
            }

            float hungerDrain = SurvivalConstants.HungerDrainPerSecond;
            if (IsSprinting)
            {
                hungerDrain *= 3.0f; // drain hunger 3x faster when sprinting
            }

            Hunger = MathF.Max(0f, Hunger - hungerDrain * deltaTime);
            if (Hunger > 0f)
            {
                return;
            }

            _starvationTimer += deltaTime;
            if (_starvationTimer < SurvivalConstants.StarvationDamageInterval)
            {
                return;
            }

            _starvationTimer = 0f;
            if (TakeDamage(SurvivalConstants.StarvationDamage, out _))
            {
                LastDeathCause = DeathCause.Starvation;
                animator?.TriggerDamage(0.6f);
            }
        }

        public float GetMoveSpeedMultiplier()
        {
            if (CreativeMode || Hunger > MaxHunger * SurvivalConstants.LowHungerFraction)
            {
                return 1f;
            }

            return SurvivalConstants.LowHungerSpeedMultiplier;
        }

        public bool TakeDamage(float amount, out bool tookDamage)
        {
            tookDamage = false;
            if (!IsAlive || _invulnerabilityTimer > 0f || amount <= 0f)
            {
                return false;
            }

            Health = Math.Max(0f, Health - amount);
            _invulnerabilityTimer = InvulnerabilityDuration;
            tookDamage = true;
            Stats.RecordDamageTaken(amount);
            return true;
        }

        public bool TakeDamage(float amount)
        {
            return TakeDamage(amount, out _);
        }

        public void UpdateInvulnerability(float deltaTime)
        {
            if (_invulnerabilityTimer > 0f)
            {
                _invulnerabilityTimer = Math.Max(0f, _invulnerabilityTimer - deltaTime);
            }
        }

        public void ClearInvulnerability()
        {
            _invulnerabilityTimer = 0f;
        }

        public void Update(float deltaTime, VoxelWorld world, Vector3 moveInput, bool swimUp = false, bool swimDown = false)
        {
            JustLanded = false;
            FallDistance = 0f;
            Vector3 prevPos = Position;

            if (CreativeMode)
            {
                float speed = CustomMoveSpeed > 0f ? CustomMoveSpeed : FlySpeed;
                if (IsSprinting)
                {
                    speed *= 1.5f; // Creative flying sprint boost
                }
                Velocity.Y = moveInput.Y * speed;
                Velocity.X = moveInput.X * speed;
                Velocity.Z = moveInput.Z * speed;

                Position += Velocity * deltaTime;
                IsGrounded = false;
                InWater = false;
                HeadUnderwater = false;
                OnWaterSurface = false;
                InLava = false;
                HeadInLava = false;
                OnLavaSurface = false;
            }
            else
            {
                Vector3 horizontalMove = new Vector3(moveInput.X, 0, moveInput.Z);
                if (horizontalMove != Vector3.Zero)
                {
                    horizontalMove = Vector3.Normalize(horizontalMove);
                    float speed = CustomMoveSpeed > 0f ? CustomMoveSpeed : WalkSpeed;
                    speed *= GetMoveSpeedMultiplier();
                    if (InWater)
                    {
                        speed = CustomMoveSpeed > 0f ? CustomMoveSpeed : SwimSpeed;
                    }
                    else if (InLava)
                    {
                        speed = CustomMoveSpeed > 0f ? CustomMoveSpeed : (SwimSpeed * 0.7f);
                    }
                    else if (IsSprinting)
                    {
                        speed *= 1.3f; // Survival sprint boost
                    }

                    horizontalMove *= speed;
                }
                else
                {
                    Velocity.X *= MathF.Pow(Damping, deltaTime * 10f);
                    Velocity.Z *= MathF.Pow(Damping, deltaTime * 10f);
                    if (MathF.Abs(Velocity.X) < 0.01f) Velocity.X = 0;
                    if (MathF.Abs(Velocity.Z) < 0.01f) Velocity.Z = 0;
                    horizontalMove = new Vector3(Velocity.X, 0, Velocity.Z);
                }

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
                    EyeHeight,
                    horizontalMove,
                    swimUp,
                    swimDown);

                Position = state.Position;
                Velocity = state.Velocity;
                IsGrounded = state.IsGrounded;
                InWater = state.InWater;
                HeadUnderwater = state.HeadUnderwater;
                OnWaterSurface = state.OnWaterSurface;
                InLava = state.InLava;
                HeadInLava = state.HeadInLava;
                OnLavaSurface = state.OnLavaSurface;

                if (HeadUnderwater)
                {
                    float prevOxygen = Oxygen;
                    Oxygen = MathF.Max(0f, Oxygen - deltaTime);
                    if (prevOxygen > 0f && Oxygen <= 0f)
                    {
                        Stats.RecordDrowning();
                    }
                }
                else
                {
                    Oxygen = MaxOxygen;
                }

                if (_wasGrounded && !IsGrounded)
                {
                    _fallStartY = Position.Y;
                }
                else if (!IsGrounded && !InWater && !InLava)
                {
                    _fallStartY = MathF.Max(_fallStartY, Position.Y);
                }
                else if (((!_wasInWater && InWater) || (!_wasInLava && InLava)) && !_wasGrounded)
                {
                    JustLanded = true;
                    FallDistance = MathF.Max(0f, _fallStartY - Position.Y);
                }
                else if (!_wasGrounded && IsGrounded)
                {
                    JustLanded = true;
                    FallDistance = MathF.Max(0f, _fallStartY - Position.Y);
                }

                _wasGrounded = IsGrounded;
                _wasInWater = InWater;
                _wasInLava = InLava;
            }

            Stats.RecordMovement(prevPos, Position, CreativeMode, IsGrounded);
        }

        public void Jump()
        {
            if (CreativeMode)
            {
                return;
            }

            if (IsGrounded)
            {
                Velocity.Y = JumpForce;
                IsGrounded = false;
                Console.WriteLine("[Player] Jumped!");
            }
            else if (InWater && !HeadUnderwater)
            {
                Velocity.Y = JumpForce * 0.85f;
                Console.WriteLine("[Player] Jumped from water!");
            }
            else if (OnLavaSurface)
            {
                Velocity.Y = JumpForce * 0.85f;
                Console.WriteLine("[Player] Jumped from lava surface!");
            }
        }

        public bool Intersects(int x, int y, int z)
        {
            float minX = Position.X - Width / 2f;
            float maxX = Position.X + Width / 2f;
            float minY = Position.Y;
            float maxY = Position.Y + Height;
            float minZ = Position.Z - Width / 2f;
            float maxZ = Position.Z + Width / 2f;

            return (minX < x + 1 && maxX > x) &&
                   (minY < y + 1 && maxY > y) &&
                   (minZ < z + 1 && maxZ > z);
        }

        public static bool IsSpaceClearAt(VoxelWorld world, Vector3 position)
        {
            return EntityCollision.IsSpaceClearAt(world, position, Width, Height);
        }

        public static Vector3 FindSafeSpawnPosition(VoxelWorld world, int x, int z)
        {
            float spawnX = x + 0.5f;
            float spawnZ = z + 0.5f;
            int surfaceY = world.GetHighestSolidY(x, z);
            if (surfaceY < 0)
            {
                surfaceY = 64;
            }

            for (int offset = 0; offset < 16; offset++)
            {
                float spawnY = surfaceY + 1f + offset;
                var candidate = new Vector3(spawnX, spawnY, spawnZ);
                if (IsSpaceClearAt(world, candidate))
                {
                    return candidate;
                }
            }

            return new Vector3(spawnX, surfaceY + 1f, spawnZ);
        }

        Vector3 INightThreatPlayer.Position => Position;

        Action<string>? INightThreatPlayer.ShowToast => ShowToast;

        Vector3 IPlayerHudView.Position => Position;

        Vector3 IPlayerMotionView.Velocity => Velocity;

        Vector3 IPlayerAmbientView.Position => Position;
    }
}

using System;
using System.Numerics;
using Autonocraft.World;

namespace Autonocraft.Entities
{
    public class Animal
    {
        private static int _nextId = 1;

        public int Id { get; }
        public AnimalType Type { get; }
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw;
        public bool IsGrounded { get; private set; }

        public float IdleTime { get; private set; }
        public Vector3 WanderDirection;
        public float WanderDistanceRemaining;
        public float AirborneTime;
        public float Health { get; private set; }
        public float HitFlashTimer { get; private set; }
        public float DeathAnimTimer { get; private set; }
        public bool IsDying { get; private set; }
        public bool ReadyForRemoval => IsDying && DeathAnimTimer <= 0f;
        public float DeathScale => IsDying ? Math.Clamp(DeathAnimTimer / DeathAnimDuration, 0f, 1f) : 1f;

        private const float HitFlashDuration = 0.15f;
        private const float DeathAnimDuration = 0.3f;

        private readonly Random _rng;
        private readonly AnimalStats _stats;

        public Animal(AnimalType type, Vector3 position, int seed)
        {
            Id = _nextId++;
            Type = type;
            Position = position;
            Velocity = Vector3.Zero;
            Yaw = 0f;
            _rng = new Random(seed);
            _stats = AnimalStats.For(type);
            Health = _stats.MaxHealth;
            IdleTime = NextIdleDuration();
            WanderDirection = Vector3.Zero;
            WanderDistanceRemaining = 0f;
        }

        public AnimalStats Stats => _stats;
        public float MaxHealth => _stats.MaxHealth;
        public bool IsAlive => Health > 0f;

        public void TakeDamage(float amount, Vector3? attackerPos)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            Health = Math.Max(0f, Health - amount);
            HitFlashTimer = HitFlashDuration;

            if (attackerPos.HasValue)
            {
                var away = Position - attackerPos.Value;
                away.Y = 0f;
                if (away != Vector3.Zero)
                {
                    WanderDirection = Vector3.Normalize(away);
                    WanderDistanceRemaining = 3f;
                }
            }
        }

        public void BeginDeathAnimation()
        {
            IsDying = true;
            DeathAnimTimer = DeathAnimDuration;
        }

        public void UpdateAnimation(float deltaTime)
        {
            if (HitFlashTimer > 0f)
            {
                HitFlashTimer = Math.Max(0f, HitFlashTimer - deltaTime);
            }

            if (DeathAnimTimer > 0f)
            {
                DeathAnimTimer = Math.Max(0f, DeathAnimTimer - deltaTime);
            }
        }

        public void Update(float deltaTime, VoxelWorld world)
        {
            if (IsDying)
            {
                return;
            }

            UpdateAi(deltaTime);

            var horizontal = new Vector3(WanderDirection.X * _stats.WalkSpeed, 0f, WanderDirection.Z * _stats.WalkSpeed);
            if (Type == AnimalType.Chicken && IsGrounded && WanderDistanceRemaining > 0f && _rng.NextDouble() < 0.02)
            {
                Velocity.Y = 3.5f;
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
                _stats.Width,
                _stats.Height,
                _stats.Height * 0.85f,
                horizontal,
                swimUp: false,
                swimDown: false);

            Position = state.Position;
            Velocity = state.Velocity;
            IsGrounded = state.IsGrounded;

            if (IsGrounded)
            {
                AirborneTime = 0f;
            }
            else
            {
                AirborneTime += deltaTime;
                if (AirborneTime > 1f)
                {
                    WanderDirection = Vector3.Zero;
                    WanderDistanceRemaining = 0f;
                }
            }

            if (WanderDirection != Vector3.Zero)
            {
                Yaw = MathF.Atan2(WanderDirection.X, WanderDirection.Z) * (180f / MathF.PI);
            }
        }

        private void UpdateAi(float deltaTime)
        {
            if (WanderDistanceRemaining > 0f)
            {
                WanderDistanceRemaining -= MathF.Abs(Velocity.X) * deltaTime + MathF.Abs(Velocity.Z) * deltaTime;
                if (WanderDistanceRemaining <= 0f)
                {
                    WanderDirection = Vector3.Zero;
                    IdleTime = NextIdleDuration();
                }
                return;
            }

            IdleTime -= deltaTime;
            if (IdleTime > 0f)
            {
                WanderDirection = Vector3.Zero;
                return;
            }

            if (!IsGrounded)
            {
                return;
            }

            float angle = (float)(_rng.NextDouble() * MathF.PI * 2f);
            WanderDirection = Vector3.Normalize(new Vector3(MathF.Sin(angle), 0f, MathF.Cos(angle)));
            WanderDistanceRemaining = 2f + (float)_rng.NextDouble() * 4f;
        }

        public void OnBlocked()
        {
            WanderDirection = Vector3.Zero;
            WanderDistanceRemaining = 0f;
            IdleTime = NextIdleDuration();
        }

        private float NextIdleDuration()
        {
            return 1.5f + (float)_rng.NextDouble() * 2f;
        }

        public bool IsInChunk(int cx, int cz)
        {
            VoxelWorld.GetChunkCoords((int)MathF.Floor(Position.X), (int)MathF.Floor(Position.Z), out int acx, out int acz, out _, out _);
            return acx == cx && acz == cz;
        }
    }
}

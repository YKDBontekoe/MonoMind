using System;
using System.Numerics;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;
using Autonocraft.Domain.Core;

namespace Autonocraft.Engine
{
    public enum ParticleKind : byte
    {
        BlockShard,
        Dust,
        Spark,
        Hint,
        Hit,
        Death,
        Bubble,
        DustMote,
        Firefly,
        FallingLeaf,
        RainDrop,
        SnowFlake
    }

    public struct Particle
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Lifetime;
        public float MaxLifetime;
        public float Size;
        public float Rotation;
        public float AngularVelocity;
        public float Gravity;
        public float Drag;
        public Vector3 Color;
        public BlockType BlockType;
        public bool UseTexture;
        public ParticleKind Kind;
        public bool Active;
    }

    public sealed class ParticleSystem
    {
        public const int MaxParticles = 512;

        private readonly Particle[] _particles = new Particle[MaxParticles];

        public ReadOnlySpan<Particle> Particles => _particles;

        public void Update(float deltaTime, VoxelWorld world)
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                ref var p = ref _particles[i];
                if (!p.Active)
                {
                    continue;
                }

                p.Lifetime -= deltaTime;
                if (p.Lifetime <= 0f)
                {
                    p.Active = false;
                    continue;
                }

                if (p.Kind == ParticleKind.SnowFlake)
                {
                    p.Velocity.X += MathF.Sin(p.Lifetime * 2.5f) * 0.08f;
                    p.Velocity.Z += MathF.Cos(p.Lifetime * 2.0f) * 0.08f;
                }

                p.Velocity += new Vector3(0f, -p.Gravity, 0f) * deltaTime;
                p.Velocity *= MathF.Pow(1f - p.Drag, deltaTime * 60f);
                p.Position += p.Velocity * deltaTime;
                p.Rotation += p.AngularVelocity * deltaTime;

                if (p.Kind == ParticleKind.RainDrop || p.Kind == ParticleKind.SnowFlake)
                {
                    int bx = (int)MathF.Floor(p.Position.X);
                    int by = (int)MathF.Floor(p.Position.Y);
                    int bz = (int)MathF.Floor(p.Position.Z);
                    if (by >= 0 && by < 256)
                    {
                        var block = world.GetBlock(bx, by, bz);
                        if (!block.IsTransparent() || block.IsFluid())
                        {
                            p.Active = false;
                        }
                    }
                }
            }
        }

        public void SpawnBlockBreak(Vector3 center, BlockType blockType, Vector3? faceNormal = null)
        {
            var rng = SeedRng(center, blockType, 73);
            var baseColor = BlockParticleColors.GetColor(blockType);
            Vector3 bias = faceNormal.HasValue ? Vector3.Normalize(faceNormal.Value) * 1.2f : Vector3.Zero;
            int shardCount = rng.Next(10, 17);

            for (int i = 0; i < shardCount; i++)
            {
                if (!TryAllocate(out int slot))
                {
                    break;
                }

                float angle = (float)rng.NextDouble() * MathF.PI * 2f;
                float speed = 1.8f + (float)rng.NextDouble() * 2.4f;
                var velocity = new Vector3(MathF.Cos(angle) * speed, 1.2f + (float)rng.NextDouble() * 1.8f, MathF.Sin(angle) * speed) + bias;

                _particles[slot] = new Particle
                {
                    Position = center + RandomOffset(rng, 0.25f),
                    Velocity = velocity,
                    Lifetime = 0.35f + (float)rng.NextDouble() * 0.25f,
                    MaxLifetime = 0.6f,
                    Size = 0.05f + (float)rng.NextDouble() * 0.06f,
                    Rotation = (float)rng.NextDouble() * MathF.PI * 2f,
                    AngularVelocity = ((float)rng.NextDouble() * 2f - 1f) * 8f,
                    Gravity = 9f,
                    Drag = 0.04f,
                    Color = BlockParticleColors.Vary(baseColor, rng),
                    BlockType = blockType,
                    UseTexture = true,
                    Kind = ParticleKind.BlockShard,
                    Active = true
                };
                _particles[slot].MaxLifetime = _particles[slot].Lifetime;
            }

            for (int i = 0; i < 5; i++)
            {
                if (!TryAllocate(out int slot))
                {
                    break;
                }

                float angle = (float)rng.NextDouble() * MathF.PI * 2f;
                float speed = 0.6f + (float)rng.NextDouble() * 0.8f;
                _particles[slot] = new Particle
                {
                    Position = center,
                    Velocity = new Vector3(MathF.Cos(angle) * speed, 0.4f + (float)rng.NextDouble() * 0.5f, MathF.Sin(angle) * speed) + bias * 0.5f,
                    Lifetime = 0.25f + (float)rng.NextDouble() * 0.15f,
                    MaxLifetime = 0.4f,
                    Size = 0.03f + (float)rng.NextDouble() * 0.02f,
                    Gravity = 4f,
                    Drag = 0.08f,
                    Color = BlockParticleColors.Vary(baseColor, rng, 0.08f),
                    Kind = ParticleKind.Dust,
                    Active = true
                };
                _particles[slot].MaxLifetime = _particles[slot].Lifetime;
            }
        }

        public void SpawnBlockPlace(Vector3 center, BlockType blockType)
        {
            var rng = SeedRng(center, blockType, 53);
            var baseColor = BlockParticleColors.GetColor(blockType);

            for (int i = 0; i < 6; i++)
            {
                if (!TryAllocate(out int slot))
                {
                    break;
                }

                float angle = (float)rng.NextDouble() * MathF.PI * 2f;
                float speed = 0.5f + (float)rng.NextDouble() * 0.7f;
                float life = 0.18f + (float)rng.NextDouble() * 0.12f;
                _particles[slot] = new Particle
                {
                    Position = center + RandomOffset(rng, 0.15f),
                    Velocity = new Vector3(MathF.Cos(angle) * speed, 0.6f + (float)rng.NextDouble() * 0.6f, MathF.Sin(angle) * speed),
                    Lifetime = life,
                    MaxLifetime = life,
                    Size = 0.035f + (float)rng.NextDouble() * 0.025f,
                    Gravity = 5f,
                    Drag = 0.1f,
                    Color = BlockParticleColors.Vary(baseColor, rng, 0.06f),
                    BlockType = blockType,
                    UseTexture = i < 2,
                    Kind = i < 2 ? ParticleKind.BlockShard : ParticleKind.Dust,
                    Active = true
                };
            }
        }

        public void SpawnMiningDust(Vector3 center, BlockType blockType, Vector3 faceNormal, float intensity = 1f)
        {
            var rng = SeedRng(center, blockType, 17);
            var baseColor = BlockParticleColors.GetColor(blockType);
            Vector3 n = Vector3.Normalize(faceNormal);
            int count = intensity > 0.5f ? 2 : 1;

            for (int i = 0; i < count; i++)
            {
                if (!TryAllocate(out int slot))
                {
                    return;
                }

                float angle = (float)rng.NextDouble() * MathF.PI * 2f;
                float speed = 0.3f + (float)rng.NextDouble() * 0.4f;
                var tangent = MathF.Abs(n.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
                var bitangent = Vector3.Normalize(Vector3.Cross(n, tangent));
                tangent = Vector3.Normalize(Vector3.Cross(bitangent, n));
                var offset = tangent * MathF.Cos(angle) * 0.3f + bitangent * MathF.Sin(angle) * 0.3f;

                _particles[slot] = new Particle
                {
                    Position = center + n * 0.45f + offset,
                    Velocity = n * 0.5f + offset * 0.8f,
                    Lifetime = 0.2f,
                    MaxLifetime = 0.2f,
                    Size = 0.025f,
                    Gravity = 2f,
                    Drag = 0.12f,
                    Color = BlockParticleColors.Vary(baseColor, rng, 0.05f),
                    Kind = ParticleKind.Dust,
                    Active = true
                };
            }
        }

        public void SpawnHint(Vector3 center)
        {
            var rng = SeedRng(center, BlockType.Air, 91);
            for (int i = 0; i < 5; i++)
            {
                if (!TryAllocate(out int slot))
                {
                    break;
                }

                float angle = (float)rng.NextDouble() * MathF.PI * 2f;
                float radius = 0.3f + (float)rng.NextDouble() * 0.2f;
                float life = 0.5f + (float)rng.NextDouble() * 0.2f;
                _particles[slot] = new Particle
                {
                    Position = center + new Vector3(MathF.Cos(angle) * radius, (float)rng.NextDouble() * 0.4f, MathF.Sin(angle) * radius),
                    Velocity = new Vector3(MathF.Cos(angle) * 0.3f, 0.8f + (float)rng.NextDouble() * 0.4f, MathF.Sin(angle) * 0.3f),
                    Lifetime = life,
                    MaxLifetime = life,
                    Size = 0.04f,
                    Rotation = angle,
                    AngularVelocity = 2f,
                    Gravity = 0.5f,
                    Drag = 0.06f,
                    Color = new Vector3(0.35f, 0.85f, 1.0f),
                    Kind = ParticleKind.Hint,
                    Active = true
                };
            }
        }

        public void SpawnToolSparks(Vector3 center, ItemStack tool, Vector3 faceNormal)
        {
            if (!tool.IsTool())
            {
                return;
            }

            var rng = SeedRng(center, BlockType.Air, (int)tool.ToolId + 31);
            var tier = ToolRegistry.Get(tool.ToolId).Tier;
            Vector3 sparkColor = tier switch
            {
                ToolTier.Gold => new Vector3(1.0f, 0.85f, 0.25f),
                ToolTier.Iron => new Vector3(0.85f, 0.88f, 0.95f),
                ToolTier.Stone => new Vector3(0.75f, 0.75f, 0.78f),
                _ => new Vector3(0.92f, 0.72f, 0.38f)
            };

            Vector3 n = faceNormal == Vector3.Zero ? Vector3.UnitY : Vector3.Normalize(faceNormal);
            int count = rng.Next(3, 6);

            for (int i = 0; i < count; i++)
            {
                if (!TryAllocate(out int slot))
                {
                    break;
                }

                float angle = (float)rng.NextDouble() * MathF.PI * 2f;
                float speed = 1.5f + (float)rng.NextDouble() * 2f;
                var tangent = MathF.Abs(n.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
                var bitangent = Vector3.Normalize(Vector3.Cross(n, tangent));
                tangent = Vector3.Normalize(Vector3.Cross(bitangent, n));
                var dir = tangent * MathF.Cos(angle) + bitangent * MathF.Sin(angle);

                float life = 0.12f + (float)rng.NextDouble() * 0.1f;
                _particles[slot] = new Particle
                {
                    Position = center + n * 0.4f,
                    Velocity = dir * speed + n * (0.5f + (float)rng.NextDouble()),
                    Lifetime = life,
                    MaxLifetime = life,
                    Size = 0.025f + (float)rng.NextDouble() * 0.015f,
                    Gravity = 6f,
                    Drag = 0.02f,
                    Color = BlockParticleColors.Vary(sparkColor, rng, 0.06f),
                    Kind = ParticleKind.Spark,
                    Active = true
                };
            }
        }

        public void SpawnMeleeHit(Vector3 center, AnimalType animalType, Vector3 hitDirection)
        {
            var rng = SeedRng(center, BlockType.Air, (int)animalType + 47);
            var stats = AnimalStats.For(animalType);
            var bodyColor = new Vector3(stats.BodyColor.R / 255f, stats.BodyColor.G / 255f, stats.BodyColor.B / 255f);
            Vector3 dir = hitDirection == Vector3.Zero ? Vector3.UnitZ : Vector3.Normalize(hitDirection);

            for (int i = 0; i < 8; i++)
            {
                if (!TryAllocate(out int slot))
                {
                    break;
                }

                float angle = (float)rng.NextDouble() * MathF.PI * 2f;
                float speed = 1.2f + (float)rng.NextDouble() * 1.5f;
                var spread = new Vector3(MathF.Cos(angle), 0.4f + (float)rng.NextDouble(), MathF.Sin(angle));
                float life = 0.15f + (float)rng.NextDouble() * 0.1f;

                _particles[slot] = new Particle
                {
                    Position = center + new Vector3(0f, 0.3f, 0f),
                    Velocity = dir * speed * 0.5f + spread * speed * 0.5f,
                    Lifetime = life,
                    MaxLifetime = life,
                    Size = 0.04f + (float)rng.NextDouble() * 0.02f,
                    Gravity = 8f,
                    Drag = 0.05f,
                    Color = i % 3 == 0
                        ? new Vector3(0.95f, 0.2f, 0.15f)
                        : BlockParticleColors.Vary(bodyColor, rng, 0.1f),
                    Kind = ParticleKind.Hit,
                    Active = true
                };
            }
        }

        public void SpawnAnimalDeath(Vector3 center, AnimalType animalType)
        {
            var rng = SeedRng(center, BlockType.Air, (int)animalType + 103);
            var stats = AnimalStats.For(animalType);
            var bodyColor = new Vector3(stats.BodyColor.R / 255f, stats.BodyColor.G / 255f, stats.BodyColor.B / 255f);
            var accentColor = stats.HasAccent
                ? new Vector3(stats.AccentColor.R / 255f, stats.AccentColor.G / 255f, stats.AccentColor.B / 255f)
                : bodyColor;

            for (int i = 0; i < 16; i++)
            {
                if (!TryAllocate(out int slot))
                {
                    break;
                }

                float angle = (float)rng.NextDouble() * MathF.PI * 2f;
                float speed = 1.5f + (float)rng.NextDouble() * 2.5f;
                float life = 0.35f + (float)rng.NextDouble() * 0.35f;
                var color = i % 4 == 0 ? accentColor : BlockParticleColors.Vary(bodyColor, rng);

                _particles[slot] = new Particle
                {
                    Position = center + new Vector3(0f, 0.4f, 0f) + RandomOffset(rng, 0.3f),
                    Velocity = new Vector3(MathF.Cos(angle) * speed, 1.5f + (float)rng.NextDouble() * 2f, MathF.Sin(angle) * speed),
                    Lifetime = life,
                    MaxLifetime = life,
                    Size = 0.05f + (float)rng.NextDouble() * 0.05f,
                    Rotation = (float)rng.NextDouble() * MathF.PI * 2f,
                    AngularVelocity = ((float)rng.NextDouble() * 2f - 1f) * 6f,
                    Gravity = 10f,
                    Drag = 0.03f,
                    Color = color,
                    Kind = ParticleKind.Death,
                    Active = true
                };
            }
        }

        public void SpawnToolBreak(Vector3 center)
        {
            var rng = SeedRng(center, BlockType.Stone, 317);
            var shardColor = new Vector3(0.55f, 0.52f, 0.48f);
            int count = 8;

            for (int i = 0; i < count; i++)
            {
                if (!TryAllocate(out int slot))
                {
                    break;
                }

                float angle = (float)rng.NextDouble() * MathF.PI * 2f;
                float speed = 1f + (float)rng.NextDouble() * 1.5f;
                float life = 0.2f + (float)rng.NextDouble() * 0.15f;

                _particles[slot] = new Particle
                {
                    Position = center,
                    Velocity = new Vector3(MathF.Cos(angle) * speed, 0.8f + (float)rng.NextDouble(), MathF.Sin(angle) * speed),
                    Lifetime = life,
                    MaxLifetime = life,
                    Size = 0.04f + (float)rng.NextDouble() * 0.03f,
                    Rotation = (float)rng.NextDouble() * MathF.PI * 2f,
                    AngularVelocity = ((float)rng.NextDouble() * 2f - 1f) * 8f,
                    Gravity = 12f,
                    Drag = 0.05f,
                    Color = BlockParticleColors.Vary(shardColor, rng, 0.1f),
                    Kind = ParticleKind.Spark,
                    Active = true
                };
            }
        }

        public void SpawnFallDust(Vector3 feetPosition, float fallDistance)
        {
            if (fallDistance < 1.5f)
            {
                return;
            }

            var rng = SeedRng(feetPosition, BlockType.Dirt, 211);
            int count = Math.Clamp((int)(fallDistance * 1.5f), 3, 12);
            var dustColor = new Vector3(0.55f, 0.42f, 0.28f);

            for (int i = 0; i < count; i++)
            {
                if (!TryAllocate(out int slot))
                {
                    break;
                }

                float angle = (float)rng.NextDouble() * MathF.PI * 2f;
                float speed = 0.8f + (float)rng.NextDouble() * 1.2f;
                float life = 0.25f + (float)rng.NextDouble() * 0.2f;

                _particles[slot] = new Particle
                {
                    Position = feetPosition + new Vector3(MathF.Cos(angle) * 0.2f, 0.05f, MathF.Sin(angle) * 0.2f),
                    Velocity = new Vector3(MathF.Cos(angle) * speed, 0.3f + (float)rng.NextDouble() * 0.4f, MathF.Sin(angle) * speed),
                    Lifetime = life,
                    MaxLifetime = life,
                    Size = 0.04f + (float)rng.NextDouble() * 0.03f,
                    Gravity = 5f,
                    Drag = 0.1f,
                    Color = BlockParticleColors.Vary(dustColor, rng, 0.08f),
                    Kind = ParticleKind.Dust,
                    Active = true
                };
            }
        }

        public void SpawnWaterSplash(Vector3 center, float intensity)
        {
            var rng = SeedRng(center, BlockType.Water, 911);
            int dropletCount = Math.Clamp((int)(8 + intensity * 6f), 8, 20);

            for (int i = 0; i < dropletCount; i++)
            {
                if (!TryAllocate(out int slot))
                {
                    break;
                }

                ref var p = ref _particles[slot];
                p.Active = true;
                p.Kind = ParticleKind.Dust;
                p.BlockType = BlockType.Water;
                p.UseTexture = false;
                p.Position = center + RandomOffset(rng, 0.35f);
                p.Velocity = new Vector3(
                    ((float)rng.NextDouble() * 2f - 1f) * 2.5f * intensity,
                    ((float)rng.NextDouble() * 0.8f + 0.4f) * intensity,
                    ((float)rng.NextDouble() * 2f - 1f) * 2.5f * intensity);
                p.Lifetime = 0.35f + (float)rng.NextDouble() * 0.25f;
                p.MaxLifetime = p.Lifetime;
                p.Size = 0.08f + (float)rng.NextDouble() * 0.06f;
                p.Rotation = (float)rng.NextDouble() * MathF.PI * 2f;
                p.AngularVelocity = ((float)rng.NextDouble() * 2f - 1f) * 8f;
                p.Gravity = 12f;
                p.Drag = 0.12f;
                p.Color = new Vector3(0.35f, 0.72f, 0.98f);
            }
        }

        public void SpawnUnderwaterBubble(Vector3 cameraPosition)
        {
            var rng = SeedRng(cameraPosition, BlockType.Water, 1201);
            if (!TryAllocate(out int slot))
            {
                return;
            }

            ref var p = ref _particles[slot];
            p.Active = true;
            p.Kind = ParticleKind.Bubble;
            p.BlockType = BlockType.Water;
            p.UseTexture = false;
            p.Position = cameraPosition + new Vector3(
                ((float)rng.NextDouble() * 2f - 1f) * 0.35f,
                ((float)rng.NextDouble() * 2f - 1f) * 0.25f,
                ((float)rng.NextDouble() * 2f - 1f) * 0.35f);
            p.Velocity = new Vector3(
                ((float)rng.NextDouble() * 2f - 1f) * 0.15f,
                0.55f + (float)rng.NextDouble() * 0.35f,
                ((float)rng.NextDouble() * 2f - 1f) * 0.15f);
            p.Lifetime = 0.8f + (float)rng.NextDouble() * 0.6f;
            p.MaxLifetime = p.Lifetime;
            p.Size = 0.04f + (float)rng.NextDouble() * 0.03f;
            p.Rotation = 0f;
            p.AngularVelocity = 0f;
            p.Gravity = -0.35f;
            p.Drag = 0.02f;
            p.Color = new Vector3(0.72f, 0.92f, 1.0f);
        }

        private bool TryAllocate(out int slot)
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].Active)
                {
                    slot = i;
                    return true;
                }
            }

            slot = -1;
            return false;
        }

        private static Random SeedRng(Vector3 center, BlockType blockType, int salt)
        {
            return new Random((int)(center.X * 73 + center.Y * 37 + center.Z * 19 + (int)blockType * 13 + salt));
        }

        private static Vector3 RandomOffset(Random rng, float radius)
        {
            return new Vector3(
                ((float)rng.NextDouble() * 2f - 1f) * radius,
                ((float)rng.NextDouble() * 2f - 1f) * radius,
                ((float)rng.NextDouble() * 2f - 1f) * radius);
        }

        private float _ambientSpawnCooldown;
        private static readonly Random _ambientRng = new();

        public void UpdateAmbient(float deltaTime, Vector3 playerPos, VoxelWorld world, float timeOfDay, WeatherSystem weather)
        {
            if (weather.RainIntensity > 0.01f)
            {
                int spawnCount = (int)(weather.RainIntensity * 80f * deltaTime);
                if (spawnCount < 1 && _ambientRng.NextDouble() < (weather.RainIntensity * 0.8f))
                {
                    spawnCount = 1;
                }
                for (int i = 0; i < spawnCount; i++)
                {
                    SpawnWeatherParticle(playerPos, world, weather.RainIntensity);
                }
            }

            _ambientSpawnCooldown -= deltaTime;
            if (_ambientSpawnCooldown > 0f)
            {
                return;
            }

            _ambientSpawnCooldown = 0.1f;

            var biome = world.SampleBiome((int)playerPos.X, (int)playerPos.Z).Primary;
            bool isNight = DayNightCycle.IsNight(timeOfDay);
            bool isDay = DayNightCycle.IsBroadDaytime(timeOfDay);

            int px = (int)MathF.Floor(playerPos.X);
            int py = (int)MathF.Floor(playerPos.Y);
            int pz = (int)MathF.Floor(playerPos.Z);

            bool isOutdoors = true;
            int airCount = 0;
            for (int dy = 1; dy <= 12; dy++)
            {
                var block = world.GetBlock(px, py + dy, pz);
                if (block.IsTransparent() && !block.IsFluid())
                {
                    airCount++;
                }
            }
            isOutdoors = airCount >= 8;

            if (!isOutdoors)
            {
                if (_ambientRng.NextDouble() < 0.15f)
                {
                    SpawnDustMote(playerPos);
                }
                return;
            }

            if (isNight && (biome == BiomeType.Swamp || biome == BiomeType.Forest || biome == BiomeType.Plains))
            {
                float chance = biome == BiomeType.Swamp ? 0.85f : 0.25f;
                if (_ambientRng.NextDouble() < chance)
                {
                    SpawnFirefly(playerPos);
                }
            }

            if (isDay && (biome == BiomeType.Plains || biome == BiomeType.Forest || biome == BiomeType.Desert))
            {
                float chance = biome == BiomeType.Desert ? 0.45f : 0.2f;
                if (_ambientRng.NextDouble() < chance)
                {
                    SpawnDustMote(playerPos);
                }
            }

            if (_ambientRng.NextDouble() < 0.5f)
            {
                int minX = px - 8;
                int maxX = px + 8;
                int minY = Math.Max(0, py - 4);
                int maxY = Math.Min(190, py + 8);
                int minZ = pz - 8;
                int maxZ = pz + 8;

                int rx = _ambientRng.Next(minX, maxX + 1);
                int ry = _ambientRng.Next(minY, maxY + 1);
                int rz = _ambientRng.Next(minZ, maxZ + 1);

                var blockType = world.GetBlock(rx, ry, rz);
                if (blockType == BlockType.OakLeaves || blockType == BlockType.BirchLeaves ||
                    blockType == BlockType.PineLeaves || blockType == BlockType.WillowLeaves ||
                    blockType == BlockType.PalmLeaves)
                {
                    if (world.GetBlock(rx, ry - 1, rz).IsTransparent())
                    {
                        SpawnFallingLeaf(new Vector3(rx + (float)_ambientRng.NextDouble(), ry - 0.1f, rz + (float)_ambientRng.NextDouble()), blockType);
                    }
                }
            }
        }

        private void SpawnWeatherParticle(Vector3 playerPos, VoxelWorld world, float intensity)
        {
            if (!TryAllocate(out int slot)) return;

            float rx = playerPos.X + ((float)_ambientRng.NextDouble() * 2f - 1f) * 20f;
            float rz = playerPos.Z + ((float)_ambientRng.NextDouble() * 2f - 1f) * 20f;
            float ry = playerPos.Y + 14f;

            var biome = world.SampleBiome((int)rx, (int)rz).Primary;
            bool isSnowy = biome == BiomeType.SnowyPeaks || biome == BiomeType.Mountains;

            if (isSnowy)
            {
                _particles[slot] = new Particle
                {
                    Position = new Vector3(rx, ry, rz),
                    Velocity = new Vector3(
                        ((float)_ambientRng.NextDouble() * 2f - 1f) * 0.8f,
                        -1.8f - (float)_ambientRng.NextDouble() * 1.2f,
                        ((float)_ambientRng.NextDouble() * 2f - 1f) * 0.8f
                    ),
                    Lifetime = 8.0f,
                    MaxLifetime = 8.0f,
                    Size = 0.03f + (float)_ambientRng.NextDouble() * 0.03f,
                    Rotation = (float)_ambientRng.NextDouble() * MathF.PI * 2f,
                    AngularVelocity = ((float)_ambientRng.NextDouble() * 2f - 1f) * 2f,
                    Gravity = 0f,
                    Drag = 0.01f,
                    Color = new Vector3(0.95f, 0.95f, 0.98f),
                    Kind = ParticleKind.SnowFlake,
                    Active = true
                };
            }
            else
            {
                _particles[slot] = new Particle
                {
                    Position = new Vector3(rx, ry, rz),
                    Velocity = new Vector3(
                        ((float)_ambientRng.NextDouble() * 2f - 1f) * 0.2f,
                        -16.0f - (float)_ambientRng.NextDouble() * 4.0f,
                        ((float)_ambientRng.NextDouble() * 2f - 1f) * 0.2f
                    ),
                    Lifetime = 1.2f,
                    MaxLifetime = 1.2f,
                    Size = 0.04f + (float)_ambientRng.NextDouble() * 0.02f,
                    Rotation = 0f,
                    AngularVelocity = 0f,
                    Gravity = 0f,
                    Drag = 0f,
                    Color = new Vector3(0.5f, 0.58f, 0.72f),
                    Kind = ParticleKind.RainDrop,
                    Active = true
                };
            }
        }

        private void SpawnDustMote(Vector3 center)
        {
            if (!TryAllocate(out int slot)) return;

            var offset = new Vector3(
                ((float)_ambientRng.NextDouble() * 2f - 1f) * 16f,
                ((float)_ambientRng.NextDouble() * 2f - 1f) * 6f,
                ((float)_ambientRng.NextDouble() * 2f - 1f) * 16f
            );

            _particles[slot] = new Particle
            {
                Position = center + offset,
                Velocity = new Vector3(
                    ((float)_ambientRng.NextDouble() * 2f - 1f) * 0.12f,
                    -0.08f - (float)_ambientRng.NextDouble() * 0.12f,
                    ((float)_ambientRng.NextDouble() * 2f - 1f) * 0.12f
                ),
                Lifetime = 3.5f + (float)_ambientRng.NextDouble() * 2.0f,
                Size = 0.015f + (float)_ambientRng.NextDouble() * 0.015f,
                Rotation = (float)_ambientRng.NextDouble() * MathF.PI * 2f,
                AngularVelocity = ((float)_ambientRng.NextDouble() * 2f - 1f) * 0.5f,
                Gravity = 0f,
                Drag = 0.02f,
                Color = new Vector3(0.95f, 0.92f, 0.85f),
                Kind = ParticleKind.DustMote,
                Active = true
            };
            _particles[slot].MaxLifetime = _particles[slot].Lifetime;
        }

        private void SpawnFirefly(Vector3 center)
        {
            if (!TryAllocate(out int slot)) return;

            var offset = new Vector3(
                ((float)_ambientRng.NextDouble() * 2f - 1f) * 14f,
                ((float)_ambientRng.NextDouble() * 2f - 1f) * 4f,
                ((float)_ambientRng.NextDouble() * 2f - 1f) * 14f
            );

            _particles[slot] = new Particle
            {
                Position = center + offset,
                Velocity = new Vector3(
                    ((float)_ambientRng.NextDouble() * 2f - 1f) * 0.25f,
                    ((float)_ambientRng.NextDouble() * 2f - 1f) * 0.15f,
                    ((float)_ambientRng.NextDouble() * 2f - 1f) * 0.25f
                ),
                Lifetime = 4f + (float)_ambientRng.NextDouble() * 3.0f,
                Size = 0.02f + (float)_ambientRng.NextDouble() * 0.015f,
                Rotation = 0f,
                AngularVelocity = 0f,
                Gravity = -0.01f,
                Drag = 0.01f,
                Color = new Vector3(0.55f, 0.95f, 0.12f),
                Kind = ParticleKind.Firefly,
                Active = true
            };
            _particles[slot].MaxLifetime = _particles[slot].Lifetime;
        }

        private void SpawnFallingLeaf(Vector3 spawnPos, BlockType leafBlockType)
        {
            if (!TryAllocate(out int slot)) return;

            Vector3 color = leafBlockType switch
            {
                BlockType.PineLeaves => new Vector3(0.12f, 0.35f, 0.18f),
                BlockType.BirchLeaves => new Vector3(0.48f, 0.68f, 0.18f),
                BlockType.WillowLeaves => new Vector3(0.35f, 0.48f, 0.24f),
                BlockType.PalmLeaves => new Vector3(0.24f, 0.75f, 0.28f),
                _ => new Vector3(0.24f, 0.52f, 0.15f)
            };

            color.X += ((float)_ambientRng.NextDouble() * 2f - 1f) * 0.04f;
            color.Y += ((float)_ambientRng.NextDouble() * 2f - 1f) * 0.04f;
            color.Z += ((float)_ambientRng.NextDouble() * 2f - 1f) * 0.04f;
            color = Vector3.Clamp(color, Vector3.Zero, Vector3.One);

            _particles[slot] = new Particle
            {
                Position = spawnPos,
                Velocity = new Vector3(
                    ((float)_ambientRng.NextDouble() * 2f - 1f) * 0.45f,
                    -0.6f - (float)_ambientRng.NextDouble() * 0.4f,
                    ((float)_ambientRng.NextDouble() * 2f - 1f) * 0.45f
                ),
                Lifetime = 3f + (float)_ambientRng.NextDouble() * 2f,
                Size = 0.04f + (float)_ambientRng.NextDouble() * 0.02f,
                Rotation = (float)_ambientRng.NextDouble() * MathF.PI * 2f,
                AngularVelocity = ((float)_ambientRng.NextDouble() * 2f - 1f) * 4f,
                Gravity = 1.2f,
                Drag = 0.08f,
                Color = color,
                Kind = ParticleKind.FallingLeaf,
                Active = true
            };
            _particles[slot].MaxLifetime = _particles[slot].Lifetime;
        }
    }
}

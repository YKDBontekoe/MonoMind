using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.Core;
using Autonocraft.World;

namespace Autonocraft.Entities
{
    public class AnimalManager
    {
        public const int MaxAnimalsGlobal = 80;
        public const int MaxAnimalsPerChunk = 4;

        private readonly List<Animal> _animals = new List<Animal>();
        private readonly List<Animal> _rangeScratch = new();
        private readonly HashSet<(int cx, int cz)> _populatedChunks = new HashSet<(int cx, int cz)>();
        private readonly int _worldSeed;
        private int _nextSpawnSeed = 5000;

        public AnimalManager(int worldSeed)
        {
            _worldSeed = worldSeed;
        }

        public IReadOnlyList<Animal> Animals => _animals;

        public int Count => _animals.Count;

        public void Update(float deltaTime, VoxelWorld world)
        {
            for (int i = _animals.Count - 1; i >= 0; i--)
            {
                var animal = _animals[i];
                var prevX = animal.Position.X;
                var prevZ = animal.Position.Z;

                animal.Update(deltaTime, world);
                animal.UpdateAnimation(deltaTime);

                if (animal.ReadyForRemoval)
                {
                    _animals.RemoveAt(i);
                    continue;
                }

                if (animal.IsDying)
                {
                    continue;
                }

                if (!animal.IsGrounded && animal.Velocity.X == 0f && animal.Velocity.Z == 0f &&
                    (MathF.Abs(animal.Position.X - prevX) < 0.001f && MathF.Abs(animal.Position.Z - prevZ) < 0.001f) &&
                    animal.WanderDirection != Vector3.Zero)
                {
                    animal.OnBlocked();
                }
            }
        }

        public void OnChunksLoaded(IEnumerable<(int cx, int cz)> newChunks, VoxelWorld world)
        {
            foreach (var coord in newChunks)
            {
                TryPopulateChunk(coord.cx, coord.cz, world);
            }
        }

        public void OnChunksUnloaded(IEnumerable<(int cx, int cz)> unloadedChunks)
        {
            var unloaded = new HashSet<(int cx, int cz)>(unloadedChunks);
            for (int i = _animals.Count - 1; i >= 0; i--)
            {
                var animal = _animals[i];
                VoxelWorld.GetChunkCoords(
                    (int)MathF.Floor(animal.Position.X),
                    (int)MathF.Floor(animal.Position.Z),
                    out int cx,
                    out int cz,
                    out _,
                    out _);

                if (unloaded.Contains((cx, cz)))
                {
                    _animals.RemoveAt(i);
                }
            }

            foreach (var coord in unloaded)
            {
                _populatedChunks.Remove(coord);
            }
        }

        public void PopulateAroundSpawn(VoxelWorld world, int spawnX, int spawnZ, int chunkRadius)
        {
            VoxelWorld.GetChunkCoords(spawnX, spawnZ, out int centerCx, out int centerCz, out _, out _);

            for (int dx = -chunkRadius; dx <= chunkRadius; dx++)
            {
                for (int dz = -chunkRadius; dz <= chunkRadius; dz++)
                {
                    TryPopulateChunk(centerCx + dx, centerCz + dz, world);
                }
            }
        }

        public void TryPopulateChunk(int cx, int cz, VoxelWorld world)
        {
            if (_populatedChunks.Contains((cx, cz)))
            {
                return;
            }

            _populatedChunks.Add((cx, cz));

            if (_animals.Count >= MaxAnimalsGlobal)
            {
                return;
            }

            var rng = new Random(HashChunk(_worldSeed, cx, cz));
            int spawnAttempts = 6 + rng.Next(5);
            int spawned = 0;

            for (int attempt = 0; attempt < spawnAttempts && spawned < MaxAnimalsPerChunk && _animals.Count < MaxAnimalsGlobal; attempt++)
            {
                int wx = cx * Chunk.Width + rng.Next(Chunk.Width);
                int wz = cz * Chunk.Depth + rng.Next(Chunk.Depth);

                if (!TryFindSpawnPosition(world, wx, wz, out Vector3 spawnPos, out AnimalType type, rng))
                {
                    continue;
                }

                if (IsOccupied(spawnPos, 1.2f))
                {
                    continue;
                }

                _animals.Add(new Animal(type, spawnPos, _nextSpawnSeed++));
                spawned++;
            }
        }

        public Animal? SpawnAt(AnimalType type, Vector3 position, VoxelWorld world)
        {
            if (_animals.Count >= MaxAnimalsGlobal)
            {
                return null;
            }

            var stats = AnimalStats.For(type);
            if (!EntityCollision.IsSpaceClearAt(world, position, stats.Width, stats.Height))
            {
                return null;
            }

            var animal = new Animal(type, position, _nextSpawnSeed++);
            _animals.Add(animal);
            return animal;
        }

        public int SpawnInFrontOfPlayer(Player player, VoxelWorld world, AnimalType type, int count)
        {
            count = Math.Clamp(count, 1, 8);
            int spawned = 0;

            float yawRad = player.Yaw * (MathF.PI / 180f);
            var forward = new Vector3(MathF.Cos(yawRad), 0f, MathF.Sin(yawRad));

            for (int i = 0; i < count; i++)
            {
                var offset = forward * (2f + i * 1.2f);
                var candidate = player.Position + offset;
                candidate.Y = world.GetHighestSolidY((int)MathF.Floor(candidate.X), (int)MathF.Floor(candidate.Z)) + 1f;

                if (SpawnAt(type, candidate, world) != null)
                {
                    spawned++;
                }
            }

            return spawned;
        }

        public List<Animal> GetAnimalsInRange(Vector3 center, float radius)
        {
            float radiusSq = radius * radius;
            _rangeScratch.Clear();

            foreach (var animal in _animals)
            {
                if (Vector3.DistanceSquared(animal.Position, center) <= radiusSq)
                {
                    _rangeScratch.Add(animal);
                }
            }

            return _rangeScratch;
        }

        public (Animal? animal, float distance) RaycastTarget(Vector3 origin, Vector3 direction, float maxDistance)
        {
            var candidates = GetAnimalsInRange(origin, maxDistance);
            return EntityRaycast.Raycast(candidates, origin, direction, maxDistance);
        }

        public bool KillAnimal(Animal animal)
        {
            return _animals.Remove(animal);
        }

        public bool RemoveAnimal(Animal animal)
        {
            return KillAnimal(animal);
        }

        public string GetCountSummary()
        {
            int sheep = 0, pig = 0, chicken = 0, wolf = 0, cow = 0, bear = 0, fox = 0, deer = 0;
            foreach (var animal in _animals)
            {
                switch (animal.Type)
                {
                    case AnimalType.Sheep: sheep++; break;
                    case AnimalType.Pig: pig++; break;
                    case AnimalType.Chicken: chicken++; break;
                    case AnimalType.Wolf: wolf++; break;
                    case AnimalType.Cow: cow++; break;
                    case AnimalType.Bear: bear++; break;
                    case AnimalType.Fox: fox++; break;
                    case AnimalType.Deer: deer++; break;
                }
            }

            return $"Animals: {Count} total (Sheep: {sheep}, Pig: {pig}, Chicken: {chicken}, Wolf: {wolf}, Cow: {cow}, Bear: {bear}, Fox: {fox}, Deer: {deer})";
        }

        private static int HashChunk(int seed, int cx, int cz)
        {
            unchecked
            {
                int hash = seed;
                hash = hash * 31 + cx;
                hash = hash * 31 + cz;
                return hash;
            }
        }

        private static bool TryFindSpawnPosition(
            VoxelWorld world,
            int wx,
            int wz,
            out Vector3 position,
            out AnimalType type,
            Random rng)
        {
            position = Vector3.Zero;
            type = AnimalType.Sheep;

            int surfaceY = world.GetHighestSolidY(wx, wz);
            if (surfaceY < 0)
            {
                return false;
            }

            if (world.GetBlock(wx, surfaceY, wz) != BlockType.Grass)
            {
                return false;
            }

            for (int y = surfaceY + 1; y <= surfaceY + 3; y++)
            {
                if (world.GetBlock(wx, y, wz) != BlockType.Air)
                {
                    return false;
                }
            }

            var biome = world.SampleBiome(wx, wz).Primary;
            int typeRoll = rng.Next(100);

            if (biome == BiomeType.Forest)
            {
                type = typeRoll switch
                {
                    < 20 => AnimalType.Deer,
                    < 40 => AnimalType.Bear,
                    < 60 => AnimalType.Fox,
                    < 75 => AnimalType.Pig,
                    < 90 => AnimalType.Chicken,
                    _ => AnimalType.Cow
                };
            }
            else if (biome == BiomeType.Plains)
            {
                type = typeRoll switch
                {
                    < 30 => AnimalType.Sheep,
                    < 55 => AnimalType.Cow,
                    < 75 => AnimalType.Deer,
                    < 90 => AnimalType.Pig,
                    _ => AnimalType.Fox
                };
            }
            else if (biome == BiomeType.Swamp)
            {
                type = typeRoll switch
                {
                    < 40 => AnimalType.Pig,
                    < 70 => AnimalType.Chicken,
                    _ => AnimalType.Bear
                };
            }
            else if (biome == BiomeType.Mountains)
            {
                type = typeRoll switch
                {
                    < 40 => AnimalType.Sheep,
                    < 70 => AnimalType.Cow,
                    _ => AnimalType.Bear
                };
            }
            else if (biome == BiomeType.SnowyPeaks)
            {
                type = typeRoll switch
                {
                    < 50 => AnimalType.Fox,
                    _ => AnimalType.Sheep
                };
            }
            else
            {
                type = typeRoll switch
                {
                    < 35 => AnimalType.Sheep,
                    < 70 => AnimalType.Pig,
                    _ => AnimalType.Chicken
                };
            }

            var stats = AnimalStats.For(type);
            position = new Vector3(wx + 0.5f, surfaceY + 1f, wz + 0.5f);

            return EntityCollision.IsSpaceClearAt(world, position, stats.Width, stats.Height);
        }

        private bool IsOccupied(Vector3 position, float minDistance)
        {
            float minDistSq = minDistance * minDistance;
            foreach (var animal in _animals)
            {
                if (Vector3.DistanceSquared(animal.Position, position) < minDistSq)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

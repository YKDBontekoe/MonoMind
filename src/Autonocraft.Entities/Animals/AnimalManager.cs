using System;
using System.Collections.Generic;
using System.Numerics;
using Autonocraft.World;

namespace Autonocraft.Entities
{
    public class AnimalManager
    {
        public const int MaxAnimalsGlobal = 80;
        public const int MaxAnimalsPerChunk = 4;

        private static readonly AnimalType[] CountSummaryOrder =
        {
            AnimalType.Sheep,
            AnimalType.Pig,
            AnimalType.Chicken,
            AnimalType.Wolf,
            AnimalType.Cow,
            AnimalType.Bear,
            AnimalType.Fox,
            AnimalType.Deer
        };

        private static readonly AnimalSpawnChoice[] ForestSpawns =
        {
            new(20, AnimalType.Deer),
            new(40, AnimalType.Bear),
            new(60, AnimalType.Fox),
            new(75, AnimalType.Pig),
            new(90, AnimalType.Chicken),
            new(100, AnimalType.Cow)
        };

        private static readonly AnimalSpawnChoice[] PlainsSpawns =
        {
            new(30, AnimalType.Sheep),
            new(55, AnimalType.Cow),
            new(75, AnimalType.Deer),
            new(90, AnimalType.Pig),
            new(100, AnimalType.Fox)
        };

        private static readonly AnimalSpawnChoice[] SwampSpawns =
        {
            new(40, AnimalType.Pig),
            new(70, AnimalType.Chicken),
            new(100, AnimalType.Bear)
        };

        private static readonly AnimalSpawnChoice[] MountainSpawns =
        {
            new(40, AnimalType.Sheep),
            new(70, AnimalType.Cow),
            new(100, AnimalType.Bear)
        };

        private static readonly AnimalSpawnChoice[] SnowyPeakSpawns =
        {
            new(50, AnimalType.Fox),
            new(100, AnimalType.Sheep)
        };

        private static readonly AnimalSpawnChoice[] BadlandsSpawns =
        {
            new(40, AnimalType.Fox),
            new(70, AnimalType.Pig),
            new(100, AnimalType.Chicken)
        };

        private static readonly AnimalSpawnChoice[] MangroveSpawns =
        {
            new(45, AnimalType.Pig),
            new(75, AnimalType.Chicken),
            new(100, AnimalType.Bear)
        };

        private static readonly AnimalSpawnChoice[] MushroomForestSpawns =
        {
            new(35, AnimalType.Fox),
            new(65, AnimalType.Bear),
            new(100, AnimalType.Deer)
        };

        private static readonly AnimalSpawnChoice[] BorealTaigaSpawns =
        {
            new(40, AnimalType.Deer),
            new(70, AnimalType.Fox),
            new(100, AnimalType.Sheep)
        };

        private static readonly AnimalSpawnChoice[] VolcanicSpawns =
        {
            new(100, AnimalType.Fox)
        };

        private static readonly AnimalSpawnChoice[] DefaultSpawns =
        {
            new(35, AnimalType.Sheep),
            new(70, AnimalType.Pig),
            new(100, AnimalType.Chicken)
        };

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

        public int SpawnInFrontOfPlayer(Vector3 playerPosition, float playerYaw, VoxelWorld world, AnimalType type, int count)
        {
            count = Math.Clamp(count, 1, 8);
            int spawned = 0;

            float yawRad = playerYaw * (MathF.PI / 180f);
            var forward = new Vector3(MathF.Cos(yawRad), 0f, MathF.Sin(yawRad));

            for (int i = 0; i < count; i++)
            {
                var offset = forward * (2f + i * 1.2f);
                var candidate = playerPosition + offset;
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
            // Return a new list snapshot each call — _rangeScratch was shared and caused
            // "Collection was modified" crashes when render thread iterated while update thread
            // was spawning/despawning animals.
            var result = new List<Animal>();
            foreach (var animal in _animals)
            {
                if (Vector3.DistanceSquared(animal.Position, center) <= radiusSq)
                {
                    result.Add(animal);
                }
            }

            return result;
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
            var counts = new Dictionary<AnimalType, int>();
            foreach (var animal in _animals)
            {
                counts.TryGetValue(animal.Type, out int count);
                counts[animal.Type] = count + 1;
            }

            var parts = new List<string>(CountSummaryOrder.Length);
            foreach (var type in CountSummaryOrder)
            {
                counts.TryGetValue(type, out int count);
                parts.Add($"{type}: {count}");
            }

            return $"Animals: {Count} total ({string.Join(", ", parts)})";
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

            var surface = world.GetBlock(wx, surfaceY, wz);
            var biome = world.SampleBiome(wx, wz).Primary;
            if (!IsValidSpawnSurface(surface, biome))
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
            type = PickSpawnType(biome, rng.Next(100));

            var stats = AnimalStats.For(type);
            position = new Vector3(wx + 0.5f, surfaceY + 1f, wz + 0.5f);

            return EntityCollision.IsSpaceClearAt(world, position, stats.Width, stats.Height);
        }

        private static bool IsValidSpawnSurface(BlockType surface, BiomeType biome)
        {
            return biome switch
            {
                BiomeType.Badlands => surface is BlockType.RedSand or BlockType.Sand,
                BiomeType.Mangrove => surface is BlockType.Mud or BlockType.Grass,
                BiomeType.Volcanic => surface is BlockType.Basalt or BlockType.Stone,
                _ => surface == BlockType.Grass
            };
        }

        private static AnimalType PickSpawnType(BiomeType biome, int roll)
        {
            var table = GetSpawnTable(biome);
            foreach (var choice in table)
            {
                if (roll < choice.UpperExclusive)
                {
                    return choice.Type;
                }
            }

            return table[^1].Type;
        }

        private static AnimalSpawnChoice[] GetSpawnTable(BiomeType biome) => biome switch
        {
            BiomeType.Forest or BiomeType.Jungle => ForestSpawns,
            BiomeType.Plains => PlainsSpawns,
            BiomeType.Swamp => SwampSpawns,
            BiomeType.Mountains => MountainSpawns,
            BiomeType.SnowyPeaks => SnowyPeakSpawns,
            BiomeType.Badlands => BadlandsSpawns,
            BiomeType.Mangrove => MangroveSpawns,
            BiomeType.MushroomForest => MushroomForestSpawns,
            BiomeType.BorealTaiga => BorealTaigaSpawns,
            BiomeType.Volcanic => VolcanicSpawns,
            _ => DefaultSpawns
        };

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

        private readonly struct AnimalSpawnChoice
        {
            public AnimalSpawnChoice(int upperExclusive, AnimalType type)
            {
                UpperExclusive = upperExclusive;
                Type = type;
            }

            public int UpperExclusive { get; }
            public AnimalType Type { get; }
        }
    }
}

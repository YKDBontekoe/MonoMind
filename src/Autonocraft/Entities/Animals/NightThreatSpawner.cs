using System;
using System.Numerics;
using Autonocraft.Core;
using Autonocraft.Domain.Core;
using Autonocraft.World;

namespace Autonocraft.Entities
{
    public sealed class NightThreatSpawner
    {
        private float _spawnCooldown;

        public void Update(
            float deltaTime,
            float timeOfDay,
            bool spawnWarmupActive,
            VoxelWorld world,
            Player player,
            AnimalManager animals)
        {
            if (player.CreativeMode || !player.IsAlive || spawnWarmupActive)
            {
                return;
            }

            if (!DayNightCycle.IsNight(timeOfDay))
            {
                DespawnWolves(animals);
                return;
            }

            if (!IsPlayerOutside(world, player.Position))
            {
                return;
            }

            int wolfCount = CountWolvesNear(player.Position, animals, SurvivalConstants.NightWolfSpawnRadius);
            if (wolfCount >= SurvivalConstants.MaxNightWolves)
            {
                return;
            }

            _spawnCooldown -= deltaTime;
            if (_spawnCooldown > 0f)
            {
                return;
            }

            _spawnCooldown = 8f;
            if (TrySpawnWolfNear(player, world, animals))
            {
                player.ShowToast?.Invoke("A wolf prowls nearby — craft a sword or seek shelter!");
            }
        }

        private static void DespawnWolves(AnimalManager animals)
        {
            for (int i = animals.Animals.Count - 1; i >= 0; i--)
            {
                var animal = animals.Animals[i];
                if (animal.Type == AnimalType.Wolf && animal.IsNightThreat)
                {
                    animals.RemoveAnimal(animal);
                }
            }
        }

        private static int CountWolvesNear(Vector3 position, AnimalManager animals, float radius)
        {
            int count = 0;
            float radiusSq = radius * radius;
            foreach (var animal in animals.Animals)
            {
                if (animal.Type == AnimalType.Wolf &&
                    animal.IsAlive &&
                    Vector3.DistanceSquared(animal.Position, position) <= radiusSq)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool TrySpawnWolfNear(Player player, VoxelWorld world, AnimalManager animals)
        {
            var rng = new Random(world.Seed ^ (int)(player.Position.X * 17) ^ (int)(player.Position.Z * 31));
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float angle = rng.NextSingle() * MathF.PI * 2f;
                float dist = 14f + rng.NextSingle() * 12f;
                float x = player.Position.X + MathF.Cos(angle) * dist;
                float z = player.Position.Z + MathF.Sin(angle) * dist;
                int wx = (int)MathF.Floor(x);
                int wz = (int)MathF.Floor(z);
                int surfaceY = world.GetHighestSolidY(wx, wz);
                if (surfaceY < 0)
                {
                    continue;
                }

                var pos = new Vector3(wx + 0.5f, surfaceY + 1f, wz + 0.5f);
                var wolf = animals.SpawnAt(AnimalType.Wolf, pos, world);
                if (wolf != null)
                {
                    wolf.IsNightThreat = true;
                    return true;
                }
            }

            return false;
        }

        public static bool IsPlayerOutside(VoxelWorld world, Vector3 position)
        {
            int x = (int)MathF.Floor(position.X);
            int z = (int)MathF.Floor(position.Z);
            int headY = (int)MathF.Floor(position.Y + Player.EyeHeight);
            for (int dy = 0; dy <= 2; dy++)
            {
                if (world.GetBlock(x, headY + dy, z).IsSolidForSpawn())
                {
                    return false;
                }
            }

            return true;
        }
    }
}

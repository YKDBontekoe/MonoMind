using System;
using System.Numerics;
using Autonocraft.Core;
using Autonocraft.Domain.Core;
using Autonocraft.Village;
using Autonocraft.World;

namespace Autonocraft.Entities
{
    public sealed class NightThreatSpawner
    {
        private readonly Random _rng = new();
        private float _spawnTimer;
        private bool _firstNightToastShown;

        public Action<string>? ShowToast { get; set; }

        public void Update(
            float deltaTime,
            bool spawnWarmupComplete,
            float timeOfDay,
            Player player,
            AnimalManager animals,
            VillageManager villages,
            VoxelWorld world)
        {
            bool isNight = DayNightCycle.IsNight(timeOfDay);
            if (!spawnWarmupComplete || !isNight || player.FlyingMode)
            {
                if (!isNight)
                {
                    _spawnTimer = 0f;
                }

                return;
            }

            if (!_firstNightToastShown)
            {
                _firstNightToastShown = true;
                ShowToast?.Invoke("Night falls — stay near the Town Heart or build shelter.");
            }

            _spawnTimer -= deltaTime;
            if (_spawnTimer > 0f)
            {
                return;
            }

            _spawnTimer = SurvivalConstants.WolfSpawnIntervalSeconds;
            int toSpawn = 1 + _rng.Next(2);
            for (int i = 0; i < toSpawn; i++)
            {
                TrySpawnWolfNear(player, animals, villages, world);
            }
        }

        private void TrySpawnWolfNear(Player player, AnimalManager animals, VillageManager villages, VoxelWorld world)
        {
            if (animals.CountHostile() >= SurvivalConstants.MaxHostileMobsGlobal)
            {
                return;
            }

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = (float)(_rng.NextDouble() * MathF.PI * 2f);
                float dist = SurvivalConstants.WolfSpawnMinDistance +
                             (float)_rng.NextDouble() * (SurvivalConstants.WolfSpawnMaxDistance - SurvivalConstants.WolfSpawnMinDistance);
                float x = player.Position.X + MathF.Cos(angle) * dist;
                float z = player.Position.Z + MathF.Sin(angle) * dist;
                var candidate = new Vector3(x, player.Position.Y, z);

                if (IsSafeZone(candidate, villages, world))
                {
                    continue;
                }

                int wx = (int)MathF.Floor(x);
                int wz = (int)MathF.Floor(z);
                int surfaceY = world.GetHighestSolidY(wx, wz);
                if (surfaceY < 0)
                {
                    continue;
                }

                candidate.Y = surfaceY + 1f;
                if (animals.SpawnHostile(AnimalType.Wolf, candidate, world) != null)
                {
                    return;
                }
            }
        }

        private static bool IsSafeZone(Vector3 position, VillageManager villages, VoxelWorld world)
        {
            foreach (var village in villages.Villages)
            {
                if (village.Contains(position))
                {
                    return true;
                }
            }

            int px = (int)MathF.Floor(position.X);
            int py = (int)MathF.Floor(position.Y);
            int pz = (int)MathF.Floor(position.Z);
            int radius = (int)MathF.Ceiling(SurvivalConstants.SafeZoneBenchRadius);
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -2; dy <= 3; dy++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        int x = px + dx;
                        int y = py + dy;
                        int z = pz + dz;
                        var block = world.GetBlock(x, y, z);
                        if (block == BlockType.StationBench)
                        {
                            float distSq = (x + 0.5f - position.X) * (x + 0.5f - position.X) +
                                           (z + 0.5f - position.Z) * (z + 0.5f - position.Z);
                            if (distSq <= SurvivalConstants.SafeZoneBenchRadius * SurvivalConstants.SafeZoneBenchRadius)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}

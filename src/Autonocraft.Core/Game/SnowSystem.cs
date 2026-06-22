using System;
using System.Collections.Generic;
using Autonocraft.Domain.World;
using Autonocraft.Engine;
using Autonocraft.World;

namespace Autonocraft.Core
{
    public sealed class SnowSystem
    {
        private readonly Random _rng = new();
        private float _timer;

        public void Update(float deltaTime, VoxelWorld world, WeatherSystem weather, float timeOfDay)
        {
            _timer += deltaTime;
            if (_timer < 0.25f) return;

            _timer = 0f;

            var activeChunks = world.ActiveChunks;
            if (activeChunks.Count == 0) return;

            // Determine how many columns to update per chunk
            int columnsPerChunk = 1;
            if (weather.CurrentWeather == WeatherKind.Storm || weather.TargetWeather == WeatherKind.Storm)
            {
                // Storms should feel faster, but not at the cost of scanning every column in every active chunk.
                columnsPerChunk = 4;
            }
            else if (weather.CurrentWeather == WeatherKind.Thunderstorm || weather.TargetWeather == WeatherKind.Thunderstorm)
            {
                columnsPerChunk = 3;
            }

            foreach (var chunk in activeChunks)
            {
                for (int i = 0; i < columnsPerChunk; i++)
                {
                    int lx = _rng.Next(16);
                    int lz = _rng.Next(16);
                    int wx = chunk.ChunkX * 16 + lx;
                    int wz = chunk.ChunkZ * 16 + lz;

                    UpdateColumn(world, weather, wx, wz, timeOfDay);
                }
            }
        }

        internal void UpdateColumn(VoxelWorld world, WeatherSystem weather, int wx, int wz, float timeOfDay)
        {
            int y = GetWeatherSurfaceY(world, wx, wz);
            if (y < 0)
            {
                return;
            }

            BlockType topBlock = world.GetBlock(wx, y, wz);

            // Calculate temperature at this position
            float temp = GetTemperature(world, weather, wx, y, wz, timeOfDay);

            bool isSnowing = temp < 0.0f && weather.RainIntensity > 0.01f;
            bool isMelting = temp > 0.0f;

            if (isSnowing)
            {
                // Try snow build-up
                if (topBlock.IsPassable() && !topBlock.IsWater() && topBlock != BlockType.Air && !topBlock.IsSnowLayer() && topBlock != BlockType.SnowSlab && topBlock != BlockType.Snow)
                {
                    // Replace the flora block with SnowLayer1
                    if (y > 0)
                    {
                        BlockType below = world.GetBlock(wx, y - 1, wz);
                        if (CanSnowAccumulateOn(below))
                        {
                            // Random chance of accumulation
                            double rate = weather.CurrentWeather == WeatherKind.Storm ? 0.85 :
                                          (weather.CurrentWeather == WeatherKind.Thunderstorm ? 0.60 : 0.20);
                            if (_rng.NextDouble() < rate)
                            {
                                world.SetBlock(wx, y, wz, BlockType.SnowLayer1, null);
                            }
                        }
                    }
                }
                else if (CanSnowAccumulateOn(topBlock) || topBlock.IsSnowLayer() || topBlock == BlockType.SnowSlab || topBlock == BlockType.Snow)
                {
                    // Accumulate on top or increment layer
                    double rate = weather.CurrentWeather == WeatherKind.Storm ? 0.85 :
                                  (weather.CurrentWeather == WeatherKind.Thunderstorm ? 0.60 : 0.20);
                    if (_rng.NextDouble() < rate)
                    {
                        AccumulateSnow(world, wx, y, wz, topBlock);
                    }
                }
            }
            else if (isMelting)
            {
                // Try melting snow
                if (topBlock.IsSnowLayer() || topBlock == BlockType.SnowSlab || topBlock == BlockType.Snow)
                {
                    // Random chance of melting
                    double meltRate = weather.CurrentWeather == WeatherKind.Rain ||
                                      weather.CurrentWeather == WeatherKind.Thunderstorm ||
                                      weather.CurrentWeather == WeatherKind.Storm ? 0.50 : 0.15;
                    if (_rng.NextDouble() < meltRate)
                    {
                        MeltSnow(world, wx, y, wz, topBlock);
                    }
                }
            }
        }

        private void AccumulateSnow(VoxelWorld world, int wx, int wy, int wz, BlockType topBlock)
        {
            if (topBlock == BlockType.Snow)
            {
                if (wy + 1 < Chunk.Height)
                {
                    world.SetBlock(wx, wy + 1, wz, BlockType.SnowLayer1, null);
                }
            }
            else if (topBlock.IsSnowLayer() || topBlock == BlockType.SnowSlab)
            {
                int currentLevel = topBlock.GetSnowLevel();
                int nextLevel = currentLevel + 1;
                BlockType nextType = BlockTypeExtensions.GetSnowBlockTypeForLevel(nextLevel);
                world.SetBlock(wx, wy, wz, nextType, null);
            }
            else
            {
                if (wy + 1 < Chunk.Height)
                {
                    world.SetBlock(wx, wy + 1, wz, BlockType.SnowLayer1, null);
                }
            }
        }

        private static int GetWeatherSurfaceY(VoxelWorld world, int wx, int wz)
        {
            // Start from cached terrain height, then walk through transient weather/flora
            // blocks that may sit above it but may not be represented in mesh height.
            int y = world.GetHighestMeshY(wx, wz);
            if (y < 0)
            {
                return y;
            }

            while (y + 1 < Chunk.Height)
            {
                BlockType above = world.GetBlock(wx, y + 1, wz);
                if (above == BlockType.Air || above.IsWater())
                {
                    break;
                }

                if (above.IsPassable() || above.IsSnowLayer() || above == BlockType.SnowSlab || above == BlockType.Snow)
                {
                    y++;
                    continue;
                }

                break;
            }

            return y;
        }

        private void MeltSnow(VoxelWorld world, int wx, int wy, int wz, BlockType topBlock)
        {
            if (topBlock == BlockType.Snow)
            {
                world.SetBlock(wx, wy, wz, BlockType.SnowLayer9, null);
            }
            else if (topBlock.IsSnowLayer() || topBlock == BlockType.SnowSlab)
            {
                int currentLevel = topBlock.GetSnowLevel();
                if (currentLevel <= 1)
                {
                    world.SetBlock(wx, wy, wz, BlockType.Air, null);
                }
                else
                {
                    int prevLevel = currentLevel - 1;
                    BlockType prevType = BlockTypeExtensions.GetSnowBlockTypeForLevel(prevLevel);
                    world.SetBlock(wx, wy, wz, prevType, null);
                }
            }
        }

        public float GetTemperature(VoxelWorld world, WeatherSystem weather, int wx, int wy, int wz, float timeOfDay)
        {
            // Base temperature from biome
            float baseTemp = world.SampleBiome(wx, wz).Temperature;

            // Altitude factor: drops as height increases above sea level
            float heightDiff = wy - WorldConstants.SeaLevel;
            float altitudeFactor = -0.005f * heightDiff;

            // Diurnal factor: colder at night (midnight is 0.0 or 1.0, noon is 0.5)
            float diurnalFactor = 0.15f * MathF.Cos((timeOfDay - 0.5f) * 2f * MathF.PI);

            // Weather factor: rain or storm drops temperature
            float weatherFactor = -0.2f * weather.RainIntensity;

            // Global offset
            float offset = weather.TemperatureOffset;

            return baseTemp + altitudeFactor + diurnalFactor + weatherFactor + offset;
        }

        private static bool CanSnowAccumulateOn(BlockType type)
        {
            if (type == BlockType.Air || type == BlockType.Water || type == BlockType.Lava || type.IsSnowLayer() || type == BlockType.SnowSlab || type == BlockType.Snow)
            {
                return false;
            }

            return !type.IsPassable() || type.IsLeaf();
        }
    }
}

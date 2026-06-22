using System;
using System.Collections.Generic;
using Autonocraft.Domain.World;
using Autonocraft.World;
using Autonocraft.World.Generation.Trees;

namespace Autonocraft.Core
{
    public sealed class SaplingGrowthSystem
    {
        private readonly Dictionary<(int x, int y, int z), float> _trackedSaplings = new();
        private readonly Random _rng = new();

        public void BindWorld(VoxelWorld world)
        {
            world.BlockChanged += OnBlockChanged;
            world.ChunksLoaded += coords => OnChunksLoaded(coords, world);
        }

        private void OnBlockChanged(int x, int y, int z, BlockType previous, BlockType current)
        {
            if (current.IsSapling())
            {
                if (!_trackedSaplings.ContainsKey((x, y, z)))
                {
                    // 15 to 30 seconds growth time
                    _trackedSaplings[(x, y, z)] = 15f + (float)_rng.NextDouble() * 15f;
                }
            }
            else if (previous.IsSapling())
            {
                _trackedSaplings.Remove((x, y, z));
            }
        }

        private void OnChunksLoaded(IReadOnlyList<(int cx, int cz)> chunks, VoxelWorld world)
        {
            foreach (var (cx, cz) in chunks)
            {
                int startX = cx * 16;
                int startZ = cz * 16;
                for (int z = 0; z < 16; z++)
                {
                    int wz = startZ + z;
                    for (int x = 0; x < 16; x++)
                    {
                        int wx = startX + x;
                        int surfaceY = world.GetHighestSolidY(wx, wz);
                        
                        // Check a small window around surfaceY
                        for (int wy = Math.Max(0, surfaceY - 1); wy <= Math.Min(Chunk.Height - 1, surfaceY + 2); wy++)
                        {
                            BlockType type = world.GetBlock(wx, wy, wz);
                            if (type.IsSapling())
                            {
                                var pos = (wx, wy, wz);
                                if (!_trackedSaplings.ContainsKey(pos))
                                {
                                    // 15 to 30 seconds growth time
                                    _trackedSaplings[pos] = 15f + (float)_rng.NextDouble() * 15f;
                                }
                            }
                        }
                    }
                }
            }
        }

        public void Update(float deltaTime, VoxelWorld world)
        {
            var readyToGrow = new List<(int x, int y, int z, BlockType type)>();
            var keys = new List<(int x, int y, int z)>(_trackedSaplings.Keys);

            foreach (var pos in keys)
            {
                if (_trackedSaplings.TryGetValue(pos, out float time))
                {
                    time -= deltaTime;
                    if (time <= 0f)
                    {
                        BlockType type = world.GetBlock(pos.x, pos.y, pos.z);
                        if (type.IsSapling())
                        {
                            readyToGrow.Add((pos.x, pos.y, pos.z, type));
                        }
                        _trackedSaplings.Remove(pos);
                    }
                    else
                    {
                        _trackedSaplings[pos] = time;
                    }
                }
            }

            foreach (var (x, y, z, type) in readyToGrow)
            {
                GrowTree(world, x, y, z, type);
            }
        }

        private void GrowTree(VoxelWorld world, int x, int y, int z, BlockType saplingType)
        {
            // Grow tree using TreeShapeGenerator
            TreeSpecies species = GetSpeciesForSapling(saplingType);
            
            // Set the sapling itself to Air first
            world.SetBlock(x, y, z, BlockType.Air, null);

            var voxels = TreeShapeGenerator.Generate(species, x, z, y - 1, world.Seed, 0.5f, 0.38f, 1f);
            foreach (var voxel in voxels)
            {
                int vx = x + voxel.Dx;
                int vy = y - 1 + voxel.Dy;
                int vz = z + voxel.Dz;

                BlockType current = world.GetBlock(vx, vy, vz);
                if (current == BlockType.Air || current.IsPassable() || current.IsLeaf())
                {
                    world.SetBlock(vx, vy, vz, voxel.Type, null);
                }
            }

            world.SetBlock(x, y, z, species.Log, null);
            Console.WriteLine($"[Sapling] Grew {saplingType} at ({x}, {y}, {z}) into a tree.");
        }

        private static TreeSpecies GetSpeciesForSapling(BlockType type)
        {
            return type switch
            {
                BlockType.OakSapling => TreeSpecies.Oak(),
                BlockType.BirchSapling => TreeSpecies.Birch(),
                BlockType.PineSapling => TreeSpecies.Pine(),
                BlockType.WillowSapling => TreeSpecies.Willow(),
                BlockType.PalmSapling => TreeSpecies.Palm(),
                BlockType.CherrySapling => TreeSpecies.Cherry(),
                BlockType.MahoganySapling => TreeSpecies.Mahogany(),
                BlockType.MapleSapling => TreeSpecies.Maple(),
                _ => TreeSpecies.Oak()
            };
        }
    }
}

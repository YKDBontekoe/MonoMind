using System;
using System.Collections.Generic;
using Autonocraft.World;

namespace Autonocraft.Tests.Integration;

internal static class WorldGenTestHelpers
{
    public static TerrainColumn? FindPreviewColumn(
        WorldGenerator generator,
        Func<TerrainColumn, bool> predicate,
        int radius,
        int step)
    {
        var match = FindPreviewCoord(generator, predicate, radius, step);
        return match?.column;
    }

    public static (int x, int z, TerrainColumn column)? FindPreviewCoord(
        WorldGenerator generator,
        Func<TerrainColumn, bool> predicate,
        int radius,
        int step)
    {
        int chunkRadius = (radius + Chunk.Width - 1) / Chunk.Width;
        int chunkStep = Math.Max(1, step / Chunk.Width);

        // Check center (0,0) first
        {
            var columns = generator.PreviewChunkColumns(0, 0);
            for (int lz = 0; lz < Chunk.Depth; lz += step)
            {
                for (int lx = 0; lx < Chunk.Width; lx += step)
                {
                    int wx = lx;
                    int wz = lz;
                    if (Math.Abs(wx) <= radius && Math.Abs(wz) <= radius)
                    {
                        var column = columns[lx, lz];
                        if (predicate(column))
                        {
                            return (wx, wz, column);
                        }
                    }
                }
            }
        }

        // Loop expanding outward concentric squares of radius d
        for (int d = chunkStep; d <= chunkRadius; d += chunkStep)
        {
            // We want to generate coordinates (chunkX, chunkZ) on the perimeter of square of radius d
            // Perimeter consists of 4 segments:
            // 1. chunkZ = -d, chunkX from -d to d
            // 2. chunkZ = d, chunkX from -d to d
            // 3. chunkX = -d, chunkZ from -d + chunkStep to d - chunkStep
            // 4. chunkX = d, chunkZ from -d + chunkStep to d - chunkStep

            // Segments 1 and 2
            for (int chunkX = -d; chunkX <= d; chunkX += chunkStep)
            {
                int[] zs = { -d, d };
                foreach (int chunkZ in zs)
                {
                    var columns = generator.PreviewChunkColumns(chunkX, chunkZ);
                    for (int lz = 0; lz < Chunk.Depth; lz += step)
                    {
                        for (int lx = 0; lx < Chunk.Width; lx += step)
                        {
                            int wx = chunkX * Chunk.Width + lx;
                            int wz = chunkZ * Chunk.Depth + lz;
                            if (Math.Abs(wx) > radius || Math.Abs(wz) > radius)
                            {
                                continue;
                            }

                            var column = columns[lx, lz];
                            if (predicate(column))
                            {
                                return (wx, wz, column);
                            }
                        }
                    }
                }
            }

            // Segments 3 and 4
            for (int chunkZ = -d + chunkStep; chunkZ <= d - chunkStep; chunkZ += chunkStep)
            {
                int[] xs = { -d, d };
                foreach (int chunkX in xs)
                {
                    var columns = generator.PreviewChunkColumns(chunkX, chunkZ);
                    for (int lz = 0; lz < Chunk.Depth; lz += step)
                    {
                        for (int lx = 0; lx < Chunk.Width; lx += step)
                        {
                            int wx = chunkX * Chunk.Width + lx;
                            int wz = chunkZ * Chunk.Depth + lz;
                            if (Math.Abs(wx) > radius || Math.Abs(wz) > radius)
                            {
                                continue;
                            }

                            var column = columns[lx, lz];
                            if (predicate(column))
                            {
                                return (wx, wz, column);
                            }
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Cheaply finds up to <paramref name="maxResults"/> world-space coordinates where
    /// <paramref name="biomePredicate"/> is satisfied, using only <see cref="WorldGenerator.SampleBiome"/>
    /// (noise only — no terrain columns generated). Scan step is in world units.
    /// </summary>
    public static List<(int x, int z)> FindBiomeCoordsFast(
        WorldGenerator generator,
        Func<BiomeSample, bool> biomePredicate,
        int radius = 1536,
        int step = 32,
        int maxResults = 20)
    {
        var results = new List<(int x, int z)>();
        for (int wz = -radius; wz <= radius && results.Count < maxResults; wz += step)
        {
            for (int wx = -radius; wx <= radius && results.Count < maxResults; wx += step)
            {
                var biome = generator.SampleBiome(wx, wz);
                if (biomePredicate(biome))
                {
                    results.Add((wx, wz));
                    // Skip ahead to avoid many candidates in the same patch
                    wx += step * 4;
                }
            }
        }
        return results;
    }

    public static bool TryFindGrassUpperStep(WorldGenerator generator, out int wx, out int wz)
    {
        const int radius = 192;
        int chunkRadius = (radius + Chunk.Width - 1) / Chunk.Width;

        for (int chunkZ = -chunkRadius; chunkZ <= chunkRadius; chunkZ++)
        {
            for (int chunkX = -chunkRadius; chunkX <= chunkRadius; chunkX++)
            {
                var columns = generator.PreviewChunkColumns(chunkX, chunkZ);
                for (int lz = 0; lz < Chunk.Depth; lz++)
                {
                    for (int lx = 0; lx < Chunk.Width; lx++)
                    {
                        int x = chunkX * Chunk.Width + lx;
                        int z = chunkZ * Chunk.Depth + lz;
                        if (Math.Abs(x) > radius || Math.Abs(z) > radius)
                        {
                            continue;
                        }

                        var center = columns[lx, lz];
                        if (center.SurfaceHeight != 65
                            || center.SurfaceBlock != BlockType.Grass
                            || center.SurfaceBlock.IsSlab())
                        {
                            continue;
                        }

                        if (lx + 1 >= Chunk.Width)
                        {
                            continue;
                        }

                        var east = columns[lx + 1, lz];
                        if (east.SurfaceHeight == 66 && east.SurfaceBlock == BlockType.GrassSlab)
                        {
                            wx = x;
                            wz = z;
                            return true;
                        }
                    }
                }
            }
        }

        wx = 0;
        wz = 0;
        return false;
    }

    public static TerrainColumn GetPreviewColumn(
        WorldGenerator generator,
        Dictionary<(int cx, int cz), TerrainColumn[,]> cache,
        int wx,
        int wz)
    {
        VoxelWorld.GetChunkCoords(wx, wz, out int cx, out int cz, out int lx, out int lz);
        if (!cache.TryGetValue((cx, cz), out var columns))
        {
            columns = generator.PreviewChunkColumns(cx, cz);
            cache[(cx, cz)] = columns;
        }

        return columns[lx, lz];
    }

    public static int SurfaceBandMinY(Chunk chunk, int lx, int lz, int bandBelow = 28, int bandAbove = 8)
    {
        int top = chunk.GetCachedHighestSolidY(lx, lz);
        if (top < 1)
        {
            return 1;
        }

        return Math.Max(1, top - bandBelow);
    }

    public static int SurfaceBandMaxY(Chunk chunk, int lx, int lz, int bandAbove = 8)
    {
        int top = chunk.GetCachedHighestSolidY(lx, lz);
        if (top < 1)
        {
            return 1;
        }

        return Math.Min(Chunk.Height - 1, top + bandAbove);
    }
}

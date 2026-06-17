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

        for (int chunkZ = -chunkRadius; chunkZ <= chunkRadius; chunkZ += chunkStep)
        {
            for (int chunkX = -chunkRadius; chunkX <= chunkRadius; chunkX += chunkStep)
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

        return null;
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

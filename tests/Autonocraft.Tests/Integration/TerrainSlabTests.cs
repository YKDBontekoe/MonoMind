using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Autonocraft.Domain.World;
using Autonocraft.World;

namespace Autonocraft.Tests.Integration;

public static class TerrainSlabTests
{
    public static void RunTerrainSlabPlacementRules()
    {
        Console.Write("Running Terrain Slab Placement Rules Test... ");

        int[] seeds = { 1337, 42, 9001, 424242, 777 };
        var violations = new List<string>();

        foreach (int seed in seeds)
        {
            ScanSeed(seed, violations);
        }

        if (violations.Count > 0)
        {
            var report = new StringBuilder();
            report.AppendLine($"Found {violations.Count} terrain slab violation(s):");
            int limit = Math.Min(violations.Count, 12);
            for (int i = 0; i < limit; i++)
            {
                report.AppendLine("  - " + violations[i]);
            }

            if (violations.Count > limit)
            {
                report.AppendLine($"  ... and {violations.Count - limit} more");
            }

            throw new Exception(report.ToString().TrimEnd());
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunTerrainSlabUnitRules()
    {
        Console.Write("Running Terrain Slab Unit Rules Test... ");

        var heights = new float[5, 5];
        FillFlat(heights, 3, 3, 64.0f);
        heights[3, 3] = 65.0f;

        var plains = MakeDraft(BiomeType.Plains, BlockType.Grass, ridgeWeight: 0f);
        var mountains = MakeDraft(BiomeType.Mountains, BlockType.Stone, ridgeWeight: 0.75f);
        var forestHigh = MakeDraft(BiomeType.Forest, BlockType.Grass, ridgeWeight: 0f);

        if (!TerrainSlabRules.TryGetPlacement(plains, 65.0f, heights, 3, 3, BlockType.Grass, out int y, out var slab) || slab != BlockType.GrassSlab || y != 65)
        {
            throw new Exception("Expected grass slab on the upper cell of a gentle lowland step.");
        }

        if (TerrainSlabRules.TryGetPlacement(plains, 64.0f, heights, 2, 3, BlockType.Grass, out _, out _))
        {
            throw new Exception("Lower step cells must use full blocks, not terrain slabs.");
        }

        if (TerrainSlabRules.TryGetPlacement(mountains, 64.0f, heights, 2, 3, BlockType.Stone, out _, out _))
        {
            throw new Exception("Mountain stone surface must never receive terrain slabs.");
        }

        if (TerrainSlabRules.TryGetPlacement(forestHigh, 72.0f, heights, 2, 3, BlockType.Grass, out _, out _))
        {
            throw new Exception("High-elevation forest must not receive terrain slabs.");
        }

        FillFlat(heights, 3, 3, 64.0f);
        heights[3, 3] = 66.0f;
        if (TerrainSlabRules.TryGetPlacement(plains, 64.0f, heights, 2, 3, BlockType.Grass, out _, out _))
        {
            throw new Exception("Two-block cliff steps must not receive terrain slabs.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunTerrainSlabUpperStepRegression()
    {
        Console.Write("Running Terrain Slab Upper Step Regression... ");

        var generator = new WorldGenerator(103);
        var center = generator.PreviewColumn(-20, 1);
        if (center.SurfaceBlock.IsSlab())
        {
            throw new Exception($"Lower step cell (-20,1) must be a full block, got {center.SurfaceBlock} at y={center.SurfaceHeight}.");
        }

        if (center.SurfaceHeight != 65 || center.SurfaceBlock != BlockType.Grass)
        {
            throw new Exception($"Expected grass full block at y=65, got {center.SurfaceBlock} at y={center.SurfaceHeight}.");
        }

        var east = generator.PreviewColumn(-19, 1);
        if (!east.SurfaceBlock.IsSlab() || east.SurfaceHeight != 66)
        {
            throw new Exception($"Expected grass slab on upper step at (-19,1) y=66, got {east.SurfaceBlock} at y={east.SurfaceHeight}.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunGeneratedWorldHasNoMountainSlabs()
    {
        Console.Write("Running Generated World Slab Scan Test... ");

        using var world = new VoxelWorld(1337, WorldGenParams.ForType(WorldType.Default));
        world.UpdateChunksAround(null, new Vector3(0f, 80f, 0f), 6);

        var generator = new WorldGenerator(1337, WorldGenParams.ForType(WorldType.Default));
        var violations = new List<string>();

        foreach (var chunk in world.GetActiveChunks())
        {
            int baseX = chunk.ChunkX * Chunk.Width;
            int baseZ = chunk.ChunkZ * Chunk.Depth;

            for (int lz = 0; lz < Chunk.Depth; lz++)
            {
                for (int lx = 0; lx < Chunk.Width; lx++)
                {
                    int wx = baseX + lx;
                    int wz = baseZ + lz;
                    int surfaceY = chunk.GetCachedHighestSolidY(lx, lz);
                    if (surfaceY < 0)
                    {
                        continue;
                    }

                    var surface = chunk.GetBlock(lx, surfaceY, lz);
                    if (!surface.IsSlab())
                    {
                        continue;
                    }

                    var column = generator.PreviewColumn(wx, wz);
                    var biome = generator.SampleBiome(wx, wz).Primary;

                    if (surface is BlockType.StoneSlab or BlockType.SnowSlab)
                    {
                        violations.Add($"({wx}, {surfaceY}, {wz}) {surface} in {biome}");
                        continue;
                    }

                    if (biome is BiomeType.Mountains or BiomeType.SnowyPeaks)
                    {
                        violations.Add($"({wx}, {surfaceY}, {wz}) {surface} in {biome}");
                        continue;
                    }

                    if (surfaceY > WorldConstants.SeaLevel + 9)
                    {
                        violations.Add($"({wx}, {surfaceY}, {wz}) {surface} above lowland cap in {biome}");
                    }
                }
            }
        }

        if (violations.Count > 0)
        {
            throw new Exception($"Generated world slab violations: {string.Join("; ", violations.GetRange(0, Math.Min(8, violations.Count)))}");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    private static void ScanSeed(int seed, List<string> violations)
    {
        var generator = new WorldGenerator(seed, WorldGenParams.ForType(WorldType.Default));

        for (int chunkX = -6; chunkX <= 6; chunkX++)
        {
            for (int chunkZ = -6; chunkZ <= 6; chunkZ++)
            {
                var columns = generator.PreviewChunkColumns(chunkX, chunkZ);
                for (int lz = 0; lz < Chunk.Depth; lz++)
                {
                    for (int lx = 0; lx < Chunk.Width; lx++)
                    {
                        var column = columns[lx, lz];
                        if (!column.SurfaceBlock.IsSlab())
                        {
                            continue;
                        }

                        int wx = chunkX * Chunk.Width + lx;
                        int wz = chunkZ * Chunk.Depth + lz;
                        ValidateSlabColumn(seed, wx, column.SurfaceHeight, wz, column, violations);
                    }
                }
            }
        }
    }

    private static void ValidateSlabColumn(int seed, int wx, int y, int wz, TerrainColumn column, List<string> violations)
    {
        var surface = column.SurfaceBlock;
        var biome = column.Biome.Primary;

        if (surface is BlockType.StoneSlab or BlockType.SnowSlab)
        {
            violations.Add($"seed={seed} ({wx},{y},{wz}) forbidden {surface}");
            return;
        }

        if (biome is BiomeType.Mountains or BiomeType.SnowyPeaks or BiomeType.Ocean or BiomeType.Swamp)
        {
            violations.Add($"seed={seed} ({wx},{y},{wz}) {surface} in {biome}");
            return;
        }

        if (y > WorldConstants.SeaLevel + 9)
        {
            violations.Add($"seed={seed} ({wx},{y},{wz}) {surface} too high for lowland slabs");
            return;
        }

        if (column.Profile.RidgeWeight > 0.12f)
        {
            violations.Add($"seed={seed} ({wx},{y},{wz}) {surface} on ridged terrain");
        }
    }

    private static TerrainColumn MakeDraft(BiomeType biome, BlockType surface, float ridgeWeight)
    {
        var profile = BiomeProfile.For(biome) with { RidgeWeight = ridgeWeight };
        return new TerrainColumn
        {
            Biome = new BiomeSample { Primary = biome },
            Profile = profile,
            SurfaceBlock = surface,
            SubsurfaceBlock = BlockType.Dirt,
            FillerBlock = BlockType.Stone
        };
    }

    private static void FillFlat(float[,] heights, int x, int z, float value)
    {
        for (int iz = 0; iz < heights.GetLength(1); iz++)
        {
            for (int ix = 0; ix < heights.GetLength(0); ix++)
            {
                heights[ix, iz] = value;
            }
        }

        heights[x, z] = value;
    }
}

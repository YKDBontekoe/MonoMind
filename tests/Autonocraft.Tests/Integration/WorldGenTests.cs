using System;
using System.Collections.Generic;
using Autonocraft.Domain.Crafting;
using Autonocraft.World.Generation.Trees;
using System.IO;
using System.Numerics;
using Autonocraft.Core;
using Autonocraft.Crafting;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Tests.Integration;

public static class WorldGenTests
{
    public static void RunGameSettingsRoundTrip()
    {
        Console.Write("Running Game Settings Round-Trip Test... ");

        var settings = new GameSettings
        {
            RenderDistance = 8,
            PlayWithAi = true,
            AiProvider = AiProviderKind.LlamaCpp,
            OpenRouterModel = "anthropic/claude-3.5-sonnet",
            OpenRouterApiKey = "test-key",
            LlamaCppBaseUrl = "http://127.0.0.1:8080",
            LlamaCppModel = "my-local-model"
        };
        GameSettingsManager.Save(settings);

        var loaded = GameSettingsManager.Load();
        if (loaded.RenderDistance != 8)
        {
            throw new Exception($"Expected render distance 8, got {loaded.RenderDistance}.");
        }

        if (!loaded.PlayWithAi || loaded.AiProvider != AiProviderKind.LlamaCpp)
        {
            throw new Exception("Expected AI settings to round-trip.");
        }

        if (loaded.OpenRouterApiKey != "test-key" || loaded.LlamaCppModel != "my-local-model")
        {
            throw new Exception("Expected OpenRouter and llama.cpp fields to round-trip.");
        }

        loaded.RenderDistance = 99;
        loaded.Clamp();
        if (loaded.RenderDistance != GameSettings.MaxRenderDistance)
        {
            throw new Exception("Expected render distance to clamp to max value.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunChunkLodBands()
    {
        Console.Write("Running Chunk LOD Band Test... ");

        AssertLodDetail(4, 1, ChunkMeshDetail.Full);
        AssertLodDetail(4, 2, ChunkMeshDetail.Full);
        AssertLodDetail(4, 3, ChunkMeshDetail.Surface);
        AssertLodDetail(4, 4, ChunkMeshDetail.Surface);

        AssertLodDetail(6, 2, ChunkMeshDetail.Full);
        AssertLodDetail(6, 3, ChunkMeshDetail.Surface);
        AssertLodDetail(6, 6, ChunkMeshDetail.Shell);

        AssertLodDetail(10, 2, ChunkMeshDetail.Full);
        AssertLodDetail(10, 4, ChunkMeshDetail.Surface);
        AssertLodDetail(10, 10, ChunkMeshDetail.Shell);

        AssertLodDetail(20, 5, ChunkMeshDetail.Full);
        AssertLodDetail(20, 12, ChunkMeshDetail.Surface);
        AssertLodDetail(20, 20, ChunkMeshDetail.Shell);

        AssertLodDetail(32, 8, ChunkMeshDetail.Full);
        AssertLodDetail(32, 20, ChunkMeshDetail.Surface);
        AssertLodDetail(32, 32, ChunkMeshDetail.Shell);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunChunkLodMeshCounts()
    {
        Console.Write("Running Chunk LOD Mesh Count Test... ");

        using var world = new VoxelWorld(1337);
        world.UpdateChunksAround(null, new Vector3(16f, 64f, 16f), 1);

        var chunks = world.GetActiveChunks();
        if (chunks.Count == 0)
        {
            throw new Exception("Expected at least one loaded chunk for LOD mesh count test.");
        }

        var chunk = chunks[0];
        int fullCount = chunk.GetMeshIndexCount(world, ChunkMeshDetail.Full);
        int surfaceCount = chunk.GetMeshIndexCount(world, ChunkMeshDetail.Surface);
        int shellCount = chunk.GetMeshIndexCount(world, ChunkMeshDetail.Shell);

        if (shellCount > surfaceCount || surfaceCount > fullCount)
        {
            throw new Exception($"Expected shell <= surface <= full index counts, got shell={shellCount}, surface={surfaceCount}, full={fullCount}.");
        }

        if (fullCount <= 0)
        {
            throw new Exception("Expected full mesh to contain indices.");
        }

        int leafX = chunk.ChunkX * Chunk.Width + 4;
        int leafZ = chunk.ChunkZ * Chunk.Depth + 4;
        int leafY = Math.Min(Chunk.Height - 2, world.GetHighestSolidY(leafX, leafZ) + 8);
        world.SetBlock(leafX, leafY, leafZ, BlockType.OakLeaves);
        chunk.RebuildColumnHeights();
        chunk.InvalidateMeshes();
        _ = chunk.GetMeshIndexCount(world, ChunkMeshDetail.Shell);
        if (!chunk.HasAlphaCutoutBlocks)
        {
            throw new Exception("Expected shell LOD to include alpha-cutout leaf canopy geometry.");
        }

        chunk.InvalidateMeshes();
        int surfaceBefore = chunk.GetMeshIndexCount(world, ChunkMeshDetail.Surface);
        int trunkY = world.GetHighestSolidY(leafX, leafZ);
        for (int dy = 1; dy <= 3; dy++)
        {
            world.SetBlock(leafX, trunkY + dy, leafZ, BlockType.OakLeaves);
        }

        chunk.RebuildColumnHeights();
        chunk.InvalidateMeshes();
        int surfaceAfter = chunk.GetMeshIndexCount(world, ChunkMeshDetail.Surface);
        if (surfaceAfter <= surfaceBefore)
        {
            throw new Exception($"Expected surface LOD to include leaf blocks above trunk, indices {surfaceBefore} -> {surfaceAfter}.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunOceanShellMeshSurfaces()
    {
        Console.Write("Running Ocean Shell Mesh Test... ");

        var generator = new WorldGenerator(1337, WorldGenParams.ForType(WorldType.Default));
        var oceanCoord = FindPreviewCoord(generator, c => c.Biome.Primary == BiomeType.Ocean, 512, 8);
        if (oceanCoord == null)
        {
            throw new Exception("Expected at least one ocean biome within preview range.");
        }

        int wx = oceanCoord.Value.x;
        int wz = oceanCoord.Value.z;
        using var world = new VoxelWorld(1337);
        world.UpdateChunksAround(null, new Vector3(wx, 64f, wz), 1);

        VoxelWorld.GetChunkCoords(wx, wz, out int cx, out int cz, out _, out _);
        var chunks = world.GetActiveChunks();
        Chunk? chunk = null;
        foreach (var candidate in chunks)
        {
            if (candidate.ChunkX == cx && candidate.ChunkZ == cz)
            {
                chunk = candidate;
                break;
            }
        }

        if (chunk == null)
        {
            throw new Exception("Expected ocean chunk to load for shell mesh test.");
        }

        chunk.InvalidateMeshes();
        _ = chunk.GetMeshIndexCount(world, ChunkMeshDetail.Shell);
        if (!chunk.HasWaterBlocks)
        {
            throw new Exception("Expected ocean shell LOD to include water surface geometry.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunWorldGenerationBasics()
    {
        Console.Write("Running World Generation Basics Test... ");

        var generator = new WorldGenerator(1337, WorldGenParams.ForType(WorldType.Default));
        var column = generator.PreviewColumn(16, 16);

        if (column.SurfaceHeight <= WorldConstants.SeaLevel - 20 || column.SurfaceHeight >= Chunk.Height - 10)
        {
            throw new Exception($"Unexpected surface height at spawn preview: {column.SurfaceHeight}");
        }

        AssertLocalSlopePlayable(generator, 16, 16, maxStep: 4);

        using var world = new VoxelWorld(1337, WorldGenParams.ForType(WorldType.Default));
        world.UpdateChunksAround(null, new Vector3(16f, 64f, 16f), 1);

        bool foundOre = false;
        bool foundCave = false;
        int surfaceY = world.GetHighestSolidY(16, 16);
        if (surfaceY < 0 || !world.GetBlock(16, surfaceY, 16).IsSolidForSpawn())
        {
            throw new Exception($"Expected solid land surface at spawn, got Y={surfaceY}.");
        }

        for (int y = 1; y < surfaceY - 2; y++)
        {
            var block = world.GetBlock(16, y, 16);
            if (block == BlockType.Air)
            {
                foundCave = true;
            }

            if (block is BlockType.CoalOre or BlockType.IronOre or BlockType.GoldOre)
            {
                foundOre = true;
            }
        }

        if (!foundCave)
        {
            throw new Exception("Expected at least one underground air pocket (cave) near spawn column.");
        }

        if (!foundOre)
        {
            throw new Exception("Expected at least one ore block underground near spawn column.");
        }

        var oceanColumn = generator.PreviewColumn(0, 0);
        if (oceanColumn.Biome.Primary != BiomeType.Ocean && oceanColumn.Biome.Primary != BiomeType.Beach)
        {
            // Biome distribution is seed-dependent; verify determinism instead.
            var repeat = generator.PreviewColumn(0, 0);
            if (repeat.Biome.Primary != oceanColumn.Biome.Primary || repeat.SurfaceHeight != oceanColumn.SurfaceHeight)
            {
                throw new Exception("Biome/height preview is not deterministic.");
            }
        }

        var riverColumn = FindPreviewColumn(generator, c => c.IsRiver, radius: 256, step: 4);
        if (riverColumn == null)
        {
            throw new Exception("Expected at least one generated river within preview range.");
        }

        if (riverColumn.Value.SurfaceHeight > WorldConstants.SeaLevel + 1)
        {
            throw new Exception($"Expected river bed near sea level, got {riverColumn.Value.SurfaceHeight}.");
        }

        var deepOceanColumn = FindPreviewColumn(generator, c => c.Biome.Primary == BiomeType.Ocean, radius: 256, step: 4);
        if (deepOceanColumn == null)
        {
            throw new Exception("Expected at least one generated ocean within preview range.");
        }

        if (deepOceanColumn.Value.SurfaceHeight > WorldConstants.SeaLevel - 4)
        {
            throw new Exception($"Expected ocean floor below sea level, got {deepOceanColumn.Value.SurfaceHeight}.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunStructureGeneration()
    {
        Console.Write("Running Structure Generation Test... ");

        const int seed = 1337;
        var parameters = WorldGenParams.ForType(WorldType.Default);
        var generator = new WorldGenerator(seed, parameters);

        if (!TryFindStructureAnchor(generator, seed, parameters, out int anchorX, out int anchorZ, out StructureDefinition expectedStructure))
        {
            throw new Exception("Expected at least one valid structure anchor for test seed.");
        }

        VoxelWorld.GetChunkCoords(anchorX, anchorZ, out int anchorCx, out int anchorCz, out _, out _);
        int surfaceHeight = generator.PreviewColumn(anchorX, anchorZ).SurfaceHeight;
        bool foundSignature = false;
        for (int cz = anchorCz - 1; cz <= anchorCz + 1; cz++)
        {
            for (int cx = anchorCx - 1; cx <= anchorCx + 1; cx++)
            {
                var chunk = new Chunk(cx, cz);
                generator.GenerateChunkTerrain(chunk, null);
                if (ChunkContainsStructureBlocks(chunk, anchorX, anchorZ, surfaceHeight, expectedStructure))
                {
                    foundSignature = true;
                }
            }
        }

        if (!foundSignature)
        {
            throw new Exception($"Expected structure blocks near anchor ({anchorX}, {anchorZ}).");
        }

        var chunkA = new Chunk(anchorCx, anchorCz);
        var chunkB = new Chunk(anchorCx, anchorCz);
        generator.GenerateChunkTerrain(chunkA, null);
        generator.GenerateChunkTerrain(chunkB, null);
        if (!ChunksEqual(chunkA, chunkB))
        {
            throw new Exception("Structure generation is not deterministic for the same chunk.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunChunkStreamingStability()
    {
        Console.Write("Running Chunk Streaming Stability Test... ");

        using var world = new VoxelWorld(1337, WorldGenParams.ForType(WorldType.Default));
        const int renderDistance = 4;
        int peakPendingMesh = 0;

        for (int step = 0; step < 5; step++)
        {
            var pos = new Vector3(16f, 64f, 16f + step * 16);
            world.UpdateChunksAround(null, pos, renderDistance);

            for (int frame = 0; frame < 20; frame++)
            {
                world.ProcessPendingWork(null, pos, renderDistance);
                peakPendingMesh = Math.Max(peakPendingMesh, world.PendingMeshCount);
            }
        }

        if (peakPendingMesh > 200)
        {
            throw new Exception($"Expected bounded pending mesh queue during streaming, peak was {peakPendingMesh}.");
        }

        var chunks = world.GetActiveChunks();
        if (chunks.Count < 9)
        {
            throw new Exception($"Expected multiple loaded chunks after streaming walk, got {chunks.Count}.");
        }

        float totalShellMs = 0f;
        float totalFullMs = 0f;
        int compared = 0;
        int shellFasterCount = 0;
        int sampleCount = Math.Min(5, chunks.Count);
        for (int i = 0; i < sampleCount; i++)
        {
            var chunk = chunks[i];
            if (!world.TryCreateMeshBuildContext(chunk, out var context))
            {
                continue;
            }

            float shellMs = chunk.BenchmarkBuildMeshCpu(context, ChunkMeshDetail.Shell);
            float fullMs = chunk.BenchmarkBuildMeshCpu(context, ChunkMeshDetail.Full);
            totalShellMs += shellMs;
            totalFullMs += fullMs;
            compared++;

            if (shellMs <= fullMs * 0.85f)
            {
                shellFasterCount++;
            }
        }

        if (compared == 0)
        {
            throw new Exception("Expected at least one chunk with mesh build context.");
        }

        float avgShellMs = totalShellMs / compared;
        float avgFullMs = totalFullMs / compared;
        if (avgFullMs <= 0f)
        {
            throw new Exception("Expected positive full mesh build time.");
        }

        if (avgShellMs > avgFullMs * 0.55f || shellFasterCount < compared / 2)
        {
            throw new Exception(
                $"Expected shell mesh to be cheaper than full mesh on average, got avgShell={avgShellMs:F2}ms avgFull={avgFullMs:F2}ms fasterChunks={shellFasterCount}/{compared}.");
        }

        var freshChunk = chunks[0];
        int distance = 2;
        var firstBuild = ChunkLod.SelectBuildDetail(freshChunk, distance, renderDistance);
        if (firstBuild != ChunkMeshDetail.Shell)
        {
            throw new Exception($"Expected first streaming build to target Shell LOD, got {firstBuild}.");
        }

        if (VoxelWorld.MeshBuildBudgetMs > 16f)
        {
            throw new Exception("Mesh build budget per frame is too high for stable gameplay.");
        }

        Console.WriteLine($"[Streaming] peakPending={peakPendingMesh} avgShell={avgShellMs:F1}ms avgFull={avgFullMs:F1}ms");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }
    public static void RunBiomeTreeSpecies()
    {
        Console.Write("Running Biome Tree Species Test... ");

        var generator = new WorldGenerator(1337, WorldGenParams.ForType(WorldType.Default));
        var swampCoord = FindPreviewCoord(generator, c => c.Biome.Primary == BiomeType.Swamp, 512, 8);
        var desertCoord = FindPreviewCoord(generator, c => c.Biome.Primary == BiomeType.Desert, 512, 8);

        if (swampCoord == null || desertCoord == null)
        {
            throw new Exception("Expected swamp and desert biomes within preview range.");
        }

        if (swampCoord.Value.column.SurfaceBlock != BlockType.Mud)
        {
            throw new Exception($"Expected swamp surface to be Mud, got {swampCoord.Value.column.SurfaceBlock}.");
        }

        if (!ScanGeneratedChunksForLog(generator, BiomeType.Swamp, BlockType.WillowLog))
        {
            throw new Exception("Expected WillowLog in generated swamp biome.");
        }

        if (!ScanGeneratedChunksForLog(generator, BiomeType.Desert, BlockType.PalmLog))
        {
            throw new Exception("Expected PalmLog in generated desert biome.");
        }

        var forestCoord = FindPreviewCoord(generator, c => c.Biome.Primary == BiomeType.Forest, 512, 8);
        if (forestCoord == null)
        {
            throw new Exception("Expected forest biome within preview range.");
        }

        int forestSpecies = CountForestTreeSpecies(generator);
        if (forestSpecies < 2)
        {
            throw new Exception($"Expected at least 2 forest tree species, found {forestSpecies}.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunFloraPlacement()
    {
        Console.Write("Running Flora Placement Test... ");

        var generator = new WorldGenerator(1337, WorldGenParams.ForType(WorldType.Default));

        if (!ScanGeneratedChunksForFlora(generator, BiomeType.Forest, BlockType.Fern))
        {
            throw new Exception("Expected Fern in generated forest biome.");
        }

        if (!ScanGeneratedChunksForFlora(generator, BiomeType.Desert, BlockType.DeadBush))
        {
            throw new Exception("Expected DeadBush in generated desert biome.");
        }

        if (!ScanGeneratedChunksForFlora(generator, BiomeType.Swamp, BlockType.LilyPad))
        {
            throw new Exception("Expected LilyPad in generated swamp biome.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunDesertPalmDensity()
    {
        Console.Write("Running Desert Palm Density Test... ");

        var generator = new WorldGenerator(1337, WorldGenParams.ForType(WorldType.Default));
        var desertCoord = FindPreviewCoord(
            generator,
            c => c.Biome.Primary == BiomeType.Desert && c.Profile.TreeDensity >= 0.02f,
            512,
            4);

        if (desertCoord == null)
        {
            throw new Exception("Expected pure desert columns within preview range.");
        }

        int palmCount = 0;
        for (int chunkZ = -24; chunkZ <= 24; chunkZ++)
        {
            for (int chunkX = -24; chunkX <= 24; chunkX++)
            {
                var columns = generator.PreviewChunkColumns(chunkX, chunkZ);
                bool hasDesert = false;
                for (int lx = 0; lx < Chunk.Width; lx++)
                {
                    for (int lz = 0; lz < Chunk.Depth; lz++)
                    {
                        if (columns[lx, lz].Biome.Primary == BiomeType.Desert)
                        {
                            hasDesert = true;
                            break;
                        }
                    }

                    if (hasDesert)
                    {
                        break;
                    }
                }

                if (!hasDesert)
                {
                    continue;
                }

                var chunk = new Chunk(chunkX, chunkZ);
                generator.GenerateChunkTerrain(chunk, null);
                palmCount += CountBlockInChunk(chunk, BlockType.PalmLog);
            }
        }

        if (palmCount <= 0)
        {
            throw new Exception("Expected PalmLog in pure desert after TreeDensity fix.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunTreeShapeDiversity()
    {
        Console.Write("Running Tree Shape Diversity Test... ");

        var generator = new WorldGenerator(1337, WorldGenParams.ForType(WorldType.Default));

        var pineSpan = MeasureLeafVerticalSpan(generator, BiomeType.SnowyPeaks, BlockType.PineLeaves);
        var palmSpan = MeasureLeafVerticalSpan(generator, BiomeType.Desert, BlockType.PalmLeaves);
        if (pineSpan <= palmSpan)
        {
            throw new Exception($"Expected Pine Y-span ({pineSpan}) > Palm Y-span ({palmSpan}).");
        }

        var willowWidth = MeasureGeneratedLeafHorizontalSpan(TreeSpecies.Willow());
        var birchWidth = MeasureGeneratedLeafHorizontalSpan(TreeSpecies.Birch());
        if (willowWidth <= birchWidth)
        {
            throw new Exception($"Expected Willow canopy width ({willowWidth}) > Birch width ({birchWidth}).");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunTreeBlockBudget()
    {
        Console.Write("Running Tree Block Budget Test... ");

        TreeSpecies[] species =
        [
            TreeSpecies.Oak(), TreeSpecies.Birch(), TreeSpecies.Pine(),
            TreeSpecies.Willow(), TreeSpecies.Palm(), TreeSpecies.Cherry(),
            TreeSpecies.Mahogany(), TreeSpecies.Maple()
        ];

        foreach (var treeSpecies in species)
        {
            var voxels = TreeShapeGenerator.Generate(treeSpecies, 16, 16, 64, 1337, 0.9f, 0.3f);
            var unique = new HashSet<(int dx, int dy, int dz)>();
            foreach (var voxel in voxels)
            {
                unique.Add((voxel.Dx, voxel.Dy, voxel.Dz));
            }

            if (unique.Count > treeSpecies.MaxBlocks)
            {
                throw new Exception(
                    $"Species {treeSpecies.Log} exceeded MaxBlocks ({treeSpecies.MaxBlocks}), got {unique.Count}.");
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunBiomeFloraPresence()
    {
        Console.Write("Running Biome Flora Presence Test... ");

        var generator = new WorldGenerator(1337, WorldGenParams.ForType(WorldType.Default));

        if (!ScanGeneratedChunksForFlora(generator, BiomeType.Forest, BlockType.Shrub))
        {
            throw new Exception("Expected Shrub in generated forest biome.");
        }

        if (!ScanGeneratedChunksForFlora(generator, BiomeType.SnowyPeaks, BlockType.Heather))
        {
            throw new Exception("Expected Heather in generated snowy peaks biome.");
        }

        if (!ScanGeneratedChunksForFlora(generator, BiomeType.SnowyPeaks, BlockType.Juniper))
        {
            throw new Exception("Expected Juniper in generated snowy peaks biome.");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void RunFloraMeshBuilderTileMapping()
    {
        Console.Write("Running Flora Mesh Builder Tile Mapping Test... ");

        foreach (BlockType type in Enum.GetValues<BlockType>())
        {
            if (!type.IsFloraModel())
            {
                continue;
            }

            string tileId = FloraMeshBuilder.GetTileId(type);
            if (tileId == "tall_grass" && type != BlockType.TallGrass)
            {
                throw new Exception($"Flora block {type} maps to generic tall_grass tile.");
            }
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ResetColor();
    }

    public static void AssertLodDetail(int renderDistance, int chunkDistance, ChunkMeshDetail expected)
    {
        var detail = ChunkLod.SelectDetail(chunkDistance, renderDistance);
        if (detail != expected)
        {
            throw new Exception($"Expected LOD {expected} at distance {chunkDistance} with render distance {renderDistance}, got {detail}.");
        }
    }

    public static void AssertLocalSlopePlayable(WorldGenerator generator, int wx, int wz, int maxStep)
    {
        VoxelWorld.GetChunkCoords(wx, wz, out int cx, out int cz, out int lx, out int lz);
        var columns = generator.PreviewChunkColumns(cx, cz);
        int centerHeight = columns[lx, lz].SurfaceHeight;
        var center = columns[lx, lz];

        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0)
                {
                    continue;
                }

                int nx = lx + dx;
                int nz = lz + dz;
                if (nx < 0 || nz < 0 || nx >= Chunk.Width || nz >= Chunk.Depth)
                {
                    continue;
                }

                var neighbor = columns[nx, nz];
                if (!IsPlayableSlopeCell(center) || !IsPlayableSlopeCell(neighbor))
                {
                    continue;
                }

                int delta = Math.Abs(neighbor.SurfaceHeight - centerHeight);
                if (delta > maxStep)
                {
                    throw new Exception($"Expected playable local slope near spawn, got height step {delta}.");
                }
            }
        }
    }

    private static bool TryFindStructureAnchor(
        WorldGenerator generator,
        int seed,
        WorldGenParams parameters,
        out int anchorX,
        out int anchorZ,
        out StructureDefinition expectedStructure)
    {
        anchorX = 0;
        anchorZ = 0;
        expectedStructure = StructureRegistry.All[0];

        if (TryFindStructureAnchorForTier(generator, seed, parameters, StructureTier.Small, 96, 900, 11, out anchorX, out anchorZ, out expectedStructure))
        {
            return true;
        }

        return TryFindStructureAnchorForTier(generator, seed, parameters, StructureTier.Medium, 192, 5000, 29, out anchorX, out anchorZ, out expectedStructure);
    }
    private static bool TryFindStructureAnchorForTier(
        WorldGenerator generator,
        int seed,
        WorldGenParams parameters,
        StructureTier tier,
        int cellSize,
        int baseRarity,
        int salt,
        out int anchorX,
        out int anchorZ,
        out StructureDefinition expectedStructure)
    {
        anchorX = 0;
        anchorZ = 0;
        expectedStructure = StructureRegistry.All[0];

        float density = Math.Max(0.1f, parameters.StructureDensityScale);
        int rarity = Math.Max(64, (int)(baseRarity / density));

        for (int cellZ = -96; cellZ <= 96; cellZ++)
        {
            for (int cellX = -96; cellX <= 96; cellX++)
            {
                int candidateX = cellX * cellSize + cellSize / 2;
                int candidateZ = cellZ * cellSize + cellSize / 2;
                int hash = StructureHash(candidateX, candidateZ, seed, salt);
                if (hash % rarity != 37)
                {
                    continue;
                }

                var column = generator.PreviewColumn(candidateX, candidateZ);
                if (column.Biome.Primary == BiomeType.Ocean || column.IsRiver || column.IsLake)
                {
                    continue;
                }

                var candidates = StructureRegistry.GetCandidates(column.Biome.Primary, tier);
                if (candidates.Count == 0)
                {
                    continue;
                }

                var definition = candidates[hash % candidates.Count];
                if (!IsStructureFootprintFlat(generator, candidateX, candidateZ, definition.Template.FootprintRadius))
                {
                    continue;
                }

                anchorX = candidateX;
                anchorZ = candidateZ;
                expectedStructure = definition;
                return true;
            }
        }

        return false;
    }
    private static bool IsStructureFootprintFlat(WorldGenerator generator, int anchorX, int anchorZ, int radius)
    {
        var center = generator.PreviewColumn(anchorX, anchorZ);
        int baseHeight = center.SurfaceHeight;

        for (int dz = -radius; dz <= radius; dz++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                var column = generator.PreviewColumn(anchorX + dx, anchorZ + dz);
                if (column.Biome.Primary == BiomeType.Ocean || column.IsRiver || column.IsLake)
                {
                    return false;
                }

                if (Math.Abs(column.SurfaceHeight - baseHeight) > 2)
                {
                    return false;
                }
            }
        }

        return true;
    }
    private static bool ChunkContainsStructureBlocks(
        Chunk chunk,
        int anchorX,
        int anchorZ,
        int surfaceHeight,
        StructureDefinition definition)
    {
        int chunkMinX = chunk.ChunkX * Chunk.Width;
        int chunkMinZ = chunk.ChunkZ * Chunk.Depth;
        int matchedBlocks = 0;

        foreach (var block in definition.Template.Blocks)
        {
            int wx = anchorX + block.Dx;
            int wz = anchorZ + block.Dz;
            int wy = surfaceHeight + block.Dy;

            int lx = wx - chunkMinX;
            int lz = wz - chunkMinZ;
            if (lx < 0 || lx >= Chunk.Width || lz < 0 || lz >= Chunk.Depth)
            {
                continue;
            }

            if (wy <= 0 || wy >= Chunk.Height)
            {
                continue;
            }

            if (chunk.GetBlock(lx, wy, lz) == block.Type)
            {
                matchedBlocks++;
            }
        }

        return matchedBlocks > 0;
    }
    private static bool ChunksEqual(Chunk a, Chunk b)
    {
        for (int lx = 0; lx < Chunk.Width; lx++)
        {
            for (int lz = 0; lz < Chunk.Depth; lz++)
            {
                for (int y = 0; y < Chunk.Height; y++)
                {
                    if (a.GetBlock(lx, y, lz) != b.GetBlock(lx, y, lz))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }
    private static int StructureHash(int wx, int wz, int seed, int salt)
    {
        unchecked
        {
            int h = wx * 92821 + wz * 68917 + seed + salt;
            h ^= h >> 13;
            h *= 1274126177;
            h ^= h >> 16;
            return Math.Abs(h);
        }
    }
    private static TerrainColumn? FindPreviewColumn(WorldGenerator generator, Func<TerrainColumn, bool> predicate, int radius, int step)
    {
        for (int z = -radius; z <= radius; z += step)
        {
            for (int x = -radius; x <= radius; x += step)
            {
                var column = generator.PreviewColumn(x, z);
                if (predicate(column))
                {
                    return column;
                }
            }
        }

        return null;
    }
    private static bool IsPlayableSlopeCell(TerrainColumn column)
    {
        return column.Biome.Primary is BiomeType.Plains or BiomeType.Forest or BiomeType.Swamp or BiomeType.Desert
            && !column.IsRiver
            && !column.IsLake;
    }
    private static (int x, int z, TerrainColumn column)? FindPreviewCoord(
        WorldGenerator generator,
        Func<TerrainColumn, bool> predicate,
        int radius,
        int step)
    {
        for (int z = -radius; z <= radius; z += step)
        {
            for (int x = -radius; x <= radius; x += step)
            {
                var column = generator.PreviewColumn(x, z);
                if (predicate(column))
                {
                    return (x, z, column);
                }
            }
        }

        return null;
    }
    private static bool ScanGeneratedChunksForLog(WorldGenerator generator, BiomeType biome, BlockType logType)
    {
        for (int chunkZ = -24; chunkZ <= 24; chunkZ++)
        {
            for (int chunkX = -24; chunkX <= 24; chunkX++)
            {
                var columns = generator.PreviewChunkColumns(chunkX, chunkZ);
                bool hasBiomeTrees = false;
                for (int lx = 0; lx < Chunk.Width; lx++)
                {
                    for (int lz = 0; lz < Chunk.Depth; lz++)
                    {
                        var column = columns[lx, lz];
                        if (column.Biome.Primary == biome
                            && column.Profile.TreeDensity > 0f
                            && !column.IsRiver
                            && !column.IsLake)
                        {
                            hasBiomeTrees = true;
                            break;
                        }
                    }

                    if (hasBiomeTrees)
                    {
                        break;
                    }
                }

                if (!hasBiomeTrees)
                {
                    continue;
                }

                var chunk = new Chunk(chunkX, chunkZ);
                generator.GenerateChunkTerrain(chunk, null);
                if (ChunkContainsLog(chunk, logType))
                {
                    return true;
                }
            }
        }

        return false;
    }
    private static bool ChunkContainsLog(Chunk chunk, BlockType logType)
    {
        for (int lx = 0; lx < Chunk.Width; lx++)
        {
            for (int lz = 0; lz < Chunk.Depth; lz++)
            {
                for (int y = 1; y < Chunk.Height; y++)
                {
                    if (chunk.GetBlock(lx, y, lz) == logType)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static int CountForestTreeSpecies(WorldGenerator generator)
    {
        var species = new HashSet<BlockType>();
        BlockType[] forestLogs =
        [
            BlockType.OakLog, BlockType.BirchLog, BlockType.MahoganyLog, BlockType.MapleLog
        ];

        for (int chunkZ = -24; chunkZ <= 24; chunkZ++)
        {
            for (int chunkX = -24; chunkX <= 24; chunkX++)
            {
                var columns = generator.PreviewChunkColumns(chunkX, chunkZ);
                bool hasForest = false;
                for (int lx = 0; lx < Chunk.Width; lx++)
                {
                    for (int lz = 0; lz < Chunk.Depth; lz++)
                    {
                        if (columns[lx, lz].Biome.Primary == BiomeType.Forest)
                        {
                            hasForest = true;
                            break;
                        }
                    }

                    if (hasForest)
                    {
                        break;
                    }
                }

                if (!hasForest)
                {
                    continue;
                }

                var chunk = new Chunk(chunkX, chunkZ);
                generator.GenerateChunkTerrain(chunk, null);
                foreach (var logType in forestLogs)
                {
                    if (ChunkContainsLog(chunk, logType))
                    {
                        species.Add(logType);
                    }
                }

                if (species.Count >= 2)
                {
                    return species.Count;
                }
            }
        }

        return species.Count;
    }

    private static bool ScanGeneratedChunksForFlora(WorldGenerator generator, BiomeType biome, BlockType floraType)
    {
        for (int chunkZ = -24; chunkZ <= 24; chunkZ++)
        {
            for (int chunkX = -24; chunkX <= 24; chunkX++)
            {
                var columns = generator.PreviewChunkColumns(chunkX, chunkZ);
                bool hasBiome = false;
                for (int lx = 0; lx < Chunk.Width; lx++)
                {
                    for (int lz = 0; lz < Chunk.Depth; lz++)
                    {
                        var column = columns[lx, lz];
                        if (column.Biome.Primary == biome && !column.IsRiver && !column.IsLake)
                        {
                            hasBiome = true;
                            break;
                        }
                    }

                    if (hasBiome)
                    {
                        break;
                    }
                }

                if (!hasBiome)
                {
                    continue;
                }

                var chunk = new Chunk(chunkX, chunkZ);
                generator.GenerateChunkTerrain(chunk, null);
                if (ChunkContainsBlock(chunk, floraType))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ChunkContainsBlock(Chunk chunk, BlockType blockType)
    {
        for (int lx = 0; lx < Chunk.Width; lx++)
        {
            for (int lz = 0; lz < Chunk.Depth; lz++)
            {
                for (int y = 1; y < Chunk.Height; y++)
                {
                    if (chunk.GetBlock(lx, y, lz) == blockType)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static int CountBlockInChunk(Chunk chunk, BlockType blockType)
    {
        int count = 0;
        for (int lx = 0; lx < Chunk.Width; lx++)
        {
            for (int lz = 0; lz < Chunk.Depth; lz++)
            {
                for (int y = 1; y < Chunk.Height; y++)
                {
                    if (chunk.GetBlock(lx, y, lz) == blockType)
                    {
                        count++;
                    }
                }
            }
        }

        return count;
    }

    private static int MeasureLeafVerticalSpan(WorldGenerator generator, BiomeType biome, BlockType leafType)
    {
        int bestSpan = 0;
        for (int chunkZ = -16; chunkZ <= 16; chunkZ++)
        {
            for (int chunkX = -16; chunkX <= 16; chunkX++)
            {
                if (!ChunkHasBiome(generator, chunkX, chunkZ, biome))
                {
                    continue;
                }

                var chunk = new Chunk(chunkX, chunkZ);
                generator.GenerateChunkTerrain(chunk, null);
                foreach (var (minY, maxY) in FindLeafVerticalSpans(chunk, leafType))
                {
                    bestSpan = Math.Max(bestSpan, maxY - minY);
                }
            }
        }

        return bestSpan;
    }

    private static int MeasureLeafHorizontalSpan(WorldGenerator generator, BiomeType biome, BlockType leafType)
    {
        int bestWidth = 0;
        for (int chunkZ = -16; chunkZ <= 16; chunkZ++)
        {
            for (int chunkX = -16; chunkX <= 16; chunkX++)
            {
                if (!ChunkHasBiome(generator, chunkX, chunkZ, biome))
                {
                    continue;
                }

                var chunk = new Chunk(chunkX, chunkZ);
                generator.GenerateChunkTerrain(chunk, null);
                foreach (var width in FindLeafHorizontalSpans(chunk, leafType))
                {
                    bestWidth = Math.Max(bestWidth, width);
                }
            }
        }

        return bestWidth;
    }

    private static int MeasureGeneratedLeafHorizontalSpan(TreeSpecies species)
    {
        var voxels = TreeShapeGenerator.Generate(species, 16, 16, 64, 1337, 0.9f, 0.3f);
        int minX = int.MaxValue;
        int maxX = int.MinValue;
        int minZ = int.MaxValue;
        int maxZ = int.MinValue;
        bool found = false;

        foreach (var voxel in voxels)
        {
            if (voxel.Type != species.Leaves)
            {
                continue;
            }

            found = true;
            minX = Math.Min(minX, voxel.Dx);
            maxX = Math.Max(maxX, voxel.Dx);
            minZ = Math.Min(minZ, voxel.Dz);
            maxZ = Math.Max(maxZ, voxel.Dz);
        }

        if (!found)
        {
            return 0;
        }

        return Math.Max(maxX - minX, maxZ - minZ);
    }

    private static int ScanMaxTreeComponentSize(WorldGenerator generator)
    {
        int maxSize = 0;
        for (int chunkZ = -12; chunkZ <= 12; chunkZ++)
        {
            for (int chunkX = -12; chunkX <= 12; chunkX++)
            {
                var chunk = new Chunk(chunkX, chunkZ);
                generator.GenerateChunkTerrain(chunk, null);
                maxSize = Math.Max(maxSize, FindLargestTreeComponent(chunk));
            }
        }

        return maxSize;
    }

    private static bool ChunkHasBiome(WorldGenerator generator, int chunkX, int chunkZ, BiomeType biome)
    {
        var columns = generator.PreviewChunkColumns(chunkX, chunkZ);
        for (int lx = 0; lx < Chunk.Width; lx++)
        {
            for (int lz = 0; lz < Chunk.Depth; lz++)
            {
                if (columns[lx, lz].Biome.Primary == biome)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<(int minY, int maxY)> FindLeafVerticalSpans(Chunk chunk, BlockType leafType)
    {
        var visited = new bool[Chunk.Width, Chunk.Depth];
        for (int lx = 0; lx < Chunk.Width; lx++)
        {
            for (int lz = 0; lz < Chunk.Depth; lz++)
            {
                if (visited[lx, lz])
                {
                    continue;
                }

                int minY = int.MaxValue;
                int maxY = int.MinValue;
                bool found = false;
                for (int y = 1; y < Chunk.Height; y++)
                {
                    if (chunk.GetBlock(lx, y, lz) == leafType)
                    {
                        found = true;
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                    }
                }

                if (found)
                {
                    visited[lx, lz] = true;
                    yield return (minY, maxY);
                }
            }
        }
    }

    private static IEnumerable<int> FindLeafHorizontalSpans(Chunk chunk, BlockType leafType)
    {
        var visited = new bool[Chunk.Width, Chunk.Height, Chunk.Depth];
        for (int lx = 0; lx < Chunk.Width; lx++)
        {
            for (int lz = 0; lz < Chunk.Depth; lz++)
            {
                for (int y = 1; y < Chunk.Height; y++)
                {
                    if (visited[lx, y, lz] || chunk.GetBlock(lx, y, lz) != leafType)
                    {
                        continue;
                    }

                    int minX = lx;
                    int maxX = lx;
                    int minZ = lz;
                    int maxZ = lz;
                    var queue = new Queue<(int x, int y, int z)>();
                    queue.Enqueue((lx, y, lz));
                    visited[lx, y, lz] = true;
                    int count = 0;

                    while (queue.Count > 0)
                    {
                        var (cx, cy, cz) = queue.Dequeue();
                        count++;
                        minX = Math.Min(minX, cx);
                        maxX = Math.Max(maxX, cx);
                        minZ = Math.Min(minZ, cz);
                        maxZ = Math.Max(maxZ, cz);

                        foreach (var (nx, ny, nz) in Neighbors(cx, cy, cz))
                        {
                            if (nx < 0 || nx >= Chunk.Width || nz < 0 || nz >= Chunk.Depth || ny <= 0 || ny >= Chunk.Height)
                            {
                                continue;
                            }

                            if (visited[nx, ny, nz] || chunk.GetBlock(nx, ny, nz) != leafType)
                            {
                                continue;
                            }

                            visited[nx, ny, nz] = true;
                            queue.Enqueue((nx, ny, nz));
                        }
                    }

                    if (count >= 4)
                    {
                        yield return Math.Max(maxX - minX, maxZ - minZ);
                    }
                }
            }
        }
    }

    private static int FindLargestTreeComponent(Chunk chunk)
    {
        var visited = new bool[Chunk.Width, Chunk.Height, Chunk.Depth];
        int maxSize = 0;

        for (int lx = 0; lx < Chunk.Width; lx++)
        {
            for (int lz = 0; lz < Chunk.Depth; lz++)
            {
                for (int y = 1; y < Chunk.Height; y++)
                {
                    var block = chunk.GetBlock(lx, y, lz);
                    if (!block.IsAnyLog() && !block.IsAnyLeaves())
                    {
                        continue;
                    }

                    if (visited[lx, y, lz])
                    {
                        continue;
                    }

                    int size = FloodFillTreeSize(chunk, visited, lx, y, lz);
                    maxSize = Math.Max(maxSize, size);
                }
            }
        }

        return maxSize;
    }

    private static int FloodFillTreeSize(Chunk chunk, bool[,,] visited, int startX, int startY, int startZ)
    {
        var queue = new Queue<(int x, int y, int z)>();
        queue.Enqueue((startX, startY, startZ));
        visited[startX, startY, startZ] = true;
        int size = 0;

        while (queue.Count > 0)
        {
            var (x, y, z) = queue.Dequeue();
            size++;

            foreach (var (nx, ny, nz) in Neighbors(x, y, z))
            {
                if (nx < 0 || nx >= Chunk.Width || nz < 0 || nz >= Chunk.Depth || ny <= 0 || ny >= Chunk.Height)
                {
                    continue;
                }

                if (visited[nx, ny, nz])
                {
                    continue;
                }

                var block = chunk.GetBlock(nx, ny, nz);
                if (!block.IsAnyLog() && !block.IsAnyLeaves())
                {
                    continue;
                }

                visited[nx, ny, nz] = true;
                queue.Enqueue((nx, ny, nz));
            }
        }

        return size;
    }

    private static IEnumerable<(int x, int y, int z)> Neighbors(int x, int y, int z)
    {
        yield return (x + 1, y, z);
        yield return (x - 1, y, z);
        yield return (x, y + 1, z);
        yield return (x, y - 1, z);
        yield return (x, y, z + 1);
        yield return (x, y, z - 1);
    }
}

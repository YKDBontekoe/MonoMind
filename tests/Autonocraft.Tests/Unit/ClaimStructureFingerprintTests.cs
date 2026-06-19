using System.Collections.Generic;
using System.Linq;
using Autonocraft.Domain.World;
using Autonocraft.Entities;
using Autonocraft.Tests.Integration;
using Autonocraft.Village;
using Autonocraft.World;
using Autonocraft.World.Structures;
using Xunit;

namespace Autonocraft.Tests.Unit;

public sealed class ClaimStructureFingerprintTests
{
    [Fact]
    public void QuickClaimScan_IgnoresMegaStructures()
    {
        using var world = new VoxelWorld(4242);
        world.UpdateChunksAround(null, new System.Numerics.Vector3(64.5f, 64f, 64.5f), 4);

        bool found = new VillageFoundingService(new VillagerManager(), new HashSet<long>())
            .TryFindClaimableStructure(world, new System.Numerics.Vector3(64.5f, 70f, 64.5f), 16f, out _, out _, out _, quickScan: true);

        Assert.False(found);
    }

    [Fact]
    public void ManualPlainsCottage_MatchesFingerprint()
    {
        using var world = new VoxelWorld(5555);
        world.UpdateChunksAround(null, new System.Numerics.Vector3(32.5f, 64f, 32.5f), 2);

        int ax = 32;
        int az = 32;
        world.UpdateChunksAround(null, new System.Numerics.Vector3(ax + 0.5f, 64f, az + 0.5f), 2);

        int surfaceY = StructureFingerprint.FindSurfaceAnchorY(world, ax, az);
        int ay = surfaceY - 1;
        var cottage = StructureRegistry.All.First(s => s.Id == "PlainsCottage");
        var biome = world.SampleBiome(ax, az).Primary;
        int placementHash = StructureFingerprint.StructureHashForTests(ax, az, world.Seed, 11);
        int variantSalt = StructurePlacementKeys.VariantSaltForStructure(world.Seed, ax, az, cottage.Id, placementHash);
        var template = cottage.ResolveTemplate(world.Seed, ax, az, variantSalt, biome);

        foreach (var block in template.Blocks)
        {
            world.SetBlock(ax + block.Dx, ay + block.Dy, az + block.Dz, BlockType.Air);
        }

        foreach (var block in template.Blocks)
        {
            world.SetBlock(ax + block.Dx, ay + block.Dy, az + block.Dz, block.Type);
        }

        float ratio = StructureFingerprint.ComputeMatchRatio(world, ax, ay, az, template);
        Assert.True(ratio > 0.8f, $"Direct ratio={ratio:F2} surfaceY={surfaceY} ay={ay} blocks={template.Blocks.Length} biome={biome}");

        Assert.True(
            StructureFingerprint.TryMatchWorldStructure(world, ax, ay, az, out _, out ratio),
            $"TryMatch ratio={ratio:F2}");
    }

    [Fact]
    public void ForestShelter_ExteriorQuality()
    {
        var template = StructureQualityAssertions.ResolveTemplate("ForestShelter", anchorX: 32, anchorZ: 48);
        StructureQualityAssertions.AssertExteriorQuality("ForestShelter", template);
    }
}

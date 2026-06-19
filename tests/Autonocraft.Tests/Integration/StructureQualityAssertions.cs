using System;
using System.Collections.Generic;
using System.Linq;
using Autonocraft.Domain.World;
using Autonocraft.World;
using Autonocraft.World.Structures;
using Autonocraft.Village;

namespace Autonocraft.Tests.Integration;

internal static class StructureQualityAssertions
{
    private static readonly string[] TargetBuildingIds =
    [
        "ForestShelter",
        "PlainsCottage",
        "VillageOutpost",
        "ForestWatchtower",
        "SnowyHut"
    ];

    public static IReadOnlyList<string> TargetBuildings => TargetBuildingIds;

    public static StructureTemplate ResolveTemplate(string id, int seed = StructureGallery.Seed, int anchorX = 0, int anchorZ = 0)
    {
        var definition = StructureRegistry.All.FirstOrDefault(s => s.Id == id)
            ?? throw new Exception($"Structure '{id}' is not registered.");
        int placementHash = StructureFingerprint.StructureHashForTests(anchorX, anchorZ, seed, 11);
        int variantSalt = StructurePlacementKeys.VariantSaltForStructure(seed, anchorX, anchorZ, id, placementHash);
        return definition.ResolveTemplate(seed, anchorX, anchorZ, variantSalt, BiomeType.Plains);
    }

    public static void AssertExteriorQuality(string id, StructureTemplate template)
    {
        var nonAir = template.Blocks.Where(b => b.Type != BlockType.Air).ToArray();
        if (nonAir.Length < 80)
        {
            throw new Exception($"{id} should have a richer exterior block count, got {nonAir.Length}.");
        }

        int minX = nonAir.Min(b => b.Dx);
        int maxX = nonAir.Max(b => b.Dx);
        int minZ = nonAir.Min(b => b.Dz);
        int maxZ = nonAir.Max(b => b.Dz);
        int maxY = nonAir.Max(b => b.Dy);
        if (maxY < 5)
        {
            throw new Exception($"{id} should have a recognizable vertical silhouette, maxY={maxY}.");
        }

        if (Math.Max(Math.Max(Math.Abs(minX), Math.Abs(maxX)), Math.Max(Math.Abs(minZ), Math.Abs(maxZ))) > template.FootprintRadius)
        {
            throw new Exception($"{id} has blocks outside its declared footprint radius {template.FootprintRadius}.");
        }

        int uniqueMaterials = nonAir.Select(b => b.Type).Distinct().Count();
        if (uniqueMaterials < 6)
        {
            throw new Exception($"{id} should use at least 6 visible materials, got {uniqueMaterials}.");
        }

        int detailCount = nonAir.Count(b => IsDetailBlock(b.Type));
        if (detailCount < 6)
        {
            throw new Exception($"{id} should include at least 6 exterior/detail blocks, got {detailCount}.");
        }

        bool hasNonBoxyFootprint = nonAir.Any(b =>
            (Math.Abs(b.Dx) == template.FootprintRadius || Math.Abs(b.Dz) == template.FootprintRadius)
            && b.Dy is >= 1 and <= 4);
        bool hasRoofPeak = nonAir.Any(b => b.Dy >= maxY - 1 && IsDetailBlock(b.Type));
        if (!hasNonBoxyFootprint && !hasRoofPeak)
        {
            throw new Exception($"{id} should have porch/facade depth or roof landmark detail.");
        }
    }

    public static void AssertInteriorQuality(string id, StructureTemplate template)
    {
        int maxInteriorY = id == "ForestWatchtower" ? 12 : 4;
        int interiorAir = template.Blocks.Count(b =>
            b.Type == BlockType.Air && b.Dy >= 1 && b.Dy <= maxInteriorY);
        if (interiorAir < 8)
        {
            throw new Exception($"{id} should have at least 8 navigable interior air cells, got {interiorAir}.");
        }

        if (!HasTwoHighDoor(template))
        {
            throw new Exception($"{id} should expose a clear two-block-high entrance.");
        }

        int usableFeatureCount = template.Blocks.Count(b => IsUsableInteriorBlock(b.Type)) + template.Chests.Length;
        if (usableFeatureCount < 3)
        {
            throw new Exception($"{id} should contain at least 3 usable or readable interior features, got {usableFeatureCount}.");
        }

        foreach (var chest in template.Chests)
        {
            bool chestHeadroom = template.Blocks.Any(b => b.Dx == chest.Dx && b.Dy == chest.Dy + 1 && b.Dz == chest.Dz && b.Type == BlockType.Air);
            if (!chestHeadroom)
            {
                throw new Exception($"{id} chest at ({chest.Dx},{chest.Dy},{chest.Dz}) should have air above it.");
            }
        }
    }

    public static void AssertGalleryPlacementReachable(VoxelWorld world, StructureGallery.Placement placement)
    {
        int entranceX = placement.AnchorX;
        int entranceZ = placement.AnchorZ - placement.FootprintRadius;
        int y = placement.SurfaceY + 1;
        bool hasNearEntranceAir = false;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dz = -1; dz <= 1; dz++)
            {
                if (world.GetBlock(entranceX + dx, y, entranceZ + dz) == BlockType.Air
                    && world.GetBlock(entranceX + dx, y + 1, entranceZ + dz) == BlockType.Air)
                {
                    hasNearEntranceAir = true;
                }
            }
        }

        if (!hasNearEntranceAir)
        {
            throw new Exception($"{placement.Id} should have a two-block-high reachable entrance zone in the gallery.");
        }
    }

    private static bool HasTwoHighDoor(StructureTemplate template)
    {
        var air = template.Blocks.Where(b => b.Type == BlockType.Air).ToArray();
        var airSet = new HashSet<(int X, int Y, int Z)>(air.Select(b => (b.Dx, b.Dy, b.Dz)));
        int minZ = air.Length == 0 ? 0 : air.Min(b => b.Dz);
        return air.Any(b => b.Dz <= minZ + 1 && b.Dy >= 1 && airSet.Contains((b.Dx, b.Dy + 1, b.Dz)));
    }

    private static bool IsDetailBlock(BlockType type) =>
        type is BlockType.Glass
            or BlockType.RedStainedGlass
            or BlockType.BlueStainedGlass
            or BlockType.Lantern
            or BlockType.Rope
            or BlockType.Chest
            or BlockType.HayBale
            or BlockType.StationBench
            or BlockType.StationForge
            or BlockType.StationCrucible
            or BlockType.StationSmoker
            or BlockType.StationStonecutter
            or BlockType.StoneSlab
            or BlockType.MossCarpet
            or BlockType.Lichen
            or BlockType.BerryBush
            or BlockType.Flower
            or BlockType.Sunflower
            or BlockType.Poppy
            or BlockType.Daisy;

    private static bool IsUsableInteriorBlock(BlockType type) =>
        IsDetailBlock(type)
        || type is BlockType.OakPlank
            or BlockType.BirchPlank
            or BlockType.PinePlank
            or BlockType.CherryPlank
            or BlockType.MahoganyPlank
            or BlockType.MaplePlank;
}

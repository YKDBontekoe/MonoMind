using System;
using System.Collections.Generic;
using Autonocraft.Domain.Village;

namespace Autonocraft.Village
{
    internal static class VillageLayoutPlanner
    {
        internal readonly struct LayoutAnchor
        {
            public int AnchorX { get; init; }
            public int AnchorZ { get; init; }
            public bool PreferVillageAnchorY { get; init; }
        }

        private readonly record struct LayoutPoint(float X, float Z);

        private static readonly LayoutPoint[] CivicPoints =
        {
            new(0.0f, -1.0f),
            new(0.9f, -0.55f),
            new(1.1f, 0.15f),
            new(0.65f, 0.9f),
            new(-0.1f, 1.15f),
            new(-0.85f, 0.8f),
            new(-1.1f, -0.15f),
            new(-0.7f, -0.95f)
        };

        private static readonly LayoutPoint[] ResidentialPoints =
        {
            new(0.15f, -1.0f),
            new(0.8f, -0.8f),
            new(1.05f, -0.15f),
            new(0.95f, 0.55f),
            new(0.35f, 1.0f),
            new(-0.35f, 1.05f),
            new(-0.95f, 0.65f),
            new(-1.1f, 0.0f),
            new(-0.85f, -0.7f),
            new(-0.25f, -1.05f)
        };

        private static readonly LayoutPoint[] IndustryPoints =
        {
            new(0.2f, -1.0f),
            new(1.0f, -0.45f),
            new(1.1f, 0.35f),
            new(0.45f, 1.05f),
            new(-0.35f, 1.1f),
            new(-1.05f, 0.45f),
            new(-1.15f, -0.25f),
            new(-0.55f, -1.0f)
        };

        public static IEnumerable<LayoutAnchor> EnumerateCandidateAnchors(Village village, BuildingBlueprint blueprint)
        {
            var seen = new HashSet<(int x, int z)>();
            int spacing = Math.Max(6, blueprint.Template.FootprintRadius * 2 + 4);
            foreach (var candidate in EnumeratePattern(village, spacing, blueprint.Kind))
            {
                if (seen.Add((candidate.AnchorX, candidate.AnchorZ)))
                {
                    yield return candidate;
                }
            }

            for (int ring = spacing + 2; ring <= 26; ring += 3)
            {
                foreach (var point in ResidentialPoints)
                {
                    int x = village.AnchorX + (int)MathF.Round(point.X * ring);
                    int z = village.AnchorZ + (int)MathF.Round(point.Z * ring);
                    if (seen.Add((x, z)))
                    {
                        yield return new LayoutAnchor
                        {
                            AnchorX = x,
                            AnchorZ = z,
                            PreferVillageAnchorY = ring <= 11
                        };
                    }
                }
            }
        }

        private static IEnumerable<LayoutAnchor> EnumeratePattern(Village village, int spacing, BuildingKind kind)
        {
            var (points, rings) = kind switch
            {
                BuildingKind.TownHeart => (CivicPoints, new[] { 0 }),
                BuildingKind.House => (ResidentialPoints, new[] { spacing + 3, spacing + 7, spacing + 11 }),
                BuildingKind.FarmPlot => (IndustryPoints, new[] { spacing + 7, spacing + 11, spacing + 15 }),
                BuildingKind.LumberCamp => (IndustryPoints, new[] { spacing + 9, spacing + 14 }),
                BuildingKind.Quarry => (IndustryPoints, new[] { spacing + 11, spacing + 15 }),
                _ => (CivicPoints, new[] { spacing + 2, spacing + 6, spacing + 10 })
            };

            foreach (int ring in rings)
            {
                foreach (var point in points)
                {
                    yield return new LayoutAnchor
                    {
                        AnchorX = village.AnchorX + (int)MathF.Round(point.X * ring),
                        AnchorZ = village.AnchorZ + (int)MathF.Round(point.Z * ring),
                        PreferVillageAnchorY = ring <= spacing + 6
                    };
                }
            }
        }
    }
}

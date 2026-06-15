using System.Numerics;
using Autonocraft.Engine;
using Autonocraft.World;
using Xunit;

namespace Autonocraft.Tests.Unit;

public class CornerAoTests
{
    [Fact]
    public void FlatGrassTopFaceStaysBrightAtNoonLevel()
    {
        var chunk = new Chunk(0, 0);
        const int y = 64;

        for (int x = 6; x <= 10; x++)
        {
            for (int z = 6; z <= 10; z++)
            {
                chunk.SetBlock(x, y, z, BlockType.Grass);
            }
        }

        var context = new MeshBuildContext(chunk, null, null, null, null, seed: 42);
        var vertices = new List<Vertex>();
        var indices = new List<uint>();
        BuildMeshVertices(chunk, context, vertices, indices);

        float centerTopBrightness = FindTopFaceCornerBrightness(vertices, wx: 8, wy: y, wz: 8, cornerX: 8, cornerZ: 8);
        // Grass tint (0.86–0.94) × top shade (0.96) × no AO = ~0.82–0.90.
        // The top face must not be blocked by flat same-height neighbours (≥ 0.78 confirms no spurious darkening).
        Assert.True(centerTopBrightness > 0.78f,
            $"Expected flat grass tops not spuriously darkened by same-height neighbours, got {centerTopBrightness:F3}.");
    }

    [Fact]
    public void RidgeSouthFaceTopCornerIsDarkerThanFullyLitCorner()
    {
        var chunk = new Chunk(0, 0);
        const int y = 64;
        const int ridgeY = y + 1;

        for (int x = 7; x <= 9; x++)
        {
            for (int z = 7; z <= 9; z++)
            {
                chunk.SetBlock(x, y, z, BlockType.Stone);
            }
        }

        chunk.SetBlock(8, ridgeY, 8, BlockType.Stone);

        var context = new MeshBuildContext(chunk, null, null, null, null, seed: 42);
        var vertices = new List<Vertex>();
        var indices = new List<uint>();
        BuildMeshVertices(chunk, context, vertices, indices);

        float topCornerBrightness = FindSouthFaceTopCornerBrightness(vertices, wx: 8, wy: ridgeY, wz: 8);
        float interiorTopBrightness = FindTopFaceCornerBrightness(vertices, wx: 8, wy: ridgeY, wz: 8, cornerX: 8, cornerZ: 8);

        Assert.True(topCornerBrightness < 0.98f,
            $"Expected ridge cliff corner AO to darken the vertex, got brightness {topCornerBrightness:F3}.");
        Assert.True(topCornerBrightness < interiorTopBrightness,
            $"Expected cliff corner ({topCornerBrightness:F3}) to be darker than interior top corner ({interiorTopBrightness:F3}).");
    }

    private static void BuildMeshVertices(Chunk chunk, MeshBuildContext context, List<Vertex> vertices, List<uint> indices)
    {
        var method = typeof(Chunk).GetMethod(
            "BuildFullMeshList",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);
        method!.Invoke(chunk, new object[] { context, vertices, indices, new List<Vertex>(), new List<uint>() });
    }

    private static float FindSouthFaceTopCornerBrightness(IReadOnlyList<Vertex> vertices, int wx, int wy, int wz)
    {
        var target = new Vector3(wx, wy + 1, wz);
        const float tolerance = 0.01f;

        float darkest = 1f;
        foreach (var vertex in vertices)
        {
            if (MathF.Abs(vertex.Position.X - target.X) > tolerance
                || MathF.Abs(vertex.Position.Y - target.Y) > tolerance
                || MathF.Abs(vertex.Position.Z - target.Z) > tolerance)
            {
                continue;
            }

            if (vertex.Normal.Z > -0.5f)
            {
                continue;
            }

            float brightness = (vertex.Color.X + vertex.Color.Y + vertex.Color.Z) / 3f;
            darkest = MathF.Min(darkest, brightness);
        }

        Assert.True(darkest < 1f, "Expected to find south-face top-corner vertex on ridge block.");
        return darkest;
    }

    private static float FindTopFaceCornerBrightness(
        IReadOnlyList<Vertex> vertices,
        int wx,
        int wy,
        int wz,
        int cornerX,
        int cornerZ)
    {
        var target = new Vector3(cornerX, wy + 1, cornerZ);
        const float tolerance = 0.01f;

        foreach (var vertex in vertices)
        {
            if (MathF.Abs(vertex.Position.X - target.X) > tolerance
                || MathF.Abs(vertex.Position.Y - target.Y) > tolerance
                || MathF.Abs(vertex.Position.Z - target.Z) > tolerance)
            {
                continue;
            }

            if (vertex.Normal.Y < 0.5f)
            {
                continue;
            }

            return (vertex.Color.X + vertex.Color.Y + vertex.Color.Z) / 3f;
        }

        Assert.Fail("Expected to find top-face corner vertex on ridge block.");
        return 1f;
    }
}

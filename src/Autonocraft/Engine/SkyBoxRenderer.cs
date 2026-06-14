using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Engine
{
    /// <summary>
    /// Cached hemisphere skydome rendered with per-vertex sky colors.
    /// </summary>
    public sealed class SkyDomeRenderer : IDisposable
    {
        private const int Slices = 24;
        private const int Stacks = 12;
        private const float Radius = 480f;

        private readonly VertexPositionColor[] _vertices;
        private readonly short[] _indices;
        private readonly int _indexCount;

        public SkyDomeRenderer(GraphicsDevice device)
        {
            _ = device;
            BuildHemisphereMesh(
                out VertexPosition[] positions,
                out _indices,
                out _indexCount);

            _vertices = new VertexPositionColor[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                _vertices[i].Position = positions[i].Position;
            }
        }

        public void Draw(
            SkyEffect skyEffect,
            Matrix view,
            Matrix projection,
            SceneLighting lighting,
            float timeOfDay,
            int worldSeed)
        {
            var skyView = SkyEffect.StripTranslation(view);
            skyEffect.ApplySkyDome(skyView, projection);
            FillSkyColors(lighting, timeOfDay, worldSeed);

            skyEffect.SkyDomeEffect.View = skyView;
            skyEffect.SkyDomeEffect.Projection = projection;

            var device = skyEffect.SkyDomeEffect.GraphicsDevice;
            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            device.BlendState = BlendState.Opaque;

            foreach (var pass in skyEffect.GetSkyDomePasses())
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    _vertices,
                    0,
                    _vertices.Length,
                    _indices,
                    0,
                    _indexCount / 3);
            }
        }

        private void FillSkyColors(SceneLighting lighting, float timeOfDay, int worldSeed)
        {
            for (int i = 0; i < _vertices.Length; i++)
            {
                var dir = new System.Numerics.Vector3(
                    _vertices[i].Position.X,
                    _vertices[i].Position.Y,
                    _vertices[i].Position.Z);
                var rgb = SkyColor.Compute(dir, lighting, timeOfDay, worldSeed);
                _vertices[i].Color = new Color(rgb.X, rgb.Y, rgb.Z);
            }
        }

        private static void BuildHemisphereMesh(
            out VertexPosition[] vertices,
            out short[] indices,
            out int indexCount)
        {
            int vertCount = (Slices + 1) * (Stacks + 1);
            vertices = new VertexPosition[vertCount];
            var indexList = new List<short>();

            int vi = 0;
            for (int stack = 0; stack <= Stacks; stack++)
            {
                float v = stack / (float)Stacks;
                float phi = v * MathHelper.PiOver2;
                float y = MathF.Sin(phi) * Radius;
                float ring = MathF.Cos(phi) * Radius;

                for (int slice = 0; slice <= Slices; slice++)
                {
                    float u = slice / (float)Slices;
                    float theta = u * MathHelper.TwoPi;
                    float x = MathF.Cos(theta) * ring;
                    float z = MathF.Sin(theta) * ring;
                    vertices[vi++] = new VertexPosition(new Vector3(x, y, z));
                }
            }

            for (int stack = 0; stack < Stacks; stack++)
            {
                for (int slice = 0; slice < Slices; slice++)
                {
                    int topLeft = stack * (Slices + 1) + slice;
                    int topRight = topLeft + 1;
                    int bottomLeft = (stack + 1) * (Slices + 1) + slice;
                    int bottomRight = bottomLeft + 1;

                    indexList.Add((short)topLeft);
                    indexList.Add((short)bottomLeft);
                    indexList.Add((short)topRight);

                    indexList.Add((short)topRight);
                    indexList.Add((short)bottomLeft);
                    indexList.Add((short)bottomRight);
                }
            }

            indices = indexList.ToArray();
            indexCount = indices.Length;
        }

        public void Dispose()
        {
        }
    }
}

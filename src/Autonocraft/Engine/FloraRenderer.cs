using System;
using System.Diagnostics;
using Autonocraft.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.World;
using Matrix = Microsoft.Xna.Framework.Matrix;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace Autonocraft.Engine
{
    public sealed class FloraRenderer : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly FloraEffect _effect;
        private VertexPositionColorTexture[] _vertexScratch = Array.Empty<VertexPositionColorTexture>();

        public FloraRenderer(GraphicsDevice device, Texture2D atlas)
        {
            _device = device;
            _effect = new FloraEffect(device, atlas);
        }

        public void SetAtlas(Texture2D atlas)
        {
            _effect.SetAtlas(atlas);
        }

        public void Draw(
            IReadOnlyList<VisibleChunkDrawInfo> visibleChunks,
            Matrix view,
            Matrix projection,
            SceneLighting lighting,
            float fogStart,
            float fogEnd,
            float animTime,
            Texture2D atlas)
        {
            var sw = Stopwatch.StartNew();
            int totalVertices = 0;
            int drawCalls = 0;

            var amb = lighting.AmbientColor * 1.15f;
            var sun = lighting.SunEnabled ? lighting.SunColor * 0.35f : System.Numerics.Vector3.Zero;
            var moon = lighting.MoonEnabled ? lighting.MoonColor * 0.15f : System.Numerics.Vector3.Zero;
            float litX = amb.X + sun.X + moon.X;
            float litY = amb.Y + sun.Y + moon.Y;
            float litZ = amb.Z + sun.Z + moon.Z;

            _device.RasterizerState = RasterizerState.CullNone;
            _device.SamplerStates[0] = SamplerState.PointClamp;
            _device.DepthStencilState = DepthStencilState.Default;
            _device.BlendState = BlendState.Opaque;

            _effect.Apply(
                Matrix.Identity,
                view,
                projection,
                atlas,
                lighting.ToMono(lighting.SkyHorizon),
                fogStart,
                fogEnd);

            foreach (var entry in visibleChunks)
            {
                if (!entry.Chunk.HasFloraMesh())
                {
                    continue;
                }

                entry.Chunk.EnsureFloraGpuBuffers(_device);
                var (vertexBuffer, indexBuffer, indexCount, sourceVertices) = entry.Chunk.GetFloraGpuDrawInfo();
                if (vertexBuffer == null || indexBuffer == null || indexCount <= 0 || sourceVertices == null || sourceVertices.Length == 0)
                {
                    continue;
                }

                int vertexCount = sourceVertices.Length;
                EnsureVertexScratch(vertexCount);
                for (int i = 0; i < vertexCount; i++)
                {
                    var source = sourceVertices[i];
                    float phase = source.WindPhase * MathHelper.TwoPi;
                    float sway = MathF.Sin(animTime * 1.8f + phase) * source.HeightFactor * 0.06f;
                    var pos = source.Position;
                    pos.X += sway;
                    pos.Z += sway * 0.7f;

                    _vertexScratch[i] = new VertexPositionColorTexture(
                        new Vector3(pos.X, pos.Y, pos.Z),
                        new Color(
                            MathHelper.Clamp(source.Color.X * litX, 0f, 1f),
                            MathHelper.Clamp(source.Color.Y * litY, 0f, 1f),
                            MathHelper.Clamp(source.Color.Z * litZ, 0f, 1f)),
                        new Vector2(source.TexCoords.X, source.TexCoords.Y));
                }

                vertexBuffer.SetData(_vertexScratch, 0, vertexCount, SetDataOptions.Discard);
                _device.SetVertexBuffer(vertexBuffer);
                _device.Indices = indexBuffer;
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, indexCount / 3);
                }

                totalVertices += vertexCount;
                drawCalls++;
            }

            sw.Stop();
            PerfCounters.FloraVertexCount = totalVertices;
            PerfCounters.FloraDrawCalls = drawCalls;
            PerfCounters.FloraDrawMs = (float)sw.Elapsed.TotalMilliseconds;
        }

        private void EnsureVertexScratch(int vertexCount)
        {
            if (_vertexScratch.Length < vertexCount)
            {
                int capacity = Math.Max(vertexCount, _vertexScratch.Length < 1 ? 4096 : _vertexScratch.Length * 2);
                _vertexScratch = new VertexPositionColorTexture[capacity];
            }
        }

        public void Dispose()
        {
            _effect.Dispose();
        }
    }
}

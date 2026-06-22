using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        // Batched GPU buffers
        private DynamicVertexBuffer? _batchedVertexBuffer;
        private DynamicIndexBuffer? _batchedIndexBuffer;
        private int _batchedVertexCapacity = 0;
        private int _batchedIndexCapacity = 0;

        // CPU scratch arrays
        private VertexPositionColorTexture[] _vertexScratch = Array.Empty<VertexPositionColorTexture>();
        private uint[] _indexScratch = Array.Empty<uint>();

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
            int renderDistance,
            float animTime,
            Texture2D atlas)
        {
            var sw = Stopwatch.StartNew();
            int totalVertices = 0;
            int totalIndices = 0;

            // 1. Calculate total vertex/index counts
            foreach (var entry in visibleChunks)
            {
                if (!entry.Chunk.HasFloraMesh())
                {
                    continue;
                }

                var (vertices, indices, indexCount) = entry.Chunk.GetFloraMesh();
                if (vertices == null || indices == null || indexCount <= 0)
                {
                    continue;
                }

                totalVertices += vertices.Length;
                totalIndices += indexCount;
            }

            if (totalVertices == 0 || totalIndices == 0)
            {
                sw.Stop();
                PerfCounters.FloraVertexCount = 0;
                PerfCounters.FloraDrawCalls = 0;
                PerfCounters.FloraDrawMs = (float)sw.Elapsed.TotalMilliseconds;
                return;
            }

            // 2. Ensure capacities
            EnsureScratchCapacity(totalVertices, totalIndices);
            EnsureGpuBuffers(totalVertices, totalIndices);

            // 3. Setup lighting multipliers
            var amb = lighting.AmbientColor * 1.15f;
            var sun = lighting.SunEnabled ? lighting.SunColor * 0.35f : System.Numerics.Vector3.Zero;
            var moon = lighting.MoonEnabled ? lighting.MoonColor * 0.15f : System.Numerics.Vector3.Zero;
            float litX = amb.X + sun.X + moon.X;
            float litY = amb.Y + sun.Y + moon.Y;
            float litZ = amb.Z + sun.Z + moon.Z;

            // 4. Populate batched data
            int vertexOffset = 0;
            int indexOffset = 0;

            foreach (var entry in visibleChunks)
            {
                if (!entry.Chunk.HasFloraMesh())
                {
                    continue;
                }

                var (vertices, indices, indexCount) = entry.Chunk.GetFloraMesh();
                if (vertices == null || indices == null || indexCount <= 0)
                {
                    continue;
                }

                bool animate = ChunkLod.ShouldAnimateFloraEveryFrame(entry.ChunkDistance, renderDistance);
                int chunkVertexCount = vertices.Length;

                for (int i = 0; i < chunkVertexCount; i++)
                {
                    var source = vertices[i];
                    float phase = source.WindPhase * MathHelper.TwoPi;
                    float windScale = 0.5f + lighting.WindIntensity * 2.5f;
                    float swaySpeed = 1.8f * (0.8f + lighting.WindIntensity * 1.5f);
                    float sway = animate
                        ? MathF.Sin(animTime * swaySpeed + phase) * source.HeightFactor * 0.06f * windScale
                        : 0f;
                    var pos = source.Position;
                    pos.X += sway;
                    pos.Z += sway * 0.7f;

                    _vertexScratch[vertexOffset + i] = new VertexPositionColorTexture(
                        new Vector3(pos.X, pos.Y, pos.Z),
                        new Color(
                            MathHelper.Clamp(source.Color.X * litX, 0f, 1f),
                            MathHelper.Clamp(source.Color.Y * litY, 0f, 1f),
                            MathHelper.Clamp(source.Color.Z * litZ, 0f, 1f)),
                        new Vector2(source.TexCoords.X, source.TexCoords.Y));
                }

                for (int i = 0; i < indexCount; i++)
                {
                    _indexScratch[indexOffset + i] = (uint)(indices[i] + vertexOffset);
                }

                vertexOffset += chunkVertexCount;
                indexOffset += indexCount;
            }

            // 5. Upload to GPU
            _batchedVertexBuffer!.SetData(_vertexScratch, 0, totalVertices, SetDataOptions.Discard);
            _batchedIndexBuffer!.SetData(_indexScratch, 0, totalIndices, SetDataOptions.Discard);

            // 6. Bind states and draw
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

            _device.SetVertexBuffer(_batchedVertexBuffer);
            _device.Indices = _batchedIndexBuffer;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, totalIndices / 3);
            }

            sw.Stop();
            PerfCounters.FloraVertexCount = totalVertices;
            PerfCounters.FloraDrawCalls = 1;
            PerfCounters.FloraDrawMs = (float)sw.Elapsed.TotalMilliseconds;
        }

        private void EnsureScratchCapacity(int vertexCount, int indexCount)
        {
            if (_vertexScratch.Length < vertexCount)
            {
                int newCap = Math.Max(vertexCount, _vertexScratch.Length == 0 ? 8192 : _vertexScratch.Length * 2);
                Array.Resize(ref _vertexScratch, newCap);
            }
            if (_indexScratch.Length < indexCount)
            {
                int newCap = Math.Max(indexCount, _indexScratch.Length == 0 ? 12288 : _indexScratch.Length * 2);
                Array.Resize(ref _indexScratch, newCap);
            }
        }

        private void EnsureGpuBuffers(int vertexCount, int indexCount)
        {
            if (_batchedVertexBuffer == null || _batchedVertexCapacity < vertexCount)
            {
                _batchedVertexBuffer?.Dispose();
                _batchedVertexCapacity = Math.Max(vertexCount, _batchedVertexCapacity == 0 ? 8192 : _batchedVertexCapacity * 2);
                _batchedVertexBuffer = new DynamicVertexBuffer(
                    _device,
                    VertexPositionColorTexture.VertexDeclaration,
                    _batchedVertexCapacity,
                    BufferUsage.WriteOnly);
            }

            if (_batchedIndexBuffer == null || _batchedIndexCapacity < indexCount)
            {
                _batchedIndexBuffer?.Dispose();
                _batchedIndexCapacity = Math.Max(indexCount, _batchedIndexCapacity == 0 ? 12288 : _batchedIndexCapacity * 2);
                _batchedIndexBuffer = new DynamicIndexBuffer(
                    _device,
                    IndexElementSize.ThirtyTwoBits,
                    _batchedIndexCapacity,
                    BufferUsage.WriteOnly);
            }
        }

        public void Dispose()
        {
            _effect.Dispose();
            _batchedVertexBuffer?.Dispose();
            _batchedIndexBuffer?.Dispose();
        }
    }
}

using System;
using System.Collections.Generic;
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
        private readonly AlphaTestEffect _effect;
        private DynamicVertexBuffer? _batchVertexBuffer;
        private DynamicIndexBuffer? _batchIndexBuffer;
        private int _batchVertexCapacity;
        private int _batchIndexCapacity;
        private readonly List<VertexPositionColorTexture> _verticesScratch = new(8192);
        private readonly List<int> _indicesScratch = new(12288);

        public FloraRenderer(GraphicsDevice device, Texture2D atlas)
        {
            _device = device;
            _effect = new AlphaTestEffect(device)
            {
                Texture = atlas,
                VertexColorEnabled = true,
                FogEnabled = true,
                AlphaFunction = CompareFunction.Greater,
                ReferenceAlpha = 128
            };
        }

        public void SetAtlas(Texture2D atlas)
        {
            _effect.Texture = atlas;
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
            _verticesScratch.Clear();
            _indicesScratch.Clear();

            foreach (var entry in visibleChunks)
            {
                if (!entry.Chunk.HasFloraMesh())
                {
                    continue;
                }

                var (floraVertices, floraIndices, indexCount) = entry.Chunk.GetFloraMesh();
                if (floraVertices == null || floraIndices == null || indexCount == 0)
                {
                    continue;
                }

                uint baseVertex = (uint)_verticesScratch.Count;
                for (int i = 0; i < floraVertices.Length; i++)
                {
                    var source = floraVertices[i];
                    float phase = source.WindPhase * MathHelper.TwoPi;
                    float sway = MathF.Sin(animTime * 1.8f + phase) * source.HeightFactor * 0.06f;
                    var pos = source.Position;
                    pos.X += sway;
                    pos.Z += sway * 0.7f;

                    _verticesScratch.Add(new VertexPositionColorTexture(
                        new Vector3(pos.X, pos.Y, pos.Z),
                        BakeFloraColor(source.Color, lighting),
                        new Vector2(source.TexCoords.X, source.TexCoords.Y)));
                }

                for (int i = 0; i < indexCount; i++)
                {
                    _indicesScratch.Add((int)(baseVertex + floraIndices[i]));
                }
            }

            if (_verticesScratch.Count == 0)
            {
                return;
            }

            EnsureBatchCapacity(_verticesScratch.Count, _indicesScratch.Count);
            _batchVertexBuffer!.SetData(_verticesScratch.ToArray(), 0, _verticesScratch.Count, SetDataOptions.Discard);
            _batchIndexBuffer!.SetData(_indicesScratch.ToArray(), 0, _indicesScratch.Count, SetDataOptions.Discard);

            _device.RasterizerState = RasterizerState.CullNone;
            _device.SamplerStates[0] = SamplerState.PointClamp;
            _device.DepthStencilState = DepthStencilState.Default;
            _device.BlendState = BlendState.Opaque;

            _effect.World = Matrix.Identity;
            _effect.View = view;
            _effect.Projection = projection;
            _effect.Texture = atlas;
            _effect.AlphaFunction = CompareFunction.Greater;
            _effect.ReferenceAlpha = 128;
            _effect.FogEnabled = true;
            _effect.FogColor = lighting.ToMono(lighting.SkyHorizon);
            _effect.FogStart = fogStart;
            _effect.FogEnd = fogEnd;

            _device.SetVertexBuffer(_batchVertexBuffer);
            _device.Indices = _batchIndexBuffer;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    0,
                    0,
                    _indicesScratch.Count / 3);
            }
        }

        private static Color BakeFloraColor(System.Numerics.Vector3 vertexColor, SceneLighting lighting)
        {
            var amb = lighting.AmbientColor * 1.15f;
            var sun = lighting.SunEnabled ? lighting.SunColor * 0.35f : System.Numerics.Vector3.Zero;
            var moon = lighting.MoonEnabled ? lighting.MoonColor * 0.15f : System.Numerics.Vector3.Zero;
            var lit = amb + sun + moon;
            return new Color(
                MathHelper.Clamp(vertexColor.X * lit.X, 0f, 1f),
                MathHelper.Clamp(vertexColor.Y * lit.Y, 0f, 1f),
                MathHelper.Clamp(vertexColor.Z * lit.Z, 0f, 1f));
        }

        private void EnsureBatchCapacity(int vertexCount, int indexCount)
        {
            if (_batchVertexBuffer == null || _batchVertexCapacity < vertexCount)
            {
                _batchVertexBuffer?.Dispose();
                _batchVertexCapacity = Math.Max(vertexCount, _batchVertexCapacity * 2);
                if (_batchVertexCapacity < 4096)
                {
                    _batchVertexCapacity = 4096;
                }

                _batchVertexBuffer = new DynamicVertexBuffer(
                    _device,
                    VertexPositionColorTexture.VertexDeclaration,
                    _batchVertexCapacity,
                    BufferUsage.WriteOnly);
            }

            if (_batchIndexBuffer == null || _batchIndexCapacity < indexCount)
            {
                _batchIndexBuffer?.Dispose();
                _batchIndexCapacity = Math.Max(indexCount, _batchIndexCapacity * 2);
                if (_batchIndexCapacity < 6144)
                {
                    _batchIndexCapacity = 6144;
                }

                _batchIndexBuffer = new DynamicIndexBuffer(
                    _device,
                    IndexElementSize.ThirtyTwoBits,
                    _batchIndexCapacity,
                    BufferUsage.WriteOnly);
            }
        }

        public void Dispose()
        {
            _batchVertexBuffer?.Dispose();
            _batchIndexBuffer?.Dispose();
            _effect.Dispose();
        }
    }
}

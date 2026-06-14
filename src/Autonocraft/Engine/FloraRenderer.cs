using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.World;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace Autonocraft.Engine
{
    public sealed class FloraRenderer : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly BlockTerrainEffect _terrainEffect;

        public FloraRenderer(GraphicsDevice device, BlockTerrainEffect terrainEffect)
        {
            _device = device;
            _terrainEffect = terrainEffect;
        }

        public void Draw(
            IReadOnlyList<VisibleChunkDrawInfo> visibleChunks,
            Matrix view,
            Matrix projection,
            SceneLighting lighting,
            float fogStart,
            float fogEnd,
            Texture2D atlas)
        {
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SamplerStates[0] = SamplerState.PointClamp;
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.BlendState = BlendState.AlphaBlend;

            _terrainEffect.ApplyLightingAndFog(
                Matrix.Identity,
                view,
                projection,
                lighting.ToMono(lighting.AmbientColor),
                lighting.ToMono(lighting.SkyHorizon),
                fogStart,
                fogEnd,
                lighting.ToMono(lighting.SunDirection),
                lighting.ToMono(lighting.SunColor),
                lighting.SunEnabled,
                lighting.ToMono(lighting.MoonDirection),
                lighting.ToMono(lighting.MoonColor),
                lighting.MoonEnabled,
                atlas);

            foreach (var entry in visibleChunks)
            {
                if (!entry.Chunk.HasFloraMesh())
                {
                    continue;
                }

                var (vb, ib, count) = entry.Chunk.GetFloraMesh();
                if (vb == null || ib == null || count == 0)
                {
                    continue;
                }

                _device.SetVertexBuffer(vb);
                _device.Indices = ib;
                foreach (var pass in _terrainEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, count / 3);
                }
            }
        }

        public void Dispose()
        {
        }
    }
}

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.World;
using Vector3 = System.Numerics.Vector3;
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
            VoxelWorld world,
            Matrix view,
            Matrix projection,
            Vector3 cameraPos,
            SceneLighting lighting,
            float fogStart,
            float fogEnd,
            Texture2D atlas,
            Microsoft.Xna.Framework.Vector4[] frustumPlanes)
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

            foreach (var chunk in world.ActiveChunks)
            {
                if (!chunk.HasFloraMesh() || !IsChunkVisible(chunk, frustumPlanes))
                {
                    continue;
                }

                var (vb, ib, count) = chunk.GetFloraMesh();
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

        private static bool IsChunkVisible(Chunk chunk, Microsoft.Xna.Framework.Vector4[] planes)
        {
            float minX = chunk.ChunkX * Chunk.Width;
            float minY = 0f;
            float minZ = chunk.ChunkZ * Chunk.Depth;
            float maxX = minX + Chunk.Width;
            float maxY = Chunk.Height;
            float maxZ = minZ + Chunk.Depth;

            for (int i = 0; i < planes.Length; i++)
            {
                float px = planes[i].X >= 0f ? maxX : minX;
                float py = planes[i].Y >= 0f ? maxY : minY;
                float pz = planes[i].Z >= 0f ? maxZ : minZ;

                if (planes[i].X * px + planes[i].Y * py + planes[i].Z * pz + planes[i].W < 0f)
                {
                    return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
        }
    }
}

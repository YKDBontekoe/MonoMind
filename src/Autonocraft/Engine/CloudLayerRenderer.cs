using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Autonocraft.Engine
{
    /// <summary>
    /// Scrolling soft cloud quads on the sky shell, rendered via SkyEffect or BasicEffect fallback.
    /// </summary>
    public sealed class CloudLayerRenderer : IDisposable
    {
        private readonly Texture2D _cloudTexture;
        private readonly short[] _quadIndices = { 0, 1, 2, 0, 2, 3 };
        private readonly VertexPositionColorTexture[] _layerVertices = new VertexPositionColorTexture[4];

        private readonly struct CloudLayerSpec
        {
            public float Elevation { get; init; }
            public float Width { get; init; }
            public float Depth { get; init; }
            public float WindScale { get; init; }
            public float WindOffset { get; init; }
            public float AlphaScale { get; init; }
            public float HeightOffset { get; init; }
        }

        private static readonly CloudLayerSpec[] Layers =
        {
            new() { Elevation = 0.26f, Width = 520f, Depth = 340f, WindScale = 1.0f, WindOffset = 0f, AlphaScale = 1.0f, HeightOffset = 0f },
            new() { Elevation = 0.36f, Width = 440f, Depth = 290f, WindScale = 0.78f, WindOffset = 160f, AlphaScale = 0.88f, HeightOffset = 20f },
            new() { Elevation = 0.46f, Width = 370f, Depth = 250f, WindScale = 0.58f, WindOffset = -130f, AlphaScale = 0.74f, HeightOffset = 38f },
            new() { Elevation = 0.56f, Width = 300f, Depth = 210f, WindScale = 0.42f, WindOffset = 90f, AlphaScale = 0.55f, HeightOffset = 54f },
        };

        public CloudLayerRenderer(GraphicsDevice device)
        {
            _ = device;
            _cloudTexture = CreateCloudTexture(device);
        }

        public Texture2D CloudTexture => _cloudTexture;

        public void Draw(
            GraphicsDevice device,
            SkyEffect skyEffect,
            Matrix view,
            Matrix projection,
            float timeOfDay,
            SceneLighting lighting)
        {
            if (lighting.DayLight < 0.06f)
            {
                return;
            }

            float alpha = MathHelper.Lerp(0.22f, 0.48f, lighting.DayLight);
            var warmTint = Color.Lerp(Color.White, new Color(1f, 0.82f, 0.62f), lighting.SunsetFactor * 0.72f);

            device.DepthStencilState = DepthStencilState.None;
            device.RasterizerState = RasterizerState.CullNone;
            device.BlendState = BlendState.AlphaBlend;
            device.SamplerStates[0] = SamplerState.LinearClamp;

            float windX = timeOfDay * 120f;
            float windZ = timeOfDay * 80f;

            foreach (var layer in Layers)
            {
                float layerAlpha = alpha * layer.AlphaScale;
                var cloudColor = new Color(warmTint.R, warmTint.G, warmTint.B) * layerAlpha;

                float scrollX = windX * layer.WindScale + layer.WindOffset;
                float scrollZ = windZ * layer.WindScale * 0.75f - layer.WindOffset * 0.4f;
                var world = BuildDomeAlignedWorld(scrollX, scrollZ, layer);

                skyEffect.ApplyCloudLayer(world, view, projection, cloudColor, _cloudTexture);

                FillLayerVertices(layer, cloudColor);

                foreach (var pass in skyEffect.GetCloudLayerPasses())
                {
                    pass.Apply();
                    device.DrawUserIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        _layerVertices,
                        0,
                        4,
                        _quadIndices,
                        0,
                        2);
                }
            }
        }

        private static Matrix BuildDomeAlignedWorld(float scrollX, float scrollZ, CloudLayerSpec layer)
        {
            float y = layer.Elevation * 420f + layer.HeightOffset;
            float curve = layer.Elevation * 28f;
            return Matrix.CreateTranslation(scrollX, y + curve, scrollZ);
        }

        private void FillLayerVertices(CloudLayerSpec layer, Color cloudColor)
        {
            float hw = layer.Width * 0.5f;
            float hd = layer.Depth * 0.5f;
            float bow = layer.Elevation * 16f;

            _layerVertices[0] = new VertexPositionColorTexture(new Vector3(-hw, bow, -hd), cloudColor, new Vector2(0f, 1f));
            _layerVertices[1] = new VertexPositionColorTexture(new Vector3(hw, bow, -hd), cloudColor, new Vector2(1f, 1f));
            _layerVertices[2] = new VertexPositionColorTexture(new Vector3(hw, -bow, hd), cloudColor, new Vector2(1f, 0f));
            _layerVertices[3] = new VertexPositionColorTexture(new Vector3(-hw, -bow, hd), cloudColor, new Vector2(0f, 0f));
        }

        private static Texture2D CreateCloudTexture(GraphicsDevice device)
        {
            const int size = 64;
            var data = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = x / (float)size;
                    float ny = y / (float)size;
                    float n = SimpleNoise(nx * 3.2f, ny * 2.8f);
                    n += SimpleNoise(nx * 6.1f + 0.4f, ny * 5.7f) * 0.5f;
                    n = MathHelper.Clamp(n, 0f, 1f);
                    float edge = Math.Min(Math.Min(nx, 1f - nx), Math.Min(ny, 1f - ny)) * 4f;
                    float alpha = MathHelper.Clamp(n * edge, 0f, 1f);
                    data[y * size + x] = new Color(1f, 1f, 1f, alpha * 0.75f);
                }
            }

            var texture = new Texture2D(device, size, size);
            texture.SetData(data);
            return texture;
        }

        private static float SimpleNoise(float x, float y)
        {
            float ix = MathF.Floor(x);
            float iy = MathF.Floor(y);
            float fx = x - ix;
            float fy = y - iy;
            float a = Hash(ix, iy);
            float b = Hash(ix + 1f, iy);
            float c = Hash(ix, iy + 1f);
            float d = Hash(ix + 1f, iy + 1f);
            float ux = fx * fx * (3f - 2f * fx);
            float uy = fy * fy * (3f - 2f * fy);
            return MathHelper.Lerp(MathHelper.Lerp(a, b, ux), MathHelper.Lerp(c, d, ux), uy);
        }

        private static float Hash(float x, float y)
        {
            int h = (int)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }

        public void Dispose()
        {
            _cloudTexture.Dispose();
        }
    }
}

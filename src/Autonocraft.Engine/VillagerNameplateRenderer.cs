using System;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Core;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.World;
using Vector3 = System.Numerics.Vector3;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace Autonocraft.Engine
{
    public sealed class VillagerNameplateRenderer : IDisposable
    {
        private const float MaxDistance = 24f;

        private readonly GraphicsDevice _device;
        private readonly SpriteBatch _spriteBatch;
        private readonly Texture2D _whiteTexture;

        public VillagerNameplateRenderer(GraphicsDevice device, Texture2D white)
        {
            _device = device;
            _whiteTexture = white;
            _spriteBatch = new SpriteBatch(device);
        }

        public void Draw(GameRenderContext ctx, float screenWidth, float screenHeight)
        {
            if (ctx.Villagers == null)
            {
                return;
            }

            float alpha = ctx.VillageUiOpen ? 0.35f : 0.92f;
            if (alpha <= 0.01f)
            {
                return;
            }

            float aspect = screenWidth / screenHeight;
            var view = ctx.Camera.GetViewMatrix();
            var proj = ctx.Camera.GetProjectionMatrix(aspect, ChunkLod.GetProjectionFarPlane(ctx.RenderDistance));
            var monoView = ConvertMatrix(view);
            var monoProj = ConvertMatrix(proj);
            var viewProjection = monoView * monoProj;

            var cameraPos = ctx.Camera.Position;
            var villagers = ctx.Villagers.GetVillagersInRange(cameraPos, MaxDistance);

            _device.DepthStencilState = DepthStencilState.None;
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            foreach (var villager in villagers)
            {
                var worldPos = villager.Position + new Vector3(0f, Villager.Height + 0.25f, 0f);
                if (!TryProject(worldPos, viewProjection, screenWidth, screenHeight, out float sx, out float sy))
                {
                    continue;
                }

                string line1 = villager.Name.ToUpperInvariant();
                string line2 = UI.Village.VillagerActivityText.Describe(villager).ToUpperInvariant();
                string line3 = UI.Village.VillagerActivityText.DescribeProgress(villager);
                if (!string.IsNullOrEmpty(line3))
                {
                    line2 += $" · {line3.ToUpperInvariant()}";
                }

                float pixelSize = 0.95f;
                float line1Width = PixelFont.MeasureString(line1, pixelSize);
                float line2Width = PixelFont.MeasureString(line2, pixelSize * 0.85f);
                float pillW = MathF.Max(line1Width, line2Width) + 12f;
                float pillH = 7f * pixelSize + 7f * pixelSize * 0.85f + 10f;
                float pillX = sx - pillW / 2f;
                float pillY = sy - pillH - 4f;

                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)pillX, (int)pillY, (int)pillW, (int)pillH), new Color(0.04f, 0.06f, 0.09f) * (0.88f * alpha));
                DrawRectOutline(pillX, pillY, pillW, pillH, 1f, VillagerVisuals.GetRoleColor(villager.Role), alpha * 0.65f);
                PixelFont.DrawString(_spriteBatch, _whiteTexture, line1, pillX + 6f, pillY + 4f, pixelSize, new Color(0.92f, 0.94f, 0.98f), alpha);
                PixelFont.DrawString(_spriteBatch, _whiteTexture, line2, pillX + 6f, pillY + 4f + 7f * pixelSize, pixelSize * 0.85f, new Color(0.62f, 0.72f, 0.78f), alpha * 0.95f);
            }

            _spriteBatch.End();
        }

        private static bool TryProject(Vector3 worldPos, Matrix viewProjection, float screenWidth, float screenHeight, out float screenX, out float screenY)
        {
            var wp = new Microsoft.Xna.Framework.Vector4(worldPos.X, worldPos.Y, worldPos.Z, 1f);
            var clip = Microsoft.Xna.Framework.Vector4.Transform(wp, viewProjection);
            if (clip.W <= 0.01f)
            {
                screenX = 0f;
                screenY = 0f;
                return false;
            }

            float ndcX = clip.X / clip.W;
            float ndcY = clip.Y / clip.W;
            if (ndcX < -1.1f || ndcX > 1.1f || ndcY < -1.1f || ndcY > 1.1f)
            {
                screenX = 0f;
                screenY = 0f;
                return false;
            }

            screenX = (ndcX + 1f) * 0.5f * screenWidth;
            screenY = (1f - ndcY) * 0.5f * screenHeight;
            return true;
        }

        private void DrawRectOutline(float x, float y, float w, float h, float thickness, Color color, float alpha)
        {
            var drawCol = color * alpha;
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)y, (int)w, (int)thickness), drawCol);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)(y + h - thickness), (int)w, (int)thickness), drawCol);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)x, (int)(y + thickness), (int)thickness, (int)(h - 2 * thickness)), drawCol);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(x + w - thickness), (int)(y + thickness), (int)thickness, (int)(h - 2 * thickness)), drawCol);
        }

        private static Matrix ConvertMatrix(System.Numerics.Matrix4x4 m) =>
            new Matrix(m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);

        public void Dispose() => _spriteBatch.Dispose();
    }
}

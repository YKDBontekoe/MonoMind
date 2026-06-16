using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Items;
using Autonocraft.World;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace Autonocraft.Engine
{
    /// <summary>Draws inventory-style item stacks using the same visuals as the HUD hotbar.</summary>
    public sealed class UiItemStackRenderer : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly BasicEffect _effect;

        public UiItemStackRenderer(GraphicsDevice device)
        {
            _device = device;
            _effect = new BasicEffect(device)
            {
                TextureEnabled = true,
                VertexColorEnabled = true,
                LightingEnabled = false
            };
        }

        public void DrawStack(
            Texture2D atlas,
            UiRenderer ui,
            ItemStack stack,
            Rectangle rect,
            UiLayout layout,
            float alpha = 1f,
            bool dimmed = false)
        {
            if (stack.IsEmpty)
            {
                return;
            }

            float itemAlpha = dimmed ? alpha * 0.45f : alpha;

            if (stack.IsBlock())
            {
                float cx = rect.X + rect.Width / 2f;
                float cy = rect.Y + rect.Height / 2f;
                DrawIsometricBlock(atlas, cx, cy + layout.S(6f), layout.S(13f), stack.BlockType, itemAlpha);
            }
            else if (stack.IsTool())
            {
                DrawToolIcon(ui, atlas, rect, stack, itemAlpha);
                DrawDurabilityBar(ui, rect, stack, layout, itemAlpha);
            }
            else if (stack.IsFluidContainer())
            {
                DrawFluidContainerIcon(ui, rect, stack, layout, itemAlpha);
            }
            else if (stack.IsMaterial())
            {
                DrawMaterialIcon(ui, stack, rect, layout, itemAlpha);
            }
            else if (stack.IsFood())
            {
                DrawFoodIcon(ui, stack, rect, layout, itemAlpha);
            }

            if (stack.Count > 1 || stack.IsFood() || stack.IsMaterial())
            {
                DrawStackCount(ui, stack.Count, rect, layout, itemAlpha);
            }
        }

        private static void DrawStackCount(
            UiRenderer ui,
            int count,
            Rectangle rect,
            UiLayout layout,
            float alpha)
        {
            string countStr = count.ToString();
            float fontSize = layout.S(UiTheme.FontSmall);
            float textWidth = ui.MeasureString(countStr, fontSize);
            float textX = rect.Right - textWidth - layout.S(4f);
            float textY = rect.Bottom - fontSize - layout.S(2f);
            ui.DrawString(countStr, textX + 1f, textY + 1f, fontSize, Color.Black, alpha * 0.75f);
            ui.DrawString(countStr, textX, textY, fontSize, UiTheme.HudTextPrimary, alpha);
        }

        private static void DrawDurabilityBar(
            UiRenderer ui,
            Rectangle rect,
            ItemStack stack,
            UiLayout layout,
            float alpha)
        {
            if (!stack.IsTool() || stack.MaxDurability <= 0)
            {
                return;
            }

            float durRatio = Math.Clamp(stack.Durability / (float)stack.MaxDurability, 0f, 1f);
            float durBarH = layout.S(3f);
            float durBarW = rect.Width - layout.S(6f);
            float durBarX = rect.X + layout.S(3f);
            float durBarY = rect.Bottom - durBarH - layout.S(3f);
            ui.DrawFilledRect(durBarX, durBarY, durBarW, durBarH, new Color(0.08f, 0.09f, 0.11f) * (0.95f * alpha));
            Color durabilityColor = durRatio > 0.5f
                ? new Color(0.2f, 0.85f, 0.35f)
                : durRatio > 0.2f
                    ? new Color(0.95f, 0.75f, 0.15f)
                    : new Color(0.95f, 0.25f, 0.2f);
            ui.DrawFilledRect(durBarX, durBarY, durBarW * durRatio, durBarH, durabilityColor * alpha);
        }

        private static void DrawToolIcon(UiRenderer ui, Texture2D atlas, Rectangle rect, ItemStack tool, float alpha)
        {
            if (!tool.IsTool())
            {
                return;
            }

            string tileId = ToolRegistry.GetAtlasTileId(tool.ToolId);
            var tile = BlockAtlas.LayoutData.GetTile(tileId);
            int atlasTileSize = BlockAtlas.LayoutData.TileSize;
            var source = new Rectangle(tile.Col * atlasTileSize, tile.Row * atlasTileSize, atlasTileSize, atlasTileSize);

            float pad = rect.Width * 0.12f;
            ui.DrawAtlasTile(
                atlas,
                new Rectangle(
                    (int)(rect.X + pad),
                    (int)(rect.Y + pad),
                    (int)(rect.Width - pad * 2f),
                    (int)(rect.Height - pad * 2f)),
                source,
                alpha);
        }

        private static void DrawFluidContainerIcon(UiRenderer ui, Rectangle rect, ItemStack stack, UiLayout layout, float alpha)
        {
            float pad = layout.S(8f);
            Color color = stack.IsWaterBucket()
                ? new Color(0.28f, 0.48f, 0.92f)
                : new Color(0.62f, 0.58f, 0.52f);
            ui.DrawFilledRect(rect.X + pad, rect.Y + pad, rect.Width - pad * 2f, rect.Height - pad * 2f, color * alpha);
        }

        private static void DrawMaterialIcon(UiRenderer ui, ItemStack stack, Rectangle rect, UiLayout layout, float alpha)
        {
            float pad = layout.S(8f);
            if (stack.MaterialId == ItemId.Stick)
            {
                float stickW = layout.S(4f);
                float stickH = rect.Height - pad * 2f;
                float stickX = rect.X + rect.Width / 2f - stickW / 2f;
                float stickY = rect.Y + pad;
                ui.DrawFilledRect(stickX, stickY, stickW, stickH, new Color(0.62f, 0.45f, 0.24f) * alpha);
                ui.DrawFilledRect(stickX - layout.S(1f), stickY + layout.S(3f), stickW + layout.S(2f), stickH - layout.S(6f),
                    new Color(0.72f, 0.54f, 0.30f) * alpha);
                return;
            }

            ui.DrawFilledRect(rect.X + pad, rect.Y + pad, rect.Width - pad * 2f, rect.Height - pad * 2f, UiTheme.StatValue * alpha);
        }

        private static void DrawFoodIcon(UiRenderer ui, ItemStack stack, Rectangle rect, UiLayout layout, float alpha)
        {
            float pad = layout.S(8f);
            Color color = stack.FoodId switch
            {
                ItemId.RawMeat => new Color(0.78f, 0.32f, 0.28f),
                ItemId.CookedMeat => new Color(0.62f, 0.38f, 0.24f),
                ItemId.Bread => new Color(0.78f, 0.62f, 0.28f),
                _ => UiTheme.StatValue
            };
            ui.DrawFilledRect(rect.X + pad, rect.Y + pad, rect.Width - pad * 2f, rect.Height - pad * 2f, color * alpha);
        }

        private void DrawIsometricBlock(Texture2D atlas, float cx, float cy, float r, BlockType type, float alpha)
        {
            _effect.Texture = atlas;
            _effect.View = Matrix.CreateLookAt(new Vector3(0, 0, 1), Vector3.Zero, Vector3.Up);
            _effect.Projection = Matrix.CreateOrthographicOffCenter(0, _device.Viewport.Width, _device.Viewport.Height, 0, -1, 1);
            _effect.World = Matrix.Identity;

            float h = r * 0.5f;
            float w = r * 0.866f;

            var pTop = new Vector3(cx, cy - r, 0f);
            var pBottom = new Vector3(cx, cy + r, 0f);
            var pLeft = new Vector3(cx - w, cy - h, 0f);
            var pRight = new Vector3(cx + w, cy - h, 0f);
            var pCenter = new Vector3(cx, cy, 0f);
            var pBottomLeft = new Vector3(cx - w, cy + h, 0f);
            var pBottomRight = new Vector3(cx + w, cy + h, 0f);

            var uvTop = BlockAtlas.GetFaceUVs(type, new System.Numerics.Vector3(0f, 1f, 0f));
            var uvSide = BlockAtlas.GetFaceUVs(type, new System.Numerics.Vector3(0f, 0f, 1f));

            var vertices = new VertexPositionColorTexture[12];

            var topColor = Color.White * alpha;
            vertices[0] = new VertexPositionColorTexture(pTop, topColor, new Vector2(uvTop.uMin, uvTop.vMin));
            vertices[1] = new VertexPositionColorTexture(pLeft, topColor, new Vector2(uvTop.uMin, uvTop.vMax));
            vertices[2] = new VertexPositionColorTexture(pCenter, topColor, new Vector2(uvTop.uMax, uvTop.vMax));
            vertices[3] = new VertexPositionColorTexture(pRight, topColor, new Vector2(uvTop.uMax, uvTop.vMin));

            var leftColor = new Color(0.8f, 0.8f, 0.8f, alpha);
            vertices[4] = new VertexPositionColorTexture(pLeft, leftColor, new Vector2(uvSide.uMin, uvSide.vMin));
            vertices[5] = new VertexPositionColorTexture(pBottomLeft, leftColor, new Vector2(uvSide.uMin, uvSide.vMax));
            vertices[6] = new VertexPositionColorTexture(pBottom, leftColor, new Vector2(uvSide.uMax, uvSide.vMax));
            vertices[7] = new VertexPositionColorTexture(pCenter, leftColor, new Vector2(uvSide.uMax, uvSide.vMin));

            var rightColor = new Color(0.6f, 0.6f, 0.6f, alpha);
            vertices[8] = new VertexPositionColorTexture(pCenter, rightColor, new Vector2(uvSide.uMin, uvSide.vMin));
            vertices[9] = new VertexPositionColorTexture(pBottom, rightColor, new Vector2(uvSide.uMin, uvSide.vMax));
            vertices[10] = new VertexPositionColorTexture(pBottomRight, rightColor, new Vector2(uvSide.uMax, uvSide.vMax));
            vertices[11] = new VertexPositionColorTexture(pRight, rightColor, new Vector2(uvSide.uMax, uvSide.vMin));

            var indices = new short[]
            {
                0, 1, 2, 0, 2, 3,
                4, 5, 6, 4, 6, 7,
                8, 9, 10, 8, 10, 11
            };

            _device.DepthStencilState = DepthStencilState.None;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SamplerStates[0] = SamplerState.PointClamp;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, 12, indices, 0, 6);
            }

        }

        public void Dispose() => _effect.Dispose();
    }

}


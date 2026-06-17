using System;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Domain.Core;
using Autonocraft.Engine.Animation;
using Autonocraft.Items;
using Autonocraft.World;
using Autonocraft.Village;
using Vector3 = System.Numerics.Vector3;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace Autonocraft.Engine
{
    public sealed partial class HudRenderer : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly BasicEffect _hudEffect;
        private readonly SpriteBatch _spriteBatch;
        private Texture2D _atlasTexture;
        private readonly Texture2D _whiteTexture;
        private UiTypography? _typography;

        public void SetTypography(UiTypography typography) => _typography = typography;

        public HudRenderer(GraphicsDevice device, Texture2D atlas, Texture2D white)
        {
            _device = device;
            _atlasTexture = atlas;
            _whiteTexture = white;
            _spriteBatch = new SpriteBatch(device);

            _hudEffect = new BasicEffect(device)
            {
                TextureEnabled = true,
                Texture = atlas,
                VertexColorEnabled = true,
                LightingEnabled = false
            };
        }

        public void Draw(GameRenderContext ctx, float sw, float sh)
        {
            var swUi = System.Diagnostics.Stopwatch.StartNew();
            DrawCore(ctx, sw, sh);
            if (PerfCounters.ShowPerfHud)
            {
                _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                DrawPerformanceHud(new UiLayout(sw, sh), ctx);
                _spriteBatch.End();
            }
            swUi.Stop();
            PerfCounters.DrawUiMs = (float)swUi.Elapsed.TotalMilliseconds;
        }

        private void DrawCore(GameRenderContext ctx, float sw, float sh)
        {
            var layout = new UiLayout(sw, sh);
            float cx = layout.CenterX;
            float cy = layout.CenterY;
            var player = ctx.Player;
            var interaction = ctx.BlockInteraction;
            var animator = ctx.InteractionAnimator;
            int activeChunksCount = ctx.Grid.ActiveChunkCount;
            float hotbarPulse = interaction.HotbarPulseScale * (player.SelectedSlot >= 0 ? animator.HotbarWiggleScale : 1f);

            float slotSize = layout.S(46f);
            float slotSpacing = layout.S(5f);
            float totalWidth = (9 * slotSize) + (8 * slotSpacing);
            float hotbarXMin = cx - totalWidth / 2f;
            float hotbarYMin = layout.Height - layout.S(68f);
            float hotbarPad = layout.S(12f);

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            DrawHudBottomVignette(layout);
            DrawHudCrosshair(layout, cx, cy, interaction, animator);
            DrawHudCompass(layout, cx, player.Yaw);
            DrawVillageCompassMarker(layout, cx, layout.Padding, layout.S(34f), ctx);
            DrawHudTimeBadge(layout, ctx.TimeOfDay);
            DrawHudStatusCard(layout, player, activeChunksCount);
            DrawHudModeBadge(layout, player);
            ctx.HudToast?.Draw(_spriteBatch, _whiteTexture, layout, hotbarYMin - hotbarPad);

            float plateX = hotbarXMin - hotbarPad;
            float plateY = hotbarYMin - hotbarPad;
            float plateW = totalWidth + hotbarPad * 2f;
            float plateH = slotSize + hotbarPad * 2f;
            DrawHudGlassPanel(_spriteBatch, plateX, plateY, plateW, plateH, UiTheme.Accent, UiTheme.HudGlassAlpha);
            _spriteBatch.Draw(
                _whiteTexture,
                new Rectangle((int)plateX, (int)plateY, (int)plateW, (int)Math.Max(1f, layout.S(2f))),
                UiTheme.Accent * 0.35f);

            for (int i = 0; i < 9; i++)
            {
                float slotXMin = hotbarXMin + i * (slotSize + slotSpacing);
                bool selected = i == player.SelectedSlot;
                Color slotFill = selected
                    ? UiTheme.HudSlotSelected * 0.95f
                    : UiTheme.HudSlotFill * 0.92f;
                Color slotBorder = selected
                    ? UiTheme.Accent
                    : UiTheme.HudSlotBorder;
                float borderAlpha = selected ? 0.95f : 0.65f;

                UiDrawingUtils.DrawRoundedPanel(_spriteBatch, _whiteTexture, slotXMin, hotbarYMin, slotSize, slotSize, slotFill, slotBorder, borderAlpha, 1f, UiTheme.RadiusMd);

                if (selected)
                {
                    float bracketPad = layout.S(2f) * hotbarPulse;
                    float bracketSize = (slotSize + layout.S(4f)) * hotbarPulse;
                    DrawCornerBrackets(
                        _spriteBatch,
                        slotXMin - bracketPad,
                        hotbarYMin - bracketPad,
                        bracketSize,
                        bracketSize,
                        layout.S(8f),
                        layout.S(2f),
                        UiTheme.Accent,
                        0.95f);
                    UiDrawingUtils.DrawRoundedRect(_spriteBatch, _whiteTexture, slotXMin, hotbarYMin, slotSize, slotSize, UiTheme.RadiusMd, UiTheme.Accent * 0.10f);
                }
            }

            _spriteBatch.End();

            _hudEffect.View = Matrix.CreateLookAt(new Microsoft.Xna.Framework.Vector3(0, 0, 1), Microsoft.Xna.Framework.Vector3.Zero, Microsoft.Xna.Framework.Vector3.Up);
            _hudEffect.Projection = Matrix.CreateOrthographicOffCenter(0, sw, sh, 0, -1, 1);
            _hudEffect.World = Matrix.Identity;
            _device.DepthStencilState = DepthStencilState.None;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.SamplerStates[0] = SamplerState.PointClamp;

            for (int i = 0; i < 9; i++)
            {
                var slotItem = player.GetHotbarSlot(i);
                if (slotItem.IsBlock())
                {
                    float slotXMin = hotbarXMin + i * (slotSize + slotSpacing);
                    float slotCx = slotXMin + slotSize / 2f;
                    float slotCy = hotbarYMin + slotSize / 2f;
                    DrawIsometricBlock(slotCx, slotCy + layout.S(8f), layout.S(14f), slotItem.BlockType);
                }
            }



            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);

            DrawHudStatusCardText(layout, player, activeChunksCount);
            DrawHudModeBadgeText(layout, player);
            DrawKeyHintsBar(layout, ctx);

            if (!string.IsNullOrEmpty(ctx.NearbyClaimHint))
            {
                string claimHint = ctx.NearbyClaimHint!;
                float claimSize = layout.S(0.9f);
                float claimWidth = MeasureHudText(claimHint, claimSize);
                DrawHudText(_spriteBatch, _whiteTexture, claimHint, cx - claimWidth / 2f, hotbarYMin - layout.S(52f), claimSize, UiTheme.AccentGlow, 0.95f);
            }

            var selectedStack = player.GetSelectedStack();
            if (!selectedStack.IsEmpty)
            {
                string activeName = selectedStack.GetDisplayName().ToUpperInvariant();
                string labelText = selectedStack.IsTool()
                    ? activeName
                    : $"{activeName} ({selectedStack.Count})";
                float activeLabelSize = layout.S(1.1f);
                float labelWidth = MeasureHudText(labelText, activeLabelSize);
                float pillPadX = layout.S(10f);
                float pillPadY = layout.S(5f);
                float pillH = 7f * activeLabelSize + pillPadY * 2f;
                float pillW = labelWidth + pillPadX * 2f;
                float pillX = cx - pillW / 2f;
                float pillY = hotbarYMin - layout.S(34f);
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)pillX, (int)pillY, (int)pillW, (int)pillH), UiTheme.HudGlassFill * 0.92f);
                DrawRectOutline(_spriteBatch, pillX, pillY, pillW, pillH, 1f, UiTheme.HudGlassBorder, 0.75f);
                DrawHudText(_spriteBatch, _whiteTexture, labelText, pillX + pillPadX, pillY + pillPadY, activeLabelSize, UiTheme.HudTextPrimary, 0.95f);
            }
            else if (!string.IsNullOrEmpty(ctx.VillageHudHint))
            {
                string hint = ctx.VillageHudHint!;
                float hintSize = layout.S(0.95f);
                float hintWidth = MeasureHudText(hint, hintSize);
                DrawHudText(_spriteBatch, _whiteTexture, hint, cx - hintWidth / 2f, hotbarYMin - layout.S(30f), hintSize, UiTheme.AccentGlow, 0.92f);
            }
            else if (!string.IsNullOrEmpty(ctx.HudPlacementHint))
            {
                string hint = ctx.HudPlacementHint!;
                float hintSize = layout.S(0.95f);
                float hintWidth = MeasureHudText(hint, hintSize);
                DrawHudText(_spriteBatch, _whiteTexture, hint, cx - hintWidth / 2f, hotbarYMin - layout.S(30f), hintSize, UiTheme.AccentGlow, 0.92f);
            }
            else if (ctx.Crafting.ShowCraftingHint)
            {
                string hint = "BUILD PATTERNS  SHIFT+CLICK TO AWAKEN";
                float hintSize = layout.S(0.95f);
                float hintWidth = MeasureHudText(hint, hintSize);
                DrawHudText(_spriteBatch, _whiteTexture, hint, cx - hintWidth / 2f, hotbarYMin - layout.S(30f), hintSize, UiTheme.AccentGlow, 0.88f);
            }

            float keyLabelSize = layout.S(UiTheme.ScaleSmall);
            float countLabelSize = layout.S(UiTheme.ScaleNormal);
            for (int i = 0; i < 9; i++)
            {
                float slotXMin = hotbarXMin + i * (slotSize + slotSpacing);
                float slotXMax = slotXMin + slotSize;
                float slotYMax = hotbarYMin + slotSize;

                string keyLabel = (i + 1).ToString();
                DrawHudText(_spriteBatch, _whiteTexture, keyLabel, slotXMin + layout.S(3f), hotbarYMin + layout.S(2f), keyLabelSize, UiTheme.HudTextSecondary, 0.85f);

                var slotItem = player.GetHotbarSlot(i);
                if (slotItem.IsEmpty)
                {
                    continue;
                }

                if (slotItem.IsTool())
                {
                    DrawToolIcon(slotXMin, hotbarYMin, slotSize, slotItem);
                    float durRatio = slotItem.MaxDurability > 0
                        ? Math.Clamp(slotItem.Durability / (float)slotItem.MaxDurability, 0f, 1f)
                        : 0f;
                    float durBarH = layout.S(3f);
                    float durBarY = slotYMax - durBarH - layout.S(2f);
                    float durBarW = slotSize - layout.S(4f);
                    float durBarX = slotXMin + layout.S(2f);
                    _spriteBatch.Draw(_whiteTexture, new Rectangle((int)durBarX, (int)durBarY, (int)durBarW, (int)durBarH), new Color(0.08f, 0.09f, 0.11f) * 0.95f);
                    Color durabilityColor = durRatio > 0.5f
                        ? new Color(0.2f, 0.85f, 0.35f)
                        : durRatio > 0.2f
                            ? new Color(0.95f, 0.75f, 0.15f)
                            : new Color(0.95f, 0.25f, 0.2f);
                    _spriteBatch.Draw(_whiteTexture, new Rectangle((int)durBarX, (int)durBarY, (int)(durBarW * durRatio), (int)durBarH), durabilityColor);
                    continue;
                }

                string countStr = slotItem.Count.ToString();
                float textX = slotXMax - MeasureHudText(countStr, countLabelSize) - layout.S(3f);
                float textY = slotYMax - layout.S(10f);
                DrawHudText(_spriteBatch, _whiteTexture, countStr, textX + 1f, textY + 1f, countLabelSize, Color.Black, 0.75f);
                DrawHudText(_spriteBatch, _whiteTexture, countStr, textX, textY, countLabelSize, Color.White, 1.0f);
            }


            DrawDamageOverlay(layout, animator.DamageFlashAlpha);

            _spriteBatch.End();
        }

        public void SetAtlasTexture(Texture2D atlas)
        {
            _atlasTexture = atlas;
            _hudEffect.Texture = atlas;
        }

        public void Dispose()
        {
            _hudEffect.Dispose();
            _spriteBatch.Dispose();
        }

        private void DrawHeldToolItem(UiLayout layout, float sw, float sh, IPlayerHudView player, InteractionAnimator animator)
        {
            var stack = player.GetSelectedStack();
            if (stack.IsEmpty || !stack.IsTool())
            {
                return;
            }

            float itemSize = layout.S(72f);
            float pivotX = sw - layout.S(24f);
            float pivotY = sh - layout.S(24f);
            float swingDeg = animator.GetHeldItemSwingDegrees();
            float offsetY = animator.GetHeldItemOffsetY();
            float rad = MathHelper.ToRadians(swingDeg);
            float cos = MathF.Cos(rad);
            float sin = MathF.Sin(rad);
            float half = itemSize * 0.5f;

            string tileId = ToolRegistry.GetAtlasTileId(stack.ToolId);
            var tile = BlockAtlas.LayoutData.GetTile(tileId);
            int atlasTileSize = BlockAtlas.LayoutData.TileSize;
            var source = new Rectangle(tile.Col * atlasTileSize, tile.Row * atlasTileSize, atlasTileSize, atlasTileSize);
            DrawRotatedSprite(source, pivotX, pivotY + offsetY, itemSize, rad, cos, sin, half);
        }

        private void DrawHeldBlockItem(UiLayout layout, float sw, float sh, IPlayerHudView player, InteractionAnimator animator)
        {
            var stack = player.GetSelectedStack();
            if (stack.IsEmpty || !stack.IsBlock())
            {
                return;
            }

            float itemSize = layout.S(72f);
            float pivotX = sw - layout.S(24f);
            float pivotY = sh - layout.S(24f);
            float offsetY = animator.GetHeldItemOffsetY();
            float swingDeg = animator.GetHeldItemSwingDegrees();
            float blockCx = pivotX - itemSize * 0.35f + MathF.Sin(MathHelper.ToRadians(swingDeg)) * layout.S(8f);
            float blockCy = pivotY + offsetY - itemSize * 0.35f;
            DrawIsometricBlock(blockCx, blockCy + layout.S(8f), itemSize * 0.22f, stack.BlockType);
        }


        private void DrawRotatedSprite(Rectangle source, float pivotX, float pivotY, float size, float rad, float cos, float sin, float half)
        {
            var dest = new Rectangle((int)(pivotX - half), (int)(pivotY - half), (int)size, (int)size);
            _spriteBatch.Draw(_atlasTexture, dest, source, Color.White, rad, new Microsoft.Xna.Framework.Vector2(half, half), SpriteEffects.None, 0f);
        }

        private void DrawRotatedRect(float pivotX, float pivotY, float size, float rad, float cos, float sin, float half, Color color)
        {
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(pivotX - half), (int)(pivotY - half), (int)size, (int)size), color);
        }

        private void DrawDamageOverlay(UiLayout layout, float alpha)
        {
            if (alpha <= 0.01f)
            {
                return;
            }

            float edge = layout.S(80f);
            int strips = 6;
            var damageColor = new Color(0.85f, 0.08f, 0.08f);

            for (int i = 0; i < strips; i++)
            {
                float t = i / (float)(strips - 1);
                float stripAlpha = alpha * (1f - t) * 0.55f;
                float stripSize = edge / strips;
                Color c = damageColor * stripAlpha;
                _spriteBatch.Draw(_whiteTexture, new Rectangle(0, (int)(i * stripSize), (int)stripSize, (int)layout.Height), c);
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(layout.Width - stripSize), (int)(i * stripSize), (int)stripSize, (int)layout.Height), c);
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(i * stripSize), 0, (int)layout.Width, (int)stripSize), c);
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(i * stripSize), (int)(layout.Height - stripSize), (int)layout.Width, (int)stripSize), c);
            }

            _spriteBatch.Draw(_whiteTexture, new Rectangle(0, 0, (int)layout.Width, (int)layout.Height), damageColor * (alpha * 0.12f));
        }

        private void DrawHudBottomVignette(UiLayout layout)
        {
            float vignetteH = layout.S(120f);
            int strips = 8;
            for (int i = 0; i < strips; i++)
            {
                float t = i / (float)(strips - 1);
                float alpha = 0.28f * t * t;
                float stripH = vignetteH / strips;
                float y = layout.Height - vignetteH + i * stripH;
                _spriteBatch.Draw(
                    _whiteTexture,
                    new Rectangle(0, (int)y, (int)layout.Width, (int)Math.Ceiling(stripH) + 1),
                    UiTheme.HudGlassFill * alpha);
            }
        }

        private void DrawHudCrosshair(UiLayout layout, float cx, float cy, IBlockInteractionOverlay interaction, InteractionAnimator animator)
        {
            cx += animator.InvalidShakePhase * layout.S(6f);
            cy += animator.InvalidShakePhase * layout.S(3f);

            Color crosshairColor = interaction.Crosshair switch
            {
                CrosshairState.Mining => new Color(1.0f, 0.72f, 0.28f),
                CrosshairState.Melee => new Color(1.0f, 0.45f, 0.22f),
                CrosshairState.ValidPlace => new Color(0.35f, 1.0f, 0.45f),
                CrosshairState.InvalidPlace => new Color(1.0f, 0.32f, 0.32f),
                CrosshairState.InteractStation => new Color(0.45f, 0.85f, 1.0f),
                CrosshairState.InteractChest => new Color(0.95f, 0.75f, 0.25f),
                CrosshairState.Flash => Color.White,
                _ => new Color(0.92f, 0.94f, 0.98f)
            };

            float crosshairAlpha = 0.55f;
            float crosshairArm = layout.S(9f);
            float crosshairGap = layout.S(4f);
            if (interaction.Crosshair == CrosshairState.Mining)
            {
                crosshairArm *= 1.12f;
                crosshairAlpha = 0.85f;
            }
            else if (interaction.Crosshair == CrosshairState.Melee)
            {
                crosshairArm *= 1.08f;
                crosshairAlpha = 0.75f + 0.25f * animator.MeleeCrosshairAlpha;
            }
            else if (interaction.Crosshair == CrosshairState.Flash)
            {
                crosshairAlpha = 0.45f + 0.55f * interaction.CrosshairFlashAlpha;
            }

            float ringR = layout.S(11f);
            DrawRectOutline(_spriteBatch, cx - ringR, cy - ringR, ringR * 2f, ringR * 2f, 1f, crosshairColor, crosshairAlpha * 0.25f);

            Color ch = crosshairColor * crosshairAlpha;
            int dot = (int)Math.Max(1f, layout.S(2f));
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(cx - dot * 0.5f), (int)(cy - dot * 0.5f), dot, dot), ch);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(cx - crosshairArm), (int)(cy - 0.5f), (int)layout.S(6f), 1), ch);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(cx + crosshairGap), (int)(cy - 0.5f), (int)layout.S(6f), 1), ch);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(cx - 0.5f), (int)(cy - crosshairArm), 1, (int)layout.S(6f)), ch);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(cx - 0.5f), (int)(cy + crosshairGap), 1, (int)layout.S(6f)), ch);



            if (interaction.Crosshair == CrosshairState.InteractStation || interaction.Crosshair == CrosshairState.InteractChest)
            {
                string prompt = interaction.Crosshair == CrosshairState.InteractChest
                    ? "RIGHT-CLICK TO LOOT"
                    : "RIGHT-CLICK TO OPEN";
                float promptSize = layout.S(0.95f);
                float promptW = MeasureHudText(prompt, promptSize);
                DrawHudText(
                    _spriteBatch,
                    _whiteTexture,
                    prompt,
                    cx - promptW / 2f,
                    cy + layout.S(26f),
                    promptSize,
                    new Color(0.45f, 0.85f, 1.0f),
                    0.9f);
            }
        }

        private void DrawHudCompass(UiLayout layout, float cx, float yaw)
        {
            float compassW = layout.S(220f);
            float compassH = layout.S(34f);
            float compassX = cx - compassW / 2f;
            float compassY = layout.Padding;
            DrawHudGlassPanel(_spriteBatch, compassX, compassY, compassW, compassH, UiTheme.Accent, UiTheme.HudGlassAlpha);

            string facing = GetDirection(yaw);
            string[] dirs = { "N", "E", "S", "W" };
            string[] full = { "NORTH", "EAST", "SOUTH", "WEST" };
            float markerX = compassX + compassW / 2f;

            for (int i = 0; i < dirs.Length; i++)
            {
                bool active = full[i] == facing;
                float dirX = compassX + compassW * (0.15f + i * 0.23f);
                float dirSize = layout.S(active ? 1.15f : 0.95f);
                Color dirColor = active
                    ? UiTheme.Accent
                    : UiTheme.HudTextSecondary;
                DrawHudText(_spriteBatch, _whiteTexture, dirs[i], dirX, compassY + layout.S(10f), dirSize, dirColor, active ? 1f : 0.75f);
                if (active)
                {
                    markerX = dirX + layout.S(8f);
                }
            }

            float markerY = compassY + compassH - layout.S(6f);
            int markerW = (int)layout.S(10f);
            int markerH = (int)Math.Max(1f, layout.S(2f));
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(markerX - markerW / 2f), (int)markerY, markerW, markerH), UiTheme.Accent);
        }

        private void DrawHudTimeBadge(UiLayout layout, float timeOfDay)
        {
            float badgeW = layout.S(118f);
            float badgeH = layout.S(34f);
            float badgeX = layout.Width - layout.Padding - badgeW;
            float badgeY = layout.Padding;
            bool isDay = DayNightCycle.IsBroadDaytime(timeOfDay);
            Color accent = isDay ? new Color(0.95f, 0.78f, 0.28f) : new Color(0.45f, 0.55f, 0.92f);
            DrawHudGlassPanel(_spriteBatch, badgeX, badgeY, badgeW, badgeH, accent, 0.84f);

            float iconR = layout.S(6f);
            float iconCx = badgeX + layout.S(18f);
            float iconCy = badgeY + badgeH / 2f;
            _spriteBatch.Draw(
                _whiteTexture,
                new Rectangle((int)(iconCx - iconR), (int)(iconCy - iconR), (int)(iconR * 2f), (int)(iconR * 2f)),
                accent * 0.95f);

            string timeLabel = DayNightCycle.GetHudTimeLabel(timeOfDay);
            float textSize = layout.S(0.95f);
            DrawHudText(_spriteBatch, _whiteTexture, timeLabel, badgeX + layout.S(34f), badgeY + layout.S(10f), textSize, UiTheme.HudTextPrimary, 0.95f);
        }

        private void DrawHudStatusCard(UiLayout layout, IPlayerHudView player, int activeChunksCount)
        {
            float cardW = layout.S(168f);
            float cardH = layout.S(124f);
            float cardX = layout.Padding;
            float cardY = layout.Height - layout.S(204f);
            float hpRatio = Math.Clamp(player.Health / player.MaxHealth, 0f, 1f);
            bool lowHealth = hpRatio < 0.25f && player.Health > 0f;

            Color accent = lowHealth ? new Color(1.0f, 0.2f, 0.3f) : new Color(0.95f, 0.25f, 0.35f);
            DrawHudGlassPanel(_spriteBatch, cardX, cardY, cardW, cardH, accent, 0.84f);

            float barX = cardX + layout.S(14f);
            float barY = cardY + layout.S(16f);
            float barW = cardW - layout.S(28f);
            float barH = layout.S(10f);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(barX - 1), (int)(barY - 1), (int)(barW + 2), (int)(barH + 2)), UiTheme.HudGlassBorder * 0.55f);
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)barX, (int)barY, (int)barW, (int)barH), UiTheme.HudBarTrack * 0.95f);
            if (hpRatio > 0.01f)
            {
                Color fill = lowHealth ? new Color(1.0f, 0.18f, 0.28f) : new Color(0.92f, 0.18f, 0.32f);
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)barX, (int)barY, (int)(barW * hpRatio), (int)barH), fill);
                float highlightH = Math.Max(1f, barH * 0.35f);
                _spriteBatch.Draw(
                    _whiteTexture,
                    new Rectangle((int)barX, (int)barY, (int)(barW * hpRatio), (int)highlightH),
                    Color.White * 0.18f);
            }

            DrawRectOutline(_spriteBatch, barX, barY, barW, barH, 1f, new Color(0.72f, 0.42f, 0.46f), 0.65f);

            float hungerY = barY + barH + layout.S(8f);
            if (!player.CreativeMode)
            {
                float hungerRatio = Math.Clamp(player.Hunger / player.MaxHunger, 0f, 1f);
                bool lowHunger = hungerRatio < SurvivalConstants.HungerWarningFraction && player.Hunger > 0f;
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(barX - 1), (int)(hungerY - 1), (int)(barW + 2), (int)(barH + 2)), UiTheme.HudGlassBorder * 0.55f);
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)barX, (int)hungerY, (int)barW, (int)barH), UiTheme.HudBarTrack * 0.95f);
                if (hungerRatio > 0.01f)
                {
                    Color hungerFill = lowHunger ? new Color(0.95f, 0.55f, 0.15f) : new Color(0.88f, 0.62f, 0.18f);
                    _spriteBatch.Draw(_whiteTexture, new Rectangle((int)barX, (int)hungerY, (int)(barW * hungerRatio), (int)barH), hungerFill);
                }

                DrawRectOutline(_spriteBatch, barX, hungerY, barW, barH, 1f, new Color(0.72f, 0.58f, 0.36f), 0.65f);
            }

            if (player.HeadUnderwater)
            {
                float o2Ratio = Math.Clamp(player.Oxygen / PlayerConstants.MaxOxygen, 0f, 1f);
                float o2Y = barY + barH + layout.S(8f);
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)(barX - 1), (int)(o2Y - 1), (int)(barW + 2), (int)(barH + 2)), UiTheme.HudGlassBorder * 0.55f);
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)barX, (int)o2Y, (int)barW, (int)barH), UiTheme.HudBarTrack * 0.95f);
                if (o2Ratio > 0.01f)
                {
                    Color o2Fill = o2Ratio < 0.25f ? new Color(0.95f, 0.35f, 0.25f) : new Color(0.25f, 0.65f, 0.95f);
                    _spriteBatch.Draw(_whiteTexture, new Rectangle((int)barX, (int)o2Y, (int)(barW * o2Ratio), (int)barH), o2Fill);
                }

                DrawRectOutline(_spriteBatch, barX, o2Y, barW, barH, 1f, new Color(0.42f, 0.58f, 0.72f), 0.65f);
            }

            float skillY = cardY + layout.S(58f);
            float skillLineH = layout.S(18f);
            DrawSkillBar(layout, "MIN", player.Skills.Mining, cardX + layout.S(14f), skillY, cardW - layout.S(28f), skillLineH);
            DrawSkillBar(layout, "WDC", player.Skills.Woodcutting, cardX + layout.S(14f), skillY + skillLineH, cardW - layout.S(28f), skillLineH);
            DrawSkillBar(layout, "CMB", player.Skills.Combat, cardX + layout.S(14f), skillY + skillLineH * 2f, cardW - layout.S(28f), skillLineH);
        }

        private void DrawHudStatusCardText(UiLayout layout, IPlayerHudView player, int activeChunksCount)
        {
            float cardW = layout.S(168f);
            float cardH = layout.S(124f);
            float cardX = layout.Padding;
            float cardY = layout.Height - layout.S(204f);
            float hpTextSize = layout.S(0.95f);
            string hpText = $"{MathF.Round(player.Health)}/{MathF.Round(player.MaxHealth)}";
            DrawHudText(_spriteBatch, _whiteTexture, "HEALTH", cardX + layout.S(14f), cardY + layout.S(4f), hpTextSize, UiTheme.HudTextSecondary, 0.9f);
            if (!player.CreativeMode)
            {
                string hungerText = $"{MathF.Round(player.Hunger)}/{MathF.Round(player.MaxHunger)}";
                DrawHudText(_spriteBatch, _whiteTexture, "FOOD", cardX + layout.S(14f), cardY + layout.S(28f), layout.S(UiTheme.ScaleSmall), UiTheme.HudTextSecondary, 0.9f);
                float hungerTextW = MeasureHudText(hungerText, layout.S(UiTheme.ScaleSmall));
                DrawHudText(_spriteBatch, _whiteTexture, hungerText, cardX + cardW - layout.S(14f) - hungerTextW, cardY + layout.S(28f), layout.S(UiTheme.ScaleSmall), new Color(0.95f, 0.72f, 0.28f), 0.95f);
            }

            if (player.HeadUnderwater)
            {
                DrawHudText(_spriteBatch, _whiteTexture, "O2", cardX + layout.S(14f), cardY + layout.S(28f), layout.S(UiTheme.ScaleSmall), UiTheme.HudTextSecondary, 0.9f);
            }
            float hpTextW = MeasureHudText(hpText, hpTextSize);
            DrawHudText(_spriteBatch, _whiteTexture, hpText, cardX + cardW - layout.S(14f) - hpTextW, cardY + layout.S(4f), hpTextSize, new Color(0.95f, 0.35f, 0.42f), 0.95f);

            string posText = $"{player.Position.X:F0} {player.Position.Y:F0} {player.Position.Z:F0}";
            float metaSize = layout.S(UiTheme.ScaleSmall);
            float metaY = cardY + cardH - layout.S(12f);
            DrawHudText(_spriteBatch, _whiteTexture, posText, cardX + layout.S(14f), metaY, metaSize, UiTheme.HudTextSecondary, 0.9f);
            string chunkText = $"{activeChunksCount} CHK";
            float chunkW = MeasureHudText(chunkText, metaSize);
            DrawHudText(_spriteBatch, _whiteTexture, chunkText, cardX + cardW - layout.S(14f) - chunkW, metaY, metaSize, UiTheme.HudTextSecondary, 0.9f);
        }

        private void DrawHudModeBadge(UiLayout layout, IPlayerHudView player)
        {
            float badgeW = layout.S(118f);
            float badgeH = layout.S(34f);
            float badgeX = layout.Width - layout.Padding - badgeW;
            float badgeY = layout.Height - layout.S(188f);
            Color accent = player.CreativeMode ? UiTheme.Accent : new Color(0.95f, 0.68f, 0.22f);
            DrawHudGlassPanel(_spriteBatch, badgeX, badgeY, badgeW, badgeH, accent, 0.82f);
        }

        private void DrawHudModeBadgeText(UiLayout layout, IPlayerHudView player)
        {
            float badgeW = layout.S(118f);
            float badgeH = layout.S(34f);
            float badgeX = layout.Width - layout.Padding - badgeW;
            float badgeY = layout.Height - layout.S(188f);
            string modeLabel = player.CreativeMode ? "CREATIVE" : "SURVIVAL";
            Color modeColor = player.CreativeMode ? UiTheme.AccentGlow : new Color(0.95f, 0.72f, 0.32f);
            float textSize = layout.S(0.95f);
            float textW = MeasureHudText(modeLabel, textSize);
            DrawHudText(_spriteBatch, _whiteTexture, modeLabel, badgeX + (badgeW - textW) / 2f, badgeY + layout.S(10f), textSize, modeColor, 0.95f);

            string grounded = player.IsGrounded ? "GROUNDED" : "AIRBORNE";
            float subSize = layout.S(UiTheme.ScaleSmall);
            float subW = MeasureHudText(grounded, subSize);
            DrawHudText(_spriteBatch, _whiteTexture, grounded, badgeX + (badgeW - subW) / 2f, badgeY + badgeH + layout.S(4f), subSize, UiTheme.HudTextSecondary, 0.95f);
        }

        private void DrawHudGlassPanel(SpriteBatch sb, float x, float y, float w, float h, Color accent, float alpha)
        {
            sb.Draw(_whiteTexture, new Rectangle((int)(x + 1f), (int)(y + 2f), (int)w, (int)h), Color.Black * (0.25f * alpha));
            sb.Draw(_whiteTexture, new Rectangle((int)x, (int)y, (int)w, (int)h), UiTheme.HudGlassFill * alpha);
            float stripeH = Math.Max(2f, h * 0.04f);
            sb.Draw(_whiteTexture, new Rectangle((int)x, (int)y, (int)w, (int)stripeH), accent * 0.85f);
            DrawRectOutline(sb, x, y, w, h, 1f, UiTheme.HudGlassBorder, 0.65f);
        }

        private void DrawCornerBrackets(SpriteBatch sb, float x, float y, float w, float h, float armLen, float thickness, Color color, float alpha)
        {
            Color drawCol = color * alpha;
            sb.Draw(_whiteTexture, new Rectangle((int)x, (int)y, (int)armLen, (int)thickness), drawCol);
            sb.Draw(_whiteTexture, new Rectangle((int)x, (int)y, (int)thickness, (int)armLen), drawCol);
            sb.Draw(_whiteTexture, new Rectangle((int)(x + w - armLen), (int)y, (int)armLen, (int)thickness), drawCol);
            sb.Draw(_whiteTexture, new Rectangle((int)(x + w - thickness), (int)y, (int)thickness, (int)armLen), drawCol);
            sb.Draw(_whiteTexture, new Rectangle((int)x, (int)(y + h - thickness), (int)armLen, (int)thickness), drawCol);
            sb.Draw(_whiteTexture, new Rectangle((int)x, (int)(y + h - armLen), (int)thickness, (int)armLen), drawCol);
            sb.Draw(_whiteTexture, new Rectangle((int)(x + w - armLen), (int)(y + h - thickness), (int)armLen, (int)thickness), drawCol);
            sb.Draw(_whiteTexture, new Rectangle((int)(x + w - thickness), (int)(y + h - armLen), (int)thickness, (int)armLen), drawCol);
        }

        private void DrawSkillBar(UiLayout layout, string label, SkillProgress progress, float x, float y, float totalW, float lineH)
        {
            float labelSize = layout.S(UiTheme.ScaleSmall);
            DrawHudText(_spriteBatch, _whiteTexture, $"{label} {progress.Level}", x, y, labelSize, UiTheme.HudTextSecondary, 0.9f);
            float barW = layout.S(72f);
            float barH = layout.S(4f);
            float barX = x + totalW - barW;
            float barY = y + lineH - barH - layout.S(3f);
            float ratio = progress.ProgressToNextLevel();
            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)barX, (int)barY, (int)barW, (int)barH), UiTheme.HudBarTrack * 0.95f);
            if (ratio > 0.01f)
            {
                _spriteBatch.Draw(_whiteTexture, new Rectangle((int)barX, (int)barY, (int)(barW * ratio), (int)barH), UiTheme.HudSkillFill);
            }
        }

        private void DrawToolIcon(float slotXMin, float hotbarYMin, float slotSize, ItemStack tool)
        {
            if (!tool.IsTool())
            {
                return;
            }

            string tileId = ToolRegistry.GetAtlasTileId(tool.ToolId);
            var tile = BlockAtlas.LayoutData.GetTile(tileId);
            int atlasTileSize = BlockAtlas.LayoutData.TileSize;
            var source = new Rectangle(tile.Col * atlasTileSize, tile.Row * atlasTileSize, atlasTileSize, atlasTileSize);

            float pad = slotSize * 0.1f;
            var dest = new Rectangle(
                (int)(slotXMin + pad),
                (int)(hotbarYMin + pad),
                (int)(slotSize - pad * 2f),
                (int)(slotSize - pad * 2f));

            _spriteBatch.Draw(_atlasTexture, dest, source, Color.White);
        }

        private void DrawRectOutline(SpriteBatch sb, float x, float y, float w, float h, float thickness, Color color, float alpha)
        {
            Color drawCol = color * alpha;
            sb.Draw(_whiteTexture, new Rectangle((int)x, (int)y, (int)w, (int)thickness), drawCol);
            sb.Draw(_whiteTexture, new Rectangle((int)x, (int)(y + h - thickness), (int)w, (int)thickness), drawCol);
            sb.Draw(_whiteTexture, new Rectangle((int)x, (int)(y + thickness), (int)thickness, (int)(h - 2 * thickness)), drawCol);
            sb.Draw(_whiteTexture, new Rectangle((int)(x + w - thickness), (int)(y + thickness), (int)thickness, (int)(h - 2 * thickness)), drawCol);
        }

        private void DrawIsometricBlock(float cx, float cy, float r, BlockType type)
        {
            float h = r * 0.5f;
            float w = r * 0.866f;

            var pTop = new Microsoft.Xna.Framework.Vector3(cx, cy - r, 0f);
            var pBottom = new Microsoft.Xna.Framework.Vector3(cx, cy + r, 0f);
            var pLeft = new Microsoft.Xna.Framework.Vector3(cx - w, cy - h, 0f);
            var pRight = new Microsoft.Xna.Framework.Vector3(cx + w, cy - h, 0f);
            var pCenter = new Microsoft.Xna.Framework.Vector3(cx, cy, 0f);
            var pBottomLeft = new Microsoft.Xna.Framework.Vector3(cx - w, cy + h, 0f);
            var pBottomRight = new Microsoft.Xna.Framework.Vector3(cx + w, cy + h, 0f);

            var uvTop = BlockAtlas.GetFaceUVs(type, new Vector3(0f, 1f, 0f));
            var uvSide = BlockAtlas.GetFaceUVs(type, new Vector3(0f, 0f, 1f));

            var vertices = new VertexPositionColorTexture[12];

            vertices[0] = new VertexPositionColorTexture(pTop, Color.White, new Microsoft.Xna.Framework.Vector2(uvTop.uMin, uvTop.vMin));
            vertices[1] = new VertexPositionColorTexture(pLeft, Color.White, new Microsoft.Xna.Framework.Vector2(uvTop.uMin, uvTop.vMax));
            vertices[2] = new VertexPositionColorTexture(pCenter, Color.White, new Microsoft.Xna.Framework.Vector2(uvTop.uMax, uvTop.vMax));
            vertices[3] = new VertexPositionColorTexture(pRight, Color.White, new Microsoft.Xna.Framework.Vector2(uvTop.uMax, uvTop.vMin));

            var leftColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            vertices[4] = new VertexPositionColorTexture(pLeft, leftColor, new Microsoft.Xna.Framework.Vector2(uvSide.uMin, uvSide.vMin));
            vertices[5] = new VertexPositionColorTexture(pBottomLeft, leftColor, new Microsoft.Xna.Framework.Vector2(uvSide.uMin, uvSide.vMax));
            vertices[6] = new VertexPositionColorTexture(pBottom, leftColor, new Microsoft.Xna.Framework.Vector2(uvSide.uMax, uvSide.vMax));
            vertices[7] = new VertexPositionColorTexture(pCenter, leftColor, new Microsoft.Xna.Framework.Vector2(uvSide.uMax, uvSide.vMin));

            var rightColor = new Color(0.6f, 0.6f, 0.6f, 1f);
            vertices[8] = new VertexPositionColorTexture(pCenter, rightColor, new Microsoft.Xna.Framework.Vector2(uvSide.uMin, uvSide.vMin));
            vertices[9] = new VertexPositionColorTexture(pBottom, rightColor, new Microsoft.Xna.Framework.Vector2(uvSide.uMin, uvSide.vMax));
            vertices[10] = new VertexPositionColorTexture(pBottomRight, rightColor, new Microsoft.Xna.Framework.Vector2(uvSide.uMax, uvSide.vMax));
            vertices[11] = new VertexPositionColorTexture(pRight, rightColor, new Microsoft.Xna.Framework.Vector2(uvSide.uMax, uvSide.vMin));

            var indices = new short[]
            {
                0, 1, 2, 0, 2, 3,
                4, 5, 6, 4, 6, 7,
                8, 9, 10, 8, 10, 11
            };

            foreach (var pass in _hudEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, 12, indices, 0, 6);
            }
        }

        private void DrawKeyHintsBar(UiLayout layout, GameRenderContext ctx)
        {
            var hasVillage = ctx.Villages != null && ctx.Villages.GetActiveVillage(ctx.Player.Position) != null;
            string hintsText = "";
            if (ctx.VillageUiOpen)
            {
                hintsText = "1-4 - Switch Tab   R - Recruit   ESC - Close";
            }
            else if (!hasVillage)
            {
                hintsText = ctx.IsStructureGalleryWorld
                    ? "Fly around — every world-gen structure is on the grid"
                    : "V - Start Settlement";
            }
            else
            {
                var village = ctx.Villages?.GetActiveVillage(ctx.Player.Position);
                if (village != null)
                {
                    var dist = Vector3.Distance(ctx.Player.Position, village.Center);
                    if (dist <= village.Radius)
                    {
                        hintsText = "V - Town Board   C - Chat   Shift+LeftClick - Mark Resource";
                    }
                    else
                    {
                        hintsText = $"V - Town Board (~{(int)dist}m away)";
                    }
                }
            }

            if (string.IsNullOrEmpty(hintsText))
            {
                return;
            }

            float cardX = layout.Padding;
            float barY = layout.Height - layout.S(72f);
            float textSize = layout.S(UiTheme.ScaleSmall);

            // Draw a subtle dark background pill for the hints
            float textWidth = MeasureHudText(hintsText, textSize);
            float pillW = textWidth + layout.S(16f);
            float pillH = layout.S(20f);

            _spriteBatch.Draw(_whiteTexture, new Rectangle((int)cardX, (int)barY, (int)pillW, (int)pillH), UiTheme.HudGlassFill * 0.90f);
            DrawRectOutline(_spriteBatch, cardX, barY, pillW, pillH, 1f, UiTheme.HudGlassBorder, 0.70f);
            DrawHudText(_spriteBatch, _whiteTexture, hintsText, cardX + layout.S(8f), barY + layout.S(4f), textSize, UiTheme.HudTextSecondary, 0.95f);
        }

        private static string GetDirection(float yaw)
        {
            float angle = (yaw % 360f + 360f) % 360f;
            if (angle >= 45f && angle < 135f) return "SOUTH";
            if (angle >= 135f && angle < 225f) return "WEST";
            if (angle >= 225f && angle < 315f) return "NORTH";
            return "EAST";
        }

        private void DrawVillageCompassMarker(UiLayout layout, float cx, float compassY, float compassH, GameRenderContext ctx)
        {
            var primaryVillage = ctx.Villages?.GetActiveVillage(ctx.Player.Position);
            if (primaryVillage == null)
            {
                return;
            }

            var player = ctx.Player;
            var playerPos = player.Position;
            var villagePos = primaryVillage.Center;

            float dx = villagePos.X - playerPos.X;
            float dz = villagePos.Z - playerPos.Z;
            float distance = MathF.Sqrt(dx * dx + dz * dz);

            if (distance <= 3f)
            {
                return;
            }

            // Calculate angle from player to village in degrees
            // +X is East (0 deg), +Z is South (90 deg), -X is West (180 deg), -Z is North (270 deg)
            float angleRad = MathF.Atan2(dz, dx);
            float angleDeg = angleRad * (180f / MathF.PI);
            angleDeg = (angleDeg % 360f + 360f) % 360f;

            // Player Yaw: 0 is East, 90 is South, 180 is West, 270 is North
            float playerYawDeg = (player.Yaw % 360f + 360f) % 360f;

            float diff = angleDeg - playerYawDeg;
            diff = (diff + 180f) % 360f;
            if (diff < 0) diff += 360f;
            diff -= 180f;

            string relDir;
            if (Math.Abs(diff) <= 22.5f) relDir = "AHEAD";
            else if (Math.Abs(diff) >= 157.5f) relDir = "BEHIND";
            else if (diff > 0) relDir = "RIGHT";
            else relDir = "LEFT";

            string text = $"{primaryVillage.Name.ToUpperInvariant()} - {(int)distance} BLOCKS {relDir}";
            float textSize = layout.S(UiTheme.ScaleSmall);
            float textW = MeasureHudText(text, textSize);
            float badgeW = textW + layout.S(16f);
            float badgeH = layout.S(20f);
            float badgeX = cx - badgeW / 2f;
            float badgeY = compassY + compassH + layout.S(4f);

            DrawHudGlassPanel(_spriteBatch, badgeX, badgeY, badgeW, badgeH, UiTheme.Accent, 0.78f);
            DrawHudText(_spriteBatch, _whiteTexture, text, badgeX + layout.S(8f), badgeY + layout.S(5f), textSize, UiTheme.HudTextPrimary, 0.95f);
        }

        private float MeasureHudText(string text, float legacySize, bool semiBold = false)
        {
            if (_typography != null)
            {
                return _typography.Measure(text, legacySize * 11f, semiBold);
            }

            return PixelFont.MeasureString(text, legacySize);
        }

        private void DrawHudText(SpriteBatch sb, Texture2D tex, string text, float x, float y, float legacySize, Color color, float alpha, bool semiBold = false)
        {
            if (_typography != null)
            {
                _typography.Draw(sb, text, x, y, legacySize * 11f, color, semiBold, alpha);
                return;
            }

            PixelFont.DrawString(sb, tex, text, x, y, legacySize, color, alpha);
        }
    }
}

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Core;
using Autonocraft.Crafting;
using Autonocraft.Engine;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.UI
{
    public class CrucibleScreen
    {
        private const float SlotSize = 44f;
        private const float SlotGap = 4f;
        private const float OutputSize = 52f;
        private const float ButtonWidth = 160f;
        private const float ButtonHeight = 40f;
        private const float TransmuteDuration = 0.8f;

        private readonly UiRenderer _ui;
        private readonly RecipeBookPanel _recipeBook;
        private int _hoveredSlot = -1;
        private bool _transmuteHovered;
        private bool _transmutePressed;
        private string _statusMessage = string.Empty;
        private float _statusTimer;
        private readonly float[] _slotPulseTimers = new float[CrucibleSession.MaxSlots];
        private float _transmuteTimer;
        private bool _transmuteAnimating;

        public bool TransmuteRequested { get; private set; }
        public bool TransmuteReady { get; private set; }
        public int ClickedSlotIndex { get; private set; } = -1;
        public bool RightClickedSlot { get; private set; }
        public float TransmuteProgress => _transmuteAnimating
            ? 1f - Math.Clamp(_transmuteTimer / TransmuteDuration, 0f, 1f)
            : 0f;
        public bool TransmuteAnimating => _transmuteAnimating;

        public CrucibleScreen(UiRenderer ui)
        {
            _ui = ui;
            _recipeBook = new RecipeBookPanel(ui);
        }

        public void SetStatus(string message)
        {
            _statusMessage = message;
            _statusTimer = 2.5f;
        }

        public void TriggerSlotPulse(int slotIndex)
        {
            if (slotIndex >= 0 && slotIndex < _slotPulseTimers.Length)
            {
                _slotPulseTimers[slotIndex] = 0.15f;
            }
        }

        public void BeginTransmuteAnimation()
        {
            _transmuteAnimating = true;
            _transmuteTimer = TransmuteDuration;
            TransmuteReady = false;
        }

        public void Update(
            Viewport viewport,
            CrucibleSession session,
            CraftEnvironment env,
            DiscoveryJournal journal,
            CraftingSystem crafting,
            Player player,
            KeyboardState kb,
            MouseState mouse,
            KeyboardState prevKb,
            MouseState prevMouse,
            float deltaTime)
        {
            TransmuteRequested = false;
            TransmuteReady = false;
            ClickedSlotIndex = -1;
            RightClickedSlot = false;
            _transmuteHovered = false;
            _hoveredSlot = -1;
            _recipeBook.ResetInteraction();

            for (int i = 0; i < _slotPulseTimers.Length; i++)
            {
                if (_slotPulseTimers[i] > 0f)
                {
                    _slotPulseTimers[i] = Math.Max(0f, _slotPulseTimers[i] - deltaTime);
                }
            }

            if (_transmuteAnimating)
            {
                _transmuteTimer = Math.Max(0f, _transmuteTimer - deltaTime);
                if (_transmuteTimer <= 0f)
                {
                    _transmuteAnimating = false;
                    TransmuteReady = true;
                }
            }

            if (!session.IsOpen)
            {
                return;
            }

            if (_statusTimer > 0f)
            {
                _statusTimer -= deltaTime;
            }

            var layout = new UiLayout(viewport.Width, viewport.Height);
            float panelW = layout.S(400f);
            float panelH = layout.S(380f);
            float panelX = layout.CenterX - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f;
            float buttonX = layout.CenterX - layout.S(ButtonWidth) / 2f;
            float buttonY = panelY + panelH - layout.S(72f);

            var slotRects = BuildSlotRects(layout, panelX, panelY, session, out _);
            Point mousePt = new Point(mouse.X, mouse.Y);

            for (int i = 0; i < session.ActiveSlotCount; i++)
            {
                if (!slotRects[i].Contains(mousePt))
                {
                    continue;
                }

                _hoveredSlot = i;
                if (!_transmuteAnimating && mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
                {
                    ClickedSlotIndex = i;
                }

                if (!_transmuteAnimating && mouse.RightButton == ButtonState.Pressed && prevMouse.RightButton == ButtonState.Released)
                {
                    ClickedSlotIndex = i;
                    RightClickedSlot = true;
                }
            }

            var transmuteRect = new Rectangle(
                (int)buttonX,
                (int)buttonY,
                (int)layout.S(ButtonWidth),
                (int)layout.S(ButtonHeight));

            if (transmuteRect.Contains(mousePt))
            {
                _transmuteHovered = true;
                if (!_transmuteAnimating && mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
                {
                    TransmuteRequested = true;
                }
            }

            _transmutePressed = mouse.LeftButton == ButtonState.Pressed && _transmuteHovered;

            if (crafting.RecipeBookOpen)
            {
                var bookRect = RecipeBookPanel.BuildPanelRect(layout, panelX, panelX + panelW, panelY, panelH);
                var recipes = RecipeBookResolver.GetVisibleRecipes(
                    session.StationType,
                    session.GridSize,
                    journal);
                _recipeBook.Update(bookRect, recipes, player, session.GridSize, mouse, prevMouse);
            }
        }

        public void HandleRecipeBookClick(CraftingSystem crafting, Player player)
        {
            var recipe = _recipeBook.ConsumeClickedRecipe();
            if (recipe == null)
            {
                return;
            }

            if (!crafting.TryApplyRecipeBookSelection(recipe, player))
            {
                player.ShowToast?.Invoke("Missing ingredients for recipe");
            }
        }

        public void Draw(
            Viewport viewport,
            CrucibleSession session,
            CraftEnvironment env,
            DiscoveryJournal journal,
            CraftingSystem crafting,
            Player player,
            Texture2D? atlasTexture)
        {
            if (!session.IsOpen)
            {
                return;
            }

            var layout = new UiLayout(viewport.Width, viewport.Height);
            float panelW = layout.S(400f);
            float panelH = layout.S(380f);
            float panelX = layout.CenterX - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f;

            _ui.DrawFullscreenBackground(UiTheme.Scrim * (UiTheme.MenuScrimAlpha * 0.9f));
            _ui.DrawPanel(panelX, panelY, panelW, panelH, UiTheme.PanelBgMuted, UiTheme.PanelBorder);

            string title = session.StationType switch
            {
                BlockType.StationForge => "FORGE",
                BlockType.StationCrucible => "ALCHEMY CRUCIBLE",
                _ => "CRAFTING BENCH"
            };
            _ui.DrawCenteredText(title, panelY + layout.S(18f), layout.S(UiTheme.FontTitle), UiTheme.Title);

            var slotRects = BuildSlotRects(layout, panelX, panelY, session, out Rectangle outputRect);
            var preview = session.GetPreview(journal, env);

            for (int i = 0; i < session.ActiveSlotCount; i++)
            {
                bool hovered = _hoveredSlot == i;
                float pulseT = _slotPulseTimers[i] > 0f ? _slotPulseTimers[i] / 0.15f : 0f;
                float pulseScale = 1f + 0.1f * MathF.Sin((1f - pulseT) * MathF.PI);
                var rect = slotRects[i];
                float size = rect.Width * pulseScale;
                float offset = (size - rect.Width) / 2f;

                Color fill = hovered ? UiTheme.PanelBgHighlight : UiTheme.PanelBgMuted;
                if (_transmuteAnimating)
                {
                    float glow = 0.5f + 0.5f * MathF.Sin(TransmuteProgress * MathF.PI * 4f);
                    fill = Color.Lerp(fill, UiTheme.Accent, glow * 0.5f);
                }

                _ui.DrawPanel(rect.X - offset, rect.Y - offset, size, size, fill, hovered ? UiTheme.Accent : UiTheme.Rule);

                if (!session.InputSlots[i].IsEmpty)
                {
                    DrawStackSwatch(session.InputSlots[i], rect, layout);
                }
            }

            _ui.DrawPanel(outputRect.X, outputRect.Y, outputRect.Width, outputRect.Height, UiTheme.PanelBgMuted, UiTheme.PanelBorder);
            if (preview.HasMatch)
            {
                DrawPreviewStack(preview.Result, outputRect, layout);
            }

            if (_transmuteAnimating)
            {
                DrawTransmuteRing(layout, outputRect.Center.X, outputRect.Center.Y, TransmuteProgress);
            }

            DrawEnvironmentStrip(layout, panelX, panelY + layout.S(52f), panelW, env);

            float buttonX = layout.CenterX - layout.S(ButtonWidth) / 2f;
            float buttonY = panelY + panelH - layout.S(72f);
            _ui.DrawButton(buttonX, buttonY, layout.S(ButtonWidth), layout.S(ButtonHeight), "Craft", _transmuteHovered, _transmutePressed,
                style: UiButtonStyle.Primary, fontSize: layout.S(UiTheme.FontSection));

            if (_statusTimer > 0f && !string.IsNullOrEmpty(_statusMessage))
            {
                _ui.DrawCenteredText(_statusMessage, panelY + panelH - layout.S(28f), layout.S(UiTheme.FontBody), UiTheme.Subtitle);
            }

            const string hint = "Left-click deposit · Right-click withdraw · B recipe book · Esc close";
            _ui.DrawCenteredText(hint, panelY + panelH + layout.S(8f), layout.S(UiTheme.FontSmall), UiTheme.Hint);

            if (crafting.RecipeBookOpen)
            {
                var bookRect = RecipeBookPanel.BuildPanelRect(layout, panelX, panelX + panelW, panelY, panelH);
                var recipes = RecipeBookResolver.GetVisibleRecipes(
                    session.StationType,
                    session.GridSize,
                    journal);
                _recipeBook.Draw(layout, bookRect, recipes, journal, player, session.GridSize);
            }
        }

        private Rectangle[] BuildSlotRects(UiLayout layout, float panelX, float panelY, CrucibleSession session, out Rectangle outputRect)
        {
            int gridDim = (int)session.GridSize;
            float size = layout.S(SlotSize);
            float gap = layout.S(SlotGap);
            float gridW = gridDim * size + (gridDim - 1) * gap;
            float gridX = layout.CenterX - gridW / 2f - layout.S(40f);
            float gridY = panelY + layout.S(90f);

            var rects = new Rectangle[session.ActiveSlotCount];
            for (int i = 0; i < session.ActiveSlotCount; i++)
            {
                int row = i / gridDim;
                int col = i % gridDim;
                rects[i] = new Rectangle(
                    (int)(gridX + col * (size + gap)),
                    (int)(gridY + row * (size + gap)),
                    (int)size,
                    (int)size);
            }

            float outputSize = layout.S(OutputSize);
            outputRect = new Rectangle(
                (int)(gridX + gridW + layout.S(24f)),
                (int)(gridY + (gridW - outputSize) / 2f),
                (int)outputSize,
                (int)outputSize);
            return rects;
        }

        private void DrawPreviewStack(ItemStack stack, Rectangle rect, UiLayout layout)
        {
            DrawStackSwatch(stack, rect, layout);

            if (stack.Count > 1)
            {
                _ui.DrawString(stack.Count.ToString(), rect.Right - layout.S(18f), rect.Bottom - layout.S(16f),
                    layout.S(UiTheme.FontSmall), UiTheme.Title);
            }
        }

        private void DrawStackSwatch(ItemStack stack, Rectangle rect, UiLayout layout)
        {
            if (stack.IsBlock())
            {
                DrawBlockSwatch(stack.BlockType, rect.X + layout.S(8f), rect.Y + layout.S(8f), rect.Width - layout.S(16f));
                return;
            }

            if (stack.IsMaterial())
            {
                Color color = stack.MaterialId == ItemId.Stick
                    ? new Color(0.62f, 0.45f, 0.24f)
                    : UiTheme.StatValue;
                float pad = layout.S(8f);
                _ui.DrawFilledRect(rect.X + pad, rect.Y + pad, rect.Width - pad * 2, rect.Height - pad * 2, color);
                return;
            }

            if (stack.IsTool())
            {
                float pad = layout.S(8f);
                _ui.DrawFilledRect(rect.X + pad, rect.Y + pad, rect.Width - pad * 2, rect.Height - pad * 2, UiTheme.Accent * 0.75f);
            }
        }

        private void DrawTransmuteRing(UiLayout layout, float cx, float cy, float progress)
        {
            float radius = layout.S(36f);
            int segments = (int)(progress * 32f);
            Color ringColor = UiTheme.AccentGlow * 0.85f;
            for (int s = 0; s < segments; s++)
            {
                float angle = -MathF.PI / 2f + s * (MathF.PI * 2f / 32f);
                float px = cx + MathF.Cos(angle) * radius;
                float py = cy + MathF.Sin(angle) * radius;
                _ui.DrawFilledRect(px - 2f, py - 2f, 4f, 4f, ringColor);
            }
        }

        private void DrawEnvironmentStrip(UiLayout layout, float x, float y, float width, CraftEnvironment env)
        {
            float pad = layout.S(16f);
            string biome = env.Biome.ToString().ToUpperInvariant();
            string phase = env.TimePhase.ToString().ToUpperInvariant();
            string water = env.HasAdjacentWater ? "WATER OK" : "NO WATER";
            string heat = env.HasAdjacentHeat || env.HasFuelInInputs ? "HEAT OK" : "NO HEAT";

            Color waterColor = env.HasAdjacentWater ? new Color(0.3f, 0.85f, 1f) : UiTheme.Danger;
            Color heatColor = env.HasAdjacentHeat || env.HasFuelInInputs ? new Color(1f, 0.65f, 0.25f) : UiTheme.Danger;

            _ui.DrawString($"{biome} | {phase}", x + pad, y, layout.S(UiTheme.FontSection), UiTheme.Subtitle);
            _ui.DrawString(water, x + pad, y + layout.S(18f), layout.S(UiTheme.FontSmall), waterColor);
            _ui.DrawString(heat, x + pad + layout.S(120f), y + layout.S(18f), layout.S(UiTheme.FontSmall), heatColor);
        }

        private void DrawBlockSwatch(BlockType type, float x, float y, float size)
        {
            Color swatch = type switch
            {
                BlockType.OakLog or BlockType.BirchLog or BlockType.PineLog or BlockType.OakPlank => new Color(0.55f, 0.38f, 0.22f),
                BlockType.Stone or BlockType.Sandstone or BlockType.IronBlock or BlockType.GoldBlock or BlockType.Cobblestone => new Color(0.55f, 0.55f, 0.58f),
                BlockType.Sand or BlockType.Clay => new Color(0.78f, 0.68f, 0.42f),
                BlockType.Dirt or BlockType.Grass => new Color(0.45f, 0.32f, 0.18f),
                BlockType.CoalOre => new Color(0.2f, 0.2f, 0.22f),
                BlockType.IronOre => new Color(0.62f, 0.42f, 0.28f),
                BlockType.GoldOre => new Color(0.82f, 0.68f, 0.22f),
                BlockType.Glass => new Color(0.55f, 0.78f, 0.92f),
                BlockType.OakLeaves or BlockType.BirchLeaves or BlockType.PineLeaves => new Color(0.25f, 0.55f, 0.22f),
                BlockType.Cactus => new Color(0.22f, 0.52f, 0.24f),
                BlockType.Gravel => new Color(0.48f, 0.46f, 0.44f),
                BlockType.Wheat => new Color(0.72f, 0.62f, 0.22f),
                _ => new Color(0.5f, 0.5f, 0.5f)
            };

            _ui.DrawFilledRect(x, y, size, size, swatch);
        }
    }
}

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Core;
using Autonocraft.Engine;
using Autonocraft.Items;
using Autonocraft.World.Containers;

namespace Autonocraft.UI
{
    public sealed class ChestScreen
    {
        private const float SlotSize = 42f;
        private const float SlotGap = 4f;
        private const int Columns = 6;

        private readonly UiRenderer _ui;
        private readonly UiItemStackRenderer _itemStacks;
        private int _hoveredSlot = -1;
        private bool _rightClicked;
        private int _clickedSlot = -1;
        private string _statusMessage = string.Empty;
        private float _statusTimer;

        public ChestScreen(UiRenderer ui)
        {
            _ui = ui;
            _itemStacks = new UiItemStackRenderer(ui.Device);
        }

        public int ClickedSlotIndex => _clickedSlot;
        public bool RightClickedSlot => _rightClicked;
        public string StatusMessage => _statusMessage;

        public void SetStatus(string message)
        {
            _statusMessage = message;
            _statusTimer = 2.5f;
        }

        public void Update(float deltaTime, Viewport viewport, ChestSession session, KeyboardState kb, MouseState mouse, KeyboardState prevKb, MouseState prevMouse)
        {
            _hoveredSlot = -1;
            _rightClicked = false;
            _clickedSlot = -1;
            _statusTimer = Math.Max(0f, _statusTimer - deltaTime);

            if (!session.IsOpen || session.ChestInventory == null)
            {
                return;
            }

            var layout = new UiLayout(viewport.Width, viewport.Height);
            var bounds = BuildSlotBounds(layout, session.ChestInventory.SlotCount);
            Point mousePt = new Point(mouse.X, mouse.Y);

            for (int i = 0; i < bounds.Length; i++)
            {
                if (!bounds[i].Contains(mousePt))
                {
                    continue;
                }

                _hoveredSlot = i;
                if (mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
                {
                    _clickedSlot = i;
                }

                if (mouse.RightButton == ButtonState.Pressed && prevMouse.RightButton == ButtonState.Released)
                {
                    _rightClicked = true;
                    _clickedSlot = i;
                }
            }
        }

        public void Draw(Viewport viewport, ChestSession session, Texture2D atlas, float alpha = 1f)
        {
            if (!session.IsOpen || session.ChestInventory == null)
            {
                return;
            }

            var layout = new UiLayout(viewport.Width, viewport.Height);
            float panelW = layout.S(420f);
            float panelH = layout.S(220f);
            float panelX = layout.CenterX - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f;
            var atlasTex = atlas;

            _ui.DrawFullscreenBackground(UiTheme.Scrim * (UiTheme.MenuScrimAlpha * 0.85f));
            _ui.DrawPanel(panelX, panelY, panelW, panelH, UiTheme.PanelBgMuted, UiTheme.PanelBorder);
            _ui.DrawCenteredText("STRUCTURE CHEST", panelY + layout.S(16f), layout.S(UiTheme.FontTitle), UiTheme.Title, alpha);
            _ui.DrawCenteredText("Left-click take · Right-click deposit hotbar", panelY + layout.S(42f),
                layout.S(UiTheme.FontCaption), UiTheme.Hint, alpha * 0.9f);

            var bounds = BuildSlotBounds(layout, session.ChestInventory.SlotCount, panelX, panelY);
            for (int i = 0; i < bounds.Length; i++)
            {
                bool hovered = i == _hoveredSlot;
                _ui.DrawPanel(bounds[i].X, bounds[i].Y, bounds[i].Width, bounds[i].Height,
                    hovered ? UiTheme.PanelBgHighlight : UiTheme.PanelBgMuted,
                    hovered ? UiTheme.Accent : UiTheme.Rule);
                _itemStacks.DrawStack(atlasTex, _ui, session.ChestInventory.GetSlot(i), bounds[i], layout, alpha);
            }

            if (_statusTimer > 0f && !string.IsNullOrEmpty(_statusMessage))
            {
                _ui.DrawCenteredText(_statusMessage, panelY + panelH - layout.S(24f), layout.S(UiTheme.FontCaption), UiTheme.Accent, alpha);
            }
        }

        private Rectangle[] BuildSlotBounds(UiLayout layout, int slotCount, float panelX = 0f, float panelY = 0f)
        {
            float size = layout.S(SlotSize);
            float gap = layout.S(SlotGap);
            int rows = (slotCount + Columns - 1) / Columns;
            float gridW = Columns * size + (Columns - 1) * gap;
            float startX = panelX > 0f ? panelX + (layout.S(420f) - gridW) / 2f : layout.CenterX - gridW / 2f;
            float startY = panelY > 0f ? panelY + layout.S(72f) : layout.CenterY - rows * size / 2f;

            var bounds = new Rectangle[slotCount];
            for (int i = 0; i < slotCount; i++)
            {
                int col = i % Columns;
                int row = i / Columns;
                bounds[i] = new Rectangle(
                    (int)(startX + col * (size + gap)),
                    (int)(startY + row * (size + gap)),
                    (int)size,
                    (int)size);
            }

            return bounds;
        }
    }
}

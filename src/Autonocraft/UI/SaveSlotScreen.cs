using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.World;

namespace Autonocraft.UI
{
    public class SaveSlotScreen
    {
        private const float ButtonWidth = 220f;
        private const float ButtonHeight = 40f;
        private const float ButtonSpacing = 12f;
        private const float PanelWidth = 520f;
        private const float PanelHeight = 460f;
        private const float SlotRowHeight = 44f;
        private const int MaxVisibleSlots = 5;

        private readonly UiRenderer _ui;
        private readonly UiTransition _panelTransition = new UiTransition();
        private float _animTime;
        private float _selectedBorderT = 1f;
        private int _prevSelectedIndex = -1;
        private List<SaveSlotInfo> _slots = new();
        private int _selectedIndex;
        private int _scrollOffset;
        private int _hoveredButton = -1;
        private bool _confirmingDelete;

        public bool LoadRequested { get; private set; }
        public bool NewWorldRequested { get; private set; }
        public bool QuitRequested { get; private set; }
        public string? SelectedSlotId { get; private set; }

        public SaveSlotScreen(UiRenderer ui)
        {
            _ui = ui;
            _panelTransition.BeginFadeIn(0.25f);
            RefreshSlots();
        }

        public void RefreshSlots()
        {
            _slots = WorldSaveManager.ListSlots();
            if (_selectedIndex >= _slots.Count)
            {
                _selectedIndex = Math.Max(0, _slots.Count - 1);
            }

            int maxScroll = Math.Max(0, _slots.Count - MaxVisibleSlots);
            if (_scrollOffset > maxScroll)
            {
                _scrollOffset = maxScroll;
            }

            _panelTransition.BeginFadeIn(0.25f);
        }

        public void Update(Viewport viewport, KeyboardState kb, MouseState mouse, KeyboardState prevKb, MouseState prevMouse, float deltaTime)
        {
            _animTime += deltaTime;
            _panelTransition.Update(deltaTime);

            if (_prevSelectedIndex != _selectedIndex)
            {
                _selectedBorderT = 0f;
                _prevSelectedIndex = _selectedIndex;
            }

            _selectedBorderT = Tween.SmoothDamp(_selectedBorderT, 1f, 12f, deltaTime);
            LoadRequested = false;
            NewWorldRequested = false;
            QuitRequested = false;
            SelectedSlotId = null;

            var layout = new UiLayout(viewport);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float buttonSpacing = layout.S(ButtonSpacing);
            float cx = layout.CenterX;
            float panelW = layout.S(PanelWidth);
            float panelH = layout.S(PanelHeight);
            float panelX = cx - panelW / 2f;
            float panelY = layout.Height * 0.14f;

            float slotListTop = panelY + layout.S(88f);
            float slotListHeight = layout.S(SlotRowHeight * MaxVisibleSlots);
            float actionY = slotListTop + slotListHeight + layout.S(24f);
            float newWorldY = actionY;
            float loadY = newWorldY + buttonH + buttonSpacing;
            float deleteY = loadY + buttonH + buttonSpacing;
            float quitY = deleteY + buttonH + buttonSpacing;

            var newWorldRect = GetButtonRect(cx, newWorldY, buttonW, buttonH);
            var loadRect = GetButtonRect(cx, loadY, buttonW, buttonH);
            var deleteRect = GetButtonRect(cx, deleteY, buttonW, buttonH);
            var quitRect = GetButtonRect(cx, quitY, buttonW, buttonH);

            _hoveredButton = -1;
            if (newWorldRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 0;
            else if (loadRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 1;
            else if (deleteRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 2;
            else if (quitRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 3;

            int clickedSlot = GetClickedSlotIndex(mouse, panelX, panelW, slotListTop, layout);
            bool click = mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released;

            if (click && clickedSlot >= 0)
            {
                _selectedIndex = clickedSlot;
                _confirmingDelete = false;
            }

            if (kb.IsKeyDown(Keys.Up) && !prevKb.IsKeyDown(Keys.Up) && _slots.Count > 0)
            {
                _selectedIndex = Math.Max(0, _selectedIndex - 1);
                EnsureSelectedVisible();
                _confirmingDelete = false;
            }

            if (kb.IsKeyDown(Keys.Down) && !prevKb.IsKeyDown(Keys.Down) && _slots.Count > 0)
            {
                _selectedIndex = Math.Min(_slots.Count - 1, _selectedIndex + 1);
                EnsureSelectedVisible();
                _confirmingDelete = false;
            }

            if (click)
            {
                if (_hoveredButton == 0)
                {
                    NewWorldRequested = true;
                    _confirmingDelete = false;
                }
                else if (_hoveredButton == 1 && _slots.Count > 0)
                {
                    SelectedSlotId = _slots[_selectedIndex].SlotId;
                    LoadRequested = true;
                    _confirmingDelete = false;
                }
                else if (_hoveredButton == 2 && _slots.Count > 0)
                {
                    if (_confirmingDelete)
                    {
                        WorldSaveManager.DeleteSlot(_slots[_selectedIndex].SlotId);
                        RefreshSlots();
                        _confirmingDelete = false;
                    }
                    else
                    {
                        _confirmingDelete = true;
                    }
                }
                else if (_hoveredButton == 3)
                {
                    QuitRequested = true;
                }
            }

            if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter) && _slots.Count > 0)
            {
                SelectedSlotId = _slots[_selectedIndex].SlotId;
                LoadRequested = true;
            }

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                QuitRequested = true;
            }

            int scrollDelta = mouse.ScrollWheelValue - prevMouse.ScrollWheelValue;
            if (scrollDelta != 0 && _slots.Count > MaxVisibleSlots)
            {
                int maxScroll = _slots.Count - MaxVisibleSlots;
                _scrollOffset = Math.Clamp(_scrollOffset - Math.Sign(scrollDelta), 0, maxScroll);
            }
        }

        public void Draw(Viewport viewport, float alpha = 1f, float offsetY = 0f)
        {
            alpha *= _panelTransition.Alpha;
            offsetY += _panelTransition.OffsetY;

            var layout = new UiLayout(viewport);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float buttonSpacing = layout.S(ButtonSpacing);
            float panelW = layout.S(PanelWidth);
            float panelH = layout.S(PanelHeight);

            _ui.DrawFullscreenBackground(new Color(0.03f, 0.04f, 0.07f));

            float cx = layout.CenterX;
            float titleY = layout.Height * 0.18f;
            float subtitleY = titleY + layout.S(34f);
            float panelX = cx - panelW / 2f;
            float panelY = layout.Height * 0.14f + offsetY;

            _ui.DrawPanel(panelX, panelY, panelW, panelH, new Color(0.04f, 0.05f, 0.08f) * 0.88f, new Color(0.2f, 0.3f, 0.4f), 0.8f, alpha);

            _ui.DrawCenteredText("AUTONOCRAFT", titleY + offsetY, layout.S(2.2f), new Color(0.8f, 0.9f, 1.0f), alpha);
            _ui.DrawCenteredText("SELECT WORLD", subtitleY + offsetY, layout.S(1.2f), new Color(0.55f, 0.65f, 0.75f), alpha);

            float slotListTop = panelY + layout.S(88f);
            float rowH = layout.S(SlotRowHeight);
            float slotListHeight = rowH * MaxVisibleSlots;
            float slotListX = panelX + layout.S(20f);
            float slotListW = panelW - layout.S(40f);

            _ui.DrawPanel(slotListX, slotListTop + offsetY, slotListW, slotListHeight, new Color(0.02f, 0.03f, 0.05f) * 0.9f, new Color(0.15f, 0.22f, 0.3f), 0.8f, alpha);

            if (_slots.Count == 0)
            {
                _ui.DrawCenteredText("NO SAVED WORLDS", slotListTop + slotListHeight / 2f - layout.S(8f) + offsetY, layout.S(1.1f), new Color(0.45f, 0.5f, 0.58f), alpha);
            }
            else
            {
                for (int i = 0; i < MaxVisibleSlots; i++)
                {
                    int slotIndex = _scrollOffset + i;
                    if (slotIndex >= _slots.Count) break;

                    var slot = _slots[slotIndex];
                    float rowY = slotListTop + i * rowH + layout.S(4f) + offsetY;
                    bool selected = slotIndex == _selectedIndex;
                    float staggerDelay = i * 0.04f;
                    float staggerT = Math.Clamp((_animTime - staggerDelay) / 0.2f, 0f, 1f);
                    float rowAlpha = alpha * Tween.EaseOut(staggerT);

                    Color rowColor = selected
                        ? Color.Lerp(new Color(0.05f, 0.07f, 0.1f), new Color(0.12f, 0.18f, 0.28f), _selectedBorderT)
                        : new Color(0.05f, 0.07f, 0.1f);
                    _ui.DrawPanel(slotListX + layout.S(6f), rowY, slotListW - layout.S(12f), rowH - layout.S(8f), rowColor, new Color(0.2f, 0.3f, 0.4f), 0.5f, rowAlpha);

                    string title = Truncate(slot.SlotName, 24);
                    string subtitle = FormatSavedAt(slot.SavedAt);
                    _ui.DrawString(title, slotListX + layout.S(16f), rowY + layout.S(6f), layout.S(1.2f), new Color(0.82f, 0.9f, 1.0f), rowAlpha);
                    _ui.DrawString(subtitle, slotListX + layout.S(16f), rowY + layout.S(22f), layout.S(0.95f), new Color(0.5f, 0.58f, 0.66f), rowAlpha);
                }
            }

            float actionY = slotListTop + slotListHeight + layout.S(24f) + offsetY;
            float newWorldY = actionY;
            float loadY = newWorldY + buttonH + buttonSpacing;
            float deleteY = loadY + buttonH + buttonSpacing;
            float quitY = deleteY + buttonH + buttonSpacing;

            DrawButton(cx, newWorldY, buttonW, buttonH, "NEW WORLD", _hoveredButton == 0, layout.S(1.4f), alpha: alpha);
            DrawButton(cx, loadY, buttonW, buttonH, _slots.Count > 0 ? "CONTINUE" : "CONTINUE", _hoveredButton == 1, layout.S(1.4f), _slots.Count == 0, alpha);
            string deleteLabel = _confirmingDelete ? "CONFIRM DELETE" : "DELETE SLOT";
            DrawButton(cx, deleteY, buttonW, buttonH, deleteLabel, _hoveredButton == 2, layout.S(1.3f), _slots.Count == 0, alpha);
            DrawButton(cx, quitY, buttonW, buttonH, "QUIT", _hoveredButton == 3, layout.S(1.4f), alpha: alpha);

            _ui.DrawCenteredText("AUTO SAVE ENABLED", layout.Height - layout.S(36f) + offsetY, layout.S(1.0f), new Color(0.4f, 0.46f, 0.54f), 0.85f * alpha);
        }

        private void DrawButton(float centerX, float y, float width, float height, string label, bool hovered, float textPixelSize, bool disabled = false, float alpha = 1f)
        {
            float x = centerX - width / 2f;
            _ui.DrawButton(x, y, width, height, label, hovered && !disabled, disabled, textPixelSize, alpha);
        }

        private int GetClickedSlotIndex(MouseState mouse, float panelX, float panelW, float slotListTop, UiLayout layout)
        {
            if (_slots.Count == 0) return -1;

            float rowH = layout.S(SlotRowHeight);
            float slotListX = panelX + layout.S(20f);
            float slotListW = panelW - layout.S(40f);
            float slotListHeight = rowH * MaxVisibleSlots;

            var listRect = new Rectangle((int)slotListX, (int)slotListTop, (int)slotListW, (int)slotListHeight);
            if (!listRect.Contains(mouse.X, mouse.Y)) return -1;

            int row = (int)((mouse.Y - slotListTop) / rowH);
            if (row < 0 || row >= MaxVisibleSlots) return -1;

            int slotIndex = _scrollOffset + row;
            return slotIndex < _slots.Count ? slotIndex : -1;
        }

        private void EnsureSelectedVisible()
        {
            if (_selectedIndex < _scrollOffset)
            {
                _scrollOffset = _selectedIndex;
            }
            else if (_selectedIndex >= _scrollOffset + MaxVisibleSlots)
            {
                _scrollOffset = _selectedIndex - MaxVisibleSlots + 1;
            }
        }

        private static string FormatSavedAt(DateTime savedAt)
        {
            return $"SAVED {savedAt.ToLocalTime():yyyy-MM-dd HH:mm}";
        }

        private static string Truncate(string value, int maxLength)
        {
            if (value.Length <= maxLength) return value;
            return value[..(maxLength - 3)] + "...";
        }

        private static Rectangle GetButtonRect(float centerX, float y, float width, float height)
        {
            return new Rectangle(
                (int)(centerX - width / 2f),
                (int)y,
                (int)width,
                (int)height);
        }
    }
}

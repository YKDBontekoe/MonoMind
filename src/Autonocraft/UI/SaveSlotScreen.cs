using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Core;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.World;

namespace Autonocraft.UI
{
    public class SaveSlotScreen
    {
        private const float PrimaryButtonWidth = 168f;
        private const float SecondaryButtonWidth = 128f;
        private const float ButtonHeight = 40f;
        private const float ButtonSpacing = 14f;
        private const float PanelWidth = 680f;
        private const float PanelHeight = 440f;
        private const float SlotRowHeight = 56f;
        private const int MaxVisibleSlots = 5;
        private const float DoubleClickWindow = 0.4f;
        private const int MaxRenameLength = 32;

        private readonly UiRenderer _ui;
        private readonly MenuBackdrop _backdrop = new MenuBackdrop();
        private readonly UiTransition _panelTransition = new UiTransition();
        private readonly float[] _buttonHoverT = new float[6];
        private readonly float[] _slotHoverT = new float[MaxVisibleSlots];

        private float _animTime;
        private float _selectedBorderT = 1f;
        private float _scrollOffsetT;
        private int _prevSelectedIndex = -1;
        private int _prevScrollOffset;
        private List<SaveSlotInfo> _slots = new();
        private int _selectedIndex;
        private int _hoveredSlotIndex = -1;
        private int _scrollOffset;
        private int _hoveredButton = -1;
        private bool _confirmingDelete;
        private string? _loadErrorMessage;
        private float _lastClickTime = -1f;
        private int _lastClickedSlot = -1;
        private float _errorFlashT;
        private PlayerStatistics _lifetimeStats = new();
        private PlayerStatistics _selectedWorldStats = new();
        private int _worldCount;
        private bool _renaming;
        private string _renameBuffer = string.Empty;

        public bool LoadRequested { get; private set; }
        public bool NewWorldRequested { get; private set; }
        public bool SettingsRequested { get; private set; }
        public bool StatsRequested { get; private set; }
        public bool QuitRequested { get; private set; }
        public string? SelectedSlotId { get; private set; }

        public string? GetSelectedSlotId()
        {
            if (_slots.Count == 0 || _selectedIndex < 0 || _selectedIndex >= _slots.Count)
            {
                return null;
            }

            return _slots[_selectedIndex].SlotId;
        }

        public string? GetSelectedSlotName()
        {
            if (_slots.Count == 0 || _selectedIndex < 0 || _selectedIndex >= _slots.Count)
            {
                return null;
            }

            return _slots[_selectedIndex].SlotName;
        }

        public SaveSlotScreen(UiRenderer ui)
        {
            _ui = ui;
            _panelTransition.BeginFadeIn(0.35f);
            RefreshSlots();
        }

        public void RefreshSlots()
        {
            _loadErrorMessage = null;
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

            _panelTransition.BeginFadeIn(0.3f);
            RefreshLifetimeStats();
            RefreshSelectedWorldStats();
        }

        private void RefreshLifetimeStats()
        {
            (_lifetimeStats, _worldCount) = WorldSaveManager.AggregateLifetimeStatistics();
        }

        private void RefreshSelectedWorldStats()
        {
            _selectedWorldStats = new PlayerStatistics();
            if (_slots.Count == 0 || _selectedIndex < 0 || _selectedIndex >= _slots.Count)
            {
                return;
            }

            var slot = _slots[_selectedIndex];
            if (!slot.IsCorrupt)
            {
                WorldSaveManager.TryLoadPlayerStatistics(slot.SlotId, out var stats, out _);
                _selectedWorldStats = stats;
            }
        }

        public void SetLoadError(string message)
        {
            _loadErrorMessage = message;
            _errorFlashT = 0f;
        }

        public void Update(Viewport viewport, KeyboardState kb, MouseState mouse, KeyboardState prevKb, MouseState prevMouse, float deltaTime)
        {
            _animTime += deltaTime;
            _backdrop.Update(deltaTime);
            _panelTransition.Update(deltaTime);
            _errorFlashT = Math.Min(1f, _errorFlashT + deltaTime * 3f);

            if (_prevSelectedIndex != _selectedIndex)
            {
                _selectedBorderT = 0f;
                _prevSelectedIndex = _selectedIndex;
                RefreshSelectedWorldStats();
            }

            if (_prevScrollOffset != _scrollOffset)
            {
                _scrollOffsetT = 0f;
                _prevScrollOffset = _scrollOffset;
            }

            _selectedBorderT = Tween.SmoothDamp(_selectedBorderT, 1f, 12f, deltaTime);
            _scrollOffsetT = Tween.SmoothDamp(_scrollOffsetT, 1f, 14f, deltaTime);

            for (int i = 0; i < _buttonHoverT.Length; i++)
            {
                float target = _hoveredButton == i ? 1f : 0f;
                _buttonHoverT[i] = Tween.SmoothDamp(_buttonHoverT[i], target, 10f, deltaTime);
            }

            for (int i = 0; i < _slotHoverT.Length; i++)
            {
                int slotIndex = _scrollOffset + i;
                float target = slotIndex == _hoveredSlotIndex ? 1f : 0f;
                _slotHoverT[i] = Tween.SmoothDamp(_slotHoverT[i], target, 12f, deltaTime);
            }

            LoadRequested = false;
            NewWorldRequested = false;
            SettingsRequested = false;
            StatsRequested = false;
            QuitRequested = false;
            SelectedSlotId = null;

            var layout = new UiLayout(viewport);
            var metrics = ComputeLayout(layout);

            _hoveredButton = -1;
            for (int i = 0; i < metrics.ButtonRects.Length; i++)
            {
                if (metrics.ButtonRects[i].Contains(mouse.X, mouse.Y))
                {
                    _hoveredButton = i;
                    break;
                }
            }

            _hoveredSlotIndex = GetHoveredSlotIndex(mouse, metrics, layout);

            int clickedSlot = GetClickedSlotIndex(mouse, metrics, layout);
            bool click = mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released;

            if (click && clickedSlot >= 0)
            {
                if (clickedSlot == _lastClickedSlot && _animTime - _lastClickTime <= DoubleClickWindow)
                {
                    _selectedIndex = clickedSlot;
                    TryRequestLoad();
                    _confirmingDelete = false;
                }
                else
                {
                    _selectedIndex = clickedSlot;
                    _confirmingDelete = false;
                }

                _lastClickedSlot = clickedSlot;
                _lastClickTime = _animTime;
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
                    TryRequestLoad();
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
                    StatsRequested = true;
                    _confirmingDelete = false;
                }
                else if (_hoveredButton == 4)
                {
                    SettingsRequested = true;
                    _confirmingDelete = false;
                }
                else if (_hoveredButton == 5)
                {
                    QuitRequested = true;
                }
            }

            if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter) && _slots.Count > 0)
            {
                if (_renaming)
                {
                    TryConfirmRename();
                }
                else
                {
                    TryRequestLoad();
                }
            }

            if (kb.IsKeyDown(Keys.Delete) && !prevKb.IsKeyDown(Keys.Delete) && _slots.Count > 0)
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

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                if (_renaming)
                {
                    _renaming = false;
                    _renameBuffer = string.Empty;
                }
                else if (_confirmingDelete)
                {
                    _confirmingDelete = false;
                }
                else
                {
                    QuitRequested = true;
                }
            }

            if (_renaming)
            {
                HandleRenameInput(kb, prevKb);
            }
            else if (kb.IsKeyDown(Keys.F2) && !prevKb.IsKeyDown(Keys.F2) && _slots.Count > 0)
            {
                var slot = _slots[_selectedIndex];
                if (!slot.IsCorrupt)
                {
                    _renaming = true;
                    _renameBuffer = slot.SlotName;
                    _confirmingDelete = false;
                }
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
            var metrics = ComputeLayout(layout);

            _backdrop.Draw(_ui, viewport, alpha);

            float titlePulse = 0.88f + 0.12f * Tween.Pulse(_animTime, 0.3f);
            float titleY = layout.Height * 0.085f + offsetY;
            float subtitleY = titleY + layout.S(40f);
            float accentY = subtitleY + layout.S(20f);

            Color titleColor = Color.Lerp(new Color(0.78f, 0.9f, 1.0f), new Color(0.92f, 0.96f, 1.0f), Tween.Pulse(_animTime, 0.2f) * 0.5f);
            _ui.DrawCenteredTitle("AUTONOCRAFT", titleY, layout.S(2.8f), titleColor * titlePulse, alpha);
            _ui.DrawCenteredText("BUILD  EXPLORE  THRIVE", subtitleY, layout.S(1.1f), new Color(0.48f, 0.6f, 0.72f), alpha * 0.92f);

            float accentW = layout.S(200f) * (0.85f + 0.15f * Tween.Pulse(_animTime, 0.25f));
            float accentX = layout.CenterX - accentW / 2f;
            _ui.DrawSoftGlow(accentX, accentY, accentW, layout.S(2f), new Color(0.15f, 0.7f, 1.0f), alpha * 0.5f, 2);
            _ui.DrawFilledRect(accentX, accentY, accentW, layout.S(2f), new Color(0.2f, 0.72f, 1.0f) * (0.85f * alpha));

            DrawLifetimeStrip(layout, accentY, alpha, offsetY);

            _ui.DrawFramedPanel(
                metrics.PanelX,
                metrics.PanelY,
                metrics.PanelW,
                metrics.PanelH,
                UiTheme.PanelFill * 0.94f,
                UiTheme.PanelBorder,
                alpha);

            DrawPanelHeader(metrics, layout, alpha);
            DrawWorldDetailStrip(metrics, layout, alpha);
            DrawSlotList(metrics, layout, alpha, offsetY);
            DrawActionDivider(metrics, layout, alpha);
            DrawActionButtons(metrics, layout, alpha);

            if (!string.IsNullOrEmpty(_loadErrorMessage))
            {
                float flash = 0.65f + 0.35f * MathF.Sin(_errorFlashT * MathF.PI * 4f);
                _ui.DrawCenteredText(
                    Truncate(_loadErrorMessage, 52),
                    metrics.PanelY + metrics.PanelH + layout.S(14f) + offsetY,
                    layout.S(1.0f),
                    new Color(0.95f, 0.35f, 0.35f) * flash,
                    alpha);
            }

            string hint = _slots.Count == 0
                ? "CREATE YOUR FIRST WORLD TO BEGIN"
                : _renaming
                    ? "ENTER SAVE  ESC CANCEL"
                    : "ARROWS SELECT  ENTER CONTINUE  F2 RENAME  DBL-CLICK LOAD  DEL DELETE";
            _ui.DrawCenteredText(hint, layout.Height - layout.S(28f) + offsetY, layout.S(0.92f), UiTheme.Hint, 0.82f * alpha);
            _ui.DrawString("VOXEL SANDBOX", layout.S(16f), layout.Height - layout.S(22f) + offsetY, layout.S(0.85f), UiTheme.Hint * 0.85f, 0.7f * alpha);
        }

        private void DrawPanelHeader(MenuMetrics metrics, UiLayout layout, float alpha)
        {
            float headerY = metrics.PanelY + layout.S(14f);
            UiTheme.DrawSectionHeader(_ui, "YOUR WORLDS", metrics.PanelX + layout.S(22f), headerY, layout, alpha, 1.35f);

            if (_slots.Count > 0)
            {
                string count = $"{_selectedIndex + 1}/{_slots.Count}";
                float countW = _ui.MeasureString(count, layout.S(1.0f));
                _ui.DrawString(count, metrics.PanelX + metrics.PanelW - layout.S(22f) - countW, headerY + layout.S(2f), layout.S(1.0f), UiTheme.Meta, alpha);

                if (_slots.Count > MaxVisibleSlots)
                {
                    int first = _scrollOffset + 1;
                    int last = Math.Min(_scrollOffset + MaxVisibleSlots, _slots.Count);
                    string scrollHint = $"{first}-{last} OF {_slots.Count}";
                    float hintW = _ui.MeasureString(scrollHint, layout.S(0.9f));
                    _ui.DrawString(scrollHint, metrics.PanelX + metrics.PanelW - layout.S(22f) - hintW, headerY + layout.S(18f), layout.S(0.9f), UiTheme.Meta * 0.9f, alpha * 0.85f);
                }
            }
        }

        private void DrawWorldDetailStrip(MenuMetrics metrics, UiLayout layout, float alpha)
        {
            if (_slots.Count == 0)
            {
                return;
            }

            float stripY = metrics.DetailStripY;
            float stripX = metrics.PanelX + layout.S(18f);
            float stripW = metrics.PanelW - layout.S(36f);
            float stripH = layout.S(22f);

            _ui.DrawPanel(stripX, stripY, stripW, stripH, new Color(0.03f, 0.05f, 0.08f) * 0.9f, new Color(0.14f, 0.22f, 0.3f), 0.6f, alpha);

            if (_renaming)
            {
                string display = _renameBuffer + "_";
                _ui.DrawString(display, stripX + layout.S(10f), stripY + layout.S(5f), layout.S(0.95f), UiTheme.Accent, alpha);
                return;
            }

            string worldStats = $"THIS WORLD  ·  {PlayerStatistics.FormatDuration(_selectedWorldStats.TotalPlayTimeSeconds)}  ·  {PlayerStatistics.FormatDistance(_selectedWorldStats.DistanceWalked)}  ·  {PlayerStatistics.FormatCount(_selectedWorldStats.AnimalsKilled)} kills";
            _ui.DrawString(worldStats, stripX + layout.S(10f), stripY + layout.S(5f), layout.S(0.92f), UiTheme.Subtitle * 0.9f, alpha * 0.88f);
        }

        private void DrawSlotList(MenuMetrics metrics, UiLayout layout, float alpha, float offsetY)
        {
            float slotListX = metrics.PanelX + layout.S(18f);
            float slotListW = metrics.PanelW - layout.S(36f);
            float slotListTop = metrics.SlotListTop + offsetY;
            float rowH = layout.S(SlotRowHeight);
            float slotListHeight = rowH * MaxVisibleSlots;
            float slideOffset = (1f - _scrollOffsetT) * layout.S(6f);

            _ui.DrawPanel(slotListX, slotListTop, slotListW, slotListHeight, new Color(0.02f, 0.03f, 0.05f) * 0.96f, new Color(0.14f, 0.22f, 0.3f), 0.75f, alpha);

            if (_slots.Count > MaxVisibleSlots)
            {
                if (_scrollOffset > 0)
                {
                    _ui.DrawFilledRect(slotListX, slotListTop, slotListW, layout.S(10f), Color.Black * (0.35f * alpha));
                    _ui.DrawCenteredText("^", slotListTop + layout.S(1f), layout.S(0.85f), UiTheme.Accent * 0.7f, alpha * 0.75f);
                }

                if (_scrollOffset < _slots.Count - MaxVisibleSlots)
                {
                    _ui.DrawFilledRect(slotListX, slotListTop + slotListHeight - layout.S(10f), slotListW, layout.S(10f), Color.Black * (0.35f * alpha));
                    _ui.DrawCenteredText("V", slotListTop + slotListHeight - layout.S(9f), layout.S(0.85f), UiTheme.Accent * 0.7f, alpha * 0.75f);
                }
            }

            if (_slots.Count == 0)
            {
                float emptyY = slotListTop + slotListHeight / 2f - layout.S(24f);
                _ui.DrawCenteredText("NO SAVED WORLDS YET", emptyY, layout.S(1.25f), new Color(0.48f, 0.56f, 0.64f), alpha);
                float pulse = 0.55f + 0.45f * Tween.Pulse(_animTime, 0.45f);
                _ui.DrawCenteredText("HIT NEW WORLD TO START", emptyY + layout.S(30f), layout.S(1.05f), new Color(0.28f, 0.68f, 0.95f) * pulse, alpha);
                return;
            }

            for (int i = 0; i < MaxVisibleSlots; i++)
            {
                int slotIndex = _scrollOffset + i;
                if (slotIndex >= _slots.Count) break;

                var slot = _slots[slotIndex];
                float rowY = slotListTop + i * rowH + layout.S(4f) + slideOffset;
                bool selected = slotIndex == _selectedIndex;
                float hoverBlend = _slotHoverT[i];
                float staggerDelay = i * 0.04f;
                float staggerT = Math.Clamp((_animTime - staggerDelay) / 0.2f, 0f, 1f);
                float rowAlpha = alpha * Tween.EaseOut(staggerT);

                Color baseColor = new Color(0.05f, 0.07f, 0.1f);
                Color hoverColor = new Color(0.09f, 0.13f, 0.19f);
                Color selectedColor = Color.Lerp(new Color(0.08f, 0.12f, 0.18f), new Color(0.11f, 0.22f, 0.34f), _selectedBorderT);
                Color rowColor = Color.Lerp(
                    selected ? selectedColor : baseColor,
                    hoverColor,
                    hoverBlend * (selected ? 0.35f : 0.85f));

                float rowX = slotListX + layout.S(6f);
                float rowW = slotListW - layout.S(12f);
                float rowInnerH = rowH - layout.S(8f);

                if (selected)
                {
                    _ui.DrawSoftGlow(rowX, rowY, rowW, rowInnerH, new Color(0.15f, 0.55f, 0.9f), rowAlpha * 0.35f, 2);
                }

                _ui.DrawPanel(rowX, rowY, rowW, rowInnerH, rowColor, selected ? new Color(0.18f, 0.58f, 0.88f) : new Color(0.16f, 0.26f, 0.36f), selected ? 0.95f : 0.55f, rowAlpha);

                if (selected)
                {
                    _ui.DrawFilledRect(rowX, rowY, layout.S(4f), rowInnerH, new Color(0.25f, 0.78f, 1.0f) * rowAlpha);
                }

                float swatch = layout.S(22f);
                float swatchX = rowX + layout.S(12f);
                float swatchY = rowY + (rowInnerH - swatch) / 2f;
                Color swatchColor = slot.IsCorrupt ? new Color(0.7f, 0.25f, 0.25f) : SeedToColor(slot.Seed);
                _ui.DrawPanel(swatchX, swatchY, swatch, swatch, swatchColor, Color.Black * 0.4f, 0.5f, rowAlpha);
                _ui.DrawFilledRect(swatchX, swatchY, swatch, Math.Max(1f, swatch * 0.22f), Color.Lerp(swatchColor, Color.White, 0.35f) * rowAlpha);

                float textX = swatchX + swatch + layout.S(10f);
                string title = Truncate(slot.SlotName, 20);
                string meta = slot.IsCorrupt ? "CORRUPT SAVE" : $"SEED {slot.Seed}  {FormatRelative(slot.SavedAt)}";
                Color titleColor = slot.IsCorrupt
                    ? new Color(0.95f, 0.45f, 0.45f)
                    : new Color(0.88f, 0.93f, 1.0f);
                _ui.DrawString(title, textX, rowY + layout.S(9f), layout.S(1.2f), titleColor, rowAlpha);
                _ui.DrawString(meta, textX, rowY + layout.S(27f), layout.S(0.95f), UiTheme.Meta, rowAlpha * 0.95f);

                if (selected)
                {
                    string marker = ">";
                    _ui.DrawString(marker, rowX + rowW - layout.S(18f), rowY + rowInnerH / 2f - layout.S(5f), layout.S(1.1f), new Color(0.35f, 0.78f, 1.0f), rowAlpha * _selectedBorderT);
                }
            }
        }

        private void DrawActionDivider(MenuMetrics metrics, UiLayout layout, float alpha)
        {
            float y = metrics.PanelY + metrics.PanelH - layout.S(118f);
            float x = metrics.PanelX + layout.S(22f);
            float w = metrics.PanelW - layout.S(44f);
            _ui.DrawHorizontalRule(x, y, w, new Color(0.16f, 0.28f, 0.4f), 1f, alpha * 0.65f);
        }

        private void DrawActionButtons(MenuMetrics metrics, UiLayout layout, float alpha)
        {
            string deleteLabel = _confirmingDelete ? "CONFIRM?" : "DELETE";
            bool hasSlots = _slots.Count > 0;

            DrawButton(metrics.ButtonRects[0], "NEW WORLD", _hoveredButton == 0, layout.S(1.35f), _buttonHoverT[0], alpha, primary: true);
            DrawButton(metrics.ButtonRects[1], "CONTINUE", _hoveredButton == 1, layout.S(1.35f), _buttonHoverT[1], alpha, !hasSlots, accent: hasSlots);
            DrawButton(metrics.ButtonRects[2], deleteLabel, _hoveredButton == 2, layout.S(1.2f), _buttonHoverT[2], alpha, !hasSlots, danger: _confirmingDelete);
            DrawButton(metrics.ButtonRects[3], "STATS", _hoveredButton == 3, layout.S(1.2f), _buttonHoverT[3], alpha, !hasSlots, accent: hasSlots);
            DrawButton(metrics.ButtonRects[4], "SETTINGS", _hoveredButton == 4, layout.S(1.2f), _buttonHoverT[4], alpha);
            DrawButton(metrics.ButtonRects[5], "QUIT", _hoveredButton == 5, layout.S(1.25f), _buttonHoverT[5], alpha);
        }

        private void DrawLifetimeStrip(UiLayout layout, float accentY, float alpha, float offsetY)
        {
            float stripY = accentY + layout.S(14f) + offsetY;
            float stripW = layout.S(620f);
            float stripH = layout.S(24f);
            float stripX = layout.CenterX - stripW / 2f;

            _ui.DrawFramedPanel(stripX, stripY, stripW, stripH, UiTheme.PanelFill * 0.88f, UiTheme.PanelBorder, alpha * 0.75f);

            string lifetimeText = _worldCount == 0
                ? "LIFETIME  |  NO WORLDS YET"
                : $"LIFETIME  |  {PlayerStatistics.FormatDuration(_lifetimeStats.TotalPlayTimeSeconds)} played  |  {PlayerStatistics.FormatDistance(_lifetimeStats.DistanceWalked)} walked  |  {PlayerStatistics.FormatCount(_lifetimeStats.AnimalsKilled)} kills  |  {_worldCount} worlds";

            _ui.DrawCenteredText(lifetimeText, stripY + layout.S(5f), layout.S(0.92f), UiTheme.Subtitle, alpha * 0.92f);
        }

        private void DrawButton(Rectangle rect, string label, bool hovered, float textSize, float hoverT, float alpha, bool disabled = false, bool accent = false, bool danger = false, bool primary = false)
        {
            if (disabled)
            {
                _ui.DrawButton(rect.X, rect.Y, rect.Width, rect.Height, label, false, false, textSize, alpha * 0.45f, 0f);
                return;
            }

            if (primary)
            {
                float glow = 0.45f + 0.55f * hoverT;
                _ui.DrawFilledRect(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2, new Color(0.15f, 0.55f, 0.35f) * (0.2f * glow * alpha));
            }

            if (accent && !disabled)
            {
                float glow = 0.5f + 0.5f * hoverT;
                _ui.DrawSoftGlow(rect.X, rect.Y, rect.Width, rect.Height, new Color(0.12f, 0.55f, 0.88f), 0.35f * glow * alpha, 2);
            }

            if (danger)
            {
                _ui.DrawSoftGlow(rect.X, rect.Y, rect.Width, rect.Height, new Color(0.85f, 0.2f, 0.2f), 0.4f * alpha, 2);
            }

            _ui.DrawButton(rect.X, rect.Y, rect.Width, rect.Height, label, hovered && !disabled, false, textSize, alpha, hoverT);
        }

        private MenuMetrics ComputeLayout(UiLayout layout)
        {
            float panelW = layout.S(PanelWidth);
            float panelH = layout.S(PanelHeight);
            float panelX = layout.CenterX - panelW / 2f;
            float panelY = layout.Height * 0.235f;

            float primaryW = layout.S(PrimaryButtonWidth);
            float secondaryW = layout.S(SecondaryButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float spacing = layout.S(ButtonSpacing);
            float rowGap = layout.S(18f);

            float row1Y = panelY + panelH - layout.S(112f);
            float row2Y = row1Y + buttonH + rowGap;
            float row1TotalW = primaryW * 2f + secondaryW + spacing * 2f;
            float row1X = layout.CenterX - row1TotalW / 2f;

            var buttons = new Rectangle[6];
            buttons[0] = new Rectangle((int)row1X, (int)row1Y, (int)primaryW, (int)buttonH);
            buttons[1] = new Rectangle((int)(row1X + primaryW + spacing), (int)row1Y, (int)primaryW, (int)buttonH);
            buttons[2] = new Rectangle((int)(row1X + (primaryW + spacing) * 2f), (int)row1Y, (int)secondaryW, (int)buttonH);

            float row2TotalW = primaryW * 2f + secondaryW + spacing * 2f;
            float row2X = layout.CenterX - row2TotalW / 2f;
            buttons[3] = new Rectangle((int)row2X, (int)row2Y, (int)primaryW, (int)buttonH);
            buttons[4] = new Rectangle((int)(row2X + primaryW + spacing), (int)row2Y, (int)primaryW, (int)buttonH);
            buttons[5] = new Rectangle((int)(row2X + (primaryW + spacing) * 2f), (int)row2Y, (int)secondaryW, (int)buttonH);

            return new MenuMetrics
            {
                PanelX = panelX,
                PanelY = panelY,
                PanelW = panelW,
                PanelH = panelH,
                DetailStripY = panelY + layout.S(52f),
                SlotListTop = panelY + layout.S(80f),
                ButtonRects = buttons
            };
        }

        private void TryConfirmRename()
        {
            if (!_renaming || _slots.Count == 0)
            {
                return;
            }

            var slot = _slots[_selectedIndex];
            if (WorldSaveManager.TryRenameSlot(slot.SlotId, _renameBuffer, out string error))
            {
                _renaming = false;
                _renameBuffer = string.Empty;
                RefreshSlots();
            }
            else
            {
                SetLoadError(error);
            }
        }

        private void HandleRenameInput(KeyboardState kb, KeyboardState prevKb)
        {
            if (kb.IsKeyDown(Keys.Back) && !prevKb.IsKeyDown(Keys.Back) && _renameBuffer.Length > 0)
            {
                _renameBuffer = _renameBuffer[..^1];
                return;
            }

            foreach (Keys key in Enum.GetValues<Keys>())
            {
                if (!kb.IsKeyDown(key) || prevKb.IsKeyDown(key))
                {
                    continue;
                }

                char? ch = KeyToChar(key, kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift));
                if (ch.HasValue && _renameBuffer.Length < MaxRenameLength)
                {
                    _renameBuffer += ch.Value;
                }
            }
        }

        private static char? KeyToChar(Keys key, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z)
            {
                char c = (char)('a' + (key - Keys.A));
                return shift ? char.ToUpper(c) : c;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                return (char)('0' + (key - Keys.D0));
            }

            return key switch
            {
                Keys.OemMinus => shift ? '_' : '-',
                Keys.OemPeriod => '.',
                Keys.Space => ' ',
                _ => null
            };
        }

        private int GetHoveredSlotIndex(MouseState mouse, MenuMetrics metrics, UiLayout layout)
        {
            return GetSlotIndexAt(mouse.X, mouse.Y, metrics, layout);
        }

        private int GetClickedSlotIndex(MouseState mouse, MenuMetrics metrics, UiLayout layout)
        {
            if (mouse.LeftButton != ButtonState.Pressed) return -1;
            return GetSlotIndexAt(mouse.X, mouse.Y, metrics, layout);
        }

        private int GetSlotIndexAt(int mouseX, int mouseY, MenuMetrics metrics, UiLayout layout)
        {
            if (_slots.Count == 0) return -1;

            float slotListX = metrics.PanelX + layout.S(18f);
            float slotListW = metrics.PanelW - layout.S(36f);
            float rowH = layout.S(SlotRowHeight);
            float slotListHeight = rowH * MaxVisibleSlots;

            var listRect = new Rectangle((int)slotListX, (int)metrics.SlotListTop, (int)slotListW, (int)slotListHeight);
            if (!listRect.Contains(mouseX, mouseY)) return -1;

            int row = (int)((mouseY - metrics.SlotListTop) / rowH);
            if (row < 0 || row >= MaxVisibleSlots) return -1;

            int slotIndex = _scrollOffset + row;
            return slotIndex < _slots.Count ? slotIndex : -1;
        }

        private void TryRequestLoad()
        {
            var slot = _slots[_selectedIndex];
            if (slot.IsCorrupt)
            {
                _loadErrorMessage = "Cannot load corrupt save slot.";
                _errorFlashT = 0f;
                return;
            }

            SelectedSlotId = slot.SlotId;
            LoadRequested = true;
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

        private static Color SeedToColor(int seed)
        {
            uint hash = (uint)seed * 2654435761u;
            float r = ((hash >> 16) & 0xFF) / 255f;
            float g = ((hash >> 8) & 0xFF) / 255f;
            float b = (hash & 0xFF) / 255f;
            return new Color(0.25f + r * 0.55f, 0.25f + g * 0.55f, 0.25f + b * 0.55f);
        }

        private static string FormatRelative(DateTime savedAt)
        {
            var diff = DateTime.UtcNow - savedAt.ToUniversalTime();
            if (diff.TotalMinutes < 1) return "JUST NOW";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}M AGO";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}H AGO";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}D AGO";
            return savedAt.ToLocalTime().ToString("MMM dd").ToUpperInvariant();
        }

        private static string Truncate(string value, int maxLength)
        {
            if (value.Length <= maxLength) return value;
            return value[..(maxLength - 3)] + "...";
        }

        private readonly struct MenuMetrics
        {
            public float PanelX { get; init; }
            public float PanelY { get; init; }
            public float PanelW { get; init; }
            public float PanelH { get; init; }
            public float DetailStripY { get; init; }
            public float SlotListTop { get; init; }
            public Rectangle[] ButtonRects { get; init; }
        }
    }
}

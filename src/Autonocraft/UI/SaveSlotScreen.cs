using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Core;
using Autonocraft.Domain.Persistence;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.UI.Menu;
using Autonocraft.World;

namespace Autonocraft.UI
{
    public class SaveSlotScreen
    {
        private const float ShellWidth = 920f;
        private const float ShellHeight = 580f;
        private const float SidebarWidth = 300f;
        private const float SlotRowHeight = 64f;
        private const int MaxVisibleSlots = 6;

        // Vertical zones inside the main card (720p reference, scaled via UiLayout)
        private const float LifetimeStatsPadTop = 16f;
        private const float LifetimeStatsHeight = 56f;
        private const float BodyTopOffset = 64f;
        private const float ActionButtonHeight = 44f;
        private const float ActionButtonGap = 10f;
        private const float FooterLinkHeight = 34f;
        private const float CardBottomPad = 20f;
        private const float DetailHeroHeight = 190f;
        private const float DoubleClickWindow = 0.4f;
        private const int MaxRenameLength = 32;

        private readonly UiRenderer _ui;
        private readonly MenuBackdrop _backdrop = new MenuBackdrop();
        private readonly UiTransition _panelTransition = new UiTransition();
        private readonly float[] _buttonHoverT = new float[7];
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
        private WorldSaveData? _selectedSaveData;
        private int _worldCount;
        private bool _renaming;
        private string _renameBuffer = string.Empty;

        public bool LoadRequested { get; private set; }
        public bool NewWorldRequested { get; private set; }
        public bool StructureGalleryRequested { get; private set; }
        public bool SettingsRequested { get; private set; }
        public bool StatsRequested { get; private set; }
        public bool QuitRequested { get; private set; }
        public bool BackRequested { get; private set; }
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
            _selectedSaveData = null;
            if (_slots.Count == 0 || _selectedIndex < 0 || _selectedIndex >= _slots.Count)
            {
                return;
            }

            var slot = _slots[_selectedIndex];
            if (!slot.IsCorrupt)
            {
                WorldSaveManager.TryLoadPlayerStatistics(slot.SlotId, out var stats, out var saveData);
                _selectedWorldStats = stats;
                _selectedSaveData = saveData;
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
            StructureGalleryRequested = false;
            SettingsRequested = false;
            StatsRequested = false;
            QuitRequested = false;
            BackRequested = false;
            SelectedSlotId = null;

            var layout = new UiLayout(viewport);
            var metrics = ComputeLayout(layout);
            float offsetY = _panelTransition.OffsetY;

            _hoveredButton = -1;
            for (int i = 0; i < metrics.ButtonRects.Length; i++)
            {
                var rect = metrics.ButtonRects[i];
                var hitRect = new Rectangle(rect.X, rect.Y + (int)offsetY, rect.Width, rect.Height);
                if (hitRect.Contains(mouse.X, mouse.Y))
                {
                    _hoveredButton = i;
                    break;
                }
            }

            _hoveredSlotIndex = GetHoveredSlotIndex(mouse, metrics, layout, offsetY);

            int clickedSlot = GetClickedSlotIndex(mouse, metrics, layout, offsetY);
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
                if (TryHandleBackClick(mouse, prevMouse, metrics, layout, offsetY))
                {
                    _confirmingDelete = false;
                }
                else if (_hoveredButton == 0)
                {
                    if (_slots.Count > 0) TryRequestLoad();
                    _confirmingDelete = false;
                }
                else if (_hoveredButton == 1)
                {
                    NewWorldRequested = true;
                    _confirmingDelete = false;
                }
                else if (_hoveredButton == 6)
                {
                    StructureGalleryRequested = true;
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
                    BackRequested = true;
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

            MenuChrome.DrawBackdrop(_backdrop, _ui, viewport, alpha);

            float titleY = layout.S(44f) + offsetY;
            _ui.DrawCenteredTitle("Worlds", titleY, layout.S(UiTheme.FontHero), UiTheme.Title, alpha);
            _ui.DrawCenteredText("Pick up where you left off or start something new", titleY + layout.S(44f), layout.S(UiTheme.FontBody), UiTheme.Subtitle, alpha * 0.92f);

            _ui.DrawCard(metrics.ShellX, metrics.ShellY + offsetY, metrics.ShellW, metrics.ShellH, alpha, UiTheme.RadiusXl);

            DrawBrowserHeader(metrics, layout, alpha, offsetY);

            float dividerX = metrics.SidebarX + metrics.SidebarW;
            float dividerTop = metrics.ShellY + layout.S(BodyTopOffset) + offsetY;
            _ui.DrawFilledRect(dividerX, dividerTop, 1f, metrics.ShellH - layout.S(BodyTopOffset + 16f), UiTheme.Rule * (0.85f * alpha));

            DrawSidebarHeader(metrics, layout, alpha, offsetY);
            DrawBackLink(metrics, layout, alpha, offsetY);
            DrawSlotList(metrics, layout, alpha, offsetY);
            DrawDetailPanel(metrics, layout, alpha, offsetY);
            DrawActionButtons(metrics, layout, alpha, offsetY);

            if (!string.IsNullOrEmpty(_loadErrorMessage))
            {
                float flash = 0.65f + 0.35f * MathF.Sin(_errorFlashT * MathF.PI * 4f);
                _ui.DrawCenteredText(
                    Truncate(_loadErrorMessage, 64),
                    metrics.ShellY + metrics.ShellH + layout.S(16f) + offsetY,
                    layout.S(UiTheme.FontBody),
                    UiTheme.Danger * flash,
                    alpha);
            }

            string hint = _slots.Count == 0
                ? "No saves yet - create a new world here or return to the hub"
                : _renaming
                    ? "Enter to save · Esc to cancel"
                    : "Up/Down select · Enter continue · F2 rename · Double-click load";
            float hintsY = metrics.ShellY + metrics.ShellH + layout.S(string.IsNullOrEmpty(_loadErrorMessage) ? 28f : 48f) + offsetY;
            _ui.DrawCenteredText(hint, hintsY, layout.S(UiTheme.FontSmall), UiTheme.Hint, 0.85f * alpha);
        }

        private void DrawLifetimeCards(UiLayout layout, float y, float alpha)
        {
            float cardH = layout.S(LifetimeStatsHeight);
            float gap = layout.S(12f);
            float cardW = layout.S(148f);
            int cardCount = _worldCount == 0 ? 1 : 4;
            float totalW = cardCount * cardW + (cardCount - 1) * gap;
            float startX = layout.CenterX - totalW / 2f;

            if (_worldCount == 0)
            {
                _ui.DrawCard(startX, y, totalW, cardH, alpha * 0.9f, UiTheme.RadiusMd);
                _ui.DrawCenteredText("No worlds yet - start a new adventure", y + layout.S(20f), layout.S(UiTheme.FontBody), UiTheme.Subtitle, alpha);
                return;
            }

            DrawLifetimeStatCard(startX, y, cardW, cardH, "Play time", PlayerStatistics.FormatDuration(_lifetimeStats.TotalPlayTimeSeconds), UiTheme.StatAccentTime, layout, alpha, (float)(_lifetimeStats.TotalPlayTimeSeconds / 86400.0));
            DrawLifetimeStatCard(startX + (cardW + gap), y, cardW, cardH, "Distance", PlayerStatistics.FormatDistance(_lifetimeStats.DistanceWalked), UiTheme.StatAccentMove, layout, alpha, _lifetimeStats.DistanceWalked / 50000f);
            DrawLifetimeStatCard(startX + (cardW + gap) * 2f, y, cardW, cardH, "Kills", PlayerStatistics.FormatCount(_lifetimeStats.AnimalsKilled), UiTheme.StatAccentCombat, layout, alpha, _lifetimeStats.AnimalsKilled / 100f);
            DrawLifetimeStatCard(startX + (cardW + gap) * 3f, y, cardW, cardH, "Worlds", _worldCount.ToString(), UiTheme.StatAccentWorld, layout, alpha, _worldCount / 8f);
        }

        private void DrawLifetimeStatCard(float x, float y, float w, float h, string label, string value, Color accent, UiLayout layout, float alpha, float fillT)
        {
            _ui.DrawRoundedRect(x, y, w, h, layout.S(UiTheme.RadiusMd), UiTheme.SurfaceElevated * (0.94f * alpha));
            _ui.DrawRoundedRectOutline(x, y, w, h, layout.S(UiTheme.RadiusMd), UiTheme.PanelBorder, 1f, 0.65f * alpha);

            float accentW = layout.S(4f);
            _ui.DrawRoundedRect(x, y, accentW, h, layout.S(3f), accent * (0.92f * alpha));

            float iconSize = layout.S(8f);
            _ui.DrawRoundedRect(x + layout.S(14f), y + layout.S(12f), iconSize, iconSize, iconSize * 0.5f, accent * (0.85f * alpha));

            _ui.DrawLabel(label, x + layout.S(28f), y + layout.S(10f), layout.S(UiTheme.FontCaption), UiTheme.StatLabel, alpha: alpha);
            _ui.DrawLabel(value, x + layout.S(14f), y + layout.S(28f), layout.S(UiTheme.FontSection), UiTheme.StatValue, semiBold: true, alpha: alpha);

            float barY = y + h - layout.S(10f);
            float barW = w - layout.S(28f);
            float barH = layout.S(3f);
            _ui.DrawRoundedRect(x + layout.S(14f), barY, barW, barH, barH * 0.5f, UiTheme.ProgressTrack * alpha);
            float progress = Math.Clamp(fillT, 0.04f, 1f);
            _ui.DrawRoundedRect(x + layout.S(14f), barY, barW * progress, barH, barH * 0.5f, accent * (0.75f * alpha));
        }

        private void DrawSidebarHeader(MenuMetrics metrics, UiLayout layout, float alpha, float offsetY)
        {
            float headerY = metrics.BodyTop + layout.S(8f) + offsetY;
            UiTheme.DrawSectionHeader(_ui, "Your worlds", metrics.SidebarX + layout.S(20f), headerY, layout, alpha);

            if (_slots.Count > 0)
            {
                string count = $"{_selectedIndex + 1} of {_slots.Count}";
                float countW = _ui.MeasureString(count, layout.S(UiTheme.FontSmall));
                _ui.DrawString(count, metrics.SidebarX + metrics.SidebarW - layout.S(20f) - countW, headerY + layout.S(2f),
                    layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
            }
        }

        private void DrawBrowserHeader(MenuMetrics metrics, UiLayout layout, float alpha, float offsetY)
        {
            float x = metrics.DetailX + layout.S(24f);
            float y = metrics.ShellY + layout.S(18f) + offsetY;
            _ui.DrawLabel("Save browser", x, y, layout.S(UiTheme.FontSection), UiTheme.Title, semiBold: true, alpha: alpha);
            _ui.DrawLabel($"{_worldCount} worlds · {PlayerStatistics.FormatDuration(_lifetimeStats.TotalPlayTimeSeconds)} total play time",
                x, y + layout.S(24f), layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha: alpha * 0.9f);
            MenuChrome.DrawSectionRule(_ui, layout, metrics.ShellX + layout.S(20f), metrics.ShellY + layout.S(58f) + offsetY,
                metrics.ShellW - layout.S(40f), alpha);
        }

        private void DrawBackLink(MenuMetrics metrics, UiLayout layout, float alpha, float offsetY)
        {
            float x = metrics.ShellX + layout.S(20f);
            float y = metrics.ShellY + layout.S(12f) + offsetY;
            MenuChrome.DrawMetaChip(_ui, layout, "Back to Main Menu", x, y, UiTheme.Accent, alpha);
        }

        private bool TryHandleBackClick(MouseState mouse, MouseState prevMouse, MenuMetrics metrics, UiLayout layout, float offsetY)
        {
            float x = metrics.ShellX + layout.S(20f);
            float y = metrics.ShellY + layout.S(12f) + offsetY;
            float w = layout.S(180f);
            float h = layout.S(24f);
            var rect = new Rectangle((int)x, (int)y, (int)w, (int)h);
            if (rect.Contains(mouse.X, mouse.Y)
                && mouse.LeftButton == ButtonState.Pressed
                && prevMouse.LeftButton == ButtonState.Released)
            {
                BackRequested = true;
                return true;
            }

            return false;
        }

        private void DrawSlotList(MenuMetrics metrics, UiLayout layout, float alpha, float offsetY)
        {
            float listX = metrics.SidebarX + layout.S(14f);
            float listW = metrics.SidebarW - layout.S(28f);
            float listTop = metrics.SlotListTop + offsetY;
            float rowH = layout.S(SlotRowHeight);
            float listHeight = rowH * MaxVisibleSlots;
            float slideOffset = (1f - _scrollOffsetT) * layout.S(6f);

            if (_slots.Count == 0)
            {
                float emptyY = listTop + listHeight / 2f - layout.S(20f);
                _ui.DrawString("No saved worlds", listX, emptyY, layout.S(UiTheme.FontBody), UiTheme.Subtitle, alpha);
                _ui.DrawString("Create one from the actions panel", listX, emptyY + layout.S(24f), layout.S(UiTheme.FontSmall), UiTheme.Accent, alpha);
                return;
            }

            for (int i = 0; i < MaxVisibleSlots; i++)
            {
                int slotIndex = _scrollOffset + i;
                if (slotIndex >= _slots.Count) break;

                var slot = _slots[slotIndex];
                float rowY = listTop + i * rowH + slideOffset;
                bool selected = slotIndex == _selectedIndex;
                float hoverBlend = _slotHoverT[i];
                float rowAlpha = alpha;

                Color fill = selected
                    ? Color.Lerp(UiTheme.AccentSoft, UiTheme.PanelBgHighlight, _selectedBorderT)
                    : Color.Lerp(UiTheme.PanelBgMuted, UiTheme.PanelBgHighlight, hoverBlend * 0.75f);

                float rowW = listW;
                float rowInnerH = rowH - layout.S(8f);
                float radius = layout.S(UiTheme.RadiusMd);

                _ui.DrawRoundedRect(listX, rowY, rowW, rowInnerH, radius, fill * rowAlpha);
                if (selected)
                {
                    _ui.DrawRoundedRectOutline(listX, rowY, rowW, rowInnerH, radius, UiTheme.Accent, 2f, 0.9f * rowAlpha);
                }
                else if (hoverBlend > 0.01f)
                {
                    _ui.DrawRoundedRectOutline(listX, rowY, rowW, rowInnerH, radius, UiTheme.PanelBorder, 1f, 0.5f * rowAlpha);
                }

                float accentBarW = layout.S(4f);
                if (selected)
                {
                    _ui.DrawRoundedRect(listX, rowY, accentBarW, rowInnerH, layout.S(3f), UiTheme.Accent * rowAlpha);
                }

                float swatch = layout.S(36f);
                float swatchX = listX + layout.S(14f);
                float swatchY = rowY + (rowInnerH - swatch) / 2f;
                if (slot.IsCorrupt)
                {
                    _ui.DrawRoundedRect(swatchX, swatchY, swatch, swatch, layout.S(8f), UiTheme.DangerSoft * rowAlpha);
                    _ui.DrawRoundedRectOutline(swatchX, swatchY, swatch, swatch, layout.S(8f), UiTheme.Danger, 1.5f, 0.8f * rowAlpha);
                }
                else
                {
                    var thumbnail = _ui.WorldThumbnails.GetThumbnail(slot.Seed);
                    _ui.DrawThumbnailFrame(thumbnail, swatchX, swatchY, swatch, rowAlpha, layout.S(8f));
                }

                float textX = swatchX + swatch + layout.S(12f);
                string title = Truncate(slot.SlotName, 18);
                string meta = slot.IsCorrupt ? "Corrupt save" : $"Seed {slot.Seed} · {FormatRelative(slot.SavedAt)}";
                Color titleColor = slot.IsCorrupt ? UiTheme.Danger : UiTheme.Title;
                _ui.DrawString(title, textX, rowY + layout.S(10f), layout.S(UiTheme.FontBody), titleColor, rowAlpha, semiBold: selected);
                _ui.DrawString(meta, textX, rowY + layout.S(30f), layout.S(UiTheme.FontSmall), UiTheme.Meta, rowAlpha * 0.95f);
            }

            if (_slots.Count > MaxVisibleSlots)
            {
                string scrollHint = $"Showing {_scrollOffset + 1}–{Math.Min(_scrollOffset + MaxVisibleSlots, _slots.Count)}";
                _ui.DrawString(scrollHint, listX, listTop + listHeight + layout.S(4f), layout.S(UiTheme.FontCaption), UiTheme.Hint, alpha * 0.85f);
            }
        }

        private void DrawDetailPanel(MenuMetrics metrics, UiLayout layout, float alpha, float offsetY)
        {
            float panelX = metrics.DetailX + layout.S(24f);
            float panelY = metrics.BodyTop + layout.S(8f) + offsetY;
            float panelW = metrics.DetailW - layout.S(48f);

            if (_slots.Count == 0)
            {
                _ui.DrawLabel("Welcome", panelX, panelY, layout.S(UiTheme.FontTitle), UiTheme.Title, semiBold: true, alpha: alpha);
                _ui.DrawLabel("Start a fresh world and shape the landscape block by block.", panelX, panelY + layout.S(36f),
                    layout.S(UiTheme.FontBody), UiTheme.Subtitle, alpha: alpha * 0.9f);
                _ui.DrawRoundedRect(panelX, panelY + layout.S(88f), panelW, layout.S(120f), layout.S(UiTheme.RadiusMd),
                    UiTheme.PanelBgAccent * (0.85f * alpha));
                _ui.DrawCenteredText("Your first world preview will appear here", panelY + layout.S(140f),
                    layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha * 0.9f);
                return;
            }

            var slot = _slots[_selectedIndex];
            string worldTitle = _renaming ? _renameBuffer + "|" : slot.SlotName;
            _ui.DrawLabel(worldTitle, panelX, panelY, layout.S(UiTheme.FontTitle), slot.IsCorrupt ? UiTheme.Danger : UiTheme.Title, semiBold: true, alpha: alpha);

            string biomeSummary = string.Empty;
            float metaY = panelY + layout.S(36f);
            if (slot.IsCorrupt)
            {
                _ui.DrawLabel("This save could not be read", panelX, metaY, layout.S(UiTheme.FontBody), UiTheme.Danger, alpha: alpha * 0.9f);
            }
            else
            {
                biomeSummary = _ui.WorldThumbnails.GetBiomeSummary(slot.Seed);
                string seedText = $"Seed {slot.Seed}";
                float seedFont = layout.S(UiTheme.FontSmall);
                _ui.DrawLabel(seedText, panelX, metaY, seedFont, UiTheme.Subtitle, alpha: alpha * 0.92f);
                float chipX = panelX + _ui.MeasureString(seedText, seedFont) + layout.S(12f);
                DrawMetaChip(chipX, metaY - layout.S(2f), biomeSummary, UiTheme.StatAccentExplore, layout, alpha);
                _ui.DrawLabel(FormatLastPlayed(slot.SavedAt), panelX, metaY + layout.S(22f), layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha: alpha * 0.95f);
            }

            float heroY = panelY + layout.S(82f);
            float heroH = layout.S(DetailHeroHeight);
            if (slot.IsCorrupt)
            {
                _ui.DrawRoundedRect(panelX, heroY, panelW, heroH, layout.S(UiTheme.RadiusMd), UiTheme.DangerSoft * (0.75f * alpha));
                _ui.DrawRoundedRectOutline(panelX, heroY, panelW, heroH, layout.S(UiTheme.RadiusMd), UiTheme.Danger, 1.5f, 0.7f * alpha);
                _ui.DrawCenteredText("Save data unavailable", heroY + heroH * 0.42f, layout.S(UiTheme.FontBody), UiTheme.Danger, alpha);
            }
            else
            {
                DrawWorldMapPreview(slot, panelX, heroY, panelW, heroH, biomeSummary, layout, alpha);
            }

            float summaryY = heroY + heroH + layout.S(12f);
            string summary = $"Played {PlayerStatistics.FormatDuration(_selectedWorldStats.TotalPlayTimeSeconds)}"
                + $" · Walked {PlayerStatistics.FormatDistance(_selectedWorldStats.DistanceWalked)}"
                + $" · {PlayerStatistics.FormatCount(_selectedWorldStats.SessionCount)} sessions";
            _ui.DrawLabel(summary, panelX, summaryY, layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha: alpha * 0.92f);
        }

        private void DrawWorldMapPreview(SaveSlotInfo slot, float x, float y, float w, float h, string biomeSummary, UiLayout layout, float alpha)
        {
            const int mapSpan = 384;
            var save = _selectedSaveData;
            int centerX = save != null && MathF.Abs(save.Player.PosX) > 0.01f ? (int)MathF.Round(save.Player.PosX) : save?.Spawn.X ?? 16;
            int centerZ = save != null && MathF.Abs(save.Player.PosZ) > 0.01f ? (int)MathF.Round(save.Player.PosZ) : save?.Spawn.Z ?? 16;

            var map = _ui.WorldThumbnails.GetMapPreview(slot.Seed, centerX, centerZ, mapSpan);
            _ui.DrawRoundedRect(x, y, w, h, layout.S(UiTheme.RadiusMd), UiTheme.PanelBgMuted * (0.75f * alpha));
            _ui.DrawTexture(map, x, y, w, h, alpha);
            _ui.DrawRoundedRectOutline(x, y, w, h, layout.S(UiTheme.RadiusMd), UiTheme.PanelBorder, 1.5f, 0.9f * alpha);

            DrawMapMarker(x, y, w, h, centerX, centerZ, centerX, centerZ, mapSpan, UiTheme.AccentGlow, layout, alpha, primary: true);

            if (save != null)
            {
                DrawMapMarker(x, y, w, h, save.Spawn.X, save.Spawn.Z, centerX, centerZ, mapSpan, UiTheme.Success, layout, alpha);
                foreach (var village in save.Villages)
                {
                    DrawMapMarker(x, y, w, h, village.AnchorX, village.AnchorZ, centerX, centerZ, mapSpan, UiTheme.StatAccentWorld, layout, alpha);
                }
            }

            string label = $"{biomeSummary} · {centerX}, {centerZ}";
            _ui.DrawLabel(label, x + layout.S(10f), y + h - layout.S(22f), layout.S(UiTheme.FontCaption), Color.White, alpha: alpha * 0.74f);
        }

        private void DrawMapMarker(
            float mapX,
            float mapY,
            float mapW,
            float mapH,
            int wx,
            int wz,
            int centerX,
            int centerZ,
            int span,
            Color color,
            UiLayout layout,
            float alpha,
            bool primary = false)
        {
            float tx = 0.5f + (wx - centerX) / (float)span;
            float ty = 0.5f + (wz - centerZ) / (float)span;
            if (tx < 0f || tx > 1f || ty < 0f || ty > 1f)
            {
                return;
            }

            float px = mapX + tx * mapW;
            float py = mapY + ty * mapH;
            float size = layout.S(primary ? 9f : 6f);
            _ui.DrawRoundedRect(px - size / 2f, py - size / 2f, size, size, size * 0.5f, color * (0.92f * alpha));
            if (primary)
            {
                _ui.DrawRoundedRectOutline(px - size / 2f - 2f, py - size / 2f - 2f, size + 4f, size + 4f, (size + 4f) * 0.5f, Color.White, 1f, 0.72f * alpha);
            }
        }

        private void DrawMetaChip(float x, float y, string label, Color accent, UiLayout layout, float alpha)
        {
            float padX = layout.S(10f);
            float padY = layout.S(4f);
            float textW = _ui.MeasureString(label, layout.S(UiTheme.FontCaption));
            float chipW = textW + padX * 2f;
            float chipH = layout.S(18f);
            _ui.DrawRoundedRect(x, y, chipW, chipH, chipH * 0.5f, accent * (0.14f * alpha));
            _ui.DrawRoundedRectOutline(x, y, chipW, chipH, chipH * 0.5f, accent * (0.45f * alpha), 1f, alpha);
            _ui.DrawLabel(label, x + padX, y + padY, layout.S(UiTheme.FontCaption), accent, semiBold: true, alpha: alpha * 0.95f);
        }

        private void DrawActionButtons(MenuMetrics metrics, UiLayout layout, float alpha, float offsetY)
        {
            bool hasSlots = _slots.Count > 0;
            bool canLoad = hasSlots && !_slots[_selectedIndex].IsCorrupt;
            string deleteLabel = _confirmingDelete ? "Confirm delete?" : "Delete world";

            DrawStyledButton(metrics.ButtonRects[0], canLoad ? "Continue" : "Cannot load this save", 0, layout, alpha, offsetY, UiButtonStyle.Primary, !canLoad);
            DrawStyledButton(metrics.ButtonRects[1], "New world", 1, layout, alpha, offsetY, UiButtonStyle.Secondary);
            DrawStyledButton(metrics.ButtonRects[6], "Gallery", 6, layout, alpha, offsetY, UiButtonStyle.Secondary);
            DrawStyledButton(metrics.ButtonRects[2], deleteLabel, 2, layout, alpha, offsetY, UiButtonStyle.Danger, !hasSlots);
            DrawStyledButton(metrics.ButtonRects[3], "Statistics", 3, layout, alpha, offsetY, UiButtonStyle.Ghost, !hasSlots);
            DrawStyledButton(metrics.ButtonRects[4], "Settings", 4, layout, alpha, offsetY, UiButtonStyle.Ghost);
            DrawStyledButton(metrics.ButtonRects[5], "Quit", 5, layout, alpha, offsetY, UiButtonStyle.Ghost);
        }

        private void DrawStyledButton(Rectangle rect, string label, int index, UiLayout layout, float alpha, float offsetY, UiButtonStyle style, bool disabled = false)
        {
            _ui.DrawButton(
                rect.X,
                rect.Y + offsetY,
                rect.Width,
                rect.Height,
                label,
                _hoveredButton == index && !disabled,
                false,
                style,
                layout.S(UiTheme.FontBody),
                alpha,
                _buttonHoverT[index],
                disabled);
        }

        private MenuMetrics ComputeLayout(UiLayout layout)
        {
            float shellW = layout.S(ShellWidth);
            float shellH = Math.Min(layout.S(ShellHeight), layout.Height - layout.S(112f));
            float shellX = layout.CenterX - shellW / 2f;
            float shellY = Math.Max(layout.S(108f), layout.CenterY - shellH / 2f + layout.S(28f));

            float sidebarW = layout.S(SidebarWidth);
            float sidebarX = shellX;
            float detailX = shellX + sidebarW;
            float detailW = shellW - sidebarW;
            float bodyTop = shellY + layout.S(BodyTopOffset);
            float lifetimeStatsY = shellY + layout.S(LifetimeStatsPadTop);

            float buttonH = layout.S(ActionButtonHeight);
            float buttonGap = layout.S(ActionButtonGap);
            float footerH = layout.S(FooterLinkHeight);
            float detailPadX = detailX + layout.S(24f);
            float buttonW = detailW - layout.S(48f);
            float splitButtonW = (buttonW - buttonGap) / 2f;
            float ghostW = (buttonW - buttonGap * 2f) / 3f;

            float footerRowY = shellY + shellH - layout.S(CardBottomPad) - footerH;
            float deleteY = footerRowY - buttonGap - buttonH;
            float secondaryY = deleteY - buttonGap - buttonH;
            float continueY = secondaryY - buttonGap - buttonH;

            var buttons = new Rectangle[7];
            buttons[0] = new Rectangle((int)detailPadX, (int)continueY, (int)buttonW, (int)buttonH);
            buttons[1] = new Rectangle((int)detailPadX, (int)secondaryY, (int)splitButtonW, (int)buttonH);
            buttons[2] = new Rectangle((int)detailPadX, (int)deleteY, (int)buttonW, (int)buttonH);
            buttons[3] = new Rectangle((int)detailPadX, (int)footerRowY, (int)ghostW, (int)footerH);
            buttons[4] = new Rectangle((int)(detailPadX + ghostW + buttonGap), (int)footerRowY, (int)ghostW, (int)footerH);
            buttons[5] = new Rectangle((int)(detailPadX + (ghostW + buttonGap) * 2f), (int)footerRowY, (int)ghostW, (int)footerH);
            buttons[6] = new Rectangle((int)(detailPadX + splitButtonW + buttonGap), (int)secondaryY, (int)splitButtonW, (int)buttonH);

            return new MenuMetrics
            {
                ShellX = shellX,
                ShellY = shellY,
                ShellW = shellW,
                ShellH = shellH,
                SidebarX = sidebarX,
                SidebarW = sidebarW,
                DetailX = detailX,
                DetailW = detailW,
                BodyTop = bodyTop,
                LifetimeStatsY = lifetimeStatsY,
                SlotListTop = bodyTop + layout.S(42f),
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

            TextInputKeys.AppendPressedCharacters(kb, prevKb, ref _renameBuffer, MaxRenameLength, TextInputCharacterSet.Name);
        }

        private int GetHoveredSlotIndex(MouseState mouse, MenuMetrics metrics, UiLayout layout, float offsetY)
        {
            return GetSlotIndexAt(mouse.X, mouse.Y, metrics, layout, offsetY);
        }

        private int GetClickedSlotIndex(MouseState mouse, MenuMetrics metrics, UiLayout layout, float offsetY)
        {
            if (mouse.LeftButton != ButtonState.Pressed) return -1;
            return GetSlotIndexAt(mouse.X, mouse.Y, metrics, layout, offsetY);
        }

        private int GetSlotIndexAt(int mouseX, int mouseY, MenuMetrics metrics, UiLayout layout, float offsetY)
        {
            if (_slots.Count == 0) return -1;

            float listX = metrics.SidebarX + layout.S(14f);
            float listW = metrics.SidebarW - layout.S(28f);
            float rowH = layout.S(SlotRowHeight);
            float listHeight = rowH * MaxVisibleSlots;
            float listTop = metrics.SlotListTop + offsetY;

            var listRect = new Rectangle((int)listX, (int)listTop, (int)listW, (int)listHeight);
            if (!listRect.Contains(mouseX, mouseY)) return -1;

            int row = (int)((mouseY - listTop) / rowH);
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

        private static string FormatLastPlayed(DateTime savedAt)
        {
            var diff = DateTime.UtcNow - savedAt.ToUniversalTime();
            if (diff.TotalMinutes < 1) return "Last played just now";
            if (diff.TotalHours < 1) return $"Last played {(int)diff.TotalMinutes}m ago";
            if (diff.TotalDays < 1) return $"Last played {(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"Last played {(int)diff.TotalDays}d ago";
            return $"Last played {savedAt.ToLocalTime():MMM d, yyyy}";
        }

        private static string FormatRelative(DateTime savedAt)
        {
            var diff = DateTime.UtcNow - savedAt.ToUniversalTime();
            if (diff.TotalMinutes < 1) return "just now";
            if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return savedAt.ToLocalTime().ToString("MMM d");
        }

        private static string Truncate(string value, int maxLength)
        {
            if (value.Length <= maxLength) return value;
            return value[..(maxLength - 3)] + "...";
        }

        private readonly struct MenuMetrics
        {
            public float ShellX { get; init; }
            public float ShellY { get; init; }
            public float ShellW { get; init; }
            public float ShellH { get; init; }
            public float SidebarX { get; init; }
            public float SidebarW { get; init; }
            public float DetailX { get; init; }
            public float DetailW { get; init; }
            public float BodyTop { get; init; }
            public float LifetimeStatsY { get; init; }
            public float SlotListTop { get; init; }
            public Rectangle[] ButtonRects { get; init; }
        }
    }
}

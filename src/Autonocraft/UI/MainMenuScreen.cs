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
    public class MainMenuScreen
    {
        public enum HubAction
        {
            Continue,
            BrowseSaves,
            NewWorld,
            Settings,
            StructureGallery,
            Quit
        }

        private const float ButtonWidth = 280f;
        private const float ButtonHeight = 48f;
        private const float ButtonSpacing = 10f;

        private readonly UiRenderer _ui;
        private readonly MenuBackdrop _backdrop = new MenuBackdrop();
        private readonly MenuFocusList _focus = new MenuFocusList();
        private readonly float[] _buttonHoverT = new float[6];

        private readonly List<HubAction> _visibleActions = new();
        private SaveSlotInfo? _continueSlot;
        private PlayerStatistics _lifetimeStats = new();
        private int _worldCount;

        public bool ContinueRequested { get; private set; }
        public bool BrowseSavesRequested { get; private set; }
        public bool NewWorldRequested { get; private set; }
        public bool SettingsRequested { get; private set; }
        public bool StructureGalleryRequested { get; private set; }
        public bool QuitRequested { get; private set; }
        public string? ContinueSlotId { get; private set; }

        public MainMenuScreen(UiRenderer ui)
        {
            _ui = ui;
            RefreshContinueEligibility();
        }

        public void RefreshContinueEligibility()
        {
            _continueSlot = WorldSaveManager.GetMostRecentSaveSlot();
            (_lifetimeStats, _worldCount) = WorldSaveManager.AggregateLifetimeStatistics();
            RebuildVisibleActions();
            _focus.Reset(_visibleActions.Count);
        }

        public void Update(Viewport viewport, KeyboardState kb, MouseState mouse, KeyboardState prevKb, MouseState prevMouse, float deltaTime)
        {
            ContinueRequested = false;
            BrowseSavesRequested = false;
            NewWorldRequested = false;
            SettingsRequested = false;
            StructureGalleryRequested = false;
            QuitRequested = false;
            ContinueSlotId = null;

            var layout = new UiLayout(viewport);
            var metrics = ComputeLayout(layout);
            var rects = BuildButtonRects(metrics, layout);

            _focus.Update(_visibleActions.Count, rects, kb, prevKb, mouse);

            for (int i = 0; i < _buttonHoverT.Length; i++)
            {
                float target = _focus.IsItemFocused(i) ? 1f : 0f;
                _buttonHoverT[i] = Tween.SmoothDamp(_buttonHoverT[i], target, 10f, deltaTime);
            }

            int clicked = _focus.GetClickedIndex(rects, mouse, prevMouse);
            if (clicked >= 0)
            {
                ActivateAction(clicked);
            }
            else if (_focus.TryConsumeEnter(kb, prevKb, _visibleActions.Count, out int enterIndex))
            {
                ActivateAction(enterIndex);
            }
        }

        public void Draw(Viewport viewport, float deltaTime, float alpha = 1f, float offsetY = 0f)
        {
            var layout = new UiLayout(viewport);
            var metrics = ComputeLayout(layout);
            var rects = BuildButtonRects(metrics, layout);

            MenuChrome.DrawBackdrop(_backdrop, _ui, viewport, deltaTime, alpha);

            DrawHeroPanel(layout, metrics, alpha, offsetY);
            DrawActionPanel(layout, metrics, alpha, offsetY);

            for (int i = 0; i < _visibleActions.Count; i++)
            {
                var rect = rects[i];
                var action = _visibleActions[i];
                string label = GetActionLabel(action);
                var style = action switch
                {
                    HubAction.Continue => UiButtonStyle.Primary,
                    HubAction.Quit or HubAction.StructureGallery => UiButtonStyle.Ghost,
                    _ => UiButtonStyle.Secondary
                };

                bool keyboardFocus = _focus.FocusedIndex == i && _focus.HoverIndex < 0;
                _ui.DrawButton(
                    rect.X,
                    rect.Y + offsetY,
                    rect.Width,
                    rect.Height,
                    label,
                    _focus.IsItemFocused(i),
                    keyboardFocus,
                    style,
                    layout.S(UiTheme.FontSection),
                    alpha,
                    _buttonHoverT[i]);
            }

            MenuChrome.DrawHintFooter(_ui, layout, "Up/Down or Tab to navigate · Enter to select", alpha);
        }

        public static MainMenuLayoutMetrics ComputeLayoutMetrics(int viewportWidth, int viewportHeight, bool hasContinueSave = false)
        {
            var layout = new UiLayout(viewportWidth, viewportHeight);
            int actionCount = hasContinueSave ? 6 : 5;
            float shellW = Math.Min(layout.S(560f), layout.Width - layout.S(56f));
            float shellH = layout.S(500f);
            float shellX = layout.CenterX - shellW / 2f;
            float shellY = layout.CenterY - shellH / 2f + layout.S(32f);
            float buttonW = layout.S(320f);
            float actionX = layout.CenterX - buttonW / 2f;
            float actionY = shellY + layout.S(172f);
            float buttonH = layout.S(ButtonHeight);
            float buttonSpacing = layout.S(ButtonSpacing);

            var buttonRects = new List<Rectangle>();
            float y = actionY;
            for (int i = 0; i < actionCount; i++)
            {
                buttonRects.Add(new Rectangle(
                    (int)actionX,
                    (int)y,
                    (int)buttonW,
                    (int)buttonH));
                y += buttonH + buttonSpacing;
            }

            return new MainMenuLayoutMetrics
            {
                ViewportWidth = (int)layout.Width,
                ViewportHeight = (int)layout.Height,
                ShellRect = new Rectangle((int)shellX, (int)shellY, (int)shellW, (int)shellH),
                ButtonRects = buttonRects
            };
        }

        private MainMenuLayoutMetrics ComputeLayout(UiLayout layout)
        {
            float shellW = Math.Min(layout.S(560f), layout.Width - layout.S(56f));
            float shellH = layout.S(500f);
            float shellX = layout.CenterX - shellW / 2f;
            float shellY = layout.CenterY - shellH / 2f + layout.S(32f);
            float buttonW = layout.S(320f);
            float actionX = layout.CenterX - buttonW / 2f;
            float actionY = shellY + layout.S(172f);
            float buttonH = layout.S(ButtonHeight);
            float buttonSpacing = layout.S(ButtonSpacing);

            var buttonRects = new List<Rectangle>();
            float y = actionY;
            for (int i = 0; i < _visibleActions.Count; i++)
            {
                buttonRects.Add(new Rectangle(
                    (int)actionX,
                    (int)y,
                    (int)buttonW,
                    (int)buttonH));
                y += buttonH + buttonSpacing;
            }

            return new MainMenuLayoutMetrics
            {
                ViewportWidth = (int)layout.Width,
                ViewportHeight = (int)layout.Height,
                ShellRect = new Rectangle((int)shellX, (int)shellY, (int)shellW, (int)shellH),
                ButtonRects = buttonRects
            };
        }

        private void DrawHeroPanel(UiLayout layout, MainMenuLayoutMetrics metrics, float alpha, float offsetY)
        {
            float y = metrics.ShellRect.Y + offsetY;
            _ui.DrawCenteredTitle("Autonocraft", y + layout.S(18f), layout.S(52f), UiTheme.Title, alpha);
            _ui.DrawCenteredText("Survive, build, and grow a settlement", y + layout.S(78f), layout.S(UiTheme.FontBody), UiTheme.Subtitle, alpha * 0.9f);

            float chipY = y + layout.S(116f);
            string worlds = $"{_worldCount} worlds";
            string time = PlayerStatistics.FormatDuration(_lifetimeStats.TotalPlayTimeSeconds);
            string last = _continueSlot != null && !_continueSlot.IsCorrupt ? $"Last: {_continueSlot.SlotName}" : "No recent world";
            float totalW = layout.S(118f) + layout.S(132f) + layout.S(190f) + layout.S(20f);
            float x = layout.CenterX - totalW / 2f;
            MenuChrome.DrawMetaChip(_ui, layout, worlds, x, chipY, UiTheme.StatAccentWorld, alpha);
            MenuChrome.DrawMetaChip(_ui, layout, $"Played {time}", x + layout.S(128f), chipY, UiTheme.StatAccentTime, alpha);
            MenuChrome.DrawMetaChip(_ui, layout, Truncate(last, 28), x + layout.S(270f), chipY, UiTheme.StatAccentExplore, alpha);
        }

        private void DrawActionPanel(UiLayout layout, MainMenuLayoutMetrics metrics, float alpha, float offsetY)
        {
            if (metrics.ButtonRects.Count == 0)
            {
                return;
            }

            float x = metrics.ButtonRects[0].X;
            float y = metrics.ButtonRects[0].Y - layout.S(22f) + offsetY;
            MenuChrome.DrawSectionRule(_ui, layout, x, y, metrics.ButtonRects[0].Width, alpha);
        }

        private void DrawHubStat(float x, float y, float w, string label, string value, Color accent, UiLayout layout, float alpha)
        {
            float h = layout.S(54f);
            _ui.DrawRoundedRect(x, y, w, h, layout.S(UiTheme.RadiusMd), UiTheme.PanelBgMuted * (0.82f * alpha));
            _ui.DrawRoundedRectOutline(x, y, w, h, layout.S(UiTheme.RadiusMd), UiTheme.PanelBorder, 1f, 0.5f * alpha);
            _ui.DrawRoundedRect(x, y, layout.S(3f), h, layout.S(2f), accent * (0.85f * alpha));
            _ui.DrawLabel(label, x + layout.S(12f), y + layout.S(9f), layout.S(UiTheme.FontCaption), UiTheme.StatLabel, alpha: alpha);
            _ui.DrawLabel(value, x + layout.S(12f), y + layout.S(26f), layout.S(UiTheme.FontBody), UiTheme.StatValue, semiBold: true, alpha: alpha);
        }

        private List<Rectangle> BuildButtonRects(MainMenuLayoutMetrics metrics, UiLayout layout)
        {
            if (metrics.ButtonRects.Count == _visibleActions.Count)
            {
                return metrics.ButtonRects;
            }

            return ComputeLayout(layout).ButtonRects;
        }

        private void RebuildVisibleActions()
        {
            _visibleActions.Clear();
            if (_continueSlot != null && !_continueSlot.IsCorrupt)
            {
                _visibleActions.Add(HubAction.Continue);
            }

            _visibleActions.Add(HubAction.BrowseSaves);
            _visibleActions.Add(HubAction.NewWorld);
            _visibleActions.Add(HubAction.Settings);
            _visibleActions.Add(HubAction.StructureGallery);
            _visibleActions.Add(HubAction.Quit);
        }

        private string GetActionLabel(HubAction action)
        {
            return action switch
            {
                HubAction.Continue => _continueSlot != null
                    ? $"Continue — {_continueSlot.SlotName}"
                    : "Continue",
                HubAction.BrowseSaves => "Play / Browse Saves",
                HubAction.NewWorld => "New World",
                HubAction.Settings => "Settings",
                HubAction.StructureGallery => "Structure Gallery",
                HubAction.Quit => "Quit",
                _ => action.ToString()
            };
        }

        private static string Truncate(string value, int maxLength)
        {
            if (value.Length <= maxLength) return value;
            return value[..(maxLength - 3)] + "...";
        }

        private void ActivateAction(int index)
        {
            if (index < 0 || index >= _visibleActions.Count)
            {
                return;
            }

            switch (_visibleActions[index])
            {
                case HubAction.Continue:
                    ContinueRequested = true;
                    ContinueSlotId = _continueSlot?.SlotId;
                    break;
                case HubAction.BrowseSaves:
                    BrowseSavesRequested = true;
                    break;
                case HubAction.NewWorld:
                    NewWorldRequested = true;
                    break;
                case HubAction.Settings:
                    SettingsRequested = true;
                    break;
                case HubAction.StructureGallery:
                    StructureGalleryRequested = true;
                    break;
                case HubAction.Quit:
                    QuitRequested = true;
                    break;
            }
        }
    }

    public sealed class MainMenuLayoutMetrics
    {
        public int ViewportWidth { get; init; }
        public int ViewportHeight { get; init; }
        public Rectangle ShellRect { get; init; }
        public List<Rectangle> ButtonRects { get; init; } = new();
    }
}

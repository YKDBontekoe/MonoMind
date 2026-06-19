using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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
            MenuChrome.DrawTitleBlock(_ui, layout, "Autonocraft", "A voxel sandbox for builders and explorers", 0.24f, alpha, offsetY);

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

            MenuChrome.DrawHintFooter(_ui, layout, "↑↓ or Tab to navigate · Enter to select", alpha);
        }

        public static MainMenuLayoutMetrics ComputeLayoutMetrics(int viewportWidth, int viewportHeight, bool hasContinueSave = false)
        {
            var layout = new UiLayout(viewportWidth, viewportHeight);
            int actionCount = hasContinueSave ? 6 : 5;
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float buttonSpacing = layout.S(ButtonSpacing);
            float cx = layout.CenterX;
            float startY = layout.CenterY - layout.S(12f);

            var buttonRects = new List<Rectangle>();
            float y = startY;
            for (int i = 0; i < actionCount; i++)
            {
                buttonRects.Add(new Rectangle(
                    (int)(cx - buttonW / 2f),
                    (int)y,
                    (int)buttonW,
                    (int)buttonH));
                y += buttonH + buttonSpacing;
            }

            return new MainMenuLayoutMetrics
            {
                ViewportWidth = (int)layout.Width,
                ViewportHeight = (int)layout.Height,
                ButtonRects = buttonRects
            };
        }

        private MainMenuLayoutMetrics ComputeLayout(UiLayout layout)
        {
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float buttonSpacing = layout.S(ButtonSpacing);
            float cx = layout.CenterX;
            float startY = layout.CenterY - layout.S(12f);

            var buttonRects = new List<Rectangle>();
            float y = startY;
            for (int i = 0; i < _visibleActions.Count; i++)
            {
                buttonRects.Add(new Rectangle(
                    (int)(cx - buttonW / 2f),
                    (int)y,
                    (int)buttonW,
                    (int)buttonH));
                y += buttonH + buttonSpacing;
            }

            return new MainMenuLayoutMetrics
            {
                ViewportWidth = (int)layout.Width,
                ViewportHeight = (int)layout.Height,
                ButtonRects = buttonRects
            };
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
        public List<Rectangle> ButtonRects { get; init; } = new();
    }
}

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Core;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;

namespace Autonocraft.UI
{
    public class PauseMenuScreen
    {
        private enum PanelMode
        {
            Main,
            Settings,
            Controls
        }

        private const float ButtonWidth = 220f;
        private const float ButtonHeight = 40f;
        private const float ButtonSpacing = 10f;
        private const float PanelWidth = 420f;
        private const float PanelHeight = 500f;
        private const float SliderWidth = 300f;
        private const float SliderTrackHeight = 12f;
        private const float SliderThumbSize = 22f;

        private readonly UiRenderer _ui;
        private readonly float[] _buttonHoverT = new float[6];
        private float _settingsFadeT = 1f;
        private PanelMode _prevPanelMode = PanelMode.Main;
        private PanelMode _panelMode = PanelMode.Main;
        private int _hoveredButton = -1;
        private bool _sliderDragging;
        private bool _sliderHovered;

        public bool IsOpen { get; private set; }
        public bool ResumeRequested { get; private set; }
        public bool SaveNowRequested { get; private set; }
        public bool MainMenuRequested { get; private set; }
        public bool QuitRequested { get; private set; }
        public int RenderDistance { get; private set; } = GameSettings.DefaultRenderDistance;
        public bool MuteAudio { get; private set; }

        public event Action<int>? RenderDistanceChanged;
        public event Action<bool>? MuteAudioChanged;

        public PauseMenuScreen(UiRenderer ui)
        {
            _ui = ui;
        }

        public void SetRenderDistance(int value)
        {
            RenderDistance = Math.Clamp(value, GameSettings.MinRenderDistance, GameSettings.MaxRenderDistance);
        }

        public void ApplyAudioSettings(GameSettings settings)
        {
            MuteAudio = settings.MuteAudio;
        }

        public void Open()
        {
            IsOpen = true;
            _panelMode = PanelMode.Main;
            _hoveredButton = -1;
            _sliderDragging = false;
        }

        public void Close()
        {
            IsOpen = false;
            _panelMode = PanelMode.Main;
            _hoveredButton = -1;
            _sliderDragging = false;
        }

        public void Update(Viewport viewport, KeyboardState kb, MouseState mouse, KeyboardState prevKb, MouseState prevMouse, float deltaTime)
        {
            ResumeRequested = false;
            SaveNowRequested = false;
            MainMenuRequested = false;
            QuitRequested = false;

            if (!IsOpen) return;

            if (_panelMode != _prevPanelMode)
            {
                _settingsFadeT = 0f;
                _prevPanelMode = _panelMode;
            }

            _settingsFadeT = Tween.SmoothDamp(_settingsFadeT, 1f, 10f, deltaTime);

            for (int i = 0; i < _buttonHoverT.Length; i++)
            {
                float target = _hoveredButton == i ? 1f : 0f;
                _buttonHoverT[i] = Tween.SmoothDamp(_buttonHoverT[i], target, 8f, deltaTime);
            }

            if (_panelMode == PanelMode.Settings)
            {
                UpdateSettingsPanel(viewport, kb, mouse, prevKb, prevMouse);
                return;
            }

            if (_panelMode == PanelMode.Controls)
            {
                UpdateControlsPanel(viewport, kb, mouse, prevKb, prevMouse);
                return;
            }

            UpdateMainPanel(viewport, kb, mouse, prevKb, prevMouse);
        }

        private void UpdateMainPanel(Viewport viewport, KeyboardState kb, MouseState mouse, KeyboardState prevKb, MouseState prevMouse)
        {
            var layout = new UiLayout(viewport);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float buttonSpacing = layout.S(ButtonSpacing);
            float cx = layout.CenterX;
            float resumeY = layout.CenterY - layout.S(92f);
            float saveY = resumeY + buttonH + buttonSpacing;
            float settingsY = saveY + buttonH + buttonSpacing;
            float controlsY = settingsY + buttonH + buttonSpacing;
            float mainMenuY = controlsY + buttonH + buttonSpacing;
            float quitY = mainMenuY + buttonH + buttonSpacing;

            var resumeRect = GetButtonRect(cx, resumeY, buttonW, buttonH);
            var saveRect = GetButtonRect(cx, saveY, buttonW, buttonH);
            var settingsRect = GetButtonRect(cx, settingsY, buttonW, buttonH);
            var controlsRect = GetButtonRect(cx, controlsY, buttonW, buttonH);
            var mainMenuRect = GetButtonRect(cx, mainMenuY, buttonW, buttonH);
            var quitRect = GetButtonRect(cx, quitY, buttonW, buttonH);

            _hoveredButton = -1;
            if (resumeRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 0;
            else if (saveRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 1;
            else if (settingsRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 2;
            else if (controlsRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 3;
            else if (mainMenuRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 4;
            else if (quitRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 5;

            bool click = mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released;
            if (click)
            {
                if (_hoveredButton == 0) ResumeRequested = true;
                else if (_hoveredButton == 1) SaveNowRequested = true;
                else if (_hoveredButton == 2) _panelMode = PanelMode.Settings;
                else if (_hoveredButton == 3) _panelMode = PanelMode.Controls;
                else if (_hoveredButton == 4) MainMenuRequested = true;
                else if (_hoveredButton == 5) QuitRequested = true;
            }

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                ResumeRequested = true;
            }
        }

        private void UpdateSettingsPanel(Viewport viewport, KeyboardState kb, MouseState mouse, KeyboardState prevKb, MouseState prevMouse)
        {
            var layout = new UiLayout(viewport);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float sliderW = layout.S(SliderWidth);
            float trackH = layout.S(SliderTrackHeight);
            float thumbSize = layout.S(SliderThumbSize);
            float cx = layout.CenterX;
            float sliderX = cx - sliderW / 2f;
            float sliderY = layout.CenterY - layout.S(24f);
            float muteY = sliderY + thumbSize + layout.S(42f);
            float backY = muteY + layout.S(48f);

            var sliderRect = UiRenderer.GetSliderTrackRect(sliderX, sliderY, sliderW, trackH, thumbSize);
            var muteRect = new Rectangle((int)(cx - buttonW / 2f), (int)muteY, (int)buttonW, (int)layout.S(28f));
            var backRect = GetButtonRect(cx, backY, buttonW, buttonH);

            _sliderHovered = sliderRect.Contains(mouse.X, mouse.Y);
            _hoveredButton = backRect.Contains(mouse.X, mouse.Y) ? 0 : -1;

            bool mouseDown = mouse.LeftButton == ButtonState.Pressed;
            bool mouseReleased = mouse.LeftButton == ButtonState.Released && prevMouse.LeftButton == ButtonState.Pressed;

            if (mouseReleased)
            {
                _sliderDragging = false;
            }

            if (mouseDown && prevMouse.LeftButton == ButtonState.Released && sliderRect.Contains(mouse.X, mouse.Y))
            {
                _sliderDragging = true;
            }

            if (_sliderDragging && mouseDown)
            {
                int next = UiRenderer.GetSliderValueFromPosition(
                    sliderX,
                    sliderW,
                    thumbSize,
                    GameSettings.MinRenderDistance,
                    GameSettings.MaxRenderDistance,
                    mouse.X);
                ApplyRenderDistance(next);
            }

            bool click = mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released;
            if (click && muteRect.Contains(mouse.X, mouse.Y))
            {
                MuteAudio = !MuteAudio;
                MuteAudioChanged?.Invoke(MuteAudio);
            }
            else if (click && _hoveredButton == 0)
            {
                _panelMode = PanelMode.Main;
            }

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                _panelMode = PanelMode.Main;
            }
        }

        private void ApplyRenderDistance(int value)
        {
            int next = Math.Clamp(value, GameSettings.MinRenderDistance, GameSettings.MaxRenderDistance);
            if (next == RenderDistance)
            {
                return;
            }

            RenderDistance = next;
            RenderDistanceChanged?.Invoke(RenderDistance);
        }

        public void Draw(Viewport viewport, float alpha = 1f, float offsetY = 0f)
        {
            if (!IsOpen) return;

            var layout = new UiLayout(viewport);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float buttonSpacing = layout.S(ButtonSpacing);
            float panelW = layout.S(PanelWidth);
            float panelH = layout.S(PanelHeight);

            _ui.DrawFullscreenBackground(new Color(0.03f, 0.04f, 0.07f) * 0.72f * alpha);

            float cx = layout.CenterX;
            float panelX = cx - panelW / 2f;
            float panelY = layout.Height * 0.16f + offsetY;
            _ui.DrawPanel(panelX, panelY, panelW, panelH, new Color(0.04f, 0.05f, 0.08f) * 0.92f, new Color(0.2f, 0.3f, 0.4f), 0.8f, alpha);

            if (_panelMode == PanelMode.Settings)
            {
                DrawSettingsPanel(layout, cx, buttonW, buttonH, alpha, offsetY);
                return;
            }

            if (_panelMode == PanelMode.Controls)
            {
                DrawControlsPanel(layout, cx, buttonW, buttonH, alpha, offsetY);
                return;
            }

            DrawMainPanel(layout, cx, buttonW, buttonH, buttonSpacing, alpha, offsetY);
        }

        private void DrawMainPanel(UiLayout layout, float cx, float buttonW, float buttonH, float buttonSpacing, float alpha, float offsetY)
        {
            float titleY = layout.Height * 0.24f + offsetY;
            float subtitleY = titleY + layout.S(34f);
            float resumeY = layout.CenterY - layout.S(92f) + offsetY;
            float saveY = resumeY + buttonH + buttonSpacing;
            float settingsY = saveY + buttonH + buttonSpacing;
            float controlsY = settingsY + buttonH + buttonSpacing;
            float mainMenuY = controlsY + buttonH + buttonSpacing;
            float quitY = mainMenuY + buttonH + buttonSpacing;

            _ui.DrawCenteredText("PAUSED", titleY, layout.S(2.2f), new Color(0.8f, 0.9f, 1.0f), alpha);
            _ui.DrawCenteredText("GAME MENU", subtitleY, layout.S(1.2f), new Color(0.55f, 0.65f, 0.75f), alpha);

            DrawButton(cx, resumeY, buttonW, buttonH, "RESUME", 0, layout.S(1.5f), alpha);
            DrawButton(cx, saveY, buttonW, buttonH, "SAVE NOW", 1, layout.S(1.35f), alpha);
            DrawButton(cx, settingsY, buttonW, buttonH, "SETTINGS", 2, layout.S(1.35f), alpha);
            DrawButton(cx, controlsY, buttonW, buttonH, "CONTROLS", 3, layout.S(1.35f), alpha);
            DrawButton(cx, mainMenuY, buttonW, buttonH, "MAIN MENU", 4, layout.S(1.35f), alpha);
            DrawButton(cx, quitY, buttonW, buttonH, "QUIT", 5, layout.S(1.45f), alpha);

            _ui.DrawCenteredText("AUTO-SAVES EVERY 5 MIN", layout.Height - layout.S(48f) + offsetY, layout.S(1.0f), new Color(0.45f, 0.5f, 0.58f), 0.9f * alpha);
            _ui.DrawCenteredText("ESC TO RESUME", layout.Height - layout.S(28f) + offsetY, layout.S(0.95f), new Color(0.4f, 0.46f, 0.54f), 0.85f * alpha);
        }

        private void DrawSettingsPanel(UiLayout layout, float cx, float buttonW, float buttonH, float alpha, float offsetY)
        {
            float panelAlpha = alpha * _settingsFadeT;
            float titleY = layout.Height * 0.24f + offsetY;
            float labelY = layout.CenterY - layout.S(52f) + offsetY;
            float sliderW = layout.S(SliderWidth);
            float trackH = layout.S(SliderTrackHeight);
            float thumbSize = layout.S(SliderThumbSize);
            float sliderX = cx - sliderW / 2f;
            float sliderY = layout.CenterY - layout.S(24f) + offsetY;
            float muteY = sliderY + thumbSize + layout.S(42f);
            float backY = muteY + layout.S(48f);

            int blockRadius = RenderDistance * 16;
            int chunkArea = RenderDistance * 2 + 1;
            int loadedChunks = chunkArea * chunkArea;

            _ui.DrawCenteredText("SETTINGS", titleY, layout.S(2.0f), new Color(0.8f, 0.9f, 1.0f), panelAlpha);
            _ui.DrawCenteredText("RENDER DISTANCE", labelY, layout.S(1.2f), new Color(0.55f, 0.65f, 0.75f), panelAlpha);
            _ui.DrawCenteredText($"{RenderDistance} CHUNKS  /  {blockRadius} BLOCKS", labelY + layout.S(22f), layout.S(1.0f), new Color(0.72f, 0.78f, 0.86f), 0.95f * panelAlpha);

            _ui.DrawIntSlider(
                sliderX,
                sliderY,
                sliderW,
                trackH,
                thumbSize,
                GameSettings.MinRenderDistance,
                GameSettings.MaxRenderDistance,
                RenderDistance,
                _sliderHovered,
                _sliderDragging);

            float minLabelX = sliderX;
            float maxLabelX = sliderX + sliderW - _ui.MeasureString($"{GameSettings.MaxRenderDistance}", layout.S(0.85f));
            _ui.DrawString($"{GameSettings.MinRenderDistance}", minLabelX, sliderY + thumbSize + layout.S(10f), layout.S(0.85f), new Color(0.45f, 0.5f, 0.58f), 0.85f * panelAlpha);
            _ui.DrawString($"{GameSettings.MaxRenderDistance}", maxLabelX, sliderY + thumbSize + layout.S(10f), layout.S(0.85f), new Color(0.45f, 0.5f, 0.58f), 0.85f * panelAlpha);

            _ui.DrawCenteredText(
                $"~{loadedChunks} CHUNKS LOADED",
                sliderY + thumbSize + layout.S(30f),
                layout.S(0.95f),
                new Color(0.45f, 0.5f, 0.58f),
                0.85f * panelAlpha);

            _ui.DrawCenteredText($"MUTE AUDIO: {(MuteAudio ? "ON" : "OFF")} (click)", muteY, layout.S(1.0f), new Color(0.72f, 0.78f, 0.86f), 0.95f * panelAlpha);

            DrawButton(cx, backY, buttonW, buttonH, "BACK", 0, layout.S(1.4f), panelAlpha);
            _ui.DrawCenteredText("DRAG SLIDER TO ADJUST", layout.Height - layout.S(48f) + offsetY, layout.S(0.95f), new Color(0.45f, 0.5f, 0.58f), 0.85f * panelAlpha);
            _ui.DrawCenteredText("ESC TO GO BACK", layout.Height - layout.S(28f) + offsetY, layout.S(0.95f), new Color(0.4f, 0.46f, 0.54f), 0.85f * panelAlpha);
        }

        private void UpdateControlsPanel(Viewport viewport, KeyboardState kb, MouseState mouse, KeyboardState prevKb, MouseState prevMouse)
        {
            var layout = new UiLayout(viewport);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float cx = layout.CenterX;
            float backY = layout.CenterY + layout.S(150f);

            var backRect = GetButtonRect(cx, backY, buttonW, buttonH);
            _hoveredButton = backRect.Contains(mouse.X, mouse.Y) ? 0 : -1;

            bool click = mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released;
            if (click && _hoveredButton == 0)
            {
                _panelMode = PanelMode.Main;
            }

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                _panelMode = PanelMode.Main;
            }
        }

        private void DrawControlsPanel(UiLayout layout, float cx, float buttonW, float buttonH, float alpha, float offsetY)
        {
            float panelAlpha = alpha * _settingsFadeT;
            float titleY = layout.Height * 0.18f + offsetY;
            float lineY = layout.Height * 0.28f + offsetY;
            float lineStep = layout.S(18f);
            float backY = layout.CenterY + layout.S(150f) + offsetY;

            string[] lines =
            {
                "WASD - MOVE",
                "SPACE - JUMP / FLY UP",
                "SHIFT - FLY DOWN",
                "G - TOGGLE FLYING",
                "LEFT CLICK - MINE / ATTACK",
                "RIGHT CLICK - PLACE / OPEN STATION",
                "SHIFT+RIGHT CLICK - AWAKEN SIGIL",
                "J - DISCOVERY JOURNAL",
                "1-9 / SCROLL - HOTBAR",
                "ESC - PAUSE MENU",
                "F3 OR ` - DEV CONSOLE"
            };

            _ui.DrawCenteredText("CONTROLS", titleY, layout.S(2.0f), new Color(0.8f, 0.9f, 1.0f), panelAlpha);
            for (int i = 0; i < lines.Length; i++)
            {
                _ui.DrawCenteredText(lines[i], lineY + i * lineStep, layout.S(0.95f), new Color(0.72f, 0.78f, 0.86f), 0.95f * panelAlpha);
            }

            DrawButton(cx, backY, buttonW, buttonH, "BACK", 0, layout.S(1.35f), panelAlpha);
        }

        private void DrawButton(float centerX, float y, float width, float height, string label, int buttonIndex, float textPixelSize, float alpha = 1f)
        {
            float x = centerX - width / 2f;
            _ui.DrawButton(x, y, width, height, label, _hoveredButton == buttonIndex, false, textPixelSize, alpha, _buttonHoverT[buttonIndex]);
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

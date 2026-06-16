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
        public bool IsOpen { get; private set; }
        public bool ResumeRequested { get; private set; }
        public bool SaveNowRequested { get; private set; }
        public bool MainMenuRequested { get; private set; }
        public bool MainMenuSettingsRequested { get; private set; }
        public bool QuitRequested { get; private set; }
        public int RenderDistance { get; private set; } = GameSettings.GetDefaultRenderDistance();
        public bool MuteAudio { get; private set; }
        public bool VSync { get; private set; } = true;
        public bool HighQualityLighting { get; private set; } = GameSettings.GetDefaultHighQualityLighting();






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

        public void ApplyGraphicsSettings(GameSettings settings)
        {
            RenderDistance = Math.Clamp(settings.RenderDistance, GameSettings.MinRenderDistance, GameSettings.MaxRenderDistance);
            VSync = settings.VSync;
            HighQualityLighting = settings.HighQualityLighting;
        }

        public void Open()
        {
            IsOpen = true;
            _panelMode = PanelMode.Main;
            _hoveredButton = -1;

        }

        public void Close()
        {
            IsOpen = false;
            _panelMode = PanelMode.Main;
            _hoveredButton = -1;

        }

        public void Update(Viewport viewport, KeyboardState kb, MouseState mouse, KeyboardState prevKb, MouseState prevMouse, float deltaTime)
        {
            ResumeRequested = false;
            SaveNowRequested = false;
            MainMenuRequested = false;
            MainMenuSettingsRequested = false;
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
                else if (_hoveredButton == 2) MainMenuSettingsRequested = true;
                else if (_hoveredButton == 3) _panelMode = PanelMode.Controls;
                else if (_hoveredButton == 4) MainMenuRequested = true;
                else if (_hoveredButton == 5) QuitRequested = true;
            }

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                ResumeRequested = true;
            }
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

            _ui.DrawFullscreenBackground(UiTheme.Scrim * (UiTheme.MenuScrimAlpha * alpha));

            float cx = layout.CenterX;
            float panelX = cx - panelW / 2f;
            float panelY = layout.Height * 0.16f + offsetY;
            _ui.DrawPanel(panelX, panelY, panelW, panelH, UiTheme.PanelBgMuted * 0.92f, UiTheme.PanelBorder, 0.8f, alpha);



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

            _ui.DrawCenteredTitle("Paused", titleY, layout.S(UiTheme.FontTitle), UiTheme.Title, alpha);
            _ui.DrawCenteredText("Game menu", subtitleY, layout.S(UiTheme.FontBody), UiTheme.Subtitle, alpha);

            DrawButton(cx, resumeY, buttonW, buttonH, "Resume", 0, UiButtonStyle.Primary, layout, alpha);
            DrawButton(cx, saveY, buttonW, buttonH, "Save now", 1, UiButtonStyle.Secondary, layout, alpha);
            DrawButton(cx, settingsY, buttonW, buttonH, "Settings", 2, UiButtonStyle.Secondary, layout, alpha);
            DrawButton(cx, controlsY, buttonW, buttonH, "Controls", 3, UiButtonStyle.Ghost, layout, alpha);
            DrawButton(cx, mainMenuY, buttonW, buttonH, "Main menu", 4, UiButtonStyle.Ghost, layout, alpha);
            DrawButton(cx, quitY, buttonW, buttonH, "Quit", 5, UiButtonStyle.Ghost, layout, alpha);

            _ui.DrawCenteredText("Auto-saves every 5 minutes", layout.Height - layout.S(48f) + offsetY, layout.S(UiTheme.FontSmall), UiTheme.Hint, 0.9f * alpha);
            _ui.DrawCenteredText("Esc to resume", layout.Height - layout.S(28f) + offsetY, layout.S(UiTheme.FontSmall), UiTheme.Hint, 0.85f * alpha);
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
                "SPACE - JUMP / CREATIVE UP",
                "SHIFT - CREATIVE DOWN",
                "G - TOGGLE CREATIVE",
                "LEFT CLICK - MINE / ATTACK",
                "RIGHT CLICK - PLACE / OPEN STATION",
                "SHIFT+RIGHT CLICK - AWAKEN SIGIL",
                "V - TOWN BOARD (RECRUIT & BUILD)",
                "C - VILLAGE STEWARD CHAT",
                "J - DISCOVERY JOURNAL",
                "1-9 / SCROLL - HOTBAR",
                "ESC - PAUSE MENU",
                "F3 OR ` - DEV CONSOLE"
            };

            _ui.DrawCenteredText("Controls", titleY, layout.S(UiTheme.FontTitle), UiTheme.Title, panelAlpha);
            for (int i = 0; i < lines.Length; i++)
            {
                _ui.DrawCenteredText(lines[i], lineY + i * lineStep, layout.S(UiTheme.FontSmall), UiTheme.Subtitle, 0.95f * panelAlpha);
            }

            DrawButton(cx, backY, buttonW, buttonH, "Back", 0, UiButtonStyle.Secondary, layout, panelAlpha);
        }

        private void DrawButton(float centerX, float y, float width, float height, string label, int buttonIndex, UiButtonStyle style, UiLayout layout, float alpha = 1f)
        {
            float x = centerX - width / 2f;
            _ui.DrawButton(x, y, width, height, label, _hoveredButton == buttonIndex, false, style, layout.S(UiTheme.FontBody), alpha, _buttonHoverT[buttonIndex]);
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

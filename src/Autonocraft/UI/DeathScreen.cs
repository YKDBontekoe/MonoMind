using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;

namespace Autonocraft.UI
{
    public class DeathScreen
    {
        private const float ButtonWidth = 240f;
        private const float ButtonHeight = 48f;
        private const float ButtonSpacing = 12f;
        private const float PanelWidth = 440f;
        private const float PanelHeight = 360f;

        private readonly UiRenderer _ui;
        private readonly float[] _buttonHoverT = new float[2];
        private int _hoveredButton = -1;

        public bool IsOpen { get; private set; }
        public bool RespawnRequested { get; private set; }
        public bool MainMenuRequested { get; private set; }
        public string? CauseText { get; private set; }
        public string? PenaltyText { get; private set; }

        public DeathScreen(UiRenderer ui)
        {
            _ui = ui;
        }

        public void Open(string? causeText = null, string? penaltyText = null)
        {
            IsOpen = true;
            CauseText = causeText;
            PenaltyText = penaltyText;
            _hoveredButton = -1;
        }

        public void Close()
        {
            IsOpen = false;
            _hoveredButton = -1;
        }

        public void Update(Viewport viewport, KeyboardState kb, MouseState mouse, KeyboardState prevKb, MouseState prevMouse, float deltaTime)
        {
            RespawnRequested = false;
            MainMenuRequested = false;

            if (!IsOpen) return;

            for (int i = 0; i < _buttonHoverT.Length; i++)
            {
                float target = _hoveredButton == i ? 1f : 0f;
                _buttonHoverT[i] = Tween.SmoothDamp(_buttonHoverT[i], target, 8f, deltaTime);
            }

            var layout = new UiLayout(viewport);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float buttonSpacing = layout.S(ButtonSpacing);
            float cx = layout.CenterX;
            float respawnY = layout.CenterY + layout.S(20f);
            float mainMenuY = respawnY + buttonH + buttonSpacing;

            var respawnRect = GetButtonRect(cx, respawnY, buttonW, buttonH);
            var mainMenuRect = GetButtonRect(cx, mainMenuY, buttonW, buttonH);

            _hoveredButton = -1;
            if (respawnRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 0;
            else if (mainMenuRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 1;

            bool click = mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released;
            if (click)
            {
                if (_hoveredButton == 0) RespawnRequested = true;
                else if (_hoveredButton == 1) MainMenuRequested = true;
            }

            if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter))
            {
                RespawnRequested = true;
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

            _ui.DrawFullscreenBackground(UiTheme.DeathScrim * 0.65f * alpha);

            float cx = layout.CenterX;
            float panelX = cx - panelW / 2f;
            float panelY = layout.Height * 0.2f + offsetY;
            _ui.DrawCard(panelX, panelY, panelW, panelH, alpha, UiTheme.RadiusXl);
            _ui.DrawRoundedRectOutline(panelX, panelY, panelW, panelH, UiTheme.RadiusXl, UiTheme.Danger * 0.35f, 2f, alpha);

            float titleY = layout.Height * 0.28f + offsetY;
            float subtitleY = titleY + layout.S(40f);
            float respawnY = layout.CenterY + layout.S(20f) + offsetY;
            float mainMenuY = respawnY + buttonH + buttonSpacing;

            _ui.DrawCenteredTitle("You died", titleY, layout.S(UiTheme.FontTitle), UiTheme.Danger, alpha);
            string subtitle = string.IsNullOrWhiteSpace(CauseText)
                ? "Your adventure isn't over"
                : CauseText!;
            _ui.DrawCenteredText(subtitle, subtitleY, layout.S(UiTheme.FontBody), UiTheme.Subtitle, alpha);
            if (!string.IsNullOrWhiteSpace(PenaltyText))
            {
                _ui.DrawCenteredText(PenaltyText!, subtitleY + layout.S(26f), layout.S(UiTheme.FontSmall),
                    UiTheme.Meta, alpha * 0.95f);
            }

            DrawButton(cx, respawnY, buttonW, buttonH, "Respawn", 0, UiButtonStyle.Primary, layout, alpha);
            DrawButton(cx, mainMenuY, buttonW, buttonH, "Main menu", 1, UiButtonStyle.Ghost, layout, alpha);

            _ui.DrawCenteredText("Press Enter to respawn", layout.Height - layout.S(28f) + offsetY, layout.S(UiTheme.FontSmall), UiTheme.Hint, 0.85f * alpha);
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

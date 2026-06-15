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
        private const float ButtonWidth = 220f;
        private const float ButtonHeight = 44f;
        private const float ButtonSpacing = 14f;
        private const float PanelWidth = 420f;
        private const float PanelHeight = 340f;

        private readonly UiRenderer _ui;
        private readonly float[] _buttonHoverT = new float[2];
        private int _hoveredButton = -1;

        public bool IsOpen { get; private set; }
        public bool RespawnRequested { get; private set; }
        public bool MainMenuRequested { get; private set; }

        public DeathScreen(UiRenderer ui)
        {
            _ui = ui;
        }

        public void Open()
        {
            IsOpen = true;
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
            float respawnY = layout.CenterY + layout.S(12f);
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

            _ui.DrawFullscreenBackground(new Color(0.12f, 0.02f, 0.03f) * 0.78f * alpha);

            float cx = layout.CenterX;
            float panelX = cx - panelW / 2f;
            float panelY = layout.Height * 0.2f + offsetY;
            _ui.DrawPanel(panelX, panelY, panelW, panelH, new Color(0.08f, 0.03f, 0.04f) * 0.94f, new Color(0.45f, 0.15f, 0.18f), 0.8f, alpha);

            float titleY = layout.Height * 0.28f + offsetY;
            float subtitleY = titleY + layout.S(34f);
            float respawnY = layout.CenterY + layout.S(12f) + offsetY;
            float mainMenuY = respawnY + buttonH + buttonSpacing;

            _ui.DrawCenteredText("YOU DIED", titleY, layout.S(2.4f), UiTheme.Danger, alpha);
            _ui.DrawCenteredText("YOUR ADVENTURE ISN'T OVER", subtitleY, layout.S(UiTheme.ScaleSection), new Color(0.85f, 0.72f, 0.74f), alpha);

            DrawButton(cx, respawnY, buttonW, buttonH, "RESPAWN", 0, layout.S(1.5f), alpha);
            DrawButton(cx, mainMenuY, buttonW, buttonH, "MAIN MENU", 1, layout.S(1.4f), alpha);

            _ui.DrawCenteredText("CLICK OR PRESS ENTER TO RESPAWN", layout.Height - layout.S(28f) + offsetY, layout.S(UiTheme.ScaleSmall), new Color(0.72f, 0.65f, 0.67f), 0.85f * alpha);
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

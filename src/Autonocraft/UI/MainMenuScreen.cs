using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Engine;

namespace Autonocraft.UI
{
    public class MainMenuScreen
    {
        private const float ButtonWidth = 220f;
        private const float ButtonHeight = 48f;
        private const float ButtonSpacing = 16f;
        private const float PanelWidth = 420f;
        private const float PanelHeight = 340f;

        private readonly UiRenderer _ui;
        private int _hoveredButton = -1;

        public bool PlayRequested { get; private set; }
        public bool QuitRequested { get; private set; }

        public MainMenuScreen(UiRenderer ui)
        {
            _ui = ui;
        }

        public void Update(Viewport viewport, KeyboardState kb, MouseState mouse, KeyboardState prevKb, MouseState prevMouse)
        {
            PlayRequested = false;
            QuitRequested = false;

            var layout = new UiLayout(viewport);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float buttonSpacing = layout.S(ButtonSpacing);
            float cx = layout.CenterX;
            float playY = layout.CenterY + layout.S(20f);
            float quitY = playY + buttonH + buttonSpacing;

            var playRect = GetButtonRect(cx, playY, buttonW, buttonH);
            var quitRect = GetButtonRect(cx, quitY, buttonW, buttonH);

            _hoveredButton = -1;
            if (playRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 0;
            else if (quitRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 1;

            bool click = mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released;
            if (click)
            {
                if (_hoveredButton == 0) PlayRequested = true;
                else if (_hoveredButton == 1) QuitRequested = true;
            }

            if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter))
            {
                PlayRequested = true;
            }

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                QuitRequested = true;
            }
        }

        public void Draw(Viewport viewport)
        {
            var layout = new UiLayout(viewport);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float buttonSpacing = layout.S(ButtonSpacing);
            float panelW = layout.S(PanelWidth);
            float panelH = layout.S(PanelHeight);

            _ui.DrawFullscreenBackground(new Color(0.03f, 0.04f, 0.07f));

            float cx = layout.CenterX;
            float titleY = layout.Height * 0.22f;
            float subtitleY = titleY + layout.S(42f);
            float playY = layout.CenterY + layout.S(20f);
            float quitY = playY + buttonH + buttonSpacing;

            float panelX = cx - panelW / 2f;
            float panelY = layout.Height * 0.16f;
            _ui.DrawPanel(panelX, panelY, panelW, panelH, new Color(0.04f, 0.05f, 0.08f) * 0.88f, new Color(0.2f, 0.3f, 0.4f));

            _ui.DrawCenteredText("AUTONOCRAFT", titleY, layout.S(2.4f), new Color(0.8f, 0.9f, 1.0f));
            _ui.DrawCenteredText("VOXEL SANDBOX", subtitleY, layout.S(1.3f), new Color(0.55f, 0.65f, 0.75f));

            DrawButton(cx, playY, buttonW, buttonH, "PLAY", _hoveredButton == 0, layout.S(1.6f));
            DrawButton(cx, quitY, buttonW, buttonH, "QUIT", _hoveredButton == 1, layout.S(1.6f));

            _ui.DrawCenteredText("CLICK OR PRESS ENTER TO PLAY", layout.Height - layout.S(48f), layout.S(1.1f), new Color(0.45f, 0.5f, 0.58f), 0.9f);
        }

        private void DrawButton(float centerX, float y, float width, float height, string label, bool hovered, float textPixelSize)
        {
            float x = centerX - width / 2f;
            _ui.DrawButton(x, y, width, height, label, hovered, false, textPixelSize);
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

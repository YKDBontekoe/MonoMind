using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Engine;

namespace Autonocraft.UI
{
    public class MainMenuScreen
    {
        private const float ButtonWidth = 240f;
        private const float ButtonHeight = 48f;
        private const float ButtonSpacing = 12f;

        private readonly UiRenderer _ui;
        private readonly MenuBackdrop _backdrop = new MenuBackdrop();
        private int _hoveredButton = -1;
        private float _playHoverT;
        private float _quitHoverT;

        public bool PlayRequested { get; private set; }
        public bool QuitRequested { get; private set; }

        public MainMenuScreen(UiRenderer ui)
        {
            _ui = ui;
        }

        public void Update(Viewport viewport, KeyboardState kb, MouseState mouse, KeyboardState prevKb, MouseState prevMouse, float deltaTime)
        {
            PlayRequested = false;
            QuitRequested = false;

            _backdrop.Update(deltaTime);

            var layout = new UiLayout(viewport);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float buttonSpacing = layout.S(ButtonSpacing);
            float cx = layout.CenterX;
            float playY = layout.CenterY + layout.S(36f);
            float quitY = playY + buttonH + buttonSpacing;

            var playRect = GetButtonRect(cx, playY, buttonW, buttonH);
            var quitRect = GetButtonRect(cx, quitY, buttonW, buttonH);

            _hoveredButton = -1;
            if (playRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 0;
            else if (quitRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 1;

            _playHoverT = Autonocraft.Engine.Animation.Tween.SmoothDamp(_playHoverT, _hoveredButton == 0 ? 1f : 0f, 10f, deltaTime);
            _quitHoverT = Autonocraft.Engine.Animation.Tween.SmoothDamp(_quitHoverT, _hoveredButton == 1 ? 1f : 0f, 10f, deltaTime);

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

        public void Draw(Viewport viewport, float deltaTime = 0f)
        {
            var layout = new UiLayout(viewport);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float buttonSpacing = layout.S(ButtonSpacing);

            _backdrop.Draw(_ui, viewport);

            float cx = layout.CenterX;
            float titleY = layout.Height * 0.28f;
            float subtitleY = titleY + layout.S(52f);
            float playY = layout.CenterY + layout.S(36f);
            float quitY = playY + buttonH + buttonSpacing;

            _ui.DrawCenteredTitle("Autonocraft", titleY, layout.S(UiTheme.FontHero), UiTheme.Title);
            _ui.DrawCenteredText("A voxel sandbox for builders and explorers", subtitleY, layout.S(UiTheme.FontBody), UiTheme.Subtitle);

            _ui.DrawButton(cx - buttonW / 2f, playY, buttonW, buttonH, "Play", _hoveredButton == 0, false,
                UiButtonStyle.Primary, layout.S(UiTheme.FontSection), 1f, _playHoverT);
            _ui.DrawButton(cx - buttonW / 2f, quitY, buttonW, buttonH, "Quit", _hoveredButton == 1, false,
                UiButtonStyle.Ghost, layout.S(UiTheme.FontBody), 1f, _quitHoverT);

            _ui.DrawCenteredText("Press Enter to play", layout.Height - layout.S(32f), layout.S(UiTheme.FontSmall), UiTheme.Hint, 0.85f);
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

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Engine;
using Autonocraft.World;

namespace Autonocraft.UI
{
    public class NewWorldSetupScreen
    {
        private const float ButtonWidth = 220f;
        private const float ButtonHeight = 40f;
        private const float ButtonSpacing = 12f;
        private const float PanelWidth = 520f;
        private const float PanelHeight = 420f;

        private readonly UiRenderer _ui;
        private readonly Random _random = new Random();
        private int _hoveredButton = -1;
        private int _selectedWorldType;
        private string _seedText = WorldConstants.DefaultSeed.ToString();
        private bool _seedFocused;

        public bool CreateRequested { get; private set; }
        public bool BackRequested { get; private set; }
        public int SelectedSeed { get; private set; } = WorldConstants.DefaultSeed;
        public WorldType SelectedWorldType { get; private set; } = WorldType.Default;

        private static readonly WorldType[] WorldTypes =
        {
            WorldType.Default,
            WorldType.Mountains,
            WorldType.Islands,
            WorldType.Flat
        };

        public NewWorldSetupScreen(UiRenderer ui)
        {
            _ui = ui;
        }

        public void Reset()
        {
            _seedText = WorldConstants.DefaultSeed.ToString();
            _selectedWorldType = 0;
            _seedFocused = false;
            SelectedSeed = WorldConstants.DefaultSeed;
            SelectedWorldType = WorldType.Default;
        }

        public void Update(Viewport viewport, KeyboardState kb, MouseState mouse, KeyboardState prevKb, MouseState prevMouse)
        {
            CreateRequested = false;
            BackRequested = false;

            var layout = new UiLayout(viewport);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float buttonSpacing = layout.S(ButtonSpacing);
            float cx = layout.CenterX;
            float panelY = layout.Height * 0.14f;
            float typeY = panelY + layout.S(120f);
            float seedY = typeY + layout.S(110f);
            float createY = seedY + layout.S(90f);
            float backY = createY + buttonH + buttonSpacing;
            float randomY = seedY + layout.S(42f);

            var createRect = GetButtonRect(cx, createY, buttonW, buttonH);
            var backRect = GetButtonRect(cx, backY, buttonW, buttonH);
            var randomRect = GetButtonRect(cx + layout.S(90f), randomY, layout.S(120f), layout.S(30f));

            _hoveredButton = -1;
            if (createRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 0;
            else if (backRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 1;
            else if (randomRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 2;

            bool click = mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released;
            if (click)
            {
                if (_hoveredButton == 0)
                {
                    CommitSeed();
                    SelectedWorldType = WorldTypes[_selectedWorldType];
                    CreateRequested = true;
                }
                else if (_hoveredButton == 1)
                {
                    BackRequested = true;
                }
                else if (_hoveredButton == 2)
                {
                    int randomSeed = _random.Next(1, int.MaxValue);
                    _seedText = randomSeed.ToString();
                    SelectedSeed = randomSeed;
                }
                else
                {
                    float typeListX = cx - layout.S(180f);
                    for (int i = 0; i < WorldTypes.Length; i++)
                    {
                        float rowY = typeY + i * layout.S(28f);
                        var rowRect = new Rectangle((int)typeListX, (int)rowY, (int)layout.S(360f), (int)layout.S(24f));
                        if (rowRect.Contains(mouse.X, mouse.Y))
                        {
                            _selectedWorldType = i;
                        }
                    }

                    var seedBox = new Rectangle((int)(cx - layout.S(120f)), (int)seedY, (int)layout.S(240f), (int)layout.S(32f));
                    _seedFocused = seedBox.Contains(mouse.X, mouse.Y);
                }
            }

            if (_seedFocused)
            {
                foreach (var key in kb.GetPressedKeys())
                {
                    if (prevKb.IsKeyDown(key)) continue;

                    if (key == Keys.Back)
                    {
                        if (_seedText.Length > 0)
                        {
                            _seedText = _seedText[..^1];
                        }
                    }
                    else if (key == Keys.Enter)
                    {
                        CommitSeed();
                        SelectedWorldType = WorldTypes[_selectedWorldType];
                        CreateRequested = true;
                    }
                    else if (key == Keys.Escape)
                    {
                        _seedFocused = false;
                    }
                    else if (key >= Keys.D0 && key <= Keys.D9 && _seedText.Length < 10)
                    {
                        _seedText += (char)('0' + (key - Keys.D0));
                    }
                    else if (key >= Keys.NumPad0 && key <= Keys.NumPad9 && _seedText.Length < 10)
                    {
                        _seedText += (char)('0' + (key - Keys.NumPad0));
                    }
                }
            }

            if (kb.IsKeyDown(Keys.Left) && !prevKb.IsKeyDown(Keys.Left))
            {
                _selectedWorldType = (_selectedWorldType + WorldTypes.Length - 1) % WorldTypes.Length;
            }

            if (kb.IsKeyDown(Keys.Right) && !prevKb.IsKeyDown(Keys.Right))
            {
                _selectedWorldType = (_selectedWorldType + 1) % WorldTypes.Length;
            }
        }

        private void CommitSeed()
        {
            if (!int.TryParse(_seedText, out int seed) || seed == 0)
            {
                seed = WorldConstants.DefaultSeed;
                _seedText = seed.ToString();
            }

            SelectedSeed = seed;
        }

        public void Draw(Viewport viewport)
        {
            var layout = new UiLayout(viewport);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float buttonSpacing = layout.S(ButtonSpacing);
            float panelW = layout.S(PanelWidth);
            float panelH = layout.S(PanelHeight);
            float cx = layout.CenterX;
            float panelX = cx - panelW / 2f;
            float panelY = layout.Height * 0.14f;

            _ui.DrawFullscreenBackground(new Color(0.03f, 0.04f, 0.07f));
            _ui.DrawPanel(panelX, panelY, panelW, panelH, new Color(0.04f, 0.05f, 0.08f) * 0.88f, new Color(0.2f, 0.3f, 0.4f));

            _ui.DrawCenteredText("NEW WORLD", panelY + layout.S(24f), layout.S(2.0f), new Color(0.8f, 0.9f, 1.0f));
            _ui.DrawCenteredText("CONFIGURE GENERATION", panelY + layout.S(56f), layout.S(1.1f), new Color(0.55f, 0.65f, 0.75f));

            float typeY = panelY + layout.S(120f);
            float typeListX = cx - layout.S(180f);
            _ui.DrawString("WORLD TYPE", typeListX, typeY - layout.S(24f), layout.S(1.1f), new Color(0.7f, 0.78f, 0.88f));

            for (int i = 0; i < WorldTypes.Length; i++)
            {
                bool selected = i == _selectedWorldType;
                Color color = selected ? new Color(0.82f, 0.9f, 1.0f) : new Color(0.5f, 0.58f, 0.66f);
                string prefix = selected ? "> " : "  ";
                _ui.DrawString(prefix + WorldTypes[i].ToString().ToUpperInvariant(), typeListX, typeY + i * layout.S(28f), layout.S(1.15f), color);
            }

            float seedY = typeY + layout.S(110f);
            _ui.DrawString("SEED", typeListX, seedY - layout.S(24f), layout.S(1.1f), new Color(0.7f, 0.78f, 0.88f));
            _ui.DrawPanel(cx - layout.S(120f), seedY, layout.S(240f), layout.S(32f),
                _seedFocused ? new Color(0.1f, 0.14f, 0.2f) : new Color(0.05f, 0.07f, 0.1f),
                new Color(0.2f, 0.3f, 0.4f));
            _ui.DrawString(_seedText, cx - layout.S(112f), seedY + layout.S(8f), layout.S(1.2f), new Color(0.85f, 0.92f, 1.0f));

            float randomY = seedY + layout.S(42f);
            DrawButton(cx + layout.S(90f), randomY, layout.S(120f), layout.S(30f), "RANDOM", _hoveredButton == 2, layout.S(1.0f));

            float createY = seedY + layout.S(90f);
            float backY = createY + buttonH + buttonSpacing;
            DrawButton(cx, createY, buttonW, buttonH, "CREATE WORLD", _hoveredButton == 0, layout.S(1.3f));
            DrawButton(cx, backY, buttonW, buttonH, "BACK", _hoveredButton == 1, layout.S(1.3f));
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

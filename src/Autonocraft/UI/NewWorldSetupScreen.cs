using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.World;

namespace Autonocraft.UI
{
    public class NewWorldSetupScreen
    {
        private const float ButtonWidth = 200f;
        private const float ButtonHeight = 44f;
        private const float PanelWidth = 560f;
        private const float PanelHeight = 400f;

        private readonly UiRenderer _ui;
        private readonly MenuBackdrop _backdrop = new MenuBackdrop(28);
        private readonly UiTransition _transition = new UiTransition();
        private readonly Random _random = new Random();
        private readonly float[] _buttonHoverT = new float[3];
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
            _transition.BeginFadeIn(0.3f);
        }

        public void Update(Viewport viewport, KeyboardState kb, MouseState mouse, KeyboardState prevKb, MouseState prevMouse, float deltaTime)
        {
            _backdrop.Update(deltaTime);
            _transition.Update(deltaTime);

            for (int i = 0; i < _buttonHoverT.Length; i++)
            {
                float target = _hoveredButton == i ? 1f : 0f;
                _buttonHoverT[i] = Tween.SmoothDamp(_buttonHoverT[i], target, 10f, deltaTime);
            }

            CreateRequested = false;
            BackRequested = false;

            var layout = new UiLayout(viewport);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float cx = layout.CenterX;
            float panelY = layout.CenterY - layout.S(PanelHeight / 2f);
            float typeY = panelY + layout.S(108f);
            float seedY = typeY + layout.S(108f);
            float createY = seedY + layout.S(86f);

            var createRect = GetButtonRect(cx - buttonW / 2f - layout.S(6f), createY, buttonW, buttonH);
            var backRect = GetButtonRect(cx + buttonW / 2f + layout.S(6f), createY, buttonW, buttonH);
            var randomRect = GetButtonRect(cx + layout.S(100f), seedY + layout.S(40f), layout.S(120f), layout.S(34f));

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
                    float typeListX = cx - layout.S(200f);
                    for (int i = 0; i < WorldTypes.Length; i++)
                    {
                        float rowY = typeY + i * layout.S(30f);
                        var rowRect = new Rectangle((int)typeListX, (int)rowY, (int)layout.S(400f), (int)layout.S(28f));
                        if (rowRect.Contains(mouse.X, mouse.Y))
                        {
                            _selectedWorldType = i;
                        }
                    }

                    var seedBox = new Rectangle((int)(cx - layout.S(130f)), (int)seedY, (int)layout.S(260f), (int)layout.S(36f));
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

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape) && !_seedFocused)
            {
                BackRequested = true;
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
            float alpha = _transition.Alpha;
            float offsetY = _transition.OffsetY;
            var layout = new UiLayout(viewport);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float panelW = layout.S(PanelWidth);
            float panelH = layout.S(PanelHeight);
            float cx = layout.CenterX;
            float panelX = cx - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f + offsetY;

            _backdrop.Draw(_ui, viewport, alpha);
            UiTheme.DrawMenuScrim(_ui, viewport, alpha);

            _ui.DrawCenteredTitle("New world", layout.Height * 0.085f + offsetY, layout.S(UiTheme.FontHero), UiTheme.Title, alpha);

            _ui.DrawCard(panelX, panelY, panelW, panelH, alpha, UiTheme.RadiusXl);
            _ui.DrawCenteredTitle("World setup", panelY + layout.S(24f), layout.S(UiTheme.FontTitle), UiTheme.Title, alpha);
            _ui.DrawCenteredText("Choose terrain — your steward awaits", panelY + layout.S(54f), layout.S(UiTheme.FontBody), UiTheme.Subtitle, alpha * 0.92f);

            float typeY = panelY + layout.S(108f);
            float typeListX = cx - layout.S(200f);
            UiTheme.DrawSectionHeader(_ui, "Terrain type", typeListX, typeY - layout.S(28f), layout, alpha);

            for (int i = 0; i < WorldTypes.Length; i++)
            {
                bool selected = i == _selectedWorldType;
                float rowY = typeY + i * layout.S(32f);
                if (selected)
                {
                    _ui.DrawRoundedRect(typeListX - layout.S(4f), rowY - layout.S(2f), layout.S(400f), layout.S(28f),
                        layout.S(UiTheme.RadiusMd), UiTheme.AccentSoft * alpha);
                    _ui.DrawRoundedRectOutline(typeListX - layout.S(4f), rowY - layout.S(2f), layout.S(400f), layout.S(28f),
                        layout.S(UiTheme.RadiusMd), UiTheme.Accent, 2f, 0.85f * alpha);
                }

                Color color = selected ? UiTheme.Title : UiTheme.Meta;
                string prefix = selected ? "› " : "  ";
                string label = prefix + FormatWorldType(WorldTypes[i]);
                _ui.DrawString(label, typeListX, rowY + layout.S(4f), layout.S(UiTheme.FontBody), color, alpha, semiBold: selected);
            }

            float seedY = typeY + layout.S(108f);
            UiTheme.DrawSectionHeader(_ui, "World seed", typeListX, seedY - layout.S(28f), layout, alpha);
            _ui.DrawPanel(
                cx - layout.S(130f),
                seedY,
                layout.S(260f),
                layout.S(36f),
                _seedFocused ? UiTheme.PanelBgHighlight : UiTheme.PanelBgMuted,
                _seedFocused ? UiTheme.Accent : UiTheme.PanelBorder,
                0.85f,
                alpha,
                UiTheme.RadiusMd);
            _ui.DrawString(_seedText, cx - layout.S(120f), seedY + layout.S(10f), layout.S(UiTheme.FontBody), UiTheme.Title, alpha);

            float randomY = seedY + layout.S(44f);
            DrawButton(cx + layout.S(100f), randomY, layout.S(120f), layout.S(34f), "Random", 2, UiButtonStyle.Ghost, layout, alpha);

            float createY = seedY + layout.S(90f);
            DrawButton(cx - buttonW / 2f - layout.S(6f), createY, buttonW, buttonH, "Create world", 0, UiButtonStyle.Primary, layout, alpha);
            DrawButton(cx + buttonW / 2f + layout.S(6f), createY, buttonW, buttonH, "Back", 1, UiButtonStyle.Ghost, layout, alpha);

            _ui.DrawCenteredText("← → change terrain", layout.Height - layout.S(32f) + offsetY, layout.S(UiTheme.FontSmall), UiTheme.Hint, 0.85f * alpha);
        }

        private void DrawButton(float x, float y, float width, float height, string label, int index, UiButtonStyle style, UiLayout layout, float alpha)
        {
            _ui.DrawButton(x, y, width, height, label, _hoveredButton == index, false, style, layout.S(UiTheme.FontBody), alpha, _buttonHoverT[index]);
        }

        private static string FormatWorldType(WorldType type) => type switch
        {
            WorldType.Default => "Default",
            WorldType.Mountains => "Mountains",
            WorldType.Islands => "Islands",
            WorldType.Flat => "Flat",
            _ => type.ToString()
        };

        private static Rectangle GetButtonRect(float x, float y, float width, float height)
        {
            return new Rectangle((int)x, (int)y, (int)width, (int)height);
        }
    }
}

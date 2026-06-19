using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.UI.Menu;
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
        private string? _seedErrorMessage;

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
            _seedErrorMessage = null;
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
            var metrics = ComputeLayout(layout, _transition.OffsetY);

            var createRect = GetButtonRect(metrics.Cx - buttonW / 2f - layout.S(6f), metrics.CreateY, buttonW, buttonH);
            var backRect = GetButtonRect(metrics.Cx + buttonW / 2f + layout.S(6f), metrics.CreateY, buttonW, buttonH);
            var randomRect = GetButtonRect(metrics.Cx + layout.S(100f), metrics.RandomY, layout.S(120f), layout.S(34f));

            _hoveredButton = -1;
            if (createRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 0;
            else if (backRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 1;
            else if (randomRect.Contains(mouse.X, mouse.Y)) _hoveredButton = 2;

            bool click = mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released;
            if (click)
            {
                if (_hoveredButton == 0)
                {
                    if (TryCommitSeedForCreate())
                    {
                        SelectedWorldType = WorldTypes[_selectedWorldType];
                        CreateRequested = true;
                    }
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
                    for (int i = 0; i < WorldTypes.Length; i++)
                    {
                        float rowY = metrics.TypeY + i * metrics.TypeRowStep;
                        var rowRect = new Rectangle((int)metrics.TypeListX, (int)rowY, (int)layout.S(400f), (int)metrics.TypeRowHeight);
                        if (rowRect.Contains(mouse.X, mouse.Y))
                        {
                            _selectedWorldType = i;
                        }
                    }

                    var seedBox = new Rectangle((int)(metrics.Cx - layout.S(130f)), (int)metrics.SeedY, (int)layout.S(260f), (int)layout.S(36f));
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
                        if (TryCommitSeedForCreate())
                        {
                            SelectedWorldType = WorldTypes[_selectedWorldType];
                            CreateRequested = true;
                        }
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

        private bool TryCommitSeedForCreate()
        {
            if (string.IsNullOrWhiteSpace(_seedText))
            {
                _seedErrorMessage = "Enter a number seed or tap Random.";
                return false;
            }

            if (!int.TryParse(_seedText, out int seed))
            {
                _seedErrorMessage = "Seed must be a whole number (digits only).";
                return false;
            }

            if (seed == 0)
            {
                _seedErrorMessage = "Seed cannot be zero — try another number.";
                return false;
            }

            _seedErrorMessage = null;
            SelectedSeed = seed;
            _seedText = seed.ToString();
            return true;
        }

        private void CommitSeed()
        {
            if (int.TryParse(_seedText, out int seed) && seed != 0)
            {
                SelectedSeed = seed;
                _seedErrorMessage = null;
                return;
            }

            seed = WorldConstants.DefaultSeed;
            _seedText = seed.ToString();
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
            var metrics = ComputeLayout(layout, offsetY);
            float panelX = metrics.Cx - panelW / 2f;

            MenuChrome.DrawBackdrop(_backdrop, _ui, viewport, alpha);
            MenuChrome.DrawTitleBlock(_ui, layout, "New world", "Choose terrain — your steward awaits", 0.085f, alpha, offsetY);

            _ui.DrawCard(panelX, metrics.PanelY, panelW, panelH, alpha, UiTheme.RadiusXl);
            _ui.DrawCenteredTitle("World setup", metrics.PanelY + layout.S(24f), layout.S(UiTheme.FontTitle), UiTheme.Title, alpha);
            _ui.DrawCenteredText("Choose terrain — your steward awaits", metrics.PanelY + layout.S(54f), layout.S(UiTheme.FontBody), UiTheme.Subtitle, alpha * 0.92f);

            UiTheme.DrawSectionHeader(_ui, "Terrain type", metrics.TypeListX, metrics.TypeY - layout.S(28f), layout, alpha);

            for (int i = 0; i < WorldTypes.Length; i++)
            {
                bool selected = i == _selectedWorldType;
                float rowY = metrics.TypeY + i * metrics.TypeRowStep;
                if (selected)
                {
                    _ui.DrawRoundedRect(metrics.TypeListX - layout.S(4f), rowY - layout.S(2f), layout.S(400f), metrics.TypeRowHeight,
                        layout.S(UiTheme.RadiusMd), UiTheme.AccentSoft * alpha);
                    _ui.DrawRoundedRectOutline(metrics.TypeListX - layout.S(4f), rowY - layout.S(2f), layout.S(400f), metrics.TypeRowHeight,
                        layout.S(UiTheme.RadiusMd), UiTheme.Accent, 2f, 0.85f * alpha);
                }

                Color color = selected ? UiTheme.Title : UiTheme.Meta;
                string prefix = selected ? "› " : "  ";
                string label = prefix + FormatWorldType(WorldTypes[i]);
                _ui.DrawString(label, metrics.TypeListX, rowY + layout.S(4f), layout.S(UiTheme.FontBody), color, alpha, semiBold: selected);
            }

            UiTheme.DrawSectionHeader(_ui, "World seed", metrics.TypeListX, metrics.SeedY - layout.S(28f), layout, alpha);
            _ui.DrawPanel(
                metrics.Cx - layout.S(130f),
                metrics.SeedY,
                layout.S(260f),
                layout.S(36f),
                _seedFocused ? UiTheme.PanelBgHighlight : UiTheme.PanelBgMuted,
                _seedFocused ? UiTheme.Accent : UiTheme.PanelBorder,
                0.85f,
                alpha,
                UiTheme.RadiusMd);
            _ui.DrawString(_seedText, metrics.Cx - layout.S(120f), metrics.SeedY + layout.S(10f), layout.S(UiTheme.FontBody), UiTheme.Title, alpha);

            if (!string.IsNullOrEmpty(_seedErrorMessage))
            {
                _ui.DrawCenteredText(_seedErrorMessage, metrics.SeedY + layout.S(44f), layout.S(UiTheme.FontSmall), UiTheme.Danger, alpha);
            }

            DrawButton(metrics.Cx + layout.S(100f), metrics.RandomY, layout.S(120f), layout.S(34f), "Random", 2, UiButtonStyle.Ghost, layout, alpha);

            DrawButton(metrics.Cx - buttonW / 2f - layout.S(6f), metrics.CreateY, buttonW, buttonH, "Create world", 0, UiButtonStyle.Primary, layout, alpha);
            DrawButton(metrics.Cx + buttonW / 2f + layout.S(6f), metrics.CreateY, buttonW, buttonH, "Back", 1, UiButtonStyle.Ghost, layout, alpha);

            MenuChrome.DrawHintFooter(_ui, layout, "← → change terrain · Esc back", alpha);
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

        private static ScreenLayout ComputeLayout(UiLayout layout, float panelYOffset = 0f)
        {
            float cx = layout.CenterX;
            float panelY = layout.CenterY - layout.S(PanelHeight / 2f) + panelYOffset;
            float typeY = panelY + layout.S(108f);
            float seedY = typeY + layout.S(108f);
            return new ScreenLayout(
                cx,
                panelY,
                cx - layout.S(200f),
                typeY,
                layout.S(32f),
                layout.S(28f),
                seedY,
                seedY + layout.S(44f),
                seedY + layout.S(90f));
        }

        private readonly struct ScreenLayout(
            float cx,
            float panelY,
            float typeListX,
            float typeY,
            float typeRowStep,
            float typeRowHeight,
            float seedY,
            float randomY,
            float createY)
        {
            public float Cx { get; } = cx;
            public float PanelY { get; } = panelY;
            public float TypeListX { get; } = typeListX;
            public float TypeY { get; } = typeY;
            public float TypeRowStep { get; } = typeRowStep;
            public float TypeRowHeight { get; } = typeRowHeight;
            public float SeedY { get; } = seedY;
            public float RandomY { get; } = randomY;
            public float CreateY { get; } = createY;
        }
    }
}

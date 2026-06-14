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
        private const float ButtonHeight = 40f;
        private const float ButtonSpacing = 12f;
        private const float PanelWidth = 560f;
        private const float PanelHeight = 400f;

        private readonly UiRenderer _ui;
        private readonly MenuBackdrop _backdrop = new MenuBackdrop(28);
        private readonly UiTransition _transition = new UiTransition();
        private readonly Random _random = new Random();
        private readonly float[] _buttonHoverT = new float[3];
        private float _animTime;
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
            _animTime += deltaTime;
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
            var randomRect = GetButtonRect(cx + layout.S(100f), seedY + layout.S(40f), layout.S(120f), layout.S(30f));

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
                        var rowRect = new Rectangle((int)typeListX, (int)rowY, (int)layout.S(400f), (int)layout.S(26f));
                        if (rowRect.Contains(mouse.X, mouse.Y))
                        {
                            _selectedWorldType = i;
                        }
                    }

                    var seedBox = new Rectangle((int)(cx - layout.S(130f)), (int)seedY, (int)layout.S(260f), (int)layout.S(34f));
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

            _ui.DrawCenteredTitle("FOUND SETTLEMENT", layout.Height * 0.085f + offsetY, layout.S(2.4f), new Color(0.82f, 0.92f, 1.0f), alpha);

            _ui.DrawFramedPanel(panelX, panelY, panelW, panelH, new Color(0.04f, 0.06f, 0.09f) * 0.94f, new Color(0.2f, 0.45f, 0.65f), alpha);
            _ui.DrawCenteredText("WORLD SETUP", panelY + layout.S(20f), layout.S(1.55f), new Color(0.75f, 0.86f, 0.96f), alpha);
            _ui.DrawCenteredText("CHOOSE TERRAIN — YOUR STEWARD AWAITS", panelY + layout.S(48f), layout.S(1.05f), new Color(0.48f, 0.56f, 0.66f), alpha);

            float typeY = panelY + layout.S(108f);
            float typeListX = cx - layout.S(200f);
            _ui.DrawString("TERRAIN TYPE", typeListX, typeY - layout.S(26f), layout.S(1.1f), new Color(0.55f, 0.68f, 0.78f), alpha);

            for (int i = 0; i < WorldTypes.Length; i++)
            {
                bool selected = i == _selectedWorldType;
                float rowY = typeY + i * layout.S(30f);
                if (selected)
                {
                    _ui.DrawSoftGlow(typeListX - layout.S(4f), rowY - layout.S(2f), layout.S(400f), layout.S(26f), new Color(0.15f, 0.55f, 0.9f), alpha * 0.25f, 2);
                    _ui.DrawPanel(typeListX - layout.S(4f), rowY - layout.S(2f), layout.S(400f), layout.S(26f), new Color(0.08f, 0.14f, 0.22f) * 0.9f, new Color(0.2f, 0.55f, 0.85f), 0.8f, alpha);
                }

                Color color = selected ? new Color(0.88f, 0.94f, 1.0f) : new Color(0.42f, 0.5f, 0.58f);
                string prefix = selected ? "> " : "  ";
                string label = prefix + FormatWorldType(WorldTypes[i]);
                _ui.DrawString(label, typeListX, rowY + layout.S(4f), layout.S(1.12f), color, alpha);
            }

            float seedY = typeY + layout.S(108f);
            _ui.DrawString("WORLD SEED", typeListX, seedY - layout.S(26f), layout.S(1.1f), new Color(0.55f, 0.68f, 0.78f), alpha);
            _ui.DrawPanel(
                cx - layout.S(130f),
                seedY,
                layout.S(260f),
                layout.S(34f),
                _seedFocused ? new Color(0.1f, 0.16f, 0.24f) : new Color(0.05f, 0.07f, 0.1f),
                _seedFocused ? new Color(0.2f, 0.6f, 0.9f) : new Color(0.18f, 0.28f, 0.38f),
                0.85f,
                alpha);
            _ui.DrawString(_seedText, cx - layout.S(120f), seedY + layout.S(9f), layout.S(1.2f), new Color(0.88f, 0.94f, 1.0f), alpha);

            float randomY = seedY + layout.S(40f);
            DrawButton(cx + layout.S(100f), randomY, layout.S(120f), layout.S(30f), "RANDOM", 2, layout.S(1.0f), alpha);

            float createY = seedY + layout.S(86f);
            DrawButton(cx - buttonW / 2f - layout.S(6f), createY, buttonW, buttonH, "CREATE", 0, layout.S(1.3f), alpha, accent: true);
            DrawButton(cx + buttonW / 2f + layout.S(6f), createY, buttonW, buttonH, "BACK", 1, layout.S(1.3f), alpha);

            _ui.DrawCenteredText("LEFT/RIGHT CHANGE TERRAIN", layout.Height - layout.S(32f) + offsetY, layout.S(0.95f), new Color(0.38f, 0.44f, 0.52f), 0.85f * alpha);
        }

        private void DrawButton(float x, float y, float width, float height, string label, int index, float textPixelSize, float alpha, bool accent = false)
        {
            if (accent)
            {
                float glow = 0.5f + 0.5f * _buttonHoverT[index];
                _ui.DrawFilledRect(x - 1, y - 1, width + 2, height + 2, new Color(0.1f, 0.5f, 0.8f) * (0.25f * glow * alpha));
            }

            _ui.DrawButton(x, y, width, height, label, _hoveredButton == index, false, textPixelSize, alpha, _buttonHoverT[index]);
        }

        private static string FormatWorldType(WorldType type) => type switch
        {
            WorldType.Default => "DEFAULT",
            WorldType.Mountains => "MOUNTAINS",
            WorldType.Islands => "ISLANDS",
            WorldType.Flat => "FLAT",
            _ => type.ToString().ToUpperInvariant()
        };

        private static Rectangle GetButtonRect(float x, float y, float width, float height)
        {
            return new Rectangle((int)x, (int)y, (int)width, (int)height);
        }
    }
}

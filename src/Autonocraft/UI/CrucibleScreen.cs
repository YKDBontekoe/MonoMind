using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Crafting;
using Autonocraft.Engine;
using Autonocraft.World;

namespace Autonocraft.UI
{
    public class CrucibleScreen
    {
        private const float PanelSize = 360f;
        private const float OrbSize = 52f;
        private const float OrbRadius = 110f;
        private const float OutputSize = 64f;
        private const float ButtonWidth = 160f;
        private const float ButtonHeight = 40f;
        private const float TransmuteDuration = 0.8f;

        private readonly UiRenderer _ui;
        private int _hoveredOrb = -1;
        private bool _transmuteHovered;
        private bool _transmutePressed;
        private string _statusMessage = string.Empty;
        private float _statusTimer;
        private readonly float[] _orbPulseTimers = new float[4];
        private float _transmuteTimer;
        private bool _transmuteAnimating;

        public bool TransmuteRequested { get; private set; }
        public bool TransmuteReady { get; private set; }
        public int ClickedOrbIndex { get; private set; } = -1;
        public bool RightClickedOrb { get; private set; }
        public float TransmuteProgress => _transmuteAnimating
            ? 1f - Math.Clamp(_transmuteTimer / TransmuteDuration, 0f, 1f)
            : 0f;
        public bool TransmuteAnimating => _transmuteAnimating;

        public CrucibleScreen(UiRenderer ui)
        {
            _ui = ui;
        }

        public void SetStatus(string message)
        {
            _statusMessage = message;
            _statusTimer = 2.5f;
        }

        public void TriggerOrbPulse(int orbIndex)
        {
            if (orbIndex >= 0 && orbIndex < _orbPulseTimers.Length)
            {
                _orbPulseTimers[orbIndex] = 0.15f;
            }
        }

        public void BeginTransmuteAnimation()
        {
            _transmuteAnimating = true;
            _transmuteTimer = TransmuteDuration;
            TransmuteReady = false;
        }

        public void Update(
            Viewport viewport,
            CrucibleSession session,
            CraftEnvironment env,
            KeyboardState kb,
            MouseState mouse,
            KeyboardState prevKb,
            MouseState prevMouse,
            float deltaTime)
        {
            TransmuteRequested = false;
            TransmuteReady = false;
            ClickedOrbIndex = -1;
            RightClickedOrb = false;
            _transmuteHovered = false;
            _hoveredOrb = -1;

            for (int i = 0; i < _orbPulseTimers.Length; i++)
            {
                if (_orbPulseTimers[i] > 0f)
                {
                    _orbPulseTimers[i] = Math.Max(0f, _orbPulseTimers[i] - deltaTime);
                }
            }

            if (_transmuteAnimating)
            {
                _transmuteTimer = Math.Max(0f, _transmuteTimer - deltaTime);
                if (_transmuteTimer <= 0f)
                {
                    _transmuteAnimating = false;
                    TransmuteReady = true;
                }
            }

            if (!session.IsOpen)
            {
                return;
            }

            if (_statusTimer > 0f)
            {
                _statusTimer -= deltaTime;
            }

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                return;
            }

            var layout = new UiLayout(viewport.Width, viewport.Height);
            float cx = layout.CenterX;
            float cy = layout.CenterY - layout.S(20f);
            float panelX = cx - layout.S(PanelSize) / 2f;
            float panelY = cy - layout.S(PanelSize) / 2f;

            GetOrbPositions(layout, cx, cy, out float[] orbX, out float[] orbY);
            float buttonX = cx - layout.S(ButtonWidth) / 2f;
            float buttonY = panelY + layout.S(PanelSize) - layout.S(72f);

            Point mousePt = new Point(mouse.X, mouse.Y);

            for (int i = 0; i < 4; i++)
            {
                var orbRect = new Rectangle(
                    (int)orbX[i],
                    (int)orbY[i],
                    (int)layout.S(OrbSize),
                    (int)layout.S(OrbSize));

                if (orbRect.Contains(mousePt))
                {
                    _hoveredOrb = i;
                    if (!_transmuteAnimating && mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
                    {
                        ClickedOrbIndex = i;
                    }

                    if (!_transmuteAnimating && mouse.RightButton == ButtonState.Pressed && prevMouse.RightButton == ButtonState.Released)
                    {
                        ClickedOrbIndex = i;
                        RightClickedOrb = true;
                    }
                }
            }

            var transmuteRect = new Rectangle(
                (int)buttonX,
                (int)buttonY,
                (int)layout.S(ButtonWidth),
                (int)layout.S(ButtonHeight));

            if (transmuteRect.Contains(mousePt))
            {
                _transmuteHovered = true;
                if (!_transmuteAnimating && mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
                {
                    TransmuteRequested = true;
                }
            }

            _transmutePressed = mouse.LeftButton == ButtonState.Pressed && _transmuteHovered;
        }

        public void Draw(
            Viewport viewport,
            CrucibleSession session,
            CraftEnvironment env,
            Texture2D? atlasTexture)
        {
            if (!session.IsOpen)
            {
                return;
            }

            var layout = new UiLayout(viewport.Width, viewport.Height);
            float cx = layout.CenterX;
            float cy = layout.CenterY - layout.S(20f);
            float panelW = layout.S(PanelSize);
            float panelH = layout.S(PanelSize);
            float panelX = cx - panelW / 2f;
            float panelY = cy - panelH / 2f;

            _ui.DrawFullscreenBackground(UiTheme.PanelFill * 0.72f);
            _ui.DrawPanel(panelX, panelY, panelW, panelH, UiTheme.PanelBgMuted, UiTheme.PanelBorder);

            string title = session.StationType switch
            {
                BlockType.StationForge => "FORGE CRUCIBLE",
                BlockType.StationCrucible => "ALCHEMY CRUCIBLE",
                _ => "BENCH CRUCIBLE"
            };
            _ui.DrawCenteredText(title, panelY + layout.S(18f), layout.S(UiTheme.ScaleTitle), UiTheme.Title);

            GetOrbPositions(layout, cx, cy, out float[] orbX, out float[] orbY);

            for (int i = 0; i < 4; i++)
            {
                bool hovered = _hoveredOrb == i;
                float pulseT = _orbPulseTimers[i] > 0f ? _orbPulseTimers[i] / 0.15f : 0f;
                float pulseScale = 1f + 0.15f * MathF.Sin((1f - pulseT) * MathF.PI);
                float orbSize = layout.S(OrbSize) * pulseScale;
                float orbOffset = (orbSize - layout.S(OrbSize)) / 2f;
                float drawX = orbX[i] - orbOffset;
                float drawY = orbY[i] - orbOffset;

                Color fill = hovered ? UiTheme.PanelBgHighlight : UiTheme.PanelBgMuted;
                if (_transmuteAnimating)
                {
                    float glow = 0.5f + 0.5f * MathF.Sin(TransmuteProgress * MathF.PI * 4f);
                    fill = Color.Lerp(fill, UiTheme.Accent, glow * 0.5f);
                }

                _ui.DrawPanel(drawX, drawY, orbSize, orbSize, fill, hovered ? UiTheme.Accent : UiTheme.Rule);

                if (session.InputSlots[i] != BlockType.Air)
                {
                    float swatchPad = layout.S(10f) + orbOffset;
                    DrawBlockSwatch(session.InputSlots[i], orbX[i] + swatchPad - orbOffset, orbY[i] + swatchPad - orbOffset, layout.S(OrbSize - 20f));
                }
            }

            float outputX = cx - layout.S(OutputSize) / 2f;
            float outputY = cy - layout.S(OutputSize) / 2f;
            _ui.DrawPanel(outputX, outputY, layout.S(OutputSize), layout.S(OutputSize), UiTheme.PanelBgMuted, UiTheme.PanelBorder);

            if (_transmuteAnimating)
            {
                DrawTransmuteRing(layout, cx, cy, TransmuteProgress);
            }

            DrawEnvironmentStrip(layout, panelX, panelY + layout.S(52f), panelW, env);

            float buttonX = cx - layout.S(ButtonWidth) / 2f;
            float buttonY = panelY + panelH - layout.S(72f);
            _ui.DrawButton(buttonX, buttonY, layout.S(ButtonWidth), layout.S(ButtonHeight), "TRANSMUTE", _transmuteHovered, _transmutePressed, layout.S(UiTheme.ScaleTitle));

            if (_statusTimer > 0f && !string.IsNullOrEmpty(_statusMessage))
            {
                _ui.DrawCenteredText(_statusMessage, panelY + panelH - layout.S(28f), layout.S(UiTheme.ScaleNormal), UiTheme.Subtitle);
            }

            _ui.DrawCenteredText("L-CLICK ORB: DEPOSIT  R-CLICK: WITHDRAW  ESC: CLOSE", panelY + panelH + layout.S(8f), layout.S(UiTheme.ScaleSmall), UiTheme.Hint);
        }

        private void DrawTransmuteRing(UiLayout layout, float cx, float cy, float progress)
        {
            float radius = layout.S(48f);
            int segments = (int)(progress * 32f);
            Color ringColor = new Color(0.35f, 0.85f, 1f) * 0.85f;
            for (int s = 0; s < segments; s++)
            {
                float angle = -MathF.PI / 2f + s * (MathF.PI * 2f / 32f);
                float px = cx + MathF.Cos(angle) * radius;
                float py = cy + MathF.Sin(angle) * radius;
                _ui.DrawFilledRect(px - 2f, py - 2f, 4f, 4f, ringColor);
            }
        }

        private void DrawEnvironmentStrip(UiLayout layout, float x, float y, float width, CraftEnvironment env)
        {
            float pad = layout.S(16f);
            string biome = env.Biome.ToString().ToUpperInvariant();
            string phase = env.TimePhase.ToString().ToUpperInvariant();
            string water = env.HasAdjacentWater ? "WATER OK" : "NO WATER";
            string heat = env.HasAdjacentHeat || env.HasFuelInInputs ? "HEAT OK" : "NO HEAT";

            Color waterColor = env.HasAdjacentWater ? new Color(0.3f, 0.85f, 1f) : UiTheme.Danger;
            Color heatColor = env.HasAdjacentHeat || env.HasFuelInInputs ? new Color(1f, 0.65f, 0.25f) : UiTheme.Danger;

            _ui.DrawString($"{biome} | {phase}", x + pad, y, layout.S(UiTheme.ScaleSection), UiTheme.Subtitle);
            _ui.DrawString(water, x + pad, y + layout.S(18f), layout.S(UiTheme.ScaleSmall), waterColor);
            _ui.DrawString(heat, x + pad + layout.S(120f), y + layout.S(18f), layout.S(UiTheme.ScaleSmall), heatColor);
        }

        private static void GetOrbPositions(UiLayout layout, float cx, float cy, out float[] orbX, out float[] orbY)
        {
            orbX = new float[4];
            orbY = new float[4];
            float radius = layout.S(OrbRadius);
            float size = layout.S(OrbSize);

            for (int i = 0; i < 4; i++)
            {
                float angle = MathF.PI * 0.5f + i * MathF.PI * 0.5f;
                orbX[i] = cx + MathF.Cos(angle) * radius - size / 2f;
                orbY[i] = cy + MathF.Sin(angle) * radius - size / 2f;
            }
        }

        private void DrawBlockSwatch(BlockType type, float x, float y, float size)
        {
            Color swatch = type switch
            {
                BlockType.OakLog or BlockType.BirchLog or BlockType.PineLog or BlockType.OakPlank => new Color(0.55f, 0.38f, 0.22f),
                BlockType.Stone or BlockType.Sandstone or BlockType.IronBlock or BlockType.GoldBlock => new Color(0.55f, 0.55f, 0.58f),
                BlockType.Sand or BlockType.Clay => new Color(0.78f, 0.68f, 0.42f),
                BlockType.Dirt or BlockType.Grass => new Color(0.45f, 0.32f, 0.18f),
                BlockType.CoalOre => new Color(0.2f, 0.2f, 0.22f),
                BlockType.IronOre => new Color(0.62f, 0.42f, 0.28f),
                BlockType.GoldOre => new Color(0.82f, 0.68f, 0.22f),
                BlockType.Glass => new Color(0.55f, 0.78f, 0.92f),
                BlockType.OakLeaves or BlockType.BirchLeaves or BlockType.PineLeaves => new Color(0.25f, 0.55f, 0.22f),
                BlockType.Cactus => new Color(0.22f, 0.52f, 0.24f),
                BlockType.Gravel => new Color(0.48f, 0.46f, 0.44f),
                _ => new Color(0.5f, 0.5f, 0.5f)
            };

            _ui.DrawFilledRect(x, y, size, size, swatch);
        }
    }
}

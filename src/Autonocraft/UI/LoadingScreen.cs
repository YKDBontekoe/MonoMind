using System;
using System.Diagnostics;
using Vector3 = System.Numerics.Vector3;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Autonocraft.Core;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.World;

namespace Autonocraft.UI
{
    public class LoadingScreen
    {
        private const int DefaultChunksPerFrame = 10;
        private const int HighDistanceChunksPerFrame = 18;
        private const int MaxLoadStepsPerFrame = 16;
        private const float LoadStepBudgetMs = 14f;
        private const float TipCycleSeconds = 5.5f;

        private static readonly string[] LoadingTips =
        {
            "Shift + right-click crafting sigils to activate stations.",
            "Press V to open the town board and manage your village.",
            "Use the 2×2 inventory grid to craft planks and basic tools.",
            "Cooked food restores more hunger than raw meat.",
            "Villagers can gather, build, and haul for your settlement.",
            "Higher render distance loads more terrain — reduce it if loading stalls.",
            "Press E to open your inventory and recipe book.",
            "Night brings wolves — build shelter before dusk.",
        };

        private readonly VoxelWorld _world;
        private readonly GraphicsDevice _device;
        private readonly UiRenderer _ui;
        private readonly MenuBackdrop _backdrop = new MenuBackdrop();
        private readonly UiTransition _panelTransition = new UiTransition();

        private float _displayProgress;
        private float _animTime;

        private bool _started;
        private bool _complete;
        private bool _timedOut;
        private string? _timeoutReason;
        private float _progress;
        private float _elapsedTime;
        private float _lastProgressValue;
        private float _stallTimer;
        private string _status = "PREPARING WORLD";
        private int _chunksPerFrame = DefaultChunksPerFrame;
        private float _totalLoadTimeoutSeconds = 120f;
        private float _stallTimeoutSeconds = 30f;

        private string _worldName = "New World";
        private string _biomeSummary = string.Empty;
        private bool _loadingFromSave;

        public LoadingScreen(VoxelWorld world, GraphicsDevice device, UiRenderer ui)
        {
            _world = world;
            _device = device;
            _ui = ui;
        }

        public bool IsComplete => _complete;
        public bool HasTimedOut => _timedOut;
        public string? TimeoutReason => _timeoutReason;
        public float Progress => _progress;
        public string StatusText => _status;

        private int _renderDistance = 8;

        public void Begin(Vector3 spawnPos, int renderDistance, WorldSaveData? saveData = null, string? worldName = null)
        {
            if (saveData != null)
            {
                _world.ApplySaveData(saveData);
            }

            _loadingFromSave = saveData != null;
            _worldName = string.IsNullOrWhiteSpace(worldName)
                ? (_loadingFromSave ? "Saved World" : "New World")
                : worldName.Trim();
            _biomeSummary = _ui.WorldThumbnails.GetBiomeSummary(_world.Seed);

            _renderDistance = renderDistance;
            _world.BeginInitialLoad(spawnPos, renderDistance);
            _chunksPerFrame = renderDistance >= 24 ? 24 :
                renderDistance >= 16 ? 20 :
                renderDistance >= 8 ? HighDistanceChunksPerFrame :
                DefaultChunksPerFrame;
            _totalLoadTimeoutSeconds = 120f + renderDistance * 4f;
            _stallTimeoutSeconds = 45f + renderDistance * 2f;
            _started = true;
            _complete = false;
            _timedOut = false;
            _timeoutReason = null;
            _progress = 0f;
            _displayProgress = 0f;
            _elapsedTime = 0f;
            _animTime = 0f;
            _lastProgressValue = 0f;
            _stallTimer = 0f;
            _status = _loadingFromSave ? "Restoring world" : "Generating terrain";
            _panelTransition.BeginFadeInSlideUp(0.45f, 18f);
        }

        public void Update(float deltaTime = 0f)
        {
            _backdrop.Update(deltaTime);
            _panelTransition.Update(deltaTime);
            _animTime += deltaTime;

            if (!_started || _complete || _timedOut)
            {
                return;
            }

            _elapsedTime += deltaTime;

            var stepTimer = Stopwatch.StartNew();
            for (int step = 0; step < MaxLoadStepsPerFrame; step++)
            {
                if (_world.AdvanceInitialLoad(_device, _chunksPerFrame, VoxelWorld.GetLoadingMeshChunksPerFrame(_renderDistance), _renderDistance, out _progress, out _status))
                {
                    _complete = true;
                    _progress = 1f;
                    _status = "Ready";
                    break;
                }

                if (step > 0 && stepTimer.Elapsed.TotalMilliseconds >= LoadStepBudgetMs)
                {
                    break;
                }
            }

            if (MathF.Abs(_progress - _lastProgressValue) > 0.001f)
            {
                _lastProgressValue = _progress;
                _stallTimer = 0f;
            }
            else
            {
                _stallTimer += deltaTime;
            }

            if (_elapsedTime >= _totalLoadTimeoutSeconds)
            {
                _timedOut = true;
                _timeoutReason = "World loading timed out. Try again or reduce render distance.";
                _status = "Load failed";
            }
            else if (_stallTimer >= _stallTimeoutSeconds)
            {
                _timedOut = true;
                _timeoutReason = "World loading stalled. Try again or reduce render distance.";
                _status = "Load failed";
            }

            _displayProgress = MathF.Max(_displayProgress, _progress);
            _displayProgress = Tween.SmoothDamp(_displayProgress, _progress, 14f, deltaTime);
            if (_displayProgress < _progress)
            {
                _displayProgress = _progress;
            }
        }

        public void Draw(Viewport viewport, float alpha = 1f, float offsetY = 0f)
        {
            float panelAlpha = alpha * _panelTransition.Alpha;
            float panelOffsetY = offsetY + _panelTransition.OffsetY;
            var layout = new UiLayout(viewport);

            _backdrop.Draw(_ui, viewport, alpha);

            float cx = layout.CenterX;
            float panelW = layout.S(540f);
            float panelH = layout.S(300f);
            float panelX = cx - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f + panelOffsetY;

            _ui.DrawCard(panelX, panelY, panelW, panelH, panelAlpha, UiTheme.RadiusXl);

            float thumbSize = layout.S(88f);
            float thumbX = panelX + layout.S(28f);
            float thumbY = panelY + layout.S(28f);
            DrawThumbnailWithPulse(thumbX, thumbY, thumbSize, panelAlpha);

            float contentX = thumbX + thumbSize + layout.S(22f);
            float contentW = panelX + panelW - contentX - layout.S(28f);

            _ui.DrawLabel(_worldName, contentX, panelY + layout.S(28f), layout.S(UiTheme.FontTitle), UiTheme.Title, semiBold: true, alpha: panelAlpha);

            string seedLine = $"Seed {_world.Seed}";
            float seedY = panelY + layout.S(62f);
            _ui.DrawLabel(seedLine, contentX, seedY, layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha: panelAlpha * 0.92f);

            if (!string.IsNullOrEmpty(_biomeSummary))
            {
                float chipX = contentX + _ui.MeasureString(seedLine, layout.S(UiTheme.FontSmall)) + layout.S(10f);
                DrawMetaChip(chipX, seedY - layout.S(2f), _biomeSummary, UiTheme.StatAccentExplore, layout, panelAlpha);
            }

            string stageText = FormatStageText(_status);
            _ui.DrawLabel(stageText, contentX, panelY + layout.S(96f), layout.S(UiTheme.FontBody), UiTheme.Section, alpha: panelAlpha);

            float barW = contentW;
            float barH = layout.S(14f);
            float barX = contentX;
            float barY = panelY + panelH - layout.S(108f);
            DrawAnimatedProgressBar(barX, barY, barW, barH, _displayProgress, layout, panelAlpha);

            string tip = GetCurrentTip();
            float tipAlpha = panelAlpha * GetTipFade();
            _ui.DrawCenteredText(tip, panelY + panelH - layout.S(36f), layout.S(UiTheme.FontCaption), UiTheme.Hint, tipAlpha);
        }

        private void DrawThumbnailWithPulse(float x, float y, float size, float alpha)
        {
            float pulse = Tween.Pulse(_animTime, 0.7f);
            float glowExpand = layoutPulseSize(size, pulse);

            _ui.DrawBatch((batch, tex) =>
            {
                float cx = x + size * 0.5f;
                float cy = y + size * 0.5f;
                const int layers = 4;
                for (int i = layers; i >= 1; i--)
                {
                    float expand = glowExpand * (i / (float)layers);
                    float layerAlpha = alpha * (0.06f + pulse * 0.04f) / i;
                    var rect = new Rectangle(
                        (int)(cx - size * 0.5f - expand),
                        (int)(cy - size * 0.5f - expand),
                        (int)(size + expand * 2f),
                        (int)(size + expand * 2f));
                    batch.Draw(tex, rect, UiTheme.AccentGlow * layerAlpha);
                }
            });

            var thumbnail = _ui.WorldThumbnails.GetThumbnail(_world.Seed);
            _ui.DrawThumbnailFrame(thumbnail, x, y, size, alpha, UiTheme.RadiusMd);
        }

        private static float layoutPulseSize(float size, float pulse)
        {
            return size * 0.06f + pulse * size * 0.04f;
        }

        private void DrawAnimatedProgressBar(float x, float y, float w, float h, float progress, UiLayout layout, float alpha)
        {
            progress = Math.Clamp(progress, 0f, 1f);
            float radius = Math.Min(UiTheme.RadiusSm, h * 0.45f);
            float pulse = Tween.Pulse(_animTime, 1.1f);

            _ui.DrawBatch((batch, tex) =>
            {
                DrawRoundedBar(batch, tex, x, y, w, h, radius, UiTheme.ProgressTrack, alpha);

                if (progress > 0.005f)
                {
                    float fillW = Math.Max(radius * 2f, w * progress);
                    if (progress < 1f)
                    {
                        float glowAlpha = alpha * (0.14f + pulse * 0.10f);
                        DrawRoundedBar(batch, tex, x - 2f, y - 2f, fillW + 4f, h + 4f, radius + 2f, UiTheme.AccentGlow, glowAlpha);
                    }

                    DrawRoundedBar(batch, tex, x, y, fillW, h, radius, UiTheme.ProgressFill, alpha);

                    if (progress < 1f)
                    {
                        float shimmerW = Math.Min(layout.S(28f), fillW * 0.35f);
                        float shimmerX = x + fillW - shimmerW - layout.S(4f);
                        float shimmerAlpha = alpha * (0.18f + pulse * 0.14f);
                        DrawRoundedBar(batch, tex, shimmerX, y + 1f, shimmerW, h - 2f, radius, Color.White, shimmerAlpha);
                    }
                }

                DrawBarOutline(batch, tex, x, y, w, h, radius, UiTheme.PanelBorder, alpha * 0.6f);
            });

            int pct = (int)MathF.Round(progress * 100f);
            _ui.DrawLabel($"{pct}%", x + w - _ui.MeasureString($"{pct}%", layout.S(UiTheme.FontCaption)), y + h + layout.S(8f),
                layout.S(UiTheme.FontCaption), UiTheme.ProgressPercent, alpha: alpha);
        }

        private static void DrawRoundedBar(SpriteBatch batch, Texture2D tex, float x, float y, float w, float h, float radius, Color color, float alpha)
        {
            if (w <= 0f || alpha <= 0f)
            {
                return;
            }

            batch.Draw(tex, new Rectangle((int)x, (int)y, (int)w, (int)h), color * alpha);
            if (radius > 1f)
            {
                int r = (int)MathF.Min(radius, h * 0.5f);
                batch.Draw(tex, new Rectangle((int)x, (int)y, r, (int)h), color * alpha);
                batch.Draw(tex, new Rectangle((int)(x + w - r), (int)y, r, (int)h), color * alpha);
            }
        }

        private static void DrawBarOutline(SpriteBatch batch, Texture2D tex, float x, float y, float w, float h, float radius, Color color, float alpha)
        {
            batch.Draw(tex, new Rectangle((int)x, (int)y, (int)w, 1), color * alpha);
            batch.Draw(tex, new Rectangle((int)x, (int)(y + h - 1f), (int)w, 1), color * alpha);
            batch.Draw(tex, new Rectangle((int)x, (int)y, 1, (int)h), color * alpha);
            batch.Draw(tex, new Rectangle((int)(x + w - 1f), (int)y, 1, (int)h), color * alpha);
        }

        private void DrawMetaChip(float x, float y, string label, Color accent, UiLayout layout, float alpha)
        {
            float padX = layout.S(8f);
            float textW = _ui.MeasureString(label, layout.S(UiTheme.FontCaption));
            float chipW = textW + padX * 2f;
            float chipH = layout.S(18f);
            _ui.DrawRoundedRect(x, y, chipW, chipH, chipH * 0.5f, accent * (0.14f * alpha));
            _ui.DrawRoundedRectOutline(x, y, chipW, chipH, chipH * 0.5f, accent * (0.45f * alpha), 1f, alpha);
            _ui.DrawLabel(label, x + padX, y + layout.S(3f), layout.S(UiTheme.FontCaption), accent, semiBold: true, alpha: alpha * 0.95f);
        }

        private static string FormatStageText(string rawStatus)
        {
            if (string.IsNullOrWhiteSpace(rawStatus))
            {
                return "Preparing world…";
            }

            string upper = rawStatus.ToUpperInvariant();
            if (upper == "READY")
            {
                return "Ready to explore";
            }

            if (upper.StartsWith("BUILDING CHUNKS", StringComparison.Ordinal))
            {
                return FormatChunkStatus("Building chunks", rawStatus, "BUILDING CHUNKS");
            }

            if (upper.StartsWith("MESHING CHUNKS", StringComparison.Ordinal))
            {
                return FormatChunkStatus("Meshing terrain", rawStatus, "MESHING CHUNKS");
            }

            if (upper == "LOAD FAILED")
            {
                return "Load failed";
            }

            return char.ToUpper(rawStatus[0]) + rawStatus[1..].ToLowerInvariant();
        }

        private static string FormatChunkStatus(string label, string rawStatus, string prefix)
        {
            int spaceIndex = rawStatus.IndexOf(' ');
            if (spaceIndex < 0 || rawStatus.Length <= prefix.Length + 1)
            {
                return label + "…";
            }

            string counts = rawStatus[(prefix.Length + 1)..].Trim();
            return $"{label} · {counts.Replace("/", " / ")}";
        }

        private string GetCurrentTip()
        {
            if (LoadingTips.Length == 0)
            {
                return string.Empty;
            }

            int index = (int)(_elapsedTime / TipCycleSeconds) % LoadingTips.Length;
            return LoadingTips[index];
        }

        private float GetTipFade()
        {
            float cyclePos = _elapsedTime % TipCycleSeconds;
            const float fadeWindow = 0.35f;
            if (cyclePos < fadeWindow)
            {
                return Tween.EaseOut(cyclePos / fadeWindow);
            }

            if (cyclePos > TipCycleSeconds - fadeWindow)
            {
                return Tween.EaseOut((TipCycleSeconds - cyclePos) / fadeWindow);
            }

            return 1f;
        }
    }
}

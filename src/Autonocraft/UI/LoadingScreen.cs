using System;
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
        private const int DefaultChunksPerFrame = 6;
        private const int HighDistanceChunksPerFrame = 14;

        private readonly VoxelWorld _world;
        private readonly GraphicsDevice _device;
        private readonly UiRenderer _ui;
        private readonly UiTransition _panelTransition = new UiTransition();
        private float _displayProgress;

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

        public void Begin(Vector3 spawnPos, int renderDistance, WorldSaveData? saveData = null)
        {
            if (saveData != null)
            {
                _world.ApplySaveData(saveData);
            }

            _renderDistance = renderDistance;
            _world.BeginInitialLoad(spawnPos, renderDistance);
            _chunksPerFrame = renderDistance >= 24 ? 20 :
                renderDistance >= 16 ? 16 :
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
            _lastProgressValue = 0f;
            _stallTimer = 0f;
            _status = saveData == null ? "GENERATING TERRAIN" : "RESTORING WORLD";
            _panelTransition.BeginFadeIn(0.2f);
        }

        public void Update(float deltaTime = 0f)
        {
            _panelTransition.Update(deltaTime);

            if (!_started || _complete || _timedOut)
            {
                return;
            }

            _elapsedTime += deltaTime;

            if (_world.AdvanceInitialLoad(_device, _chunksPerFrame, VoxelWorld.GetLoadingMeshChunksPerFrame(_renderDistance), _renderDistance, out _progress, out _status))
            {
                _complete = true;
                _progress = 1f;
                _status = "READY";
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
                _status = "LOAD FAILED";
            }
            else if (_stallTimer >= _stallTimeoutSeconds)
            {
                _timedOut = true;
                _timeoutReason = "World loading stalled. Try again or reduce render distance.";
                _status = "LOAD FAILED";
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
            alpha *= _panelTransition.Alpha;
            offsetY += _panelTransition.OffsetY;

            var layout = new UiLayout(viewport);

            _ui.DrawFullscreenBackground(new Color(0.05f, 0.09f, 0.16f) * alpha);
            _ui.DrawVignette(0.42f, alpha);

            float cx = layout.CenterX;
            float panelW = layout.S(500f);
            float panelH = layout.S(170f);
            float panelX = cx - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f + offsetY;

            _ui.DrawFramedPanel(panelX, panelY, panelW, panelH, UiTheme.PanelFill * 0.92f, UiTheme.PanelBorder, alpha);

            _ui.DrawCenteredTitle("BUILDING WORLD", panelY + layout.S(24f), layout.S(1.85f), UiTheme.Title, alpha);
            _ui.DrawCenteredText(_status, panelY + layout.S(58f), layout.S(UiTheme.ScaleSection), UiTheme.Subtitle, alpha);

            float barW = layout.S(380f);
            float barH = layout.S(12f);
            float barX = cx - barW / 2f;
            float barY = panelY + panelH - layout.S(48f);
            _ui.DrawProgressBar(barX, barY, barW, barH, _displayProgress, string.Empty, layout.Scale, alpha);
        }
    }
}

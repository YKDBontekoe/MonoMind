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
        private const int DefaultChunksPerFrame = 5;
        private const int HighDistanceChunksPerFrame = 8;
        private const float TotalLoadTimeoutSeconds = 120f;
        private const float StallTimeoutSeconds = 30f;

        private readonly VoxelWorld _world;
        private readonly GraphicsDevice _device;
        private readonly UiRenderer _ui;
        private readonly MenuBackdrop _backdrop = new MenuBackdrop(20);
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
            _chunksPerFrame = renderDistance >= 8 ? HighDistanceChunksPerFrame : DefaultChunksPerFrame;
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
            _panelTransition.BeginFadeIn(0.25f);
        }

        public void Update(float deltaTime = 0f)
        {
            _panelTransition.Update(deltaTime);
            _backdrop.Update(deltaTime);

            if (!_started || _complete || _timedOut)
            {
                return;
            }

            _elapsedTime += deltaTime;

            if (_world.AdvanceInitialLoad(_device, _chunksPerFrame, VoxelWorld.LoadingMeshChunksPerFrame, _renderDistance, out _progress, out _status))
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

            if (_elapsedTime >= TotalLoadTimeoutSeconds)
            {
                _timedOut = true;
                _timeoutReason = "World loading timed out. Try again or reduce render distance.";
                _status = "LOAD FAILED";
            }
            else if (_stallTimer >= StallTimeoutSeconds)
            {
                _timedOut = true;
                _timeoutReason = "World loading stalled. Try again or reduce render distance.";
                _status = "LOAD FAILED";
            }

            _displayProgress = Tween.SmoothDamp(_displayProgress, _progress, 8f, deltaTime);
        }

        public void Draw(Viewport viewport, float alpha = 1f, float offsetY = 0f)
        {
            alpha *= _panelTransition.Alpha;
            offsetY += _panelTransition.OffsetY;

            var layout = new UiLayout(viewport);

            _backdrop.Draw(_ui, viewport, alpha);

            float cx = layout.CenterX;
            float panelW = layout.S(540f);
            float panelH = layout.S(190f);
            float panelX = cx - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f + offsetY;

            _ui.DrawFramedPanel(panelX, panelY, panelW, panelH, new Color(0.04f, 0.06f, 0.09f) * 0.94f, new Color(0.2f, 0.45f, 0.65f), alpha);

            _ui.DrawCenteredTitle("LOADING WORLD", panelY + layout.S(22f), layout.S(1.9f), new Color(0.82f, 0.92f, 1.0f), alpha);
            _ui.DrawCenteredText(_status, panelY + layout.S(52f), layout.S(1.2f), new Color(0.6f, 0.68f, 0.78f), alpha);

            float barW = layout.S(360f);
            float barH = layout.S(14f);
            float barX = cx - barW / 2f;
            float barY = panelY + panelH - layout.S(52f);
            _ui.DrawProgressBar(barX, barY, barW, barH, _displayProgress, "PROGRESS", layout.Scale, alpha);
        }
    }
}

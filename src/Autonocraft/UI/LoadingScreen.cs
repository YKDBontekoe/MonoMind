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
        private const int DefaultChunksPerFrame = 2;
        private const int HighDistanceChunksPerFrame = 3;

        private readonly VoxelWorld _world;
        private readonly GraphicsDevice _device;
        private readonly UiRenderer _ui;
        private readonly UiTransition _panelTransition = new UiTransition();
        private float _displayProgress;

        private bool _started;
        private bool _complete;
        private float _progress;
        private string _status = "PREPARING WORLD";
        private int _chunksPerFrame = DefaultChunksPerFrame;

        public LoadingScreen(VoxelWorld world, GraphicsDevice device, UiRenderer ui)
        {
            _world = world;
            _device = device;
            _ui = ui;
        }

        public bool IsComplete => _complete;
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
            _progress = 0f;
            _displayProgress = 0f;
            _status = saveData == null ? "GENERATING TERRAIN" : "RESTORING WORLD";
            _panelTransition.BeginFadeIn(0.25f);
        }

        public void Update(float deltaTime = 0f)
        {
            _panelTransition.Update(deltaTime);

            if (!_started || _complete)
            {
                return;
            }

            if (_world.AdvanceInitialLoad(_device, _chunksPerFrame, VoxelWorld.LoadingMeshChunksPerFrame, _renderDistance, out _progress, out _status))
            {
                _complete = true;
                _progress = 1f;
                _status = "READY";
            }

            _displayProgress = Tween.SmoothDamp(_displayProgress, _progress, 8f, deltaTime);
        }

        public void Draw(Viewport viewport, float alpha = 1f, float offsetY = 0f)
        {
            alpha *= _panelTransition.Alpha;
            offsetY += _panelTransition.OffsetY;

            var layout = new UiLayout(viewport);

            _ui.DrawFullscreenBackground(new Color(0.03f, 0.04f, 0.07f));

            float cx = layout.CenterX;
            float panelW = layout.S(520f);
            float panelH = layout.S(180f);
            float panelX = cx - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f + offsetY;

            _ui.DrawPanel(panelX, panelY, panelW, panelH, new Color(0.04f, 0.05f, 0.08f) * 0.88f, new Color(0.2f, 0.3f, 0.4f), 0.8f, alpha);

            _ui.DrawCenteredText("LOADING WORLD", panelY + layout.S(24f), layout.S(1.8f), new Color(0.8f, 0.9f, 1.0f), alpha);
            _ui.DrawCenteredText(_status, panelY + layout.S(52f), layout.S(1.2f), new Color(0.6f, 0.68f, 0.78f), alpha);

            float barW = layout.S(360f);
            float barH = layout.S(14f);
            float barX = cx - barW / 2f;
            float barY = panelY + panelH - layout.S(52f);
            _ui.DrawProgressBar(barX, barY, barW, barH, _displayProgress, "PROGRESS", layout.Scale, alpha);
        }
    }
}

using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Autonocraft.Diagnostics;
using Autonocraft.Engine;
using Autonocraft.World;

namespace Autonocraft.Core
{
    public partial class AutonocraftGame
    {
        protected override void Draw(GameTime gameTime)
        {
            if (_runTests || _ui == null)
            {
                base.Draw(gameTime);
                return;
            }

            try
            {
                DrawFrame(gameTime);
            }
            catch (Exception ex)
            {
                RuntimeMetrics.RecordManagedException("Draw", ex);
                InputDebugTrace.LogException("Draw", ex);
                Console.WriteLine($"[Game] Draw fault: {ex}");
            }
            finally
            {
                FlushPendingScreenshot();
            }
        }

        private void DrawFrame(GameTime gameTime)
        {
            switch (_screens.State)
            {
                case GameState.MainMenu:
                    _screens.DrawMainMenu(GraphicsDevice, (float)gameTime.ElapsedGameTime.TotalSeconds);
                    break;
                case GameState.NewWorldSetup:
                    _screens.DrawNewWorldSetup(GraphicsDevice);
                    break;
                case GameState.WorldLoading:
                    _screens.DrawWorldLoading(GraphicsDevice);
                    break;
                case GameState.Playing:
                    if (HasBlockingGameplayOverlay())
                    {
                        _ui!.DrawFullscreenBackground(new Microsoft.Xna.Framework.Color(0.02f, 0.03f, 0.06f) * 0.92f);
                        PerfCounters.RecordDraw(0f);
                    }
                    else
                    {
                        var drawStopwatch = Stopwatch.StartNew();
                        var renderContext = _session.PrepareRenderContext(_camera, _timeOfDay, _waterAnimTime, _settings.RenderDistance);
                        _blueprints.PopulateConstructionSitePreviews(renderContext, _session.Villages, _session.Player.Position);
                        renderContext.VillageUiOpen = _screens.VillageScreen?.IsOpen == true;
                        renderContext.IsStructureGalleryWorld = _isStructureGalleryWorld;
                        _blueprints.ApplyToRenderContext(renderContext);
                        _renderer?.Draw(renderContext);
                        drawStopwatch.Stop();
                        PerfCounters.RecordDraw((float)drawStopwatch.Elapsed.TotalMilliseconds);
                    }

                    _screens.DrawPlayingOverlays(
                        GraphicsDevice,
                        _session.Crafting,
                        _session.Grid,
                        _atlasTexture,
                        _session.Player,
                        _session.Chest,
                        _timeOfDay);
                    RecordFrameMetrics((float)gameTime.ElapsedGameTime.TotalSeconds);
                    break;
            }

            base.Draw(gameTime);
            FlushPendingScreenshot();
        }
    }
}

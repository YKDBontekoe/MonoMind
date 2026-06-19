using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Ai;
using Autonocraft.Diagnostics;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.Engine.Audio;
using Autonocraft.UI.Menu;
using Autonocraft.World;
using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.Core
{
    public partial class AutonocraftGame
    {
        private bool _skipMenu;
        private bool _agentServerStarted;
        private int _agentPort = 5001;
        private float _spawnWarmupRemaining;
        private bool _fastLoadingGraphicsApplied;
        private float _claimHintTimer;
        private const float SpawnWarmupSeconds = 15f;

        private float SpawnWarmupProgress =>
            Math.Clamp(1f - (_spawnWarmupRemaining / SpawnWarmupSeconds), 0f, 1f);

        private static bool IsCiEnvironment() =>
            string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

        private void ConfigureAgentSessionGraphics()
        {
            if ((!_skipMenu && !_structureGalleryCli) || _graphics == null)
            {
                return;
            }

            _settings.VSync = false;
            if (IsCiEnvironment())
            {
                _graphics.PreferredBackBufferWidth = 800;
                _graphics.PreferredBackBufferHeight = 600;
                _graphics.HardwareModeSwitch = false;
            }

            ApplyFastLoadingGraphics();
        }

        private void ApplyFastLoadingGraphics()
        {
            if (_graphics == null || _fastLoadingGraphicsApplied)
            {
                return;
            }

            _graphics.SynchronizeWithVerticalRetrace = false;
            _graphics.ApplyChanges();
            InactiveSleepTime = TimeSpan.Zero;
            _fastLoadingGraphicsApplied = true;
        }

        private void RestoreGameplayGraphics()
        {
            if (_graphics == null || !_fastLoadingGraphicsApplied)
            {
                return;
            }

            _graphics.SynchronizeWithVerticalRetrace = _settings.VSync;
            _graphics.ApplyChanges();
            _fastLoadingGraphicsApplied = false;
        }

        private void PrepareNewWorldSettlement()
        {
            _session.Villages.EnsureStarterSettlement(_session.Grid, _worldSpawnX, _worldSpawnZ);
            _session.PlacePlayerOnSurface(_worldSpawnX, _worldSpawnZ);
            SyncCameraFromPlayer();
            _session.VillageHudHint = "V — Town board · PEOPLE tab assigns jobs";
            _session.Crafting.ShowCraftingHint = false;
        }

        private void EnterPlaying()
        {
            _screens.State = GameState.Playing;
            CloseAllGameplayOverlays();

            bool fromSave = _loadingFromSave;
            if (_isStructureGalleryWorld)
            {
                PlacePlayerOnSurface();
                _session.VillageHudHint = "Structure gallery — fly around to inspect every world-gen structure";
                _session.Crafting.ShowCraftingHint = false;
            }
            else if (_needsStarterSettlement)
            {
                _needsStarterSettlement = false;
                PrepareNewWorldSettlement();
            }
            else if (!fromSave)
            {
                PlacePlayerOnSurface();
            }
            else
            {
                _session.Villages.RepairAllVillages(_session.Grid);
            }

            if (!_isStructureGalleryWorld && !fromSave)
            {
                _session.VillageHudHint = "V — Town board · Recruit villagers and queue buildings";
                _session.Crafting.ShowCraftingHint = false;
            }
            else if (fromSave && !_isStructureGalleryWorld)
            {
                SyncCameraFromPlayer();
            }

            SyncCameraFromPlayer();

            _session.Player.Stats.RecordSessionStart();

            _loadingFromSave = false;
            _spawnWarmupRemaining = SpawnWarmupSeconds;
            _claimHintTimer = 0f;

            _session.BlockInteraction.BindAnimator(_session.InteractionAnimator);

            if (!fromSave && !_isStructureGalleryWorld)
            {
                _session.HudToast.Show(
                    "Founder's Hamlet is ready. Press V → PEOPLE tab to assign jobs to your 2 settlers.",
                    durationSeconds: 8f);
            }

            _input.IsMouseLocked = true;
            _input.ResetInactiveTimer();
            ApplyMouseCapture();
            TryActivateWindow();
            Window.Title = "Autonocraft | Playing";
            RestoreGameplayGraphics();
            TryStartAgentServer();

            Console.WriteLine("[Game] World ready — WASD move, mouse look, V village, Esc pause.");
            InputDebugTrace.Log($"ENTER_PLAYING warmup={SpawnWarmupSeconds}s active={IsActive} mouseLocked={_input.IsMouseLocked}");
        }

        private void PlacePlayerOnSurface()
        {
            _session.PlacePlayerOnSurface(_worldSpawnX, _worldSpawnZ);
            SyncCameraFromPlayer();
            Console.WriteLine($"[Spawn] Placed player on surface at ({_session.Player.Position.X:F1}, {_session.Player.Position.Y:F1}, {_session.Player.Position.Z:F1})");
        }

        private void OpenNewWorldSetup(MenuLayer backTarget)
        {
            _screens.MenuNav.NewWorldBackTarget = backTarget;
            _screens.NewWorldSetupScreen!.Reset();
            _screens.State = GameState.NewWorldSetup;
            Window.Title = "Autonocraft | New World";
        }

        private void OpenNewWorldSetup()
        {
            OpenNewWorldSetup(_screens.MenuNav.BaseLayer);
        }

        private void StartWorldLoading()
        {
            ApplyFastLoadingGraphics();
            Vector3 loadCenter = _loadingFromSave
                ? _camera.Position
                : new Vector3(_worldSpawnX + 0.5f, 64f, _worldSpawnZ + 0.5f);
            _screens.LoadingScreen!.Begin(loadCenter, _settings.RenderDistance, _pendingSaveData, _activeSlotName);
            _screens.State = GameState.WorldLoading;
            Window.Title = "Autonocraft | Loading World...";
        }

        private void ReturnToMainMenu(bool save = true)
        {
            CloseAllGameplayOverlays();
            if (save)
            {
                SaveWorld(sync: true);
            }
            _isStructureGalleryWorld = false;
            _screens.SnapOverlaysVisible();
            _screens.SaveSlotScreen!.RefreshSlots();
            _screens.MainMenuScreen!.RefreshContinueEligibility();
            _screens.MenuNav.ReturnToSaveBrowserFromGameplay();
            ReleaseMouseCapture();
            IsMouseVisible = true;
            _input.IsMouseLocked = false;
            _screens.State = GameState.MainMenu;
            UpdateMenuWindowTitle();
        }

        private void UpdateMenuWindowTitle()
        {
            Window.Title = _screens.MenuNav.Layer switch
            {
                MenuLayer.SettingsOverlay => "Autonocraft | Settings",
                MenuLayer.StatsOverlay => "Autonocraft | Player Stats",
                MenuLayer.SaveBrowser => "Autonocraft | Main Menu",
                _ => "Autonocraft | Main Menu"
            };
        }

        private void UpdateFrame(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            ProcessPendingAgentActions();

            _screens.HandleStateTransition(
                () => _input.ReleaseMouseOnLeavePlaying(),
                UpdateMusicForState);

            _audio?.Update(deltaTime, _screens.State, _session.Grid, _session.Player, _timeOfDay);
            _audio?.SetDucked(_screens.ShouldDuckAudio(_session.Crafting, _screens.State));

            _screens.UpdateFades(deltaTime);

            var kbState = Keyboard.GetState();
            var mouseState = GetUiMouseState();
            _input.BeginFrame();
            _input.UpdateFocusAndInactiveTimer(IsActive, deltaTime, _screens.State, InputBlockers);

            switch (_screens.State)
            {
                case GameState.MainMenu:
                    EnsureCursorFreeOutsideGameplay();
                    UpdateMainMenu(kbState, mouseState, deltaTime);
                    break;
                case GameState.NewWorldSetup:
                    EnsureCursorFreeOutsideGameplay();
                    UpdateNewWorldSetup(kbState, mouseState, deltaTime);
                    break;
                case GameState.WorldLoading:
                    EnsureCursorFreeOutsideGameplay();
                    UpdateWorldLoading(deltaTime, kbState, mouseState);
                    break;
                case GameState.Playing:
                    UpdateGameplay(deltaTime, kbState, mouseState);
                    break;
            }

            _input.EndFrame(kbState, mouseState);

            base.Update(gameTime);
        }

        private void UpdateMainMenu(KeyboardState kbState, MouseState mouseState, float deltaTime)
        {
            if (_screens.MenuNav.Layer == MenuLayer.StatsOverlay)
            {
                _screens.PlayerDashboardScreen!.Update(
                    GraphicsDevice.Viewport,
                    kbState,
                    _input.PrevKeyboard,
                    mouseState,
                    _input.PrevMouse,
                    deltaTime);

                if (_screens.PlayerDashboardScreen.CloseRequested)
                {
                    _screens.PlayerDashboardScreen.Close();
                    _screens.MenuNav.CloseOverlay();
                }

                UpdateMenuWindowTitle();
                return;
            }

            if (_screens.MenuNav.Layer == MenuLayer.SettingsOverlay)
            {
                _screens.MainMenuSettingsScreen!.Update(
                    GraphicsDevice.Viewport,
                    kbState,
                    _input.PrevKeyboard,
                    mouseState,
                    _input.PrevMouse,
                    deltaTime);

                if (_screens.MainMenuSettingsScreen.SaveRequested)
                {
                    ApplyGameSettings(_screens.MainMenuSettingsScreen.GetWorkingCopy());
                    _screens.MainMenuSettingsScreen.Close();
                    _screens.MenuNav.CloseOverlay();
                }
                else if (_screens.MainMenuSettingsScreen.CancelRequested)
                {
                    _screens.MainMenuSettingsScreen.Close();
                    _screens.MenuNav.CloseOverlay();
                }

                UpdateMenuWindowTitle();
                return;
            }

            if (_screens.MenuNav.Layer == MenuLayer.RootHub)
            {
                UpdateRootHub(kbState, mouseState, deltaTime);
                return;
            }

            UpdateSaveBrowser(kbState, mouseState, deltaTime);
        }

        private void UpdateRootHub(KeyboardState kbState, MouseState mouseState, float deltaTime)
        {
            _screens.MainMenuScreen!.Update(
                GraphicsDevice.Viewport,
                kbState,
                mouseState,
                _input.PrevKeyboard,
                _input.PrevMouse,
                deltaTime);

            if (_screens.MainMenuScreen.ContinueRequested && _screens.MainMenuScreen.ContinueSlotId != null)
            {
                TryStartLoadedWorld(_screens.MainMenuScreen.ContinueSlotId);
            }
            else if (_screens.MainMenuScreen.BrowseSavesRequested)
            {
                _screens.MenuNav.NavigateTo(MenuLayer.SaveBrowser);
            }
            else if (_screens.MainMenuScreen.NewWorldRequested)
            {
                OpenNewWorldSetup(MenuLayer.RootHub);
            }
            else if (_screens.MainMenuScreen.SettingsRequested)
            {
                OpenMainMenuSettings();
            }
            else if (_screens.MainMenuScreen.StructureGalleryRequested)
            {
                StartStructureGalleryWorld();
            }
            else if (_screens.MainMenuScreen.QuitRequested)
            {
                Exit();
            }

            UpdateMenuWindowTitle();
        }

        private void UpdateSaveBrowser(KeyboardState kbState, MouseState mouseState, float deltaTime)
        {
            _screens.SaveSlotScreen!.Update(GraphicsDevice.Viewport, kbState, mouseState, _input.PrevKeyboard, _input.PrevMouse, deltaTime);

            if (_screens.SaveSlotScreen.BackRequested)
            {
                _screens.MenuNav.NavigateTo(MenuLayer.RootHub);
                _screens.MainMenuScreen!.RefreshContinueEligibility();
            }
            else if (_screens.SaveSlotScreen.NewWorldRequested)
            {
                OpenNewWorldSetup(MenuLayer.SaveBrowser);
            }
            else if (_screens.SaveSlotScreen.StructureGalleryRequested)
            {
                StartStructureGalleryWorld();
            }
            else if (_screens.SaveSlotScreen.LoadRequested && _screens.SaveSlotScreen.SelectedSlotId != null)
            {
                TryStartLoadedWorld(_screens.SaveSlotScreen.SelectedSlotId);
            }
            else if (_screens.SaveSlotScreen.SettingsRequested)
            {
                OpenMainMenuSettings();
            }
            else if (_screens.SaveSlotScreen.StatsRequested)
            {
                OpenPlayerDashboard();
            }
            else if (_screens.SaveSlotScreen.QuitRequested)
            {
                Exit();
            }

            UpdateMenuWindowTitle();
        }

        private void OpenMainMenuSettings()
        {
            _screens.MainMenuSettingsScreen!.Open(_settings);
            _screens.MenuNav.OpenOverlay(MenuLayer.SettingsOverlay);
        }

        private void OpenPlayerDashboard()
        {
            _screens.PlayerDashboardScreen!.Open(
                _screens.SaveSlotScreen!.GetSelectedSlotId(),
                _screens.SaveSlotScreen.GetSelectedSlotName());
            _screens.MenuNav.OpenOverlay(MenuLayer.StatsOverlay);
        }

        private void ApplyGameSettings(GameSettings settings)
        {
            int previousRenderDistance = _settings.RenderDistance;
            _settings.RenderDistance = settings.RenderDistance;
            _settings.PlayWithAi = settings.PlayWithAi;
            _settings.AiProvider = settings.AiProvider;
            _settings.OpenRouterModel = settings.OpenRouterModel;
            _settings.OpenRouterApiKey = settings.OpenRouterApiKey;
            _settings.LlamaCppBaseUrl = settings.LlamaCppBaseUrl;
            _settings.LlamaCppModel = settings.LlamaCppModel;
            _settings.MasterVolume = settings.MasterVolume;
            _settings.SfxVolume = settings.SfxVolume;
            _settings.AmbientVolume = settings.AmbientVolume;
            _settings.MusicVolume = settings.MusicVolume;
            _settings.MuteAudio = settings.MuteAudio;
            _settings.VSync = settings.VSync;
            _settings.HighQualityLighting = settings.HighQualityLighting;
            _settings.Clamp();
            GameSettingsManager.Save(_settings);
            _audio?.ApplySettings(_settings);
            ApplyGraphicsSettings();

            if (_settings.RenderDistance != previousRenderDistance)
            {
                SetRenderDistance(_settings.RenderDistance);
            }
            else
            {
                _screens.PauseMenu?.SetRenderDistance(_settings.RenderDistance);
            }

            _screens.PauseMenu?.ApplyGraphicsSettings(_settings);
            RecreateVillageAi();
        }

        private void UpdateMusicForState(GameState state)
        {
            if (_audio == null)
            {
                return;
            }

            switch (state)
            {
                case GameState.MainMenu:
                case GameState.NewWorldSetup:
                    _audio.SetMusicState(MusicState.Menu);
                    break;
                case GameState.WorldLoading:
                case GameState.Playing:
                    _audio.SetMusicState(MusicState.Gameplay);
                    break;
            }
        }

        private void PlayUiClick() => _audio?.PlaySfx(SfxKind.UiClick);

        private void RecreateVillageAi()
        {
            if (_runTests || _ui == null)
            {
                return;
            }

            _session.RefreshVillageAi(_settings);
            _villageAiOrchestrator = _session.VillageAi;
            _screens.RecreateVillageChatScreen(_ui, _session.VillageAi);
        }

        private void UpdateNewWorldSetup(KeyboardState kbState, MouseState mouseState, float deltaTime)
        {
            _screens.NewWorldSetupScreen!.Update(GraphicsDevice.Viewport, kbState, mouseState, _input.PrevKeyboard, _input.PrevMouse, deltaTime);

            if (_screens.NewWorldSetupScreen.CreateRequested)
            {
                StartNewWorld(_screens.NewWorldSetupScreen.SelectedSeed, _screens.NewWorldSetupScreen.SelectedWorldType);
            }
            else if (_screens.NewWorldSetupScreen.BackRequested)
            {
                _screens.State = GameState.MainMenu;
                _screens.MenuNav.NavigateTo(_screens.MenuNav.NewWorldBackTarget);
                UpdateMenuWindowTitle();
            }
        }

        private void UpdateWorldLoading(float deltaTime, KeyboardState kbState, MouseState mouseState)
        {
            _screens.LoadingScreen!.Update(deltaTime);

            if (_screens.LoadingScreen.HasTimedOut)
            {
                Console.WriteLine($"[Load] World loading timed out: {_screens.LoadingScreen.TimeoutReason}");
                _screens.SaveSlotScreen?.SetLoadError(_screens.LoadingScreen.TimeoutReason ?? "World failed to load.");
                _screens.MenuNav.ReturnToSaveBrowserFromGameplay();
                ReturnToMainMenu(save: false);
                return;
            }

            if (_screens.LoadingScreen.IsComplete)
            {
                EnterPlaying();
            }
            else
            {
                Window.Title = $"Autonocraft | Loading World... {(_screens.LoadingScreen.Progress * 100f):F0}%";
            }
        }

        private void TryStartAgentServer()
        {
            if (_agentServerStarted)
            {
                return;
            }

            int[] ports = [_agentPort, 5001, 8765, 5010];
            foreach (int port in ports)
            {
                if (_agentServerStarted)
                {
                    break;
                }

                try
                {
                    AgentHttpServer.Start(this, port);
                    _agentPort = port;
                    _agentServerStarted = true;
                    if (!RuntimeMetrics.FileLoggingEnabled)
                    {
                        RuntimeMetrics.EnableFileLogging(fromCli: false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Agent] HTTP server failed on port {port}: {ex.Message}");
                }
            }

            if (!_agentServerStarted)
            {
                Console.WriteLine("[Agent] HTTP server unavailable — in-game controls still work.");
            }
        }

        private void TickSpawnWarmupAndVillageSystems(float deltaTime)
        {
            bool inSpawnWarmup = _spawnWarmupRemaining > 0f;
            if (inSpawnWarmup)
            {
                _spawnWarmupRemaining -= deltaTime;
            }

            float warmup = SpawnWarmupProgress;
            int targetTerrainPerFrame = VoxelWorld.GetRuntimeTerrainChunksPerFrame(_settings.RenderDistance);
            int targetMeshPerFrame = VoxelWorld.GetRuntimeMeshChunksPerFrame(_settings.RenderDistance);
            var streamProfile = ChunkStreamingProfile.FromMovement(
                _session.Player.Position,
                _session.Player.Velocity,
                _session.Player.CreativeMode);
            if (streamProfile.FastTravel)
            {
                targetTerrainPerFrame = Math.Min(targetTerrainPerFrame + 4, 14);
                targetMeshPerFrame = Math.Min(targetMeshPerFrame + 4, 14);
            }

            int terrainPerFrame = inSpawnWarmup
                ? Math.Max(1, (int)MathF.Round(1f + (targetTerrainPerFrame - 1f) * warmup))
                : targetTerrainPerFrame;
            int meshPerFrame = inSpawnWarmup
                ? Math.Max(1, (int)MathF.Round(1f + (targetMeshPerFrame - 1f) * warmup))
                : targetMeshPerFrame;

            _session.DeferAmbientSpawns = inSpawnWarmup && warmup < 0.7f;
            _session.Grid.GameplayMeshThrottle = inSpawnWarmup ? warmup : 1f;

            var swChunks = System.Diagnostics.Stopwatch.StartNew();
            _session.UpdateChunks(GraphicsDevice, _camera.Position, _settings.RenderDistance, terrainPerFrame, meshPerFrame);
            swChunks.Stop();
            PerfCounters.UpdateChunksMs = (float)swChunks.Elapsed.TotalMilliseconds;

            var swAnimals = System.Diagnostics.Stopwatch.StartNew();
            if (!inSpawnWarmup || warmup >= 0.75f)
            {
                _session.UpdateAnimals(deltaTime);
            }
            _session.UpdateItemDrops(deltaTime);
            swAnimals.Stop();
            PerfCounters.UpdateAnimalsMs = (float)swAnimals.Elapsed.TotalMilliseconds;

            var swVillages = System.Diagnostics.Stopwatch.StartNew();
            if (!inSpawnWarmup || warmup >= 0.6f)
            {
                _session.UpdateVillages(deltaTime, _timeOfDay);
                if (!_isStructureGalleryWorld)
                {
                    _session.UpdateSurvival(deltaTime, _timeOfDay, inSpawnWarmup);
                    _session.UpdateEarlyGuide(deltaTime, _timeOfDay, _screens.VillageScreen?.IsOpen == true);

                    _claimHintTimer += deltaTime;
                    if (_claimHintTimer >= 30f)
                    {
                        _claimHintTimer = 0f;
                        var swClaim = System.Diagnostics.Stopwatch.StartNew();
                        _session.UpdateNearbyClaimHint();
                        swClaim.Stop();
                        if (swClaim.Elapsed.TotalMilliseconds > 50f)
                        {
                            Console.WriteLine(
                                $"[Perf] Slow claim scan {swClaim.Elapsed.TotalMilliseconds:F0}ms managedMb={GC.GetTotalMemory(false) / (1024 * 1024):F0}");
                        }
                    }

                    _session.UpdateVillageHudHint(_session.Player.CreativeMode);
                }
            }
            swVillages.Stop();
            PerfCounters.UpdateVillagesMs = (float)swVillages.Elapsed.TotalMilliseconds;
        }
    }
}

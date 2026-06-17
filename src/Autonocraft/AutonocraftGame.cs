using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Diagnostics;
using Autonocraft.Domain.Core;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.Engine.Audio;
using Autonocraft.Entities;
using Autonocraft.World;
using Autonocraft.UI;
using Autonocraft.Crafting;
using Autonocraft.Ai;
using Autonocraft.Village;
using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.Core
{
    public partial class AutonocraftGame : Game, IGameAgentBridge
    {
        private readonly GraphicsDeviceManager? _graphics;
        private readonly Camera _camera;
        private readonly GameSession _session;
        private readonly GameHostContext _hostContext;

        private readonly InputManager _input;
        private readonly ScreenManager _screens;
        private readonly BlueprintPlacementSystem _blueprints;

        private Texture2D? _atlasTexture;
        private Texture2D? _whiteTexture;
        private Renderer? _renderer;
        private BlockTerrainEffect? _blockTerrainEffect;
        private SkyEffect? _skyEffect;
        private UiRenderer? _ui;
        private VillageAiOrchestrator? _villageAiOrchestrator;
        private AudioManager? _audio;

        private int? _renderDistanceOverride;
        private readonly GameSettings _settings;
        private const int DefaultSeed = WorldConstants.DefaultSeed;
        private float _timeOfDay = 0.15f;
        private float _timeScale = DayNightCycle.DefaultTimeScale;
        private float _waterAnimTime;
        private bool _playerWasInWater;
        private bool _playerWasInLava;
        private float _underwaterBubbleTimer;
        private float _titleUpdateTimer;
        private bool _timePaused;

        private const float MaxGameplayDeltaTime = 1f / 30f;
        private float _lastDeltaTime;

        private readonly ConcurrentQueue<Action> _pendingActions = new ConcurrentQueue<Action>();
        private readonly object _screenshotGate = new();
        private TaskCompletionSource<byte[]>? _screenshotTcs;
        private string? _screenshotSavePath;
        private readonly bool _runTests;
        private readonly bool _structureGalleryCli;

        public GameSession Session => _session;
        public Camera Camera => _camera;
        public VoxelWorld Grid => _session.Grid;
        public Player Player => _session.Player;
        public AnimalManager Animals => _session.Animals;
        public HashSet<Key> SimulatedKeys => _simulatedKeys;
        public GameState CurrentGameState => _screens.State;
        public ConcurrentQueue<Action> PendingActions => _pendingActions;

        public void ReleaseSimulatedKeys() => _simulatedKeys.Clear();
        public float TimeOfDay => _timeOfDay;
        public float WaterAnimTime => _waterAnimTime;
        public float TimeScale
        {
            get => _timeScale;
            set
            {
                _timeScale = Math.Max(0f, value);
                _hostContext.TimeScale = _timeScale;
            }
        }
        public bool TimePaused
        {
            get => _timePaused;
            set
            {
                _timePaused = value;
                _hostContext.TimePaused = _timePaused;
            }
        }
        public float MoveSpeedOverride
        {
            set => _session.Player.CustomMoveSpeed = value;
        }

        public int RenderDistance => _settings.RenderDistance;
        public BlockInteractionSystem BlockInteraction => _session.BlockInteraction;
        public InteractionAnimator InteractionAnimator => _session.InteractionAnimator;
        public ParticleSystem Particles => _session.Particles;
        public CombatSystem Combat => _session.Combat;
        public CraftingSystem Crafting => _session.Crafting;
        public UiTransition ScreenFade => _screens.ScreenFade;
        public UiTransition PauseFade => _screens.PauseFade;
        public UiTransition DeathFade => _screens.DeathFade;

        public GameHostContext Host => _hostContext;

        public void EnqueueAction(Action action, bool runImmediatelyInTests)
        {
            if (_runTests) action();
            else _pendingActions.Enqueue(action);
        }

        public Task<byte[]> RequestScreenshotAsync(string? savePath = null)
        {
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            EnqueueAction(() =>
            {
                lock (_screenshotGate)
                {
                    if (_screenshotTcs != null)
                    {
                        tcs.TrySetException(new InvalidOperationException("Another screenshot capture is already pending."));
                        return;
                    }

                    _screenshotTcs = tcs;
                    _screenshotSavePath = savePath;
                }
            }, runImmediatelyInTests: false);

            return tcs.Task;
        }

        private void ProcessPendingAgentActions()
        {
            while (_pendingActions.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Agent Action Error] {ex.Message}");
                }
            }
        }

        private void FlushPendingScreenshot()
        {
            TaskCompletionSource<byte[]>? tcs;
            string? savePath;
            lock (_screenshotGate)
            {
                tcs = _screenshotTcs;
                savePath = _screenshotSavePath;
                _screenshotTcs = null;
                _screenshotSavePath = null;
            }

            if (tcs == null || _graphics == null)
            {
                return;
            }

            try
            {
                byte[] png = Agent.Handlers.ScreenshotCapture.CapturePng(GraphicsDevice);
                if (!string.IsNullOrWhiteSpace(savePath))
                {
                    string? directory = Path.GetDirectoryName(savePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllBytes(savePath, png);
                }

                tcs.TrySetResult(png);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        public void SyncTimeFromHost()
        {
            _timeOfDay = _hostContext.TimeOfDay;
            _timeScale = _hostContext.TimeScale;
            _timePaused = _hostContext.TimePaused;
        }

        public void SyncCameraFromPlayer() => SyncCameraFromPlayerInternal();

        public void RequestExit() => Exit();

        public void RequestOpenVillageUi()
        {
            if (_screens.State != GameState.Playing)
            {
                return;
            }

            OpenVillageUi();
        }

        public void RequestCloseVillageUi()
        {
            if (_screens.VillageScreen?.IsOpen == true)
            {
                CloseVillageUi();
                return;
            }

            if (_screens.VillageChatScreen?.IsOpen == true)
            {
                _screens.VillageChatScreen.Close();
                _input.IsMouseLocked = _input.MouseLockedBeforeVillageUi;
                RestoreMouseLockAfterOverlay();
            }
        }

        public void SetTimeOfDay(float value)
        {
            _hostContext.SetTimeOfDay(value);
            SyncTimeFromHost();
        }

        public void SetTimeScale(float scale)
        {
            _timeScale = Math.Max(0f, scale);
            _timePaused = scale <= 0f;
            _hostContext.TimeScale = _timeScale;
            _hostContext.TimePaused = _timePaused;
        }

        public bool IsStructureGalleryWorld => _isStructureGalleryWorld;

        public WorldType CurrentWorldType => _session.Grid.GenerationParams.WorldType;

        public void RequestLoadStructureGallery()
        {
            if (_screens.State == GameState.WorldLoading)
            {
                return;
            }

            CloseAllGameplayOverlays();
            ReleaseMouseCapture();
            IsMouseVisible = true;
            _input.IsMouseLocked = false;
            StartStructureGalleryWorld();
        }

        public void SetRenderDistance(int value)
        {
            _settings.RenderDistance = Math.Clamp(value, GameSettings.MinRenderDistance, GameSettings.MaxRenderDistance);
            GameSettingsManager.Save(_settings);
            _screens.PauseMenu?.SetRenderDistance(_settings.RenderDistance);
            _session.Grid.UpdateChunksAround(GraphicsDevice, _camera.Position, _settings.RenderDistance);
        }

        private void ApplyMuteAudio(bool mute)
        {
            _settings.MuteAudio = mute;
            GameSettingsManager.Save(_settings);
            _audio?.ApplySettings(_settings);
            _screens.PauseMenu?.ApplyAudioSettings(_settings);
        }

        public string ExecuteDevCommand(string input) => DevCommands.DevCommandRouter.Execute(_hostContext, input);

        public const int DefaultSpawnX = GameConstants.DefaultSpawnX;
        public const int DefaultSpawnZ = GameConstants.DefaultSpawnZ;

        public AutonocraftGame(bool runTests = false, bool skipMenu = false, bool structureGallery = false, int agentPort = 5001, bool debugMetrics = false, int? renderDistanceOverride = null)
        {
            _input = new InputManager(this);
            _screens = new ScreenManager();
            _runTests = runTests;
            _skipMenu = skipMenu;
            _structureGalleryCli = structureGallery;
            _agentPort = agentPort;
            _renderDistanceOverride = renderDistanceOverride;
            if (debugMetrics && !RuntimeMetrics.FileLoggingEnabled)
            {
                RuntimeMetrics.EnableFileLogging(fromCli: true);
            }
            if (_skipMenu || _structureGalleryCli)
            {
                _screens.State = GameState.WorldLoading;
            }

            if (!_runTests)
            {
                _graphics = new GraphicsDeviceManager(this);
            }

            _settings = GameSettingsManager.Load();
            if (_renderDistanceOverride.HasValue)
            {
                _settings.RenderDistance = Math.Clamp(
                    _renderDistanceOverride.Value,
                    GameSettings.MinRenderDistance,
                    GameSettings.MaxRenderDistance);
            }
            else if ((_skipMenu || _structureGalleryCli) && IsCiEnvironment())
            {
                _settings.RenderDistance = Math.Min(_settings.RenderDistance, 4);
            }

            if (!_runTests)
            {
                Console.WriteLine($"[Settings] Render distance: {_settings.RenderDistance} chunks");
            }

            _camera = new Camera();
            _session = new GameSession(DefaultSeed);
            _blueprints = new BlueprintPlacementSystem(
                _session,
                _camera,
                WrapPlayerHotbar,
                msg => _session.HudToast.Show(msg));
            _hostContext = new GameHostContext(_session, _settings.RenderDistance, _settings)
            {
                SetMoveSpeedOverride = speed => _session.Player.CustomMoveSpeed = speed
            };
            _hostContext.TimeOfDay = _timeOfDay;
            _hostContext.TimeScale = _timeScale;
            _hostContext.TimePaused = _timePaused;
            _session.WireWorldEvents();
            SyncCameraFromPlayer();

            Exiting += OnGameExiting;
        }

        private void SyncCameraFromPlayerInternal()
        {
            float targetFov = 45f;
            if (_session.Player.IsSprinting)
            {
                targetFov = 55f;
            }
            _camera.CurrentFov = _camera.CurrentFov + (targetFov - _camera.CurrentFov) * (1f - MathF.Exp(-8f * _lastDeltaTime));

            Vector3 vel = _session.Player.Velocity;
            float horizontalSpeed = new Vector3(vel.X, 0f, vel.Z).Length();
            bool isBobbingActive = _session.Player.IsGrounded && horizontalSpeed > 0.1f && !_session.Player.CreativeMode && !_session.Player.InWater;

            if (isBobbingActive)
            {
                _bobbingPhase += horizontalSpeed * _lastDeltaTime * 2.8f;
                float bobY = MathF.Abs(MathF.Sin(_bobbingPhase)) * 0.02f * (horizontalSpeed / Player.WalkSpeed);
                float bobRoll = MathF.Sin(_bobbingPhase) * 0.5f * (horizontalSpeed / Player.WalkSpeed);
                _bobbingOffset = MathF.Min(bobY, 0.1f);
                _bobbingRoll = bobRoll;
            }
            else
            {
                _bobbingOffset = _bobbingOffset + (0f - _bobbingOffset) * (1f - MathF.Exp(-8f * _lastDeltaTime));
                _bobbingRoll = _bobbingRoll + (0f - _bobbingRoll) * (1f - MathF.Exp(-8f * _lastDeltaTime));
            }

            _camera.Position = _session.Player.Position + new Vector3(0f, Player.EyeHeight - _bobbingOffset, 0f);
            _camera.Yaw = _session.Player.Yaw;
            _camera.Pitch = _session.Player.Pitch;
            _camera.Roll = _bobbingRoll;
            _camera.ViewPositionOffset = _session.InteractionAnimator.PositionOffset;
            _camera.ViewPitchOffset = -_session.InteractionAnimator.PitchRecoil;
        }

        private float _bobbingPhase;
        private float _bobbingOffset;
        private float _bobbingRoll;

        protected override void Initialize()
        {
            if (_runTests)
            {
                return;
            }

            _graphics!.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            ConfigureAgentSessionGraphics();
            _graphics.SynchronizeWithVerticalRetrace = _settings.VSync;
            _graphics.ApplyChanges();

            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += OnWindowClientSizeChanged;

            _input.InitializeAtStartup();

            base.Initialize();
        }

        private InputManager.GameplayInputBlockers InputBlockers =>
            _screens.GetInputBlockers(_session.Crafting);

        private bool ShouldCaptureMouse() =>
            _input.ShouldCaptureMouse(_screens.State, InputBlockers);

        private void PrepareMouseForUi() => _input.PrepareMouseForUi();

        private void EnsureCursorFreeOutsideGameplay() =>
            _input.EnsureCursorFreeOutsideGameplay(_screens.State, InputBlockers);

        private MouseState GetUiMouseState() => _input.GetUiMouseState();

        private bool HasBlockingGameplayOverlay() =>
            _screens.HasBlockingGameplayOverlay(_session.Crafting);

        private void EnsureUiPointerMode() => _input.EnsureUiPointerMode();

        private void TryActivateWindow() => _input.TryActivateWindow();

        private void CloseAllGameplayOverlays() =>
            _screens.CloseAllGameplayOverlays(_session.Crafting);

        private void ApplyMouseCapture() => _input.ApplyMouseCapture();

        private void ReleaseMouseCapture() => _input.ReleaseMouseCapture();

        private void RestoreMouseLockAfterOverlay() => _input.RestoreMouseLockAfterOverlay();

        private bool CanProcessGameplayMouse() =>
            _input.CanProcessGameplayMouse(_screens.State, InputBlockers);

        private void EnsureMouseLockedForGameplay() => _input.EnsureMouseLockedForGameplay();

        private void OnWindowClientSizeChanged(object? sender, EventArgs e)
        {
            if (_graphics == null)
            {
                return;
            }

            int width = Math.Max(640, Window.ClientBounds.Width);
            int height = Math.Max(360, Window.ClientBounds.Height);

            if (width == _graphics.PreferredBackBufferWidth &&
                height == _graphics.PreferredBackBufferHeight)
            {
                return;
            }

            _graphics.PreferredBackBufferWidth = width;
            _graphics.PreferredBackBufferHeight = height;
            _graphics.ApplyChanges();
        }

        protected override void LoadContent()
        {
            if (_runTests) return;

            _whiteTexture = new Texture2D(GraphicsDevice, 1, 1);
            _whiteTexture.SetData(new[] { Color.White });

            RegenerateAtlasTexture(_session.Grid.Seed);
            var blockTerrainEffect = new BlockTerrainEffect(GraphicsDevice, _atlasTexture!, _settings.HighQualityLighting);
            _blockTerrainEffect = blockTerrainEffect;
            _skyEffect = SkyEffect.Create(GraphicsDevice);

            _renderer = new Renderer(GraphicsDevice, _atlasTexture!, _whiteTexture, blockTerrainEffect, _skyEffect, _settings.HighQualityLighting);
            _renderer.SetPreferPerPixelLighting(_settings.HighQualityLighting);
            BlockAtlas.UseCpuBlockVariation = true;
            var typography = new UiTypography(GraphicsDevice);
            _renderer.Hud.SetTypography(typography);
            _ui = new UiRenderer(GraphicsDevice, _whiteTexture, typography);
            _screens.Initialize(
                GraphicsDevice,
                _ui,
                _session,
                _settings,
                SetRenderDistance,
                ApplyMuteAudio,
                ApplyVSync,
                ApplyHighQualityLighting);
            _villageAiOrchestrator = _session.VillageAi;

            try
            {
                _audio = new AudioManager(enabled: !_runTests);
                _audio.Initialize();
                _audio.ApplySettings(_settings);
                _session.BindAudio(_audio);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Audio] Initialization failed: {ex.Message}");
                _audio = new AudioManager(enabled: false);
                _session.BindAudio(null);
            }

            UpdateMusicForState(_screens.State);

            if (_structureGalleryCli)
            {
                StartStructureGalleryWorld();
            }
            else if (_skipMenu)
            {
                _needsStarterSettlement = true;
                StartWorldLoading();
            }

            _input.SeedInitialInputState();
            _screens.BeginInitialFadeIn();
        }

        private void OnGameExiting(object? sender, EventArgs e)
        {
            ReleaseMouseCapture();
            IsMouseVisible = true;
            PerformExitSave();
            _audio?.Dispose();
        }

        protected override void Update(GameTime gameTime)
        {
            if (_runTests)
            {
                base.Update(gameTime);
                return;
            }

            try
            {
                UpdateFrame(gameTime);
            }
            catch (Exception ex)
            {
                RuntimeMetrics.RecordManagedException("Update", ex);
                InputDebugTrace.LogException("Update", ex);
                Console.WriteLine($"[Game] Update fault: {ex}");
            }
        }

        private void UpdateGameplay(float deltaTime, KeyboardState kbState, MouseState mouseState)
        {
            deltaTime = Math.Min(deltaTime, MaxGameplayDeltaTime);
            _lastDeltaTime = deltaTime;

            var updateStopwatch = Stopwatch.StartNew();
            try
            {
                UpdateGameplayCore(deltaTime, kbState, mouseState);
            }
            catch (Exception ex)
            {
                RuntimeMetrics.RecordManagedException("UpdateGameplay", ex);
                InputDebugTrace.LogException("UpdateGameplay", ex);
                Console.WriteLine($"[Game] UpdateGameplay fault: {ex}");
            }
            finally
            {
                updateStopwatch.Stop();
                float updateMs = (float)updateStopwatch.Elapsed.TotalMilliseconds;
                PerfCounters.RecordUpdate(updateMs);
                if (updateMs > 500f)
                {
                    Console.WriteLine(
                        $"[Perf] Slow update {updateMs:F0}ms chunks={PerfCounters.UpdateChunksMs:F1} fluids={PerfCounters.UpdateFluidsMs:F1} villages={PerfCounters.UpdateVillagesMs:F1} pendingMesh={PerfCounters.PendingMeshCount} managedMb={GC.GetTotalMemory(false) / (1024 * 1024):F0}");
                }
            }
        }

        private void UpdateGameplayCore(float deltaTime, KeyboardState kbState, MouseState mouseState)
        {
            if (ProcessGameplayInput(
                    deltaTime,
                    kbState,
                    mouseState,
                    out _,
                    out _,
                    out _,
                    out _,
                    out _,
                    out float mouseDx,
                    out float mouseDy,
                    out int centerX,
                    out int centerY))
            {
                return;
            }

            if (!_timePaused)
            {
                _timeOfDay += deltaTime * _timeScale;
                _timeOfDay -= MathF.Floor(_timeOfDay);
            }

            _waterAnimTime += deltaTime;
            var swFluids = Stopwatch.StartNew();
            if (_spawnWarmupRemaining <= 0f || SpawnWarmupProgress >= 0.5f)
            {
                _session.Grid.Fluids.Update(_session.Grid, deltaTime, GraphicsDevice);
            }
            swFluids.Stop();
            PerfCounters.UpdateFluidsMs = (float)swFluids.Elapsed.TotalMilliseconds;

            if (!_session.Player.IsAlive)
            {
                if (_screens.DeathScreen is not { IsOpen: true })
                {
                    OpenDeathScreen();
                }
                return;
            }

            PerfCounters.ResetFrame();
            _session.HudToast.Update(deltaTime);

            TickSpawnWarmupAndVillageSystems(deltaTime);
            TickAutosave(deltaTime);

            _titleUpdateTimer += deltaTime;
            if (_titleUpdateTimer >= 0.5f)
            {
                _titleUpdateTimer = 0f;
                Window.Title = $"Autonocraft | FPS: {RuntimeMetrics.RollingFps:F0} | Draw: {PerfCounters.LastDrawMs:F1}ms | Update: {PerfCounters.LastUpdateMs:F1}ms | DC: {PerfCounters.TerrainDrawCalls} | Chunks: {_session.Grid.ActiveChunkCount} | Mesh: {PerfCounters.MeshBuildMs:F1}ms | Pending: {PerfCounters.PendingMeshCount}{(_session.Player.CreativeMode ? " | CREATIVE" : "")}";
            }

            string? overlayName = null;
            if (_screens.VillageScreen?.IsOpen == true) overlayName = "village";
            else if (_screens.PauseMenu?.IsOpen == true) overlayName = "pause";
            else if (_screens.DevConsole?.IsOpen == true) overlayName = "console";

            InputDebugTrace.TickGameplay(
                deltaTime,
                _screens.State,
                IsActive,
                _input.IsMouseLocked,
                ShouldCaptureMouse(),
                _input.SkipMouseLookFrame,
                mouseState.X,
                mouseState.Y,
                centerX,
                centerY,
                mouseDx,
                mouseDy,
                _camera.Yaw,
                _spawnWarmupRemaining,
                PerfCounters.PendingMeshCount,
                PerfCounters.MeshBuildMs,
                overlayName != null,
                overlayName);
        }

        private void ApplyGraphicsSettings()
        {
            if (_graphics != null)
            {
                _graphics.SynchronizeWithVerticalRetrace = _settings.VSync;
                _graphics.ApplyChanges();
            }

            _blockTerrainEffect?.SetPreferPerPixelLighting(_settings.HighQualityLighting);
            _renderer?.SetPreferPerPixelLighting(_settings.HighQualityLighting);
            _screens.PauseMenu?.ApplyGraphicsSettings(_settings);
        }

        private void ApplyVSync(bool enabled)
        {
            _settings.VSync = enabled;
            ApplyGraphicsSettings();
            GameSettingsManager.Save(_settings);
        }

        private void ApplyHighQualityLighting(bool enabled)
        {
            _settings.HighQualityLighting = enabled;
            ApplyGraphicsSettings();
            GameSettingsManager.Save(_settings);
        }

        private void RecordFrameMetrics(float deltaTime)
        {
            if (_screens.State != GameState.Playing)
            {
                return;
            }

            RuntimeMetrics.RecordFrame(
                deltaTime,
                _screens.State,
                _session.Grid.ActiveChunkCount,
                PerfCounters.PendingMeshCount,
                PerfCounters.MeshBuildMs,
                _spawnWarmupRemaining,
                PerfCounters.LastUpdateMs,
                PerfCounters.LastDrawMs,
                PerfCounters.TerrainDrawCalls);
        }

        public void SaveScreenshot(string path) =>
            Agent.Handlers.ScreenshotCapture.SavePng(GraphicsDevice, path);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ReleaseMouseCapture();
                IsMouseVisible = true;
                PerformExitSave();
                Exiting -= OnGameExiting;
                Window.ClientSizeChanged -= OnWindowClientSizeChanged;
                AgentHttpServer.Stop();
                _session.Grid.Dispose();
                _whiteTexture?.Dispose();
                _atlasTexture?.Dispose();
                _renderer?.Dispose();
                _skyEffect?.Dispose();
                _ui?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

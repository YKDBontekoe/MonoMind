using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.Engine.Audio;
using Autonocraft.Entities;
using Autonocraft.World;
using Autonocraft.UI;
using Autonocraft.Crafting;
using Autonocraft.Items;
using Autonocraft.Ai;
using Autonocraft.Village;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace Autonocraft.Core
{
    public class AutonocraftGame : Game, IGameAgentBridge
    {
        private readonly GraphicsDeviceManager? _graphics;
        private readonly Camera _camera;
        private readonly GameSession _session;
        private readonly GameHostContext _hostContext;

        private readonly InputManager _input;
        private readonly ScreenManager _screens;
        private readonly BlueprintPlacementSystem _blueprints;

        // Graphics resources
        private Texture2D? _atlasTexture;
        private Texture2D? _whiteTexture;
        private Renderer? _renderer;
        private BlockTerrainEffect? _blockTerrainEffect;
        private SkyEffect? _skyEffect;
        private UiRenderer? _ui;
        private VillageAiOrchestrator? _villageAiOrchestrator;
        private AudioManager? _audio;

        // Game flow
        private bool _skipMenu;
        private bool _agentServerStarted;
        private int _agentPort = 5001;
        private int? _renderDistanceOverride;
        private string? _activeSlotId;
        private string? _activeSlotName;
        private WorldSaveData? _pendingSaveData;
        private bool _loadingFromSave;
        private bool _needsStarterSettlement;
        private float _autosaveTimer;
        private bool _saveInProgress;
        private bool _exitSaveDone;
        private int _worldSpawnX = GameConstants.DefaultSpawnX;
        private int _worldSpawnZ = GameConstants.DefaultSpawnZ;

        // Time and cycle parameters
        private readonly GameSettings _settings;
        private const int DefaultSeed = WorldConstants.DefaultSeed;
        private float _timeOfDay = 0.3f;
        private float _timeScale = 0.01f;
        private float _waterAnimTime;
        private bool _playerWasInWater;
        private float _underwaterBubbleTimer;
        private float _titleUpdateTimer;
        private bool _timePaused;

        // Input state tracking
        private bool _prevSpacePressed = false;
        private bool _pendingAutoOpenPeopleTab;
        private string? _deathCauseText;
        private string? _deathPenaltyText;
        private float _spawnWarmupRemaining;
        private float _claimHintTimer = 10f;
        private const float MaxGameplayDeltaTime = 1f / 30f;
        private const float SpawnWarmupSeconds = 15f;

        private bool _prevQPressed;

        // Sprint and Bobbing state
        private bool _doubleTapSprintActive;
        private float _lastWPressTime;
        private bool _prevWPressed;
        private float _bobbingPhase;
        private float _bobbingOffset;
        private float _bobbingRoll;
        private float _lastDeltaTime;

        // AI Agent simulation state
        private readonly HashSet<Key> _simulatedKeys = new HashSet<Key>();
        private readonly ConcurrentQueue<Action> _pendingActions = new ConcurrentQueue<Action>();
        private readonly bool _runTests;

        // Properties for server/tests access
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

        public AutonocraftGame(bool runTests = false, bool skipMenu = false, int agentPort = 5001, bool debugMetrics = false, int? renderDistanceOverride = null)
        {
            _input = new InputManager(this);
            _screens = new ScreenManager();
            _runTests = runTests;
            _skipMenu = skipMenu;
            _agentPort = agentPort;
            _renderDistanceOverride = renderDistanceOverride;
            if (debugMetrics && !RuntimeMetrics.FileLoggingEnabled)
            {
                RuntimeMetrics.EnableFileLogging(fromCli: true);
            }
            if (_skipMenu)
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
            else if (_skipMenu && IsCiEnvironment())
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
            // Dynamic FOV interpolation
            float targetFov = 45f;
            if (_session.Player.IsSprinting)
            {
                targetFov = 55f; // widen FOV when sprinting
            }
            _camera.CurrentFov = _camera.CurrentFov + (targetFov - _camera.CurrentFov) * (1f - MathF.Exp(-8f * _lastDeltaTime));

            // View Bobbing
            Vector3 vel = _session.Player.Velocity;
            float horizontalSpeed = new Vector3(vel.X, 0f, vel.Z).Length();
            bool isBobbingActive = _session.Player.IsGrounded && horizontalSpeed > 0.1f && !_session.Player.CreativeMode && !_session.Player.InWater;

            if (isBobbingActive)
            {
                // Accumulate bobbing phase based on footstep rate
                _bobbingPhase += horizontalSpeed * _lastDeltaTime * 2.8f;

                // Y-bobbing uses double frequency (two steps per footstep cycle)
                float bobY = MathF.Abs(MathF.Sin(_bobbingPhase)) * 0.02f * (horizontalSpeed / Player.WalkSpeed);
                // Roll-bobbing tilt
                float bobRoll = MathF.Sin(_bobbingPhase) * 0.5f * (horizontalSpeed / Player.WalkSpeed);

                _bobbingOffset = MathF.Min(bobY, 0.1f);
                _bobbingRoll = bobRoll;
            }
            else
            {
                // Smoothly decay bobbing offsets
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

        private static bool IsCiEnvironment() =>
            string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), "true", StringComparison.OrdinalIgnoreCase);

        private bool _fastLoadingGraphicsApplied;

        private void ConfigureAgentSessionGraphics()
        {
            if (!_skipMenu || _graphics == null)
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

        private float SpawnWarmupProgress =>
            Math.Clamp(1f - (_spawnWarmupRemaining / SpawnWarmupSeconds), 0f, 1f);

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

            // Re-usable 1x1 white texture for SpriteBatch solid drawing
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

            if (_skipMenu)
            {
                _needsStarterSettlement = true;
                StartWorldLoading();
            }

            _input.SeedInitialInputState();
            _screens.BeginInitialFadeIn();
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
            if (_needsStarterSettlement)
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

            if (!fromSave)
            {
                _session.VillageHudHint = "V — Town board · Recruit villagers and queue buildings";
                _session.Crafting.ShowCraftingHint = false;
            }
            else
            {
                SyncCameraFromPlayer();
            }

            SyncCameraFromPlayer();

            _session.Player.Stats.RecordSessionStart();

            _loadingFromSave = false;
            _spawnWarmupRemaining = SpawnWarmupSeconds;

            _session.BlockInteraction.BindAnimator(_session.InteractionAnimator);

            if (!fromSave)
            {
                _session.HudToast.Show(
                    "Founder's Hamlet is ready. Press V → PEOPLE tab to assign jobs to your 2 settlers.",
                    durationSeconds: 8f);
                _pendingAutoOpenPeopleTab = true;
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

        private void StartNewWorld(int seed, WorldType worldType)
        {
            string slotName = WorldSaveManager.GenerateDefaultSlotName();
            string slotId = WorldSaveManager.CreateSlotId(slotName);

            _activeSlotId = slotId;
            _activeSlotName = slotName;
            _worldSpawnX = GameConstants.DefaultSpawnX;
            _worldSpawnZ = GameConstants.DefaultSpawnZ;
            _pendingSaveData = null;
            _loadingFromSave = false;
            _autosaveTimer = 0f;

            var genParams = WorldGenParams.ForType(worldType);
            ResetWorldState(seed, genParams);
            _session.ResetPlayer();
            _session.ResetCrafting();
            SyncCameraFromPlayer();
            SetTimeOfDay(0.3f);
            _timeScale = 0.01f;
            _timePaused = false;
            _hostContext.TimeScale = _timeScale;
            _hostContext.TimePaused = _timePaused;

            _needsStarterSettlement = true;
            StartWorldLoading();
        }

        private void OpenNewWorldSetup()
        {
            _screens.NewWorldSetupScreen!.Reset();
            _screens.State = GameState.NewWorldSetup;
            Window.Title = "Autonocraft | New World";
        }

        private bool TryStartLoadedWorld(string slotId)
        {
            try
            {
                var save = WorldSaveManager.Load(slotId);

                _activeSlotId = slotId;
                _activeSlotName = save.SlotName;
                _worldSpawnX = save.Spawn?.X ?? GameConstants.DefaultSpawnX;
                _worldSpawnZ = save.Spawn?.Z ?? GameConstants.DefaultSpawnZ;
                _pendingSaveData = save;
                _loadingFromSave = true;
                _autosaveTimer = 0f;

                ResetWorldState(save.Seed, WorldGenParams.ForType(WorldType.Default));
                _session.ResetPlayer();
                _session.ResetCrafting();
                _session.Crafting.LoadJournal(save.UnlockedCraftingIds);
                _session.Crafting.ShowCraftingHint = save.UnlockedCraftingIds == null || save.UnlockedCraftingIds.Count == 0;
                WorldSaveManager.ApplyPlayerSaveData(_session.Player, save.Player);
                SetTimeOfDay(save.Time.TimeOfDay);
                _timeScale = save.Time.TimeScale;
                _timePaused = save.Time.TimePaused;
                _hostContext.TimeScale = _timeScale;
                _hostContext.TimePaused = _timePaused;
                SyncCameraFromPlayer();
                _session.LoadVillageSave(
                    save.Villages,
                    save.Villagers,
                    save.ClaimedAnchors);

                StartWorldLoading();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Save] Failed to load slot '{slotId}': {ex.Message}");
                _screens.SaveSlotScreen?.SetLoadError($"Failed to load save: {ex.Message}");
                return false;
            }
        }

        private void RegenerateAtlasTexture(int seed)
        {
            if (GraphicsDevice == null)
            {
                return;
            }

            _atlasTexture?.Dispose();
            _atlasTexture = ProceduralAtlasBuilder.Generate(GraphicsDevice, seed);
            _blockTerrainEffect?.SetAtlas(_atlasTexture);
            _renderer?.SetAtlasTexture(_atlasTexture);
        }

        private void ResetWorldState(int seed, WorldGenParams? parameters = null)
        {
            _session.ReplaceWorld(seed, parameters);
            RegenerateAtlasTexture(seed);
            BlockAtlas.UseCpuBlockVariation = true;
            _session.WireWorldEvents();
            if (_ui != null)
            {
                _screens.RecreateLoadingScreen(GraphicsDevice, _ui, _session.Grid);
            }
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

        private void OnGameExiting(object? sender, EventArgs e)
        {
            ReleaseMouseCapture();
            IsMouseVisible = true;
            PerformExitSave();
            _audio?.Dispose();
        }

        private void PerformExitSave()
        {
            if (_exitSaveDone)
            {
                return;
            }

            _exitSaveDone = true;
            PerformAutosave(sync: true);
        }

        private void PerformAutosave(bool sync)
        {
            if (_screens.State != GameState.Playing)
            {
                return;
            }

            SaveWorld(sync);
        }

        private void SaveWorld(bool sync)
        {
            if (string.IsNullOrEmpty(_activeSlotId) || _saveInProgress)
            {
                return;
            }

            string slotId = _activeSlotId;
            string slotName = _activeSlotName ?? slotId;
            var snapshot = _session.BuildSaveSnapshot(slotId, slotName, _timeOfDay, _timeScale, _timePaused, _worldSpawnX, _worldSpawnZ);
            var saveData = WorldSaveManager.BuildFromSnapshot(snapshot);

            if (sync)
            {
                try
                {
                    WorldSaveManager.Save(saveData);
                    Console.WriteLine($"[Save] World saved to slot '{slotName}'.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Save] Save failed: {ex.Message}");
                    if (_screens.State == GameState.Playing)
                    {
                        _session.HudToast.Show("Save failed!", new Microsoft.Xna.Framework.Color(1f, 0.35f, 0.35f));
                    }
                }

                return;
            }

            _saveInProgress = true;
            Task.Run(() =>
            {
                try
                {
                    WorldSaveManager.Save(saveData);
                    Console.WriteLine($"[Save] Auto-saved world '{slotName}'.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Save] Auto-save failed: {ex.Message}");
                    _pendingActions.Enqueue(() =>
                        _session.HudToast.Show("Auto-save failed!", new Microsoft.Xna.Framework.Color(1f, 0.35f, 0.35f)));
                }
                finally
                {
                    _saveInProgress = false;
                }
            });
        }

        private void OpenPauseMenu()
        {
            _input.MouseLockedBeforePause = _input.IsMouseLocked;
            _screens.OpenPauseMenu(PrepareMouseForUi, title => Window.Title = title);
        }

        private void ClosePauseMenu()
        {
            _input.IsMouseLocked = _input.MouseLockedBeforePause;
            _screens.ClosePauseMenu(RestoreMouseLockAfterOverlay);
        }

        private void OpenDeathScreen()
        {
            var player = _session.Player;
            if (!player.DeathConsequencesApplied)
            {
                DeathConsequences.ApplyOnDeath(player);
                player.DeathConsequencesApplied = true;
            }

            _deathCauseText = FormatDeathCause(player.LastDeathCause);
            _deathPenaltyText = "You dropped some supplies.";
            player.Stats.RecordDeath();
            _input.MouseLockedBeforeDeath = _input.IsMouseLocked;
            _screens.OpenDeathScreen(_deathCauseText, _deathPenaltyText, () =>
            {
                _input.IsMouseLocked = false;
                ReleaseMouseCapture();
                IsMouseVisible = true;
            });
            Window.Title = "Autonocraft | You Died";
        }

        private void CloseDeathScreen()
        {
            _input.IsMouseLocked = _input.MouseLockedBeforeDeath;
            _screens.CloseDeathScreen(RestoreMouseLockAfterOverlay);
        }

        private void ReturnToMainMenu()
        {
            CloseAllGameplayOverlays();
            SaveWorld(sync: true);
            _screens.SnapOverlaysVisible();
            _screens.SaveSlotScreen!.RefreshSlots();
            ReleaseMouseCapture();
            IsMouseVisible = true;
            _input.IsMouseLocked = false;
            _screens.State = GameState.MainMenu;
            Window.Title = "Autonocraft | Main Menu";
        }

        private void QuitFromPauseMenu()
        {
            SaveWorld(sync: true);
            Exit();
        }

        private bool IsKeyPressed(KeyboardState kbState, Key key)
        {
            if (_simulatedKeys.Contains(key)) return true;
            var monoKey = MapKey(key);
            return monoKey.HasValue && kbState.IsKeyDown(monoKey.Value);
        }

        private Microsoft.Xna.Framework.Input.Keys? MapKey(Key key)
        {
            return key switch
            {
                Key.W => Microsoft.Xna.Framework.Input.Keys.W,
                Key.S => Microsoft.Xna.Framework.Input.Keys.S,
                Key.A => Microsoft.Xna.Framework.Input.Keys.A,
                Key.D => Microsoft.Xna.Framework.Input.Keys.D,
                Key.Space => Microsoft.Xna.Framework.Input.Keys.Space,
                Key.ShiftLeft => Microsoft.Xna.Framework.Input.Keys.LeftShift,
                Key.ShiftRight => Microsoft.Xna.Framework.Input.Keys.RightShift,
                Key.ControlLeft => Microsoft.Xna.Framework.Input.Keys.LeftControl,
                Key.Q => Microsoft.Xna.Framework.Input.Keys.Q,
                Key.G => Microsoft.Xna.Framework.Input.Keys.G,
                Key.Escape => Microsoft.Xna.Framework.Input.Keys.Escape,
                Key.Number1 => Microsoft.Xna.Framework.Input.Keys.D1,
                Key.Number2 => Microsoft.Xna.Framework.Input.Keys.D2,
                Key.Number3 => Microsoft.Xna.Framework.Input.Keys.D3,
                Key.Number4 => Microsoft.Xna.Framework.Input.Keys.D4,
                Key.Number5 => Microsoft.Xna.Framework.Input.Keys.D5,
                Key.Number6 => Microsoft.Xna.Framework.Input.Keys.D6,
                Key.Number7 => Microsoft.Xna.Framework.Input.Keys.D7,
                Key.Number8 => Microsoft.Xna.Framework.Input.Keys.D8,
                Key.Number9 => Microsoft.Xna.Framework.Input.Keys.D9,
                Key.Keypad1 => Microsoft.Xna.Framework.Input.Keys.NumPad1,
                Key.Keypad2 => Microsoft.Xna.Framework.Input.Keys.NumPad2,
                Key.Keypad3 => Microsoft.Xna.Framework.Input.Keys.NumPad3,
                Key.Keypad4 => Microsoft.Xna.Framework.Input.Keys.NumPad4,
                Key.Keypad5 => Microsoft.Xna.Framework.Input.Keys.NumPad5,
                Key.Keypad6 => Microsoft.Xna.Framework.Input.Keys.NumPad6,
                Key.Keypad7 => Microsoft.Xna.Framework.Input.Keys.NumPad7,
                Key.Keypad8 => Microsoft.Xna.Framework.Input.Keys.NumPad8,
                Key.Keypad9 => Microsoft.Xna.Framework.Input.Keys.NumPad9,
                _ => null
            };
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

        private void UpdateFrame(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

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
            if (_screens.PlayerDashboardOpen)
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
                    _screens.PlayerDashboardOpen = false;
                }

                Window.Title = "Autonocraft | Player Stats";
                return;
            }

            if (_screens.MainMenuSettingsOpen)
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
                    _screens.MainMenuSettingsOpen = false;
                }
                else if (_screens.MainMenuSettingsScreen.CancelRequested)
                {
                    _screens.MainMenuSettingsScreen.Close();
                    _screens.MainMenuSettingsOpen = false;
                }

                Window.Title = "Autonocraft | Settings";
                return;
            }

            _screens.SaveSlotScreen!.Update(GraphicsDevice.Viewport, kbState, mouseState, _input.PrevKeyboard, _input.PrevMouse, deltaTime);

            if (_screens.SaveSlotScreen.NewWorldRequested)
            {
                OpenNewWorldSetup();
            }
            else if (_screens.SaveSlotScreen.LoadRequested && _screens.SaveSlotScreen.SelectedSlotId != null)
            {
                TryStartLoadedWorld(_screens.SaveSlotScreen.SelectedSlotId);
            }
            else if (_screens.SaveSlotScreen.SettingsRequested)
            {
                _screens.MainMenuSettingsScreen!.Open(_settings);
                _screens.MainMenuSettingsOpen = true;
            }
            else if (_screens.SaveSlotScreen.StatsRequested)
            {
                OpenPlayerDashboard();
            }
            else if (_screens.SaveSlotScreen.QuitRequested)
            {
                Exit();
            }

            Window.Title = "Autonocraft | Main Menu";
        }

        private void OpenPlayerDashboard()
        {
            _screens.PlayerDashboardScreen!.Open(
                _screens.SaveSlotScreen!.GetSelectedSlotId(),
                _screens.SaveSlotScreen.GetSelectedSlotName());
            _screens.PlayerDashboardOpen = true;
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
                Window.Title = "Autonocraft | Main Menu";
            }
        }

        private void UpdateWorldLoading(float deltaTime, KeyboardState kbState, MouseState mouseState)
        {
            _screens.LoadingScreen!.Update(deltaTime);

            if (_screens.LoadingScreen.HasTimedOut)
            {
                Console.WriteLine($"[Load] World loading timed out: {_screens.LoadingScreen.TimeoutReason}");
                _screens.SaveSlotScreen?.SetLoadError(_screens.LoadingScreen.TimeoutReason ?? "World failed to load.");
                ReturnToMainMenu();
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

        private bool UpdateBlockingGameplayOverlays(
            float deltaTime,
            KeyboardState kbState,
            MouseState mouseState)
        {
            if (_screens.DevConsole!.IsOpen)
            {
                EnsureUiPointerMode();
                _screens.DevConsole.Update(GraphicsDevice.Viewport, kbState, _input.PrevKeyboard, _hostContext);
                return true;
            }

            if (_screens.DeathScreen!.IsOpen)
            {
                EnsureUiPointerMode();
                _screens.DeathScreen.Update(GraphicsDevice.Viewport, kbState, mouseState, _input.PrevKeyboard, _input.PrevMouse, deltaTime);

                if (_screens.DeathScreen.RespawnRequested)
                {
                    CombatSystem.RespawnPlayer(_session.Grid, _session.Player, _worldSpawnX, _worldSpawnZ);
                    _deathCauseText = null;
                    _deathPenaltyText = null;
                    CloseDeathScreen();
                }
                else if (_screens.DeathScreen.MainMenuRequested)
                {
                    ReturnToMainMenu();
                }

                return true;
            }

            if (_screens.PauseMenu!.IsOpen)
            {
                EnsureUiPointerMode();
                _screens.PauseMenu.Update(GraphicsDevice.Viewport, kbState, mouseState, _input.PrevKeyboard, _input.PrevMouse, deltaTime);

                if (_screens.PauseMenu.ResumeRequested)
                {
                    ClosePauseMenu();
                }
                else if (_screens.PauseMenu.SaveNowRequested)
                {
                    SaveWorld(sync: true);
                    _session.HudToast.Show("World saved");
                }
                else if (_screens.PauseMenu.MainMenuRequested)
                {
                    ReturnToMainMenu();
                }
                else if (_screens.PauseMenu.QuitRequested)
                {
                    QuitFromPauseMenu();
                }

                return true;
            }

            if (_screens.VillageChatScreen!.IsOpen)
            {
                EnsureUiPointerMode();
                _screens.VillageChatScreen.Update(GraphicsDevice.Viewport, _session, kbState, _input.PrevKeyboard, mouseState, _input.PrevMouse, deltaTime);
                return true;
            }

            if (_screens.VillageScreen!.IsOpen)
            {
                EnsureUiPointerMode();
                if ((kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
                    || (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.V) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.V)))
                {
                    CloseVillageUi();
                    return true;
                }

                _screens.VillageScreen.SetPlayerPosition(_session.Player.Position);
                _screens.VillageScreen.Update(GraphicsDevice.Viewport, kbState, mouseState, _input.PrevKeyboard, _input.PrevMouse);
                if (_screens.VillageScreen.CloseRequested)
                {
                    CloseVillageUi();
                }
                else
                {
                    HandleVillageScreenActions();
                }

                return true;
            }

            if (_session.Crafting.InventoryOpen)
            {
                UpdateInventoryOverlay(kbState, mouseState);
                return true;
            }

            if (_session.Crafting.Crucible.IsOpen)
            {
                UpdateCrucibleOverlay(deltaTime, kbState, mouseState);
                return true;
            }

            if (_session.Crafting.IsJournalUiBlocking)
            {
                EnsureUiPointerMode();
                _session.Crafting.UpdateJournal(deltaTime);

                bool closeJournal = (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.J) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.J))
                    || (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape));

                if (closeJournal && (_session.Crafting.JournalOpen || _session.Crafting.JournalTransition.Alpha > 0.01f))
                {
                    _session.Crafting.CloseJournal();
                    _input.IsMouseLocked = _input.MouseLockedBeforeCrafting;
                    RestoreMouseLockAfterOverlay();
                }

                return true;
            }

            return false;
        }

        private void HandleVillageScreenActions()
        {
            if (_screens.VillageScreen!.IsFoundingMode)
            {
                if (_screens.VillageScreen.PlaceTownHeartRequested)
                {
                    _blueprints.StartFoundingTownHeartPlacement(CloseVillageUi);
                    EnsureMouseLockedForGameplay();
                }
                else if (_screens.VillageScreen.ClaimRequested)
                {
                    if (_session.Villages.TryFindClaimableStructure(
                            _session.Grid,
                            _session.Player.Position,
                            24f,
                            out int ax,
                            out int az,
                            out _))
                    {
                        if (_session.Villages.TryClaimStructure(_session.Grid, ax, az, out _))
                        {
                            CloseVillageUi();
                            OpenVillageUi();
                        }
                    }
                }

                return;
            }

            var village = _session.Villages.GetActiveVillage(_session.Player.Position);
            if (village == null)
            {
                return;
            }

            if (_screens.VillageScreen!.SummonSettlersRequested)
            {
                _session.Villages.RepairVillageCitizens(village, _session.Grid);
            }
            else if (_screens.VillageScreen.RecruitRequested)
            {
                _session.Villages.TryRecruit(village, _session.Grid);
            }
            else if (_screens.VillageScreen.ClaimRequested)
            {
                if (_session.Villages.TryFindClaimableStructure(
                        _session.Grid,
                        _session.Player.Position,
                        24f,
                        out int ax,
                        out int az,
                        out _))
                {
                    _session.Villages.TryClaimStructure(_session.Grid, ax, az, out _);
                }
            }
            else if (_screens.VillageScreen.RequestedBlueprintId != null && _screens.VillageScreen.RequestBlueprintPlacement)
            {
                _blueprints.StartBlueprintPlacement(village, _screens.VillageScreen.RequestedBlueprintId, CloseVillageUi);
                EnsureMouseLockedForGameplay();
            }
            else if (_screens.VillageScreen.RequestWorkZonePlacement)
            {
                _blueprints.StartWorkZonePlacement(CloseVillageUi);
                EnsureMouseLockedForGameplay();
            }
            else if (_screens.VillageScreen.RequestedChatVillagerId >= 0 &&
                     _session.Villagers.TryGet(_screens.VillageScreen.RequestedChatVillagerId, out var chatVillager))
            {
                CloseVillageUi();
                OpenVillageChatWithVillager(village, chatVillager.Id, chatVillager.Name);
            }
            else if (_screens.VillageScreen.RequestedAssignVillagerId >= 0 &&
                     _session.Villagers.TryGet(_screens.VillageScreen.RequestedAssignVillagerId, out var villager))
            {
                _session.Villages.TryAssignJob(village, villager, _screens.VillageScreen.RequestedAssignJob);
            }
        }

        private void OpenVillageChatWithVillager(Village.Village village, int villagerId, string villagerName)
        {
            PrepareMouseForUi();
            _screens.VillageChatScreen!.OpenWithVillager(village, villagerId, villagerName);
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

        private void UpdateGameplay(float deltaTime, KeyboardState kbState, MouseState mouseState)
        {
            // Avoid one slow streaming frame turning into a giant physics/input step.
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
                PerfCounters.RecordUpdate((float)updateStopwatch.Elapsed.TotalMilliseconds);
            }
        }

        private void UpdateGameplayCore(float deltaTime, KeyboardState kbState, MouseState mouseState)
        {
            while (_pendingActions.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { Console.WriteLine($"[Agent Action Error] {ex.Message}"); }
            }

            HandleInventoryKey(kbState);

            if (UpdateBlockingGameplayOverlays(deltaTime, kbState, mouseState))
            {
                return;
            }

            if (!_screens.PauseMenu!.IsOpen && !_screens.DeathScreen!.IsOpen && _session.Player.IsAlive)
            {
                _session.Player.Stats.RecordPlayTime(deltaTime);
            }

            bool consoleToggle = (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F3) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F3))
                || (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.OemTilde) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.OemTilde));

            if (consoleToggle)
            {
                _screens.DevConsole!.Toggle();
                if (_screens.DevConsole.IsOpen)
                {
                    _input.MouseLockedBeforeConsole = _input.IsMouseLocked;
                    PrepareMouseForUi();
                }
                else
                {
                    _input.IsMouseLocked = _input.MouseLockedBeforeConsole;
                    RestoreMouseLockAfterOverlay();
                }
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F4) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F4))
            {
                PerfCounters.ShowPerfHud = !PerfCounters.ShowPerfHud;
                Console.WriteLine($"[Debug] Toggled performance HUD: {PerfCounters.ShowPerfHud}");
            }

            if (_screens.DevConsole!.IsOpen)
            {
                _screens.DevConsole.Update(GraphicsDevice.Viewport, kbState, _input.PrevKeyboard, _hostContext);
                return;
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.V) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.V) && _session.Player.IsAlive)
            {
                if (_screens.VillageScreen!.IsOpen)
                {
                    CloseVillageUi();
                }
                else
                {
                    OpenVillageUi();
                }

                return;
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.C) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.C) && _session.Player.IsAlive)
            {
                OpenVillageChatUi();
                return;
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.J) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.J) && _session.Player.IsAlive)
            {
                _input.MouseLockedBeforeCrafting = _input.IsMouseLocked;
                PrepareMouseForUi();
                _session.Crafting.ToggleJournal();
                return;
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && _session.Player.IsAlive)
            {
                if (_blueprints.TryCancelOnEscape())
                {
                    return;
                }

                OpenPauseMenu();
                return;
            }

            _blueprints.TickPendingPreview();

            // Keyboard slot selections (D1-D9)
            for (int i = 0; i < 9; i++)
            {
                var k = (Microsoft.Xna.Framework.Input.Keys)((int)Microsoft.Xna.Framework.Input.Keys.D1 + i);
                if (kbState.IsKeyDown(k) && !_input.PrevKeyboard.IsKeyDown(k))
                {
                    _session.Player.SelectedSlot = i;
                    var stack = _session.Player.Hotbar[i];
                    Console.WriteLine($"[Selection] Selected Hotbar Slot {i + 1}: {FormatHotbarStack(stack)}");
                }
                var nk = (Microsoft.Xna.Framework.Input.Keys)((int)Microsoft.Xna.Framework.Input.Keys.NumPad1 + i);
                if (kbState.IsKeyDown(nk) && !_input.PrevKeyboard.IsKeyDown(nk))
                {
                    _session.Player.SelectedSlot = i;
                    var stack = _session.Player.Hotbar[i];
                    Console.WriteLine($"[Selection] Selected Hotbar Slot {i + 1}: {FormatHotbarStack(stack)}");
                }
            }

            // Drop selected item on Q
            bool qPressed = IsKeyPressed(kbState, Key.Q);
            if (qPressed && !_prevQPressed && _session.Player.IsAlive)
            {
                var item = _session.Player.DropOneFromSelectedSlot();
                if (!item.IsEmpty)
                {
                    var dropPos = _camera.Position + _camera.Front * 0.5f;
                    var entity = _session.SpawnItemDrop(item, dropPos);
                    if (entity != null)
                    {
                        entity.Velocity = _camera.Front * 4.0f + Vector3.UnitY * 1.5f;
                    }
                }
            }
            _prevQPressed = qPressed;

            // Toggle physics mode
            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.G) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.G))
            {
                _session.Player.CreativeMode = !_session.Player.CreativeMode;
                _session.Player.Velocity = Vector3.Zero;
                Console.WriteLine($"[Mode] Toggled creative mode: {_session.Player.CreativeMode}");
            }

            // Mouse interactions when locked (not gated on IsActive — macOS can flicker inactive during chunk hitches).
            bool leftHeld = false;
            bool leftPressed = false;
            bool rightPressed = false;
            bool shiftRightPressed = false;
            bool shiftHeld = false;

            float mouseDx = 0f;
            float mouseDy = 0f;
            int centerX = 0;
            int centerY = 0;

            if (CanProcessGameplayMouse())
            {
                var mouseLook = _input.ProcessMouseLook(mouseState, GraphicsDevice);
                mouseDx = mouseLook.DeltaX;
                mouseDy = mouseLook.DeltaY;
                centerX = mouseLook.CenterX;
                centerY = mouseLook.CenterY;

                if (mouseDx != 0f || mouseDy != 0f)
                {
                    _camera.Yaw += mouseDx * 0.15f;
                    _camera.Pitch = Math.Clamp(_camera.Pitch - mouseDy * 0.15f, -89f, 89f);

                    _session.Player.Yaw = _camera.Yaw;
                    _session.Player.Pitch = _camera.Pitch;
                }

                leftHeld = mouseState.LeftButton == ButtonState.Pressed;
                leftPressed = leftHeld && _input.PrevMouse.LeftButton == ButtonState.Released;
                rightPressed = mouseState.RightButton == ButtonState.Pressed && _input.PrevMouse.RightButton == ButtonState.Released;
                shiftHeld = kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)
                    || kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
                shiftRightPressed = rightPressed && shiftHeld;
                if (shiftHeld)
                {
                    rightPressed = false;
                }

                int scrollDelta = mouseState.ScrollWheelValue - _input.PrevMouse.ScrollWheelValue;
                if (scrollDelta != 0)
                {
                    int slotChange = -Math.Sign(scrollDelta);
                    int newSlot = _session.Player.SelectedSlot + slotChange;
                    if (newSlot < 0) newSlot = 8;
                    if (newSlot > 8) newSlot = 0;
                    _session.Player.SelectedSlot = newSlot;
                    Console.WriteLine($"[Selection] Selected Hotbar Slot {newSlot + 1}: {FormatHotbarStack(_session.Player.Hotbar[newSlot])}");
                }
            }

            if (!_timePaused)
            {
                _timeOfDay += deltaTime * _timeScale;
                _timeOfDay -= MathF.Floor(_timeOfDay);
            }

            _waterAnimTime += deltaTime;
            var swFluids = System.Diagnostics.Stopwatch.StartNew();
            if (_spawnWarmupRemaining <= 0f || SpawnWarmupProgress >= 0.5f)
            {
                _session.Grid.Fluids.Update(_session.Grid, deltaTime, GraphicsDevice);
            }
            swFluids.Stop();
            PerfCounters.UpdateFluidsMs = (float)swFluids.Elapsed.TotalMilliseconds;

            if (_session.Player.IsAlive)
            {
                Vector3 front = _camera.Front;
                Vector3 right = _camera.Right;
                Vector3 frontHorizontal = Vector3.Normalize(new Vector3(front.X, 0f, front.Z));
                Vector3 rightHorizontal = Vector3.Normalize(new Vector3(right.X, 0f, right.Z));

                var swPlayer = System.Diagnostics.Stopwatch.StartNew();
                Vector3 moveDir = Vector3.Zero;
                if (IsKeyPressed(kbState, Key.W)) moveDir += frontHorizontal;
                if (IsKeyPressed(kbState, Key.S)) moveDir -= frontHorizontal;
                if (IsKeyPressed(kbState, Key.A)) moveDir -= rightHorizontal;
                if (IsKeyPressed(kbState, Key.D)) moveDir += rightHorizontal;

                // Sprint input detection
                bool wPressedThisFrame = IsKeyPressed(kbState, Key.W) && !_prevWPressed;
                _prevWPressed = IsKeyPressed(kbState, Key.W);
                if (wPressedThisFrame)
                {
                    float timeSinceLastW = _waterAnimTime - _lastWPressTime;
                    if (timeSinceLastW < 0.25f)
                    {
                        _doubleTapSprintActive = true;
                    }
                    _lastWPressTime = _waterAnimTime;
                }
                if (!IsKeyPressed(kbState, Key.W))
                {
                    _doubleTapSprintActive = false;
                }

                bool ctrlPressed = IsKeyPressed(kbState, Key.ControlLeft);
                bool isSprintingWanted = IsKeyPressed(kbState, Key.W) && (ctrlPressed || _doubleTapSprintActive);

                if (isSprintingWanted && _session.Player.Hunger > _session.Player.MaxHunger * SurvivalConstants.LowHungerFraction && !_session.Player.InWater)
                {
                    _session.Player.IsSprinting = true;
                }
                else
                {
                    _session.Player.IsSprinting = false;
                }

                if (_session.Player.CreativeMode)
                {
                    if (IsKeyPressed(kbState, Key.Space)) moveDir += Vector3.UnitY;
                    if (IsKeyPressed(kbState, Key.ShiftLeft) || IsKeyPressed(kbState, Key.ShiftRight)) moveDir -= Vector3.UnitY;
                    _session.Player.Update(deltaTime, _session.Grid, moveDir);
                }
                else
                {
                    bool swimUp = IsKeyPressed(kbState, Key.Space);
                    bool swimDown = IsKeyPressed(kbState, Key.ShiftLeft) || IsKeyPressed(kbState, Key.ShiftRight);
                    if (IsKeyPressed(kbState, Key.Space) && !_prevSpacePressed)
                    {
                        if (!_session.Player.InWater || !_session.Player.HeadUnderwater)
                        {
                            _session.Player.Jump();
                            _session.PlayJumpSound();
                        }
                    }

                    _session.Player.Update(deltaTime, _session.Grid, moveDir, swimUp, swimDown);
                }
                swPlayer.Stop();
                PerfCounters.UpdatePlayerMs = (float)swPlayer.Elapsed.TotalMilliseconds;

                _prevSpacePressed = IsKeyPressed(kbState, Key.Space);
                _session.UpdateMovementAudio(deltaTime, moveDir);
                _session.InteractionAnimator.Update(deltaTime, _session.Player);
                _session.Weather.Update(deltaTime);

                var swParticles = System.Diagnostics.Stopwatch.StartNew();
                _session.Particles.Update(deltaTime, _session.Grid);
                _session.Particles.UpdateAmbient(deltaTime, _session.Player.Position, _session.Grid, _timeOfDay, _session.Weather);
                swParticles.Stop();
                PerfCounters.UpdateParticlesMs = (float)swParticles.Elapsed.TotalMilliseconds;

                if (_session.Player.InWater && !_playerWasInWater)
                {
                    _session.Particles.SpawnWaterSplash(_session.Player.Position + new Vector3(0f, 0.2f, 0f), 0.9f);
                    _session.PlayWaterSplashSound();
                }
                else if (!_session.Player.InWater && _playerWasInWater)
                {
                    _session.Particles.SpawnWaterSplash(_session.Player.Position, 1.0f);
                    _session.PlayWaterSplashSound();
                }
                else if (_session.Player.JustLanded && WaterQuery.IsLandingInWater(_session.Grid, _session.Player.Position))
                {
                    _session.Particles.SpawnWaterSplash(_session.Player.Position, 1.3f);
                    _session.PlayWaterSplashSound();
                }

                _playerWasInWater = _session.Player.InWater;

                if (_session.Player.HeadUnderwater)
                {
                    _underwaterBubbleTimer += deltaTime;
                    if (_underwaterBubbleTimer > 0.35f)
                    {
                        _underwaterBubbleTimer = 0f;
                        _session.Particles.SpawnUnderwaterBubble(_camera.Position);
                    }
                }
                else
                {
                    _underwaterBubbleTimer = 0f;
                }

                _session.Combat.HandleLandingEffects(_session.Player, _session.Particles, _session.InteractionAnimator);
                SyncCameraFromPlayer();

                if (CanProcessGameplayMouse())
                {
                    var solidRayHit = BlockInteractionSystem.RaycastSolidHit(
                        _session.Grid,
                        _camera.Position,
                        _camera.Front,
                        BlockInteractionSystem.RaycastRange);

                    bool shiftLeftPressed = leftPressed && shiftHeld;
                    bool handledClick = false;

                    if (_blueprints.PendingBlueprintId != null && leftPressed)
                    {
                        _blueprints.ConfirmBlueprintPlacement();
                        handledClick = true;
                    }
                    else if (_blueprints.WorkZonePlacementActive && leftPressed && solidRayHit.HasHit)
                    {
                        var zoneVillage = _session.Villages.GetActiveVillage(_session.Player.Position);
                        if (zoneVillage != null)
                        {
                            _blueprints.ConfirmWorkZoneCorner(
                                zoneVillage,
                                (int)solidRayHit.BlockPos.X,
                                (int)solidRayHit.BlockPos.Y,
                                (int)solidRayHit.BlockPos.Z);
                        }

                        handledClick = true;
                    }
                    else if (shiftLeftPressed && solidRayHit.HasHit)
                    {
                        if (_session.Villages.TryMarkWorkBlock(
                                _session.Grid,
                                (int)solidRayHit.BlockPos.X,
                                (int)solidRayHit.BlockPos.Y,
                                (int)solidRayHit.BlockPos.Z,
                                out string markMessage))
                        {
                            _session.HudToast.Show(markMessage);
                        }
                        else
                        {
                            _session.HudToast.Show(markMessage);
                        }

                        handledClick = true;
                    }

                    bool allowMining = leftHeld && !handledClick && !shiftHeld && !_session.Combat.BlocksMiningThisFrame;

                    _session.Combat.Update(
                        deltaTime,
                        _session.Grid,
                        _session.Player,
                        _session.Animals,
                        _session.BlockInteraction,
                        _session.Particles,
                        _session.InteractionAnimator,
                        _camera.Position,
                        _camera.Front,
                        allowMining ? leftHeld : false,
                        handledClick ? false : leftPressed,
                        solidRayHit);

                    _session.BlockInteraction.Update(
                        deltaTime,
                        _session.Grid,
                        _session.Player,
                        _camera.Position,
                        _camera.Front,
                        allowMining,
                        rightPressed,
                        shiftRightPressed,
                        _session.Crafting,
                        _session.Particles,
                        GraphicsDevice,
                        solidRayHit,
                        _blueprints.PendingBlueprintId != null);

                    if (_session.BlockInteraction.PendingStationOpen.HasValue)
                    {
                        var stationPos = _session.BlockInteraction.PendingStationOpen.Value;
                        OpenCrucibleAt(
                            (int)stationPos.X,
                            (int)stationPos.Y,
                            (int)stationPos.Z,
                            _session.BlockInteraction.PendingStationType);
                    }
                }
            }

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

            bool inSpawnWarmup = _spawnWarmupRemaining > 0f;
            if (inSpawnWarmup)
            {
                _spawnWarmupRemaining -= deltaTime;
            }

            float warmup = SpawnWarmupProgress;
            int targetTerrainPerFrame = VoxelWorld.GetRuntimeTerrainChunksPerFrame(_settings.RenderDistance);
            int targetMeshPerFrame = VoxelWorld.GetRuntimeMeshChunksPerFrame(_settings.RenderDistance);
            int terrainPerFrame = inSpawnWarmup
                ? Math.Max(1, (int)MathF.Round(1f + (targetTerrainPerFrame - 1f) * warmup))
                : targetTerrainPerFrame;
            int meshPerFrame = inSpawnWarmup
                ? Math.Max(1, (int)MathF.Round(1f + (targetMeshPerFrame - 1f) * warmup))
                : targetMeshPerFrame;

            _session.DeferAmbientSpawns = inSpawnWarmup && warmup < 0.7f;

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
                _session.UpdateSurvival(deltaTime, _timeOfDay, inSpawnWarmup);
                _session.UpdateEarlyGuide(deltaTime, _timeOfDay, _screens.VillageScreen?.IsOpen == true);
                // TryFindClaimableStructure is expensive — poll at most every 10 s; GameSession skips if player barely moved.
                _claimHintTimer += deltaTime;
                if (_claimHintTimer >= 10f)
                {
                    _claimHintTimer = 0f;
                    _session.UpdateNearbyClaimHint();
                }

                _session.UpdateVillageHudHint(_session.Player.CreativeMode);
            }
            swVillages.Stop();
            PerfCounters.UpdateVillagesMs = (float)swVillages.Elapsed.TotalMilliseconds;

            if (_pendingAutoOpenPeopleTab && !inSpawnWarmup && _session.Player.Stats.EarlyGuideStage <= 1)
            {
                _pendingAutoOpenPeopleTab = false;
                OpenVillageUiToPeopleTab();
            }

            _autosaveTimer += deltaTime;
            if (_autosaveTimer >= GameConstants.AutosaveIntervalSeconds)
            {
                _autosaveTimer = 0f;
                PerformAutosave(sync: false);
            }

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
        }

        private void DrawFrame(GameTime gameTime)
        {
            switch (_screens.State)
            {
                case GameState.MainMenu:
                    _screens.DrawMainMenu(GraphicsDevice);
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
                        _timeOfDay);
                    RecordFrameMetrics((float)gameTime.ElapsedGameTime.TotalSeconds);
                    break;
            }

            base.Draw(gameTime);
        }

        public void OpenCrucibleAt(int x, int y, int z, BlockType stationType)
        {
            _input.MouseLockedBeforeCrafting = _input.IsMouseLocked;
            PrepareMouseForUi();
            _session.Crafting.OpenCrucible(x, y, z, stationType);
        }

        private void CloseCrucibleUi()
        {
            _session.Crafting.CloseCrucible();
            _session.Crafting.CloseRecipeBook();
            _input.IsMouseLocked = _input.MouseLockedBeforeCrafting;
            RestoreMouseLockAfterOverlay();
        }

        private void CloseInventoryUi()
        {
            if (!_session.Crafting.CraftCursor.IsEmpty)
            {
                _session.Player.AddItem(_session.Crafting.CraftCursor);
                _session.Crafting.CraftCursor = ItemStack.Empty;
            }

            _session.Crafting.CloseInventory();
            _session.Crafting.CloseRecipeBook();
            _input.IsMouseLocked = _input.MouseLockedBeforeCrafting;
            RestoreMouseLockAfterOverlay();
        }

        private void UpdateInventoryOverlay(KeyboardState kbState, MouseState mouseState)
        {
            EnsureUiPointerMode();
            _screens.InventoryScreen!.Update(
                GraphicsDevice.Viewport,
                _session.Player,
                _session.Crafting,
                kbState,
                mouseState,
                _input.PrevKeyboard,
                _input.PrevMouse);

            _screens.InventoryScreen.HandleClicks(_session.Player, _session.Crafting);
            _screens.InventoryScreen.HandleRecipeBookClick(_session.Crafting, _session.Player);

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.B) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.B))
            {
                _session.Crafting.ToggleRecipeBook();
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
            {
                CloseInventoryUi();
            }
        }

        /// <summary>
        /// Toggle inventory on I edge. Frame guard prevents fixed-timestep double Update from open+close on one press.
        /// </summary>
        private bool HandleInventoryKey(KeyboardState kbState)
        {
            if (!_session.Player.IsAlive)
            {
                return false;
            }

            if (_screens.PauseMenu?.IsOpen == true
                || _screens.DeathScreen?.IsOpen == true
                || _screens.VillageScreen?.IsOpen == true
                || _session.Crafting.Crucible.IsOpen
                || _session.Crafting.IsJournalUiBlocking)
            {
                return false;
            }

            if (!kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.I) || _input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.I))
            {
                return false;
            }

            if (_input.LastInventoryKeyFrame == _input.GameFrameCount)
            {
                return false;
            }

            _input.LastInventoryKeyFrame = _input.GameFrameCount;

            if (_session.Crafting.InventoryOpen)
            {
                CloseInventoryUi();
            }
            else
            {
                _input.MouseLockedBeforeCrafting = _input.IsMouseLocked;
                PrepareMouseForUi();
                _session.Crafting.OpenInventory();
            }

            return true;
        }

        private void OpenVillageUi()
        {
            var village = _session.Villages.GetActiveVillage(_session.Player.Position);
            if (village == null)
            {
                _input.MouseLockedBeforeVillageUi = _input.IsMouseLocked;
                PrepareMouseForUi();
                TryActivateWindow();
                _screens.VillageScreen!.OpenFounding(
                    _session.Villages,
                    _session.Grid,
                    _session.Player.Position,
                    new PlayerHotbarAdapter(_session.Player),
                    _session.Player.CreativeMode,
                    _settings.PlayWithAi && _settings.AiProvider != AiProviderKind.Disabled);
                return;
            }

            if (_session.Villages.RepairVillageCitizens(village, _session.Grid))
            {
                village.ReconcileVillagerRegistry(_session.Villagers.All);
            }

            _session.Villages.SyncCitizensForVillage(village);

            string? openingNote = null;
            int citizens = VillageSettlementHealth.GetLivePopulation(village, _session.Villagers);
            if (citizens == 0)
            {
                openingNote = "Settlers are being summoned to your Town Heart. Check the PEOPLE tab in a moment.";
            }
            else if (citizens < 2 && village.Name.Contains("Founder", System.StringComparison.OrdinalIgnoreCase))
            {
                openingNote = $"{citizens} settler(s) on site — open PEOPLE tab to assign LUMBER or BUILD.";
            }

            _input.MouseLockedBeforeVillageUi = _input.IsMouseLocked;
            PrepareMouseForUi();
            TryActivateWindow();
            _screens.VillageScreen!.Open(
                village,
                _session.Villages,
                _session.Grid,
                _session.Player.Position,
                new PlayerHotbarAdapter(_session.Player),
                _session.Player.CreativeMode,
                openingNote,
                _settings.PlayWithAi && _settings.AiProvider != AiProviderKind.Disabled,
                earlyGuideStage: _session.Player.Stats.EarlyGuideStage);
        }

        private void OpenVillageUiToPeopleTab()
        {
            OpenVillageUi();
            _screens.VillageScreen?.OpenPeopleTab();
        }

        private static string FormatDeathCause(DeathCause cause) => cause switch
        {
            DeathCause.Fall => "You fell from a great height.",
            DeathCause.Drown => "You drowned.",
            DeathCause.Starvation => "You starved.",
            DeathCause.Wolf => "A wolf killed you.",
            DeathCause.Animal => "An animal killed you.",
            _ => "YOUR ADVENTURE ISN'T OVER"
        };

        private IItemContainer WrapPlayerHotbar() => new PlayerHotbarAdapter(_session.Player);

        private void CloseVillageUi()
        {
            _screens.VillageScreen!.Close();
            _input.IsMouseLocked = _input.MouseLockedBeforeVillageUi;
            RestoreMouseLockAfterOverlay();
        }

        private void OpenVillageChatUi()
        {
            if (!_settings.PlayWithAi || _settings.AiProvider == AiProviderKind.Disabled)
            {
                _session.HudToast.Show("Village AI is off. Enable it in main menu Settings.");
                return;
            }

            var village = _session.Villages.GetActiveVillage(_session.Player.Position);
            if (village == null)
            {
                _session.HudToast.Show("No village nearby. Press V to place a Town Heart.");
                return;
            }

            var nearest = _session.Villagers.GetNearest(_session.Player.Position, 8f);
            string target = nearest != null ? nearest.Id.ToString() : "mayor";
            _input.MouseLockedBeforeVillageUi = _input.IsMouseLocked;
            PrepareMouseForUi();
            _screens.VillageChatScreen!.Open(village, target, nearest == null);
        }

        private sealed class PlayerHotbarAdapter : IItemContainer
        {
            private readonly Player _player;
            public PlayerHotbarAdapter(Player player) => _player = player;
            public int SlotCount => _player.Hotbar.Length;
            public ItemStack GetSlot(int index) => _player.Hotbar[index];
            public void SetSlot(int index, ItemStack stack) => _player.Hotbar[index] = stack;
            public bool AddItem(ItemStack item) => _player.AddItem(item);
            public bool TryConsumeBlock(BlockType blockType, int count)
            {
                if (_player.CreativeMode)
                {
                    return true;
                }

                int have = CountBlock(blockType);
                if (have < count) return false;
                int remaining = count;
                for (int i = 0; i < _player.Hotbar.Length && remaining > 0; i++)
                {
                    if (!_player.Hotbar[i].IsBlock() || _player.Hotbar[i].BlockType != blockType) continue;
                    int take = Math.Min(_player.Hotbar[i].Count, remaining);
                    _player.Hotbar[i].Count -= take;
                    remaining -= take;
                    if (_player.Hotbar[i].Count <= 0) _player.Hotbar[i] = ItemStack.Empty;
                }
                return remaining == 0;
            }
            public int CountBlock(BlockType blockType)
            {
                if (_player.CreativeMode)
                {
                    return int.MaxValue / 2;
                }

                int total = 0;
                for (int i = 0; i < _player.Hotbar.Length; i++)
                {
                    if (_player.Hotbar[i].IsBlock() && _player.Hotbar[i].BlockType == blockType)
                        total += _player.Hotbar[i].Count;
                }
                return total;
            }
            public bool HasSpaceFor(ItemStack item) => true;
        }

        private void UpdateCrucibleOverlay(float deltaTime, KeyboardState kbState, MouseState mouseState)
        {
            var env = _session.Crafting.GetCurrentEnvironment(_session.Grid, _timeOfDay);
            _screens.CrucibleScreen!.Update(
                GraphicsDevice.Viewport,
                _session.Crafting.Crucible,
                env,
                _session.Crafting.Journal,
                _session.Crafting,
                _session.Player,
                kbState,
                mouseState,
                _input.PrevKeyboard,
                _input.PrevMouse,
                deltaTime);

            _screens.CrucibleScreen.HandleRecipeBookClick(_session.Crafting, _session.Player);

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.B) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.B))
            {
                _session.Crafting.ToggleRecipeBook();
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && !_input.PrevKeyboard.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
            {
                CloseCrucibleUi();
                return;
            }

            if (_screens.CrucibleScreen.ClickedSlotIndex >= 0)
            {
                if (_screens.CrucibleScreen.RightClickedSlot)
                {
                    if (_session.Crafting.Crucible.WithdrawToHotbar(_session.Player, _screens.CrucibleScreen.ClickedSlotIndex))
                    {
                        _screens.CrucibleScreen.TriggerSlotPulse(_screens.CrucibleScreen.ClickedSlotIndex);
                    }
                }
                else
                {
                    if (_session.Crafting.Crucible.DepositFromHotbar(_session.Player, _screens.CrucibleScreen.ClickedSlotIndex))
                    {
                        _screens.CrucibleScreen.TriggerSlotPulse(_screens.CrucibleScreen.ClickedSlotIndex);
                    }
                    else
                    {
                        var slot = _session.Player.GetSelectedStack();
                        if (!slot.IsBlock() && !slot.IsMaterial())
                        {
                            _screens.CrucibleScreen.SetStatus("SELECT BLOCKS OR STICKS");
                        }
                        else
                        {
                            _screens.CrucibleScreen.SetStatus("SLOT FULL");
                        }
                    }
                }
            }

            if (_screens.CrucibleScreen.TransmuteRequested)
            {
                _screens.CrucibleScreen.BeginTransmuteAnimation();
            }

            if (_screens.CrucibleScreen.TransmuteReady)
            {
                var result = _session.Crafting.TryTransmute(_session.Grid, _session.Player, _timeOfDay);
                if (result.Succeeded)
                {
                    string created = result.Recipe!.IsToolOutput
                        ? result.Recipe.DisplayName.ToUpperInvariant()
                        : result.Recipe.IsMaterialOutput
                            ? $"{result.Recipe.DisplayName.ToUpperInvariant()} x{result.Recipe.OutputCount}"
                            : result.Recipe.Output.ToString().ToUpperInvariant();
                    _screens.CrucibleScreen.SetStatus($"CREATED {created}");
                    _session.BlockInteraction.TriggerCrosshairFlash();
                    _session.Particles.SpawnHint(_session.Player.Position + new Vector3(0f, Player.EyeHeight, 0f));
                }
                else
                {
                    _screens.CrucibleScreen.SetStatus(result.Message.ToUpperInvariant());
                }
            }
        }

        public void SimulateClick(MouseButton button)
        {
            var action = new Action(() =>
            {
                SyncCameraFromPlayer();
                if (button == MouseButton.Left)
                {
                    if (!_session.Combat.TryInstantAttack(_session.Grid, _session.Player, _session.Animals, _session.BlockInteraction, _session.Particles, _session.InteractionAnimator, _camera.Position, _camera.Front))
                    {
                        _session.BlockInteraction.InstantMine(_session.Grid, _session.Player, _camera.Position, _camera.Front, _session.Particles, _runTests ? null : GraphicsDevice);
                    }
                }
                else if (button == MouseButton.Right)
                {
                    _session.BlockInteraction.InstantPlace(_session.Grid, _session.Player, _camera.Position, _camera.Front, _session.Particles, _runTests ? null : GraphicsDevice);
                }
            });

            if (_runTests) action();
            else _pendingActions.Enqueue(action);
        }

        public void SaveScreenshot(string path)
        {
            if (_runTests) return;

            int w = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int h = GraphicsDevice.PresentationParameters.BackBufferHeight;

            Color[] backBuffer = new Color[w * h];
            GraphicsDevice.GetBackBufferData(backBuffer);

            using (var texture = new Texture2D(GraphicsDevice, w, h))
            {
                texture.SetData(backBuffer);
                using (var stream = File.Create(path))
                {
                    texture.SaveAsPng(stream, w, h);
                }
            }
        }

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

        private static string FormatHotbarStack(ItemStack stack)
        {
            if (stack.IsEmpty)
            {
                return "empty";
            }

            if (stack.IsTool())
            {
                return $"{stack.GetDisplayName()} ({stack.Durability}/{stack.MaxDurability})";
            }

            return $"{stack.BlockType} (x{stack.Count})";
        }
    }

    internal static class SdlWindowGrab
    {
        private enum SdlBool
        {
            False = 0,
            True = 1
        }

        [DllImport("SDL2", EntryPoint = "SDL_SetWindowGrab", CallingConvention = CallingConvention.Cdecl)]
        private static extern void SetWindowGrab(IntPtr window, SdlBool grabbed);

        [DllImport("SDL2", EntryPoint = "SDL_RaiseWindow", CallingConvention = CallingConvention.Cdecl)]
        private static extern void RaiseWindowNative(IntPtr window);

        public static void RaiseWindow(IntPtr window)
        {
            if (window == IntPtr.Zero)
            {
                return;
            }

            try
            {
                RaiseWindowNative(window);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        public static void SetGrabbed(IntPtr window, bool grabbed)
        {
            if (window == IntPtr.Zero)
            {
                return;
            }

            try
            {
                SetWindowGrab(window, grabbed ? SdlBool.True : SdlBool.False);
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }
    }
}

using System;
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

        // Graphics resources
        private Texture2D? _atlasTexture;
        private Texture2D? _whiteTexture;
        private Renderer? _renderer;
        private BlockTerrainEffect? _blockTerrainEffect;
        private SkyEffect? _skyEffect;
        private UiRenderer? _ui;
        private CrucibleScreen? _crucibleScreen;
        private JournalScreen? _journalScreen;
        private VillageScreen? _villageScreen;
        private VillageChatScreen? _villageChatScreen;
        private VillageAiOrchestrator? _villageAiOrchestrator;
        private bool _mouseLockedBeforeCrafting;
        private bool _mouseLockedBeforeVillageUi;
        private readonly UiTransition _screenFade = new UiTransition();
        private readonly UiTransition _pauseFade = new UiTransition();
        private readonly UiTransition _deathFade = new UiTransition();
        private GameState _prevState = GameState.MainMenu;
        private SaveSlotScreen? _saveSlotScreen;
        private MainMenuSettingsScreen? _mainMenuSettingsScreen;
        private bool _mainMenuSettingsOpen;
        private NewWorldSetupScreen? _newWorldSetupScreen;
        private LoadingScreen? _loadingScreen;
        private DevConsole? _devConsole;
        private PauseMenuScreen? _pauseMenu;
        private DeathScreen? _deathScreen;
        private AudioManager? _audio;
        private bool _mouseLockedBeforeConsole;
        private bool _mouseLockedBeforePause;
        private bool _mouseLockedBeforeDeath;

        // Game flow
        private GameState _state = GameState.MainMenu;
        private bool _skipMenu;
        private bool _agentServerStarted;
        private int _agentPort = 5001;
        private string? _activeSlotId;
        private string? _activeSlotName;
        private WorldSaveData? _pendingSaveData;
        private bool _loadingFromSave;
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
        private KeyboardState _prevKbState;
        private MouseState _prevMouseState;
        private bool _isMouseLocked = true;
        private bool _prevSpacePressed = false;
        private bool _wasActive = true;
        private bool _skipMouseLookFrame;
        private bool _deferPrevMouseReset;
        private bool _deferAgentServerStart;
        private float _spawnWarmupRemaining;
        private float _waterAnimUpdateTimer;
        private float _inactiveTimer;
        private float _claimHintTimer = 10f;
        private const float FocusLossReleaseDelay = 0.35f;
        private const int MouseWarpRejectThreshold = 120;
        private const float MaxGameplayDeltaTime = 1f / 30f;
        private const float SpawnWarmupSeconds = 15f;

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
        public GameState CurrentGameState => _state;
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
        public UiTransition ScreenFade => _screenFade;
        public UiTransition PauseFade => _pauseFade;
        public UiTransition DeathFade => _deathFade;

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
            _pauseMenu?.SetRenderDistance(_settings.RenderDistance);
            _session.Grid.UpdateChunksAround(GraphicsDevice, _camera.Position, _settings.RenderDistance);
        }

        private void ApplyMuteAudio(bool mute)
        {
            _settings.MuteAudio = mute;
            GameSettingsManager.Save(_settings);
            _audio?.ApplySettings(_settings);
            _pauseMenu?.ApplyAudioSettings(_settings);
        }

        public string ExecuteDevCommand(string input) => DevCommands.Execute(_hostContext, input);

        public const int DefaultSpawnX = GameConstants.DefaultSpawnX;
        public const int DefaultSpawnZ = GameConstants.DefaultSpawnZ;

        public AutonocraftGame(bool runTests = false, bool skipMenu = false, int agentPort = 5001, bool debugMetrics = false)
        {
            _runTests = runTests;
            _skipMenu = skipMenu;
            _agentPort = agentPort;
            if (debugMetrics && !RuntimeMetrics.FileLoggingEnabled)
            {
                RuntimeMetrics.EnableFileLogging(fromCli: true);
            }
            if (_skipMenu)
            {
                _state = GameState.WorldLoading;
            }
            if (!_runTests)
            {
                _graphics = new GraphicsDeviceManager(this);
            }

            _settings = GameSettingsManager.Load();
            _camera = new Camera();
            _session = new GameSession(DefaultSeed);
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
            _camera.Position = _session.Player.Position + new Vector3(0f, Player.EyeHeight, 0f);
            _camera.Yaw = _session.Player.Yaw;
            _camera.Pitch = _session.Player.Pitch;
            _camera.ViewPositionOffset = _session.InteractionAnimator.PositionOffset;
            _camera.ViewPitchOffset = -_session.InteractionAnimator.PitchRecoil;
        }

        protected override void Initialize()
        {
            if (_runTests)
            {
                return;
            }

            _graphics!.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.SynchronizeWithVerticalRetrace = true;
            _graphics.ApplyChanges();

            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += OnWindowClientSizeChanged;

            IsMouseVisible = true;
            _isMouseLocked = false;
            _wasActive = false;

            base.Initialize();
        }

        private bool ShouldCaptureMouse()
        {
            return _state == GameState.Playing
                && _isMouseLocked
                && _devConsole is { IsOpen: false }
                && _pauseMenu is { IsOpen: false }
                && _deathScreen is { IsOpen: false }
                && _villageScreen is not { IsOpen: true }
                && _villageChatScreen is not { IsOpen: true }
                && !_session.Crafting.JournalOpen
                && !_session.Crafting.Crucible.IsOpen;
        }

        private void PrepareMouseForUi()
        {
            _isMouseLocked = false;
            ReleaseMouseCapture();
            IsMouseVisible = true;
            _deferPrevMouseReset = true;
            SdlWindowGrab.RaiseWindow(Window.Handle);
        }

        private void EnsureCursorFreeOutsideGameplay()
        {
            if (_state == GameState.Playing && ShouldCaptureMouse())
            {
                return;
            }

            if (SdlMouseCapture.IsRelativeModeEnabled || !IsMouseVisible)
            {
                ReleaseMouseCapture();
                IsMouseVisible = true;
            }
        }

        private MouseState GetUiMouseState() => Mouse.GetState();

        private bool HasBlockingGameplayOverlay()
        {
            return _villageScreen?.IsOpen == true
                || _villageChatScreen?.IsOpen == true
                || _pauseMenu?.IsOpen == true
                || _deathScreen?.IsOpen == true
                || _devConsole?.IsOpen == true
                || _session.Crafting.Crucible.IsOpen
                || _session.Crafting.IsJournalUiBlocking;
        }

        private void EnsureUiPointerMode()
        {
            _isMouseLocked = false;
            ReleaseMouseCapture();
            IsMouseVisible = true;
        }

        private void TryActivateWindow() => SdlWindowGrab.RaiseWindow(Window.Handle);

        private void CloseAllGameplayOverlays()
        {
            _villageScreen?.Close();
            _villageChatScreen?.Close();
            _session.Crafting.CloseCrucible();
            _session.Crafting.CloseJournal();
            if (_devConsole?.IsOpen == true)
            {
                _devConsole.Toggle();
            }

            _pauseMenu?.Close();
            _deathScreen?.Close();
        }

        private void ApplyMouseCapture()
        {
            IsMouseVisible = false;
            SdlMouseCapture.TryEnableRelativeMode();
            _prevMouseState = GetUiMouseState();
            _skipMouseLookFrame = true;
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                SdlWindowGrab.SetGrabbed(Window.Handle, true);
            }

            InputDebugTrace.Log($"MOUSE_CAPTURE relative={SdlMouseCapture.IsRelativeModeEnabled}");
        }

        private void ReleaseMouseCapture()
        {
            SdlMouseCapture.DisableRelativeMode();
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                SdlWindowGrab.SetGrabbed(Window.Handle, false);
            }
        }

        private Point GetMouseClientCenter()
        {
            int viewportCenterX = GraphicsDevice.Viewport.Width / 2;
            int viewportCenterY = GraphicsDevice.Viewport.Height / 2;

            var bounds = Window.ClientBounds;
            if (bounds.Width > 0 && bounds.Height > 0)
            {
                return UiInput.ToClient(new Point(viewportCenterX, viewportCenterY), Window, GraphicsDevice);
            }

            return new Point(viewportCenterX, viewportCenterY);
        }

        private float SpawnWarmupProgress =>
            Math.Clamp(1f - (_spawnWarmupRemaining / SpawnWarmupSeconds), 0f, 1f);

        private void HandleFocusGained()
        {
            _inactiveTimer = 0f;
            if (ShouldCaptureMouse())
            {
                ApplyMouseCapture();
                InputDebugTrace.Log("FOCUS_GAINED recaptured mouse");
            }
        }

        private void HandleFocusLost()
        {
            if (ShouldCaptureMouse())
            {
                SdlMouseCapture.DisableRelativeMode();
                IsMouseVisible = true;
                InputDebugTrace.Log("FOCUS_LOST disabled relative mouse");
            }
        }

        private bool CanProcessGameplayMouse() => _isMouseLocked && ShouldCaptureMouse();

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
            var blockTerrainEffect = new BlockTerrainEffect(GraphicsDevice, _atlasTexture!);
            _blockTerrainEffect = blockTerrainEffect;
            _skyEffect = SkyEffect.Create(GraphicsDevice);

            _renderer = new Renderer(GraphicsDevice, _atlasTexture!, _whiteTexture, blockTerrainEffect, _skyEffect);
            BlockAtlas.UseCpuBlockVariation = true;
            _ui = new UiRenderer(GraphicsDevice, _whiteTexture);
            _saveSlotScreen = new SaveSlotScreen(_ui);
            _mainMenuSettingsScreen = new MainMenuSettingsScreen(_ui);
            _newWorldSetupScreen = new NewWorldSetupScreen(_ui);
            _loadingScreen = new LoadingScreen(_session.Grid, GraphicsDevice, _ui);
            _devConsole = new DevConsole(_ui);
            _pauseMenu = new PauseMenuScreen(_ui);
            _pauseMenu.SetRenderDistance(_settings.RenderDistance);
            _pauseMenu.RenderDistanceChanged += distance => SetRenderDistance(distance);
            _pauseMenu.MuteAudioChanged += mute => ApplyMuteAudio(mute);
            _pauseMenu.ApplyAudioSettings(_settings);
            _deathScreen = new DeathScreen(_ui);
            _crucibleScreen = new CrucibleScreen(_ui);
            _journalScreen = new JournalScreen(_ui);
            _villageAiOrchestrator = new VillageAiOrchestrator(settings: _settings);
            _villageScreen = new VillageScreen(_ui, _session.Villagers);
            _villageChatScreen = new VillageChatScreen(_ui, _villageAiOrchestrator);

            _audio = new AudioManager(enabled: true);
            _audio.Initialize();
            _audio.ApplySettings(_settings);
            _session.BindAudio(_audio);
            UpdateMusicForState(_state);

            if (_skipMenu)
            {
                PrepareNewWorldSettlement();
                StartWorldLoading();
            }

            _prevMouseState = GetUiMouseState();
            _prevKbState = Keyboard.GetState();
            _screenFade.BeginFadeIn(0.25f);
            _prevState = _state;
        }

        private void PrepareNewWorldSettlement()
        {
            var (spawnX, spawnZ) = _session.Villages.InitializeStarterSettlement(_session.Grid, _worldSpawnX, _worldSpawnZ);
            _worldSpawnX = spawnX;
            _worldSpawnZ = spawnZ;
            _session.PlacePlayerOnSurface(_worldSpawnX, _worldSpawnZ);
            SyncCameraFromPlayer();
            _session.ShowVillageHint = true;
            _session.Crafting.ShowCraftingHint = false;
        }

        private void EnterPlaying()
        {
            CloseAllGameplayOverlays();

            bool fromSave = _loadingFromSave;
            if (!fromSave)
            {
                _session.ShowVillageHint = true;
                _session.Crafting.ShowCraftingHint = false;
            }
            else
            {
                SyncCameraFromPlayer();
            }

            SyncCameraFromPlayer();

            _loadingFromSave = false;
            _spawnWarmupRemaining = SpawnWarmupSeconds;
            _deferAgentServerStart = true;

            _session.BlockInteraction.BindAnimator(_session.InteractionAnimator);

            if (!fromSave)
            {
                _session.HudToast.Show("Welcome to Founder's Hamlet — press V to manage your settlement");
            }

            _isMouseLocked = true;
            _inactiveTimer = 0f;
            ApplyMouseCapture();
            TryActivateWindow();
            Window.Title = "Autonocraft | Playing";
            TryStartAgentServer();

            Console.WriteLine("[Game] World ready — WASD move, mouse look, V village, Esc pause.");
            InputDebugTrace.Log($"ENTER_PLAYING warmup={SpawnWarmupSeconds}s active={IsActive} mouseLocked={_isMouseLocked}");
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

            PrepareNewWorldSettlement();
            StartWorldLoading();
        }

        private void OpenNewWorldSetup()
        {
            _newWorldSetupScreen!.Reset();
            _state = GameState.NewWorldSetup;
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
                _saveSlotScreen?.SetLoadError($"Failed to load save: {ex.Message}");
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
                _loadingScreen = new LoadingScreen(_session.Grid, GraphicsDevice, _ui);
            }
        }

        private void StartWorldLoading()
        {
            _loadingScreen!.Begin(_camera.Position, _settings.RenderDistance, _pendingSaveData);
            _state = GameState.WorldLoading;
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
            if (_state != GameState.Playing)
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
                    if (_state == GameState.Playing)
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
            _mouseLockedBeforePause = _isMouseLocked;
            PrepareMouseForUi();
            _pauseMenu!.Open();
            _pauseFade.BeginFadeInSlideUp(0.2f, 12f);
            Window.Title = "Autonocraft | Paused";
        }

        private void ClosePauseMenu()
        {
            _pauseMenu!.Close();
            _pauseFade.SnapVisible();
            _isMouseLocked = _mouseLockedBeforePause;
            if (_isMouseLocked)
            {
                ApplyMouseCapture();
            }
            else
            {
                IsMouseVisible = true;
            }
        }

        private void OpenDeathScreen()
        {
            _mouseLockedBeforeDeath = _isMouseLocked;
            _isMouseLocked = false;
            ReleaseMouseCapture();
            IsMouseVisible = true;
            _pauseMenu!.Close();
            _pauseFade.SnapVisible();
            _deathScreen!.Open();
            _deathFade.BeginFadeInSlideUp(0.25f, 16f);
            Window.Title = "Autonocraft | You Died";
        }

        private void CloseDeathScreen()
        {
            _deathScreen!.Close();
            _deathFade.SnapVisible();
            _isMouseLocked = _mouseLockedBeforeDeath;
            if (_isMouseLocked)
            {
                ApplyMouseCapture();
            }
            else
            {
                IsMouseVisible = true;
            }
        }

        private void ReturnToMainMenu()
        {
            CloseAllGameplayOverlays();
            SaveWorld(sync: true);
            _pauseFade.SnapVisible();
            _deathFade.SnapVisible();
            _saveSlotScreen!.RefreshSlots();
            ReleaseMouseCapture();
            IsMouseVisible = true;
            _isMouseLocked = false;
            _state = GameState.MainMenu;
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

            if (_state != _prevState)
            {
                if (_prevState == GameState.Playing && _state != GameState.Playing)
                {
                    _isMouseLocked = false;
                    ReleaseMouseCapture();
                    IsMouseVisible = true;
                    InputDebugTrace.Log($"LEFT_PLAYING -> {_state}, cursor released");
                }

                _screenFade.BeginFadeIn(0.25f);
                UpdateMusicForState(_state);
                _prevState = _state;
            }

            _audio?.Update(deltaTime, _state, _session.Grid, _session.Player);
            _audio?.SetDucked(ShouldDuckAudio());

            _screenFade.Update(deltaTime);
            _pauseFade.Update(deltaTime);
            _deathFade.Update(deltaTime);

            var kbState = Keyboard.GetState();
            var mouseState = GetUiMouseState();

            if (IsActive && !_wasActive)
            {
                HandleFocusGained();
            }
            else if (!IsActive && _wasActive)
            {
                HandleFocusLost();
            }

            if (!IsActive && _state == GameState.Playing && _isMouseLocked)
            {
                _inactiveTimer += deltaTime;
                if (_inactiveTimer >= FocusLossReleaseDelay)
                {
                    SdlMouseCapture.DisableRelativeMode();
                    IsMouseVisible = true;
                }
            }
            else
            {
                if (IsActive && _inactiveTimer >= FocusLossReleaseDelay && ShouldCaptureMouse())
                {
                    ApplyMouseCapture();
                }

                _inactiveTimer = 0f;
            }

            _wasActive = IsActive;

            switch (_state)
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

            _prevKbState = kbState;
            if (_deferPrevMouseReset)
            {
                _prevMouseState = GetUiMouseState();
                _deferPrevMouseReset = false;
            }
            else
            {
                _prevMouseState = mouseState;
            }

            base.Update(gameTime);
        }

        private void UpdateMainMenu(KeyboardState kbState, MouseState mouseState, float deltaTime)
        {
            if (_mainMenuSettingsOpen)
            {
                _mainMenuSettingsScreen!.Update(
                    GraphicsDevice.Viewport,
                    kbState,
                    _prevKbState,
                    mouseState,
                    _prevMouseState,
                    deltaTime);

                if (_mainMenuSettingsScreen.SaveRequested)
                {
                    ApplyGameSettings(_mainMenuSettingsScreen.GetWorkingCopy());
                    _mainMenuSettingsScreen.Close();
                    _mainMenuSettingsOpen = false;
                }
                else if (_mainMenuSettingsScreen.CancelRequested)
                {
                    _mainMenuSettingsScreen.Close();
                    _mainMenuSettingsOpen = false;
                }

                Window.Title = "Autonocraft | Settings";
                return;
            }

            _saveSlotScreen!.Update(GraphicsDevice.Viewport, kbState, mouseState, _prevKbState, _prevMouseState, deltaTime);

            if (_saveSlotScreen.NewWorldRequested)
            {
                OpenNewWorldSetup();
            }
            else if (_saveSlotScreen.LoadRequested && _saveSlotScreen.SelectedSlotId != null)
            {
                TryStartLoadedWorld(_saveSlotScreen.SelectedSlotId);
            }
            else if (_saveSlotScreen.SettingsRequested)
            {
                _mainMenuSettingsScreen!.Open(_settings);
                _mainMenuSettingsOpen = true;
            }
            else if (_saveSlotScreen.QuitRequested)
            {
                Exit();
            }

            Window.Title = "Autonocraft | Main Menu";
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
            _settings.Clamp();
            GameSettingsManager.Save(_settings);
            _audio?.ApplySettings(_settings);

            if (_settings.RenderDistance != previousRenderDistance)
            {
                SetRenderDistance(_settings.RenderDistance);
            }
            else
            {
                _pauseMenu?.SetRenderDistance(_settings.RenderDistance);
            }

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

        private bool ShouldDuckAudio()
        {
            if (_state != GameState.Playing)
            {
                return _mainMenuSettingsOpen;
            }

            return _pauseMenu?.IsOpen == true
                || _deathScreen?.IsOpen == true
                || _devConsole?.IsOpen == true
                || _villageScreen?.IsOpen == true
                || _villageChatScreen?.IsOpen == true
                || _session.Crafting.Crucible.IsOpen
                || _session.Crafting.IsJournalUiBlocking
                || _mainMenuSettingsOpen;
        }

        private void RecreateVillageAi()
        {
            if (_runTests || _ui == null)
            {
                return;
            }

            _villageAiOrchestrator = new VillageAiOrchestrator(settings: _settings);
            _villageChatScreen = new VillageChatScreen(_ui, _villageAiOrchestrator);
        }

        private void UpdateNewWorldSetup(KeyboardState kbState, MouseState mouseState, float deltaTime)
        {
            _newWorldSetupScreen!.Update(GraphicsDevice.Viewport, kbState, mouseState, _prevKbState, _prevMouseState, deltaTime);

            if (_newWorldSetupScreen.CreateRequested)
            {
                StartNewWorld(_newWorldSetupScreen.SelectedSeed, _newWorldSetupScreen.SelectedWorldType);
            }
            else if (_newWorldSetupScreen.BackRequested)
            {
                _state = GameState.MainMenu;
                Window.Title = "Autonocraft | Main Menu";
            }
        }

        private void UpdateWorldLoading(float deltaTime, KeyboardState kbState, MouseState mouseState)
        {
            _loadingScreen!.Update(deltaTime);

            if (_loadingScreen.HasTimedOut)
            {
                Console.WriteLine($"[Load] World loading timed out: {_loadingScreen.TimeoutReason}");
                _saveSlotScreen?.SetLoadError(_loadingScreen.TimeoutReason ?? "World failed to load.");
                ReturnToMainMenu();
                return;
            }

            if (_loadingScreen.IsComplete)
            {
                EnterPlaying();
                _state = GameState.Playing;
            }
            else
            {
                Window.Title = $"Autonocraft | Loading World... {(_loadingScreen.Progress * 100f):F0}%";
            }
        }

        private bool UpdateBlockingGameplayOverlays(
            float deltaTime,
            KeyboardState kbState,
            MouseState mouseState)
        {
            if (_devConsole!.IsOpen)
            {
                EnsureUiPointerMode();
                _devConsole.Update(GraphicsDevice.Viewport, kbState, _prevKbState, _hostContext);
                return true;
            }

            if (_deathScreen!.IsOpen)
            {
                EnsureUiPointerMode();
                _deathScreen.Update(GraphicsDevice.Viewport, kbState, mouseState, _prevKbState, _prevMouseState, deltaTime);

                if (_deathScreen.RespawnRequested)
                {
                    CombatSystem.RespawnPlayer(_session.Grid, _session.Player, _worldSpawnX, _worldSpawnZ);
                    CloseDeathScreen();
                }
                else if (_deathScreen.MainMenuRequested)
                {
                    ReturnToMainMenu();
                }

                return true;
            }

            if (_pauseMenu!.IsOpen)
            {
                EnsureUiPointerMode();
                _pauseMenu.Update(GraphicsDevice.Viewport, kbState, mouseState, _prevKbState, _prevMouseState, deltaTime);

                if (_pauseMenu.ResumeRequested)
                {
                    ClosePauseMenu();
                }
                else if (_pauseMenu.SaveNowRequested)
                {
                    SaveWorld(sync: true);
                    _session.HudToast.Show("World saved");
                }
                else if (_pauseMenu.MainMenuRequested)
                {
                    ReturnToMainMenu();
                }
                else if (_pauseMenu.QuitRequested)
                {
                    QuitFromPauseMenu();
                }

                return true;
            }

            if (_villageChatScreen!.IsOpen)
            {
                EnsureUiPointerMode();
                _villageChatScreen.Update(GraphicsDevice.Viewport, _session, kbState, _prevKbState, mouseState, _prevMouseState, deltaTime);
                return true;
            }

            if (_villageScreen!.IsOpen)
            {
                EnsureUiPointerMode();
                if ((kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && !_prevKbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
                    || (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.V) && !_prevKbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.V)))
                {
                    CloseVillageUi();
                    return true;
                }

                _villageScreen.Update(GraphicsDevice.Viewport, kbState, mouseState, _prevKbState, _prevMouseState);
                if (_villageScreen.CloseRequested)
                {
                    CloseVillageUi();
                }
                else
                {
                    HandleVillageScreenActions();
                }

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

                bool closeJournal = (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.J) && !_prevKbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.J))
                    || (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && !_prevKbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape));

                if (closeJournal && (_session.Crafting.JournalOpen || _session.Crafting.JournalTransition.Alpha > 0.01f))
                {
                    _session.Crafting.CloseJournal();
                    _isMouseLocked = _mouseLockedBeforeCrafting;
                    if (_isMouseLocked)
                    {
                        ApplyMouseCapture();
                    }
                    else
                    {
                        IsMouseVisible = true;
                    }
                }

                return true;
            }

            return false;
        }

        private void HandleVillageScreenActions()
        {
            var village = _session.Villages.GetPrimaryVillage() ?? _session.Villages.GetVillageAt(_session.Player.Position);
            if (village == null)
            {
                return;
            }

            if (_villageScreen!.RecruitRequested)
            {
                _session.Villages.TryRecruit(village);
            }
            else if (_villageScreen.ClaimRequested)
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
            else if (_villageScreen.RequestedBlueprintId != null)
            {
                int ax = village.AnchorX + 6;
                int az = village.AnchorZ + 6;
                var payer = village.Storage.HasSpaceFor(ItemStack.CreateBlock(BlockType.Dirt, 1))
                    ? (IItemContainer)village.Storage
                    : WrapPlayerHotbar();
                _session.Villages.TryQueueBlueprint(
                    _session.Grid,
                    village,
                    _villageScreen.RequestedBlueprintId,
                    ax,
                    az,
                    payer);
            }
            else if (_villageScreen.RequestedAssignVillagerId >= 0 &&
                     _session.Villagers.TryGet(_villageScreen.RequestedAssignVillagerId, out var villager))
            {
                _session.Villages.TryAssignJob(village, villager, _villageScreen.RequestedAssignJob);
            }
        }

        private void TryStartAgentServer()
        {
            if (_agentServerStarted || !_deferAgentServerStart)
            {
                return;
            }

            _deferAgentServerStart = false;

            try
            {
                AgentHttpServer.Start(this, _agentPort);
                _agentServerStarted = true;
                if (!RuntimeMetrics.FileLoggingEnabled)
                {
                    RuntimeMetrics.EnableFileLogging(fromCli: false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Agent] HTTP server failed to start: {ex.Message}");
            }
        }

        private void UpdateGameplay(float deltaTime, KeyboardState kbState, MouseState mouseState)
        {
            // Avoid one slow streaming frame turning into a giant physics/input step.
            deltaTime = Math.Min(deltaTime, MaxGameplayDeltaTime);

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
        }

        private void UpdateGameplayCore(float deltaTime, KeyboardState kbState, MouseState mouseState)
        {
            while (_pendingActions.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { Console.WriteLine($"[Agent Action Error] {ex.Message}"); }
            }

            if (UpdateBlockingGameplayOverlays(deltaTime, kbState, mouseState))
            {
                return;
            }

            bool consoleToggle = (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F3) && !_prevKbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F3))
                || (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.OemTilde) && !_prevKbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.OemTilde));

            if (consoleToggle)
            {
                _devConsole!.Toggle();
                if (_devConsole.IsOpen)
                {
                    _mouseLockedBeforeConsole = _isMouseLocked;
                    PrepareMouseForUi();
                }
                else
                {
                    _isMouseLocked = _mouseLockedBeforeConsole;
                    if (_isMouseLocked)
                    {
                        ApplyMouseCapture();
                    }
                    else
                    {
                        IsMouseVisible = true;
                    }
                }
            }

            if (_devConsole!.IsOpen)
            {
                _devConsole.Update(GraphicsDevice.Viewport, kbState, _prevKbState, _hostContext);
                return;
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.V) && !_prevKbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.V) && _session.Player.IsAlive)
            {
                if (_villageScreen!.IsOpen)
                {
                    CloseVillageUi();
                }
                else
                {
                    OpenVillageUi();
                }

                return;
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.C) && !_prevKbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.C) && _session.Player.IsAlive)
            {
                OpenVillageChatUi();
                return;
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.J) && !_prevKbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.J) && _session.Player.IsAlive)
            {
                _mouseLockedBeforeCrafting = _isMouseLocked;
                PrepareMouseForUi();
                _session.Crafting.ToggleJournal();
                return;
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && !_prevKbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && _session.Player.IsAlive)
            {
                OpenPauseMenu();
                return;
            }

            // Keyboard slot selections (D1-D9)
            for (int i = 0; i < 9; i++)
            {
                var k = (Microsoft.Xna.Framework.Input.Keys)((int)Microsoft.Xna.Framework.Input.Keys.D1 + i);
                if (kbState.IsKeyDown(k) && !_prevKbState.IsKeyDown(k))
                {
                    _session.Player.SelectedSlot = i;
                    var stack = _session.Player.Hotbar[i];
                    Console.WriteLine($"[Selection] Selected Hotbar Slot {i + 1}: {FormatHotbarStack(stack)}");
                }
                var nk = (Microsoft.Xna.Framework.Input.Keys)((int)Microsoft.Xna.Framework.Input.Keys.NumPad1 + i);
                if (kbState.IsKeyDown(nk) && !_prevKbState.IsKeyDown(nk))
                {
                    _session.Player.SelectedSlot = i;
                    var stack = _session.Player.Hotbar[i];
                    Console.WriteLine($"[Selection] Selected Hotbar Slot {i + 1}: {FormatHotbarStack(stack)}");
                }
            }

            // Toggle physics mode
            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.G) && !_prevKbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.G))
            {
                _session.Player.FlyingMode = !_session.Player.FlyingMode;
                _session.Player.Velocity = Vector3.Zero;
                Console.WriteLine($"[Mode] Toggled FlyingMode to: {_session.Player.FlyingMode}");
            }

            // Mouse interactions when locked (not gated on IsActive — macOS can flicker inactive during chunk hitches).
            bool leftHeld = false;
            bool leftPressed = false;
            bool rightPressed = false;
            bool shiftRightPressed = false;

            float mouseDx = 0f;
            float mouseDy = 0f;
            int centerX = 0;
            int centerY = 0;

            if (CanProcessGameplayMouse())
            {
                var clientCenter = GetMouseClientCenter();
                centerX = clientCenter.X;
                centerY = clientCenter.Y;

                if (!_skipMouseLookFrame)
                {
                    if (SdlMouseCapture.TryGetRelativeDelta(out int relativeDx, out int relativeDy))
                    {
                        mouseDx = relativeDx;
                        mouseDy = relativeDy;
                    }
                    else
                    {
                        mouseDx = mouseState.X - _prevMouseState.X;
                        mouseDy = mouseState.Y - _prevMouseState.Y;
                        if (Math.Abs(mouseDx) > MouseWarpRejectThreshold || Math.Abs(mouseDy) > MouseWarpRejectThreshold)
                        {
                            mouseDx = 0f;
                            mouseDy = 0f;
                        }
                    }

                    if (mouseDx != 0f || mouseDy != 0f)
                    {
                        _camera.Yaw += mouseDx * 0.15f;
                        _camera.Pitch = Math.Clamp(_camera.Pitch - mouseDy * 0.15f, -89f, 89f);

                        _session.Player.Yaw = _camera.Yaw;
                        _session.Player.Pitch = _camera.Pitch;
                    }
                }
                else
                {
                    SdlMouseCapture.DrainRelativeDelta();
                    _skipMouseLookFrame = false;
                }

                leftHeld = mouseState.LeftButton == ButtonState.Pressed;
                leftPressed = leftHeld && _prevMouseState.LeftButton == ButtonState.Released;
                rightPressed = mouseState.RightButton == ButtonState.Pressed && _prevMouseState.RightButton == ButtonState.Released;
                bool shiftHeld = kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftShift)
                    || kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.RightShift);
                shiftRightPressed = rightPressed && shiftHeld;
                if (shiftHeld)
                {
                    rightPressed = false;
                }

                int scrollDelta = mouseState.ScrollWheelValue - _prevMouseState.ScrollWheelValue;
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
            if (_spawnWarmupRemaining <= 0f || SpawnWarmupProgress >= 0.5f)
            {
                _session.Grid.Fluids.Update(_session.Grid, deltaTime, GraphicsDevice);
            }

            if (_session.Player.IsAlive)
            {
                Vector3 front = _camera.Front;
                Vector3 right = _camera.Right;
                Vector3 frontHorizontal = Vector3.Normalize(new Vector3(front.X, 0f, front.Z));
                Vector3 rightHorizontal = Vector3.Normalize(new Vector3(right.X, 0f, right.Z));

                Vector3 moveDir = Vector3.Zero;
                if (IsKeyPressed(kbState, Key.W)) moveDir += frontHorizontal;
                if (IsKeyPressed(kbState, Key.S)) moveDir -= frontHorizontal;
                if (IsKeyPressed(kbState, Key.A)) moveDir -= rightHorizontal;
                if (IsKeyPressed(kbState, Key.D)) moveDir += rightHorizontal;

                if (_session.Player.FlyingMode)
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

                _prevSpacePressed = IsKeyPressed(kbState, Key.Space);
                _session.UpdateMovementAudio(deltaTime, moveDir);
                _session.InteractionAnimator.Update(deltaTime, _session.Player);
                _session.Particles.Update(deltaTime);

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
                        leftHeld,
                        leftPressed,
                        solidRayHit);

                    _session.BlockInteraction.Update(
                        deltaTime,
                        _session.Grid,
                        _session.Player,
                        _camera.Position,
                        _camera.Front,
                        leftHeld && !_session.Combat.BlocksMiningThisFrame,
                        rightPressed,
                        shiftRightPressed,
                        _session.Crafting,
                        _session.Particles,
                        GraphicsDevice,
                        solidRayHit);

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
                if (!_deathScreen.IsOpen)
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
            int terrainPerFrame = inSpawnWarmup
                ? Math.Max(1, (int)MathF.Round(1f + (VoxelWorld.DefaultTerrainChunksPerFrame - 1f) * warmup))
                : VoxelWorld.DefaultTerrainChunksPerFrame;
            int meshPerFrame = inSpawnWarmup
                ? Math.Max(1, (int)MathF.Round(1f + (VoxelWorld.DefaultMeshChunksPerFrame - 1f) * warmup))
                : VoxelWorld.DefaultMeshChunksPerFrame;

            _session.DeferAmbientSpawns = inSpawnWarmup && warmup < 0.7f;
            _session.UpdateChunks(GraphicsDevice, _camera.Position, _settings.RenderDistance, terrainPerFrame, meshPerFrame);

            if (!inSpawnWarmup || warmup >= 0.75f)
            {
                _session.UpdateAnimals(deltaTime);
            }

            if (!inSpawnWarmup || warmup >= 0.6f)
            {
                _session.UpdateVillages(deltaTime, _timeOfDay);
                // TryFindClaimableStructure is expensive — poll at most every 10 s; GameSession skips if player barely moved.
                _claimHintTimer += deltaTime;
                if (_claimHintTimer >= 10f)
                {
                    _claimHintTimer = 0f;
                    _session.UpdateNearbyClaimHint();
                }
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
                string modeStr = _session.Player.FlyingMode ? "FLY" : "PHYSICS";
                string groundedStr = _session.Player.IsGrounded ? "Grounded" : "Airborne";
                Window.Title = $"Autonocraft | {modeStr} ({groundedStr}) | Chunks: {_session.Grid.ActiveChunkCount} | Mesh: {PerfCounters.MeshBuildMs:F1}ms | Pending: {PerfCounters.PendingMeshCount}{(_session.Player.FlyingMode ? " | FLY" : "")}";
            }

            string? overlayName = null;
            if (_villageScreen?.IsOpen == true) overlayName = "village";
            else if (_pauseMenu?.IsOpen == true) overlayName = "pause";
            else if (_devConsole?.IsOpen == true) overlayName = "console";

            InputDebugTrace.TickGameplay(
                deltaTime,
                _state,
                IsActive,
                _isMouseLocked,
                ShouldCaptureMouse(),
                _skipMouseLookFrame,
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

            RuntimeMetrics.RecordFrame(
                deltaTime,
                _state,
                _session.Grid.ActiveChunkCount,
                PerfCounters.PendingMeshCount,
                PerfCounters.MeshBuildMs,
                _spawnWarmupRemaining);
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
            switch (_state)
            {
                case GameState.MainMenu:
                    _saveSlotScreen!.Draw(GraphicsDevice.Viewport, _screenFade.Alpha, _screenFade.OffsetY);
                    if (_mainMenuSettingsOpen)
                    {
                        _mainMenuSettingsScreen!.Draw(GraphicsDevice.Viewport);
                    }
                    break;
                case GameState.NewWorldSetup:
                    _newWorldSetupScreen!.Draw(GraphicsDevice.Viewport);
                    break;
                case GameState.WorldLoading:
                    _loadingScreen!.Draw(GraphicsDevice.Viewport, _screenFade.Alpha, _screenFade.OffsetY);
                    break;
                case GameState.Playing:
                    if (HasBlockingGameplayOverlay())
                    {
                        _ui!.DrawFullscreenBackground(new Microsoft.Xna.Framework.Color(0.02f, 0.03f, 0.06f) * 0.92f);
                    }
                    else
                    {
                        // Water animation via atlas.SetData causes GPU stalls on macOS — use static tile.
                        _renderer?.Draw(_session.PrepareRenderContext(_camera, _timeOfDay, _waterAnimTime, _settings.RenderDistance));
                    }

                    _crucibleScreen?.Draw(GraphicsDevice.Viewport, _session.Crafting.Crucible, _session.Crafting.GetCurrentEnvironment(_session.Grid, _timeOfDay), _atlasTexture);
                    if (_session.Crafting.ShouldDrawJournal())
                    {
                        var journalFade = _session.Crafting.JournalTransition;
                        _journalScreen?.Draw(
                            GraphicsDevice.Viewport,
                            _session.Crafting.Journal,
                            _session.Player.Skills,
                            journalFade.Alpha,
                            journalFade.OffsetY);
                    }

                    _villageScreen?.Draw(GraphicsDevice.Viewport);
                    _villageChatScreen?.Draw(GraphicsDevice.Viewport);
                    _devConsole?.Draw(GraphicsDevice.Viewport);
                    _pauseMenu?.Draw(GraphicsDevice.Viewport, _pauseFade.Alpha, _pauseFade.OffsetY);
                    _deathScreen?.Draw(GraphicsDevice.Viewport, _deathFade.Alpha, _deathFade.OffsetY);
                    break;
            }

            base.Draw(gameTime);
        }

        public void OpenCrucibleAt(int x, int y, int z, BlockType stationType)
        {
            _mouseLockedBeforeCrafting = _isMouseLocked;
            PrepareMouseForUi();
            _session.Crafting.OpenCrucible(x, y, z, stationType);
        }

        private void CloseCrucibleUi()
        {
            _session.Crafting.CloseCrucible();
            _isMouseLocked = _mouseLockedBeforeCrafting;
            if (_isMouseLocked)
            {
                ApplyMouseCapture();
            }
            else
            {
                IsMouseVisible = true;
            }
        }

        private void OpenVillageUi()
        {
            var village = _session.Villages.GetPrimaryVillage() ?? _session.Villages.GetVillageAt(_session.Player.Position);
            if (village == null)
            {
                if (_session.Villages.TryFindClaimableStructure(
                        _session.Grid,
                        _session.Player.Position,
                        24f,
                        out int ax,
                        out int az,
                        out _))
                {
                    if (!_session.Villages.TryClaimStructure(_session.Grid, ax, az, out village))
                    {
                        return;
                    }
                }
                else
                {
                    int fx = (int)MathF.Floor(_session.Player.Position.X);
                    int fz = (int)MathF.Floor(_session.Player.Position.Z);
                    if (_session.Villages.TryFoundVillage(_session.Grid, "New Settlement", fx, fz, out village))
                    {
                        _session.Villages.TryQueueBlueprint(_session.Grid, village!, "town_heart", fx, fz, new PlayerHotbarAdapter(_session.Player));
                    }
                    else
                    {
                        _session.HudToast.Show("Could not found village here.");
                        return;
                    }
                }
            }

            _mouseLockedBeforeVillageUi = _isMouseLocked;
            PrepareMouseForUi();
            TryActivateWindow();
            _villageScreen!.Open(
                village!,
                _session.Villages,
                _session.Grid,
                _session.Player.Position,
                new PlayerHotbarAdapter(_session.Player));
        }

        private IItemContainer WrapPlayerHotbar() => new PlayerHotbarAdapter(_session.Player);

        private void CloseVillageUi()
        {
            _villageScreen!.Close();
            _isMouseLocked = _mouseLockedBeforeVillageUi;
            if (_isMouseLocked)
            {
                ApplyMouseCapture();
            }
            else
            {
                IsMouseVisible = true;
            }
        }

        private void OpenVillageChatUi()
        {
            if (!_settings.PlayWithAi || _settings.AiProvider == AiProviderKind.Disabled)
            {
                _session.HudToast.Show("Village AI is off. Enable it in main menu Settings.");
                return;
            }

            var village = _session.Villages.GetPrimaryVillage() ?? _session.Villages.GetVillageAt(_session.Player.Position);
            if (village == null)
            {
                _session.HudToast.Show("No village nearby. Press V to found one.");
                return;
            }

            var nearest = _session.Villagers.GetNearest(_session.Player.Position, 8f);
            string target = nearest != null ? nearest.Id.ToString() : "mayor";
            _mouseLockedBeforeVillageUi = _isMouseLocked;
            PrepareMouseForUi();
            _villageChatScreen!.Open(village, target, nearest == null);
        }

        private sealed class PlayerHotbarAdapter : IItemContainer
        {
            private readonly Player _player;
            public PlayerHotbarAdapter(Player player) => _player = player;
            public int SlotCount => _player.Hotbar.Length;
            public ItemStack GetSlot(int index) => _player.Hotbar[index];
            public void SetSlot(int index, ItemStack stack) => _player.Hotbar[index] = stack;
            public bool AddItem(ItemStack item) { _player.AddItem(item); return true; }
            public bool TryConsumeBlock(BlockType blockType, int count)
            {
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
            _crucibleScreen!.Update(
                GraphicsDevice.Viewport,
                _session.Crafting.Crucible,
                env,
                kbState,
                mouseState,
                _prevKbState,
                _prevMouseState,
                deltaTime);

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && !_prevKbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
            {
                CloseCrucibleUi();
                return;
            }

            if (_crucibleScreen.ClickedOrbIndex >= 0)
            {
                if (_crucibleScreen.RightClickedOrb)
                {
                    if (_session.Crafting.Crucible.WithdrawToHotbar(_session.Player, _crucibleScreen.ClickedOrbIndex))
                    {
                        _crucibleScreen.TriggerOrbPulse(_crucibleScreen.ClickedOrbIndex);
                    }
                }
                else
                {
                    if (_session.Crafting.Crucible.DepositFromHotbar(_session.Player, _crucibleScreen.ClickedOrbIndex))
                    {
                        _crucibleScreen.TriggerOrbPulse(_crucibleScreen.ClickedOrbIndex);
                    }
                    else
                    {
                        var slot = _session.Player.GetSelectedStack();
                        if (!slot.IsBlock())
                        {
                            _crucibleScreen.SetStatus("SELECT A BLOCK IN HOTBAR");
                        }
                        else
                        {
                            _crucibleScreen.SetStatus("ORB SLOT FULL");
                        }
                    }
                }
            }

            if (_crucibleScreen.TransmuteRequested)
            {
                _crucibleScreen.BeginTransmuteAnimation();
            }

            if (_crucibleScreen.TransmuteReady)
            {
                var result = _session.Crafting.TryTransmute(_session.Grid, _session.Player, _timeOfDay);
                if (result.Succeeded)
                {
                    _crucibleScreen.SetStatus($"CREATED {result.Recipe!.Output}");
                    _session.BlockInteraction.TriggerCrosshairFlash();
                    _session.Particles.SpawnHint(_session.Player.Position + new Vector3(0f, Player.EyeHeight, 0f));
                }
                else
                {
                    _crucibleScreen.SetStatus(result.Message.ToUpperInvariant());
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

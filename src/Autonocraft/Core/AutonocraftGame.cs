using System;
using System.IO;
using System.Numerics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.Entities;
using Autonocraft.World;
using Autonocraft.UI;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace Autonocraft.Core
{
    public class AutonocraftGame : Game
    {
        private readonly GraphicsDeviceManager _graphics;
        private readonly Camera _camera;
        private VoxelWorld _grid;
        private Player _player;
        private AnimalManager _animals;

        // Graphics resources
        private Texture2D? _atlasTexture;
        private Texture2D? _whiteTexture;
        private Renderer? _renderer;
        private UiRenderer? _ui;
        private BlockInteractionSystem _blockInteraction = new BlockInteractionSystem();
        private CombatSystem _combatSystem = new CombatSystem();
        private readonly UiTransition _screenFade = new UiTransition();
        private readonly UiTransition _pauseFade = new UiTransition();
        private readonly UiTransition _deathFade = new UiTransition();
        private GameState _prevState = GameState.MainMenu;
        private SaveSlotScreen? _saveSlotScreen;
        private NewWorldSetupScreen? _newWorldSetupScreen;
        private LoadingScreen? _loadingScreen;
        private DevConsole? _devConsole;
        private PauseMenuScreen? _pauseMenu;
        private DeathScreen? _deathScreen;
        private bool _mouseLockedBeforeConsole;
        private bool _mouseLockedBeforePause;
        private bool _mouseLockedBeforeDeath;

        // Game flow
        private GameState _state = GameState.MainMenu;
        private bool _skipMenu;
        private bool _agentServerStarted;
        private string? _activeSlotId;
        private string? _activeSlotName;
        private WorldSaveData? _pendingSaveData;
        private bool _loadingFromSave;
        private float _autosaveTimer;
        private bool _saveInProgress;

        // Time and cycle parameters
        private readonly GameSettings _settings;
        private const int DefaultSeed = WorldConstants.DefaultSeed;
        private const float AutosaveIntervalSeconds = 300f;
        private float _timeOfDay = 0.3f;
        private float _timeScale = 0.01f;
        private bool _timePaused;

        // Input state tracking
        private KeyboardState _prevKbState;
        private MouseState _prevMouseState;
        private bool _isMouseLocked = true;
        private bool _prevSpacePressed = false;

        // AI Agent simulation state
        private readonly HashSet<Key> _simulatedKeys = new HashSet<Key>();
        private readonly ConcurrentQueue<Action> _pendingActions = new ConcurrentQueue<Action>();
        private readonly bool _runTests;

        // Properties for server/tests access
        public Camera Camera => _camera;
        public VoxelWorld Grid => _grid;
        public Player Player => _player;
        public AnimalManager Animals => _animals;
        public HashSet<Key> SimulatedKeys => _simulatedKeys;
        public ConcurrentQueue<Action> PendingActions => _pendingActions;
        public float TimeOfDay => _timeOfDay;
        public float TimeScale
        {
            get => _timeScale;
            set => _timeScale = Math.Max(0f, value);
        }
        public bool TimePaused
        {
            get => _timePaused;
            set => _timePaused = value;
        }
        public float MoveSpeedOverride
        {
            set => _player.CustomMoveSpeed = value;
        }

        public int RenderDistance => _settings.RenderDistance;
        public BlockInteractionSystem BlockInteraction => _blockInteraction;
        public CombatSystem Combat => _combatSystem;
        public UiTransition ScreenFade => _screenFade;
        public UiTransition PauseFade => _pauseFade;
        public UiTransition DeathFade => _deathFade;

        public void SetRenderDistance(int value)
        {
            _settings.RenderDistance = Math.Clamp(value, GameSettings.MinRenderDistance, GameSettings.MaxRenderDistance);
            GameSettingsManager.Save(_settings);
            _pauseMenu?.SetRenderDistance(_settings.RenderDistance);
            _grid.UpdateChunksAround(GraphicsDevice, _camera.Position, _settings.RenderDistance);
        }

        public void SetTimeOfDay(float value)
        {
            _timeOfDay = value - MathF.Floor(value);
            if (_timeOfDay < 0f) _timeOfDay += 1f;
        }

        public string ExecuteDevCommand(string input) => DevCommands.Execute(this, input);

        public const int DefaultSpawnX = 16;
        public const int DefaultSpawnZ = 16;

        public AutonocraftGame(bool runTests = false, bool skipMenu = false)
        {
            _runTests = runTests;
            _skipMenu = skipMenu;
            if (_skipMenu)
            {
                _state = GameState.WorldLoading;
            }
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "";

            _settings = GameSettingsManager.Load();
            _camera = new Camera();
            _player = CreateDefaultPlayer();
            _grid = new VoxelWorld(DefaultSeed);
            _animals = new AnimalManager(DefaultSeed);
            WireWorldEvents();
            SyncCameraFromPlayer();

            Exiting += OnGameExiting;
        }

        private static Player CreateDefaultPlayer()
        {
            var player = new Player(new Vector3(DefaultSpawnX + 0.5f, 64f, DefaultSpawnZ + 0.5f));
            player.Yaw = -90f;
            player.Pitch = 0f;
            return player;
        }

        private void SyncCameraFromPlayer()
        {
            _camera.Position = _player.Position + new Vector3(0f, Player.EyeHeight, 0f);
            _camera.Yaw = _player.Yaw;
            _camera.Pitch = _player.Pitch;
        }

        protected override void Initialize()
        {
            if (!_runTests)
            {
                _graphics.PreferredBackBufferWidth = 1280;
                _graphics.PreferredBackBufferHeight = 720;
                _graphics.SynchronizeWithVerticalRetrace = true;
                _graphics.ApplyChanges();

                Window.AllowUserResizing = true;
                Window.ClientSizeChanged += OnWindowClientSizeChanged;

                IsMouseVisible = true;
                _isMouseLocked = false;
            }

            base.Initialize();
        }

        private void OnWindowClientSizeChanged(object? sender, EventArgs e)
        {
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

            if (_isMouseLocked && _state == GameState.Playing)
            {
                Mouse.SetPosition(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);
            }
        }

        protected override void LoadContent()
        {
            if (_runTests) return;

            // Re-usable 1x1 white texture for SpriteBatch solid drawing
            _whiteTexture = new Texture2D(GraphicsDevice, 1, 1);
            _whiteTexture.SetData(new[] { Color.White });

            // Load atlas PNG texture
            using (var stream = TitleContainer.OpenStream("atlas.png"))
            {
                _atlasTexture = Texture2D.FromStream(GraphicsDevice, stream);
            }

            // Initialize custom renderer (passing nulls for removed effects/textures)
            _renderer = new Renderer(GraphicsDevice, null, null, _atlasTexture, null, _whiteTexture);
            _ui = new UiRenderer(GraphicsDevice, _whiteTexture);
            _saveSlotScreen = new SaveSlotScreen(_ui);
            _newWorldSetupScreen = new NewWorldSetupScreen(_ui);
            _loadingScreen = new LoadingScreen(_grid, GraphicsDevice, _ui);
            _devConsole = new DevConsole(_ui);
            _pauseMenu = new PauseMenuScreen(_ui);
            _pauseMenu.SetRenderDistance(_settings.RenderDistance);
            _pauseMenu.RenderDistanceChanged += distance => SetRenderDistance(distance);
            _deathScreen = new DeathScreen(_ui);

            if (_skipMenu)
            {
                StartWorldLoading();
            }

            _prevMouseState = Mouse.GetState();
            _prevKbState = Keyboard.GetState();
            _screenFade.BeginFadeIn(0.25f);
            _prevState = _state;
        }

        private void EnterPlaying()
        {
            bool fromSave = _loadingFromSave;
            if (!fromSave)
            {
                PlacePlayerOnSurface();
            }
            else
            {
                SyncCameraFromPlayer();
            }

            _loadingFromSave = false;

            if (!_agentServerStarted)
            {
                AgentHttpServer.Start(this, 5000);
                _agentServerStarted = true;
            }

            IsMouseVisible = false;
            _isMouseLocked = true;
            Mouse.SetPosition(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);

            if (_activeSlotId != null)
            {
                PerformAutosave(sync: true);
            }

            _animals.PopulateAroundSpawn(_grid, DefaultSpawnX, DefaultSpawnZ, _settings.RenderDistance);

            Console.WriteLine("\n==================================================================");
            Console.WriteLine("Autonocraft Voxel Clone (MonoGame SDK) Loaded!");
            Console.WriteLine("==================================================================");
            Console.WriteLine("Controls:");
            Console.WriteLine("- Mouse Look: Rotate camera view.");
            Console.WriteLine("- Escape: Open pause menu (save and return to main menu, or quit).");
            Console.WriteLine("- WASD: Move horizontally.");
            Console.WriteLine("- Space: Fly Up / Left-Shift: Fly Down (in Flying Mode).");
            Console.WriteLine("- Space: Jump (in Gravity Mode).");
            Console.WriteLine("- G: Toggle Gravity/Physics Mode.");
            Console.WriteLine("- Left-Click: Attack animals or mine blocks (up to 5 blocks range).");
            Console.WriteLine("- Right-Click: Place block adjacent to face (up to 5 blocks range).");
            Console.WriteLine("- 1-9: Select hotbar slot / Scroll wheel: Cycle slots.");
            Console.WriteLine("- F3 or `: Toggle developer console.");
            Console.WriteLine("- World auto-saves every 5 minutes and on exit.");
            Console.WriteLine("- Agent HTTP Server listening on http://localhost:5000/");
            Console.WriteLine("==================================================================\n");
        }

        private void PlacePlayerOnSurface()
        {
            var spawnPos = Player.FindSafeSpawnPosition(_grid, DefaultSpawnX, DefaultSpawnZ);
            _player.Position = spawnPos;
            _player.Velocity = Vector3.Zero;
            _player.FlyingMode = false;

            SyncCameraFromPlayer();

            Console.WriteLine($"[Spawn] Placed player on surface at ({spawnPos.X:F1}, {spawnPos.Y:F1}, {spawnPos.Z:F1})");
        }

        private void StartNewWorld(int seed, WorldType worldType)
        {
            string slotName = WorldSaveManager.GenerateDefaultSlotName();
            string slotId = WorldSaveManager.CreateSlotId(slotName);

            _activeSlotId = slotId;
            _activeSlotName = slotName;
            _pendingSaveData = null;
            _loadingFromSave = false;
            _autosaveTimer = 0f;

            var genParams = WorldGenParams.ForType(worldType);
            ResetWorldState(seed, genParams);
            _player = CreateDefaultPlayer();
            SyncCameraFromPlayer();
            SetTimeOfDay(0.3f);
            _timeScale = 0.01f;
            _timePaused = false;

            StartWorldLoading();
        }

        private void OpenNewWorldSetup()
        {
            _newWorldSetupScreen!.Reset();
            _state = GameState.NewWorldSetup;
            Window.Title = "Autonocraft | New World";
        }

        private void StartLoadedWorld(string slotId)
        {
            var save = WorldSaveManager.Load(slotId);

            _activeSlotId = slotId;
            _activeSlotName = save.SlotName;
            _pendingSaveData = save;
            _loadingFromSave = true;
            _autosaveTimer = 0f;

            ResetWorldState(save.Seed, WorldGenParams.ForType(WorldType.Default));
            _player = CreateDefaultPlayer();
            WorldSaveManager.ApplyPlayerSaveData(_player, save.Player);
            SetTimeOfDay(save.Time.TimeOfDay);
            _timeScale = save.Time.TimeScale;
            _timePaused = save.Time.TimePaused;
            SyncCameraFromPlayer();

            StartWorldLoading();
        }

        private void ResetWorldState(int seed, WorldGenParams? parameters = null)
        {
            _grid.Dispose();
            _grid = new VoxelWorld(seed, parameters);
            _animals = new AnimalManager(seed);
            WireWorldEvents();
            if (_ui != null)
            {
                _loadingScreen = new LoadingScreen(_grid, GraphicsDevice, _ui);
            }
        }

        private void WireWorldEvents()
        {
            _grid.ChunksLoaded += coords => _animals.OnChunksLoaded(coords, _grid);
            _grid.ChunksUnloaded += coords => _animals.OnChunksUnloaded(coords);
        }

        private void StartWorldLoading()
        {
            _loadingScreen!.Begin(_camera.Position, _settings.RenderDistance, _pendingSaveData);
            _state = GameState.WorldLoading;
            Window.Title = "Autonocraft | Loading World...";
        }

        private void OnGameExiting(object? sender, EventArgs e)
        {
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
            var saveData = WorldSaveManager.BuildFromGame(slotId, slotName, this, _grid);

            if (sync)
            {
                WorldSaveManager.Save(saveData);
                Console.WriteLine($"[Save] World saved to slot '{slotName}'.");
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
            _isMouseLocked = false;
            IsMouseVisible = true;
            _pauseMenu!.Open();
            _pauseFade.BeginFadeInSlideUp(0.2f, 12f);
            Window.Title = "Autonocraft | Paused";
        }

        private void ClosePauseMenu()
        {
            _pauseMenu!.Close();
            _pauseFade.SnapVisible();
            _isMouseLocked = _mouseLockedBeforePause;
            IsMouseVisible = !_isMouseLocked;
            if (_isMouseLocked)
            {
                Mouse.SetPosition(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);
            }
        }

        private void OpenDeathScreen()
        {
            _mouseLockedBeforeDeath = _isMouseLocked;
            _isMouseLocked = false;
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
            IsMouseVisible = !_isMouseLocked;
            if (_isMouseLocked)
            {
                Mouse.SetPosition(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);
            }
        }

        private void ReturnToMainMenu()
        {
            SaveWorld(sync: true);
            _pauseMenu!.Close();
            _deathScreen!.Close();
            _deathFade.SnapVisible();
            _saveSlotScreen!.RefreshSlots();
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

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_state != _prevState)
            {
                _screenFade.BeginFadeIn(0.25f);
                _prevState = _state;
            }

            _screenFade.Update(deltaTime);
            _pauseFade.Update(deltaTime);
            _deathFade.Update(deltaTime);

            var kbState = Keyboard.GetState();
            var mouseState = Mouse.GetState();

            switch (_state)
            {
                case GameState.MainMenu:
                    UpdateMainMenu(kbState, mouseState, deltaTime);
                    break;
                case GameState.NewWorldSetup:
                    UpdateNewWorldSetup(kbState, mouseState);
                    break;
                case GameState.WorldLoading:
                    UpdateWorldLoading(deltaTime);
                    break;
                case GameState.Playing:
                    UpdateGameplay(deltaTime, kbState, mouseState);
                    break;
            }

            _prevKbState = kbState;
            _prevMouseState = mouseState;

            base.Update(gameTime);
        }

        private void UpdateMainMenu(KeyboardState kbState, MouseState mouseState, float deltaTime)
        {
            _saveSlotScreen!.Update(GraphicsDevice.Viewport, kbState, mouseState, _prevKbState, _prevMouseState, deltaTime);

            if (_saveSlotScreen.NewWorldRequested)
            {
                OpenNewWorldSetup();
            }
            else if (_saveSlotScreen.LoadRequested && _saveSlotScreen.SelectedSlotId != null)
            {
                StartLoadedWorld(_saveSlotScreen.SelectedSlotId);
            }
            else if (_saveSlotScreen.QuitRequested)
            {
                Exit();
            }

            Window.Title = "Autonocraft | Main Menu";
        }

        private void UpdateNewWorldSetup(KeyboardState kbState, MouseState mouseState)
        {
            _newWorldSetupScreen!.Update(GraphicsDevice.Viewport, kbState, mouseState, _prevKbState, _prevMouseState);

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

        private void UpdateWorldLoading(float deltaTime)
        {
            _loadingScreen!.Update(deltaTime);

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

        private void UpdateGameplay(float deltaTime, KeyboardState kbState, MouseState mouseState)
        {
            // Process queued actions on the main thread
            while (_pendingActions.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { Console.WriteLine($"[Agent Action Error] {ex.Message}"); }
            }

            bool consoleToggle = (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F3) && !_prevKbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.F3))
                || (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.OemTilde) && !_prevKbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.OemTilde));

            if (consoleToggle)
            {
                _devConsole!.Toggle();
                if (_devConsole.IsOpen)
                {
                    _mouseLockedBeforeConsole = _isMouseLocked;
                    _isMouseLocked = false;
                    IsMouseVisible = true;
                }
                else
                {
                    _isMouseLocked = _mouseLockedBeforeConsole;
                    IsMouseVisible = !_isMouseLocked;
                    if (_isMouseLocked)
                    {
                        Mouse.SetPosition(GraphicsDevice.Viewport.Width / 2, GraphicsDevice.Viewport.Height / 2);
                    }
                }
            }

            if (_devConsole!.IsOpen)
            {
                _devConsole.Update(GraphicsDevice.Viewport, kbState, _prevKbState, this);
                return;
            }

            if (_deathScreen!.IsOpen)
            {
                _deathScreen.Update(GraphicsDevice.Viewport, kbState, mouseState, _prevKbState, _prevMouseState, deltaTime);

                if (_deathScreen.RespawnRequested)
                {
                    CombatSystem.RespawnPlayer(_grid, _player);
                    CloseDeathScreen();
                }
                else if (_deathScreen.MainMenuRequested)
                {
                    ReturnToMainMenu();
                }

                return;
            }

            if (_pauseMenu!.IsOpen)
            {
                _pauseMenu.Update(GraphicsDevice.Viewport, kbState, mouseState, _prevKbState, _prevMouseState, deltaTime);

                if (_pauseMenu.ResumeRequested)
                {
                    ClosePauseMenu();
                }
                else if (_pauseMenu.MainMenuRequested)
                {
                    ReturnToMainMenu();
                }
                else if (_pauseMenu.QuitRequested)
                {
                    QuitFromPauseMenu();
                }

                return;
            }

            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && !_prevKbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape) && _player.IsAlive)
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
                    _player.SelectedSlot = i;
                    Console.WriteLine($"[Selection] Selected Hotbar Slot {i + 1}: {_player.Hotbar[i].Type} (x{_player.Hotbar[i].Count})");
                }
                var nk = (Microsoft.Xna.Framework.Input.Keys)((int)Microsoft.Xna.Framework.Input.Keys.NumPad1 + i);
                if (kbState.IsKeyDown(nk) && !_prevKbState.IsKeyDown(nk))
                {
                    _player.SelectedSlot = i;
                    Console.WriteLine($"[Selection] Selected Hotbar Slot {i + 1}: {_player.Hotbar[i].Type} (x{_player.Hotbar[i].Count})");
                }
            }

            // Toggle physics mode
            if (kbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.G) && !_prevKbState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.G))
            {
                _player.FlyingMode = !_player.FlyingMode;
                _player.Velocity = Vector3.Zero;
                Console.WriteLine($"[Mode] Toggled FlyingMode to: {_player.FlyingMode}");
            }

            // Mouse interactions when locked
            bool leftHeld = false;
            bool leftPressed = false;
            bool rightPressed = false;

            if (IsActive && _isMouseLocked)
            {
                int centerX = GraphicsDevice.Viewport.Width / 2;
                int centerY = GraphicsDevice.Viewport.Height / 2;

                float dx = mouseState.X - centerX;
                float dy = mouseState.Y - centerY;

                if (dx != 0 || dy != 0)
                {
                    _camera.Yaw += dx * 0.15f;
                    _camera.Pitch = Math.Clamp(_camera.Pitch - dy * 0.15f, -89f, 89f);

                    _player.Yaw = _camera.Yaw;
                    _player.Pitch = _camera.Pitch;

                    Mouse.SetPosition(centerX, centerY);
                }

                leftHeld = mouseState.LeftButton == ButtonState.Pressed;
                leftPressed = leftHeld && _prevMouseState.LeftButton == ButtonState.Released;
                rightPressed = mouseState.RightButton == ButtonState.Pressed && _prevMouseState.RightButton == ButtonState.Released;

                int scrollDelta = mouseState.ScrollWheelValue - _prevMouseState.ScrollWheelValue;
                if (scrollDelta != 0)
                {
                    int slotChange = -Math.Sign(scrollDelta);
                    int newSlot = _player.SelectedSlot + slotChange;
                    if (newSlot < 0) newSlot = 8;
                    if (newSlot > 8) newSlot = 0;
                    _player.SelectedSlot = newSlot;
                    Console.WriteLine($"[Selection] Selected Hotbar Slot {newSlot + 1}: {_player.Hotbar[newSlot].Type} (x{_player.Hotbar[newSlot].Count})");
                }
            }

            if (!_timePaused)
            {
                _timeOfDay += deltaTime * _timeScale;
                _timeOfDay -= MathF.Floor(_timeOfDay);
            }

            if (_player.IsAlive)
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

                if (_player.FlyingMode)
                {
                    if (IsKeyPressed(kbState, Key.Space)) moveDir += Vector3.UnitY;
                    if (IsKeyPressed(kbState, Key.ShiftLeft) || IsKeyPressed(kbState, Key.ShiftRight)) moveDir -= Vector3.UnitY;
                }
                else
                {
                    if (IsKeyPressed(kbState, Key.Space) && !_prevSpacePressed)
                    {
                        _player.Jump();
                    }
                }
                _prevSpacePressed = IsKeyPressed(kbState, Key.Space);

                _player.Update(deltaTime, _grid, moveDir);

                _camera.Position = _player.Position + new Vector3(0f, Player.EyeHeight, 0f);
                _camera.Yaw = _player.Yaw;
                _camera.Pitch = _player.Pitch;

                if (IsActive && _isMouseLocked)
                {
                    _combatSystem.Update(
                        deltaTime,
                        _grid,
                        _player,
                        _animals,
                        _blockInteraction,
                        _camera.Position,
                        _camera.Front,
                        leftHeld,
                        leftPressed);

                    _blockInteraction.Update(
                        deltaTime,
                        _grid,
                        _player,
                        _camera.Position,
                        _camera.Front,
                        leftHeld && !_combatSystem.BlocksMiningThisFrame,
                        rightPressed,
                        GraphicsDevice);
                }
            }

            if (!_player.IsAlive)
            {
                if (!_deathScreen.IsOpen)
                {
                    OpenDeathScreen();
                }
                return;
            }

            _grid.UpdateChunksAround(GraphicsDevice, _camera.Position, _settings.RenderDistance);
            int meshBudget = Math.Min(6, VoxelWorld.DefaultMeshChunksPerFrame + _grid.PendingMeshCount / 10);
            _grid.ProcessPendingWork(
                GraphicsDevice,
                _camera.Position,
                _settings.RenderDistance,
                maxTerrainPerFrame: VoxelWorld.DefaultTerrainChunksPerFrame,
                maxMeshPerFrame: meshBudget);

            _animals.Update(deltaTime, _grid);

            _autosaveTimer += deltaTime;
            if (_autosaveTimer >= AutosaveIntervalSeconds)
            {
                _autosaveTimer = 0f;
                PerformAutosave(sync: false);
            }

            string modeStr = _player.FlyingMode ? "FLY" : "PHYSICS";
            string groundedStr = _player.IsGrounded ? "Grounded" : "Airborne";
            string hudStr = _player.GetInventoryHUD();
            Window.Title = $"Autonocraft Voxel Clone | Mode: {modeStr} ({groundedStr}) | Pos: ({_player.Position.X:F1}, {_player.Position.Y:F1}, {_player.Position.Z:F1}) | {hudStr} | Chunks: {_grid.GetActiveChunks().Count}";
        }

        protected override void Draw(GameTime gameTime)
        {
            if (_runTests || _ui == null)
            {
                base.Draw(gameTime);
                return;
            }

            switch (_state)
            {
                case GameState.MainMenu:
                    _saveSlotScreen!.Draw(GraphicsDevice.Viewport, _screenFade.Alpha, _screenFade.OffsetY);
                    break;
                case GameState.NewWorldSetup:
                    _newWorldSetupScreen!.Draw(GraphicsDevice.Viewport);
                    break;
                case GameState.WorldLoading:
                    _loadingScreen!.Draw(GraphicsDevice.Viewport, _screenFade.Alpha, _screenFade.OffsetY);
                    break;
                case GameState.Playing:
                    _renderer?.Draw(this);
                    _devConsole?.Draw(GraphicsDevice.Viewport);
                    _pauseMenu?.Draw(GraphicsDevice.Viewport, _pauseFade.Alpha, _pauseFade.OffsetY);
                    _deathScreen?.Draw(GraphicsDevice.Viewport, _deathFade.Alpha, _deathFade.OffsetY);
                    break;
            }

            base.Draw(gameTime);
        }

        public void SimulateClick(MouseButton button)
        {
            var action = new Action(() =>
            {
                if (button == MouseButton.Left)
                {
                    if (!_combatSystem.TryInstantAttack(_grid, _player, _animals, _blockInteraction, _camera.Position, _camera.Front))
                    {
                        _blockInteraction.InstantMine(_grid, _player, _camera.Position, _camera.Front, GraphicsDevice);
                    }
                }
                else if (button == MouseButton.Right)
                {
                    _blockInteraction.InstantPlace(_grid, _player, _camera.Position, _camera.Front, GraphicsDevice);
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
                PerformAutosave(sync: true);
                Exiting -= OnGameExiting;
                Window.ClientSizeChanged -= OnWindowClientSizeChanged;
                AgentHttpServer.Stop();
                _grid.Dispose();
                _whiteTexture?.Dispose();
                _atlasTexture?.Dispose();
                _renderer?.Dispose();
                _ui?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

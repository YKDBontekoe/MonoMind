using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Ai;
using Autonocraft.Crafting;
using Autonocraft.Engine;
using Autonocraft.Engine.Animation;
using Autonocraft.Engine.Audio;
using Autonocraft.UI;
using Autonocraft.Village;
using Autonocraft.World;

namespace Autonocraft.Core
{
    /// <summary>
    /// Screen instances, fade transitions, and high-level GameState routing for draw/update.
    /// </summary>
    internal sealed class ScreenManager
    {
        private GameState _state = GameState.MainMenu;
        private GameState _prevState = GameState.MainMenu;

        private readonly UiTransition _screenFade = new UiTransition();
        private readonly UiTransition _pauseFade = new UiTransition();
        private readonly UiTransition _deathFade = new UiTransition();

        private bool _mainMenuSettingsOpen;
        private bool _playerDashboardOpen;

        public GameState State
        {
            get => _state;
            set => _state = value;
        }

        public GameState PrevState => _prevState;
        public UiTransition ScreenFade => _screenFade;
        public UiTransition PauseFade => _pauseFade;
        public UiTransition DeathFade => _deathFade;
        public bool MainMenuSettingsOpen
        {
            get => _mainMenuSettingsOpen;
            set => _mainMenuSettingsOpen = value;
        }
        public bool PlayerDashboardOpen
        {
            get => _playerDashboardOpen;
            set => _playerDashboardOpen = value;
        }

        public SaveSlotScreen? SaveSlotScreen { get; private set; }
        public MainMenuSettingsScreen? MainMenuSettingsScreen { get; private set; }
        public PlayerDashboardScreen? PlayerDashboardScreen { get; private set; }
        public NewWorldSetupScreen? NewWorldSetupScreen { get; private set; }
        public LoadingScreen? LoadingScreen { get; private set; }
        public DevConsole? DevConsole { get; private set; }
        public PauseMenuScreen? PauseMenu { get; private set; }
        public DeathScreen? DeathScreen { get; private set; }
        public CrucibleScreen? CrucibleScreen { get; private set; }
        public InventoryScreen? InventoryScreen { get; private set; }
        public JournalScreen? JournalScreen { get; private set; }
        public VillageScreen? VillageScreen { get; private set; }
        public VillageChatScreen? VillageChatScreen { get; private set; }

        public void Initialize(
            GraphicsDevice graphicsDevice,
            UiRenderer ui,
            GameSession session,
            GameSettings settings,
            Action<int> onRenderDistanceChanged,
            Action<bool> onMuteAudioChanged,
            Action<bool> onVSyncChanged,
            Action<bool> onHighQualityLightingChanged)
        {
            SaveSlotScreen = new SaveSlotScreen(ui);
            MainMenuSettingsScreen = new MainMenuSettingsScreen(ui);
            PlayerDashboardScreen = new PlayerDashboardScreen(ui);
            NewWorldSetupScreen = new NewWorldSetupScreen(ui);
            LoadingScreen = new LoadingScreen(session.Grid, graphicsDevice, ui);
            DevConsole = new DevConsole(ui);
            PauseMenu = new PauseMenuScreen(ui);
            PauseMenu.SetRenderDistance(settings.RenderDistance);
            PauseMenu.RenderDistanceChanged += distance => onRenderDistanceChanged(distance);
            PauseMenu.MuteAudioChanged += mute => onMuteAudioChanged(mute);
            PauseMenu.VSyncChanged += onVSyncChanged;
            PauseMenu.HighQualityLightingChanged += onHighQualityLightingChanged;
            PauseMenu.ApplyAudioSettings(settings);
            PauseMenu.ApplyGraphicsSettings(settings);
            DeathScreen = new DeathScreen(ui);
            CrucibleScreen = new CrucibleScreen(ui);
            InventoryScreen = new InventoryScreen(ui);
            JournalScreen = new JournalScreen(ui);
            VillageScreen = new VillageScreen(ui, session.Villagers);
            VillageScreen.SetTakeRationsAction(player =>
            {
                var village = session.Villages.GetActiveVillage(player.Position);
                if (village != null)
                {
                    FoodConsumption.TryTakeRations(player, village);
                }
            });
            VillageChatScreen = new VillageChatScreen(ui, session.VillageAi);
        }

        public void RecreateVillageChatScreen(UiRenderer ui, VillageAiOrchestrator villageAi)
        {
            VillageChatScreen = new VillageChatScreen(ui, villageAi);
        }

        public void RecreateLoadingScreen(GraphicsDevice graphicsDevice, UiRenderer ui, VoxelWorld grid)
        {
            LoadingScreen = new LoadingScreen(grid, graphicsDevice, ui);
        }

        public void BeginInitialFadeIn()
        {
            _screenFade.BeginFadeIn(0.25f);
            _prevState = _state;
        }

        public void HandleStateTransition(Action onLeftPlaying, Action<GameState> onMusicForState)
        {
            if (_state == _prevState)
            {
                return;
            }

            if (_prevState == GameState.Playing && _state != GameState.Playing)
            {
                onLeftPlaying();
            }

            if (_state == GameState.WorldLoading || _prevState == GameState.WorldLoading)
            {
                _screenFade.SnapVisible();
            }
            else
            {
                _screenFade.BeginFadeIn(0.25f);
            }

            onMusicForState(_state);
            _prevState = _state;
        }

        public void UpdateFades(float deltaTime)
        {
            _screenFade.Update(deltaTime);
            _pauseFade.Update(deltaTime);
            _deathFade.Update(deltaTime);
        }

        public void OpenPauseMenu(Action prepareMouseForUi, Action<string> setWindowTitle)
        {
            prepareMouseForUi();
            PauseMenu!.Open();
            _pauseFade.BeginFadeInSlideUp(0.2f, 12f);
            setWindowTitle("Autonocraft | Paused");
        }

        public void ClosePauseMenu(Action restoreMouseLock)
        {
            PauseMenu!.Close();
            _pauseFade.SnapVisible();
            restoreMouseLock();
        }

        public void OpenDeathScreen(string causeText, string penaltyText, Action prepareDeathMouse)
        {
            prepareDeathMouse();
            PauseMenu!.Close();
            _pauseFade.SnapVisible();
            DeathScreen!.Open(causeText, penaltyText);
            _deathFade.BeginFadeInSlideUp(0.25f, 16f);
        }

        public void CloseDeathScreen(Action restoreMouseLock)
        {
            DeathScreen!.Close();
            _deathFade.SnapVisible();
            restoreMouseLock();
        }

        public void SnapOverlaysVisible()
        {
            _pauseFade.SnapVisible();
            _deathFade.SnapVisible();
        }

        public void CloseAllGameplayOverlays(CraftingSystem crafting)
        {
            VillageScreen?.Close();
            VillageChatScreen?.Close();
            crafting.CloseCrucible();
            crafting.CloseJournal();
            crafting.CloseInventory();
            if (DevConsole?.IsOpen == true)
            {
                DevConsole.Toggle();
            }

            PauseMenu?.Close();
            DeathScreen?.Close();
        }

        public bool HasBlockingGameplayOverlay(CraftingSystem crafting) =>
            VillageScreen?.IsOpen == true
            || VillageChatScreen?.IsOpen == true
            || PauseMenu?.IsOpen == true
            || DeathScreen?.IsOpen == true
            || DevConsole?.IsOpen == true
            || crafting.Crucible.IsOpen
            || crafting.InventoryOpen
            || crafting.IsJournalUiBlocking;

        public bool ShouldDuckAudio(CraftingSystem crafting, GameState state) =>
            state != GameState.Playing
                ? _mainMenuSettingsOpen || _playerDashboardOpen
                : PauseMenu?.IsOpen == true
                || DeathScreen?.IsOpen == true
                || DevConsole?.IsOpen == true
                || VillageScreen?.IsOpen == true
                || VillageChatScreen?.IsOpen == true
                || crafting.Crucible.IsOpen
                || crafting.IsJournalUiBlocking
                || _mainMenuSettingsOpen
                || _playerDashboardOpen;

        public InputManager.GameplayInputBlockers GetInputBlockers(CraftingSystem crafting) =>
            new InputManager.GameplayInputBlockers
            {
                DevConsoleOpen = DevConsole?.IsOpen == true,
                PauseMenuOpen = PauseMenu?.IsOpen == true,
                DeathScreenOpen = DeathScreen?.IsOpen == true,
                VillageScreenOpen = VillageScreen?.IsOpen == true,
                VillageChatOpen = VillageChatScreen?.IsOpen == true,
                JournalOpen = crafting.IsJournalUiBlocking,
                CrucibleOpen = crafting.Crucible.IsOpen,
                InventoryOpen = crafting.InventoryOpen
            };

        public void DrawMainMenu(GraphicsDevice graphicsDevice)
        {
            SaveSlotScreen!.Draw(graphicsDevice.Viewport, _screenFade.Alpha, _screenFade.OffsetY);
            if (_playerDashboardOpen)
            {
                PlayerDashboardScreen!.Draw(graphicsDevice.Viewport);
            }
            else if (_mainMenuSettingsOpen)
            {
                MainMenuSettingsScreen!.Draw(graphicsDevice.Viewport);
            }
        }

        public void DrawNewWorldSetup(GraphicsDevice graphicsDevice) =>
            NewWorldSetupScreen!.Draw(graphicsDevice.Viewport);

        public void DrawWorldLoading(GraphicsDevice graphicsDevice)
        {
            graphicsDevice.Clear(ClearOptions.Target, UiTheme.Scrim, 1f, 0);
            LoadingScreen!.Draw(graphicsDevice.Viewport);
        }

        public void DrawPlayingOverlays(
            GraphicsDevice graphicsDevice,
            CraftingSystem crafting,
            VoxelWorld grid,
            Texture2D? atlasTexture,
            Player player,
            float timeOfDay)
        {
            if (atlasTexture != null)
            {
                InventoryScreen?.Draw(graphicsDevice.Viewport, player, crafting, atlasTexture);
            }
            CrucibleScreen?.Draw(
                graphicsDevice.Viewport,
                crafting.Crucible,
                crafting.GetCurrentEnvironment(grid, timeOfDay),
                crafting.Journal,
                crafting,
                player,
                atlasTexture);

            if (crafting.ShouldDrawJournal())
            {
                var journalFade = crafting.JournalTransition;
                JournalScreen?.Draw(
                    graphicsDevice.Viewport,
                    crafting.Journal,
                    player.Skills,
                    journalFade.Alpha,
                    journalFade.OffsetY);
            }

            VillageScreen?.Draw(graphicsDevice.Viewport);
            VillageChatScreen?.Draw(graphicsDevice.Viewport);
            DevConsole?.Draw(graphicsDevice.Viewport);
            PauseMenu?.Draw(graphicsDevice.Viewport, _pauseFade.Alpha, _pauseFade.OffsetY);
            DeathScreen?.Draw(graphicsDevice.Viewport, _deathFade.Alpha, _deathFade.OffsetY);
        }
    }
}

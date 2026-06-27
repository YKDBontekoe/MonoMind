using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Domain.Village;
using Autonocraft.Engine;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.World;
using Autonocraft.Village;
using Autonocraft.UI.Village;
using Autonocraft.UI.VillagePanels;
using VillageEntity = Autonocraft.Village.Village;
using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.UI
{
    public sealed partial class VillageScreen
    {
        internal const float PanelWidth = 900f;
        internal const float PanelHeight = 600f;
        internal const float ContentTop = 98f;
        internal const float FooterHeight = 74f;
        internal const float ButtonWidth = 148f;
        internal const float ButtonHeight = 34f;
        private const float TabWidth = 132f;
        private const float TabHeight = 30f;
        private const int HitPadding = 6;
        internal const int VillagerRowButtonBase = 10000;

        private static readonly (string Label, JobType Job)[] AssignableJobs =
        {
            ("Idle", JobType.Idle),
            ("Lumber", JobType.Lumber),
            ("Mine", JobType.Mine),
            ("Farm", JobType.Farm),
            ("Build", JobType.Build),
            ("Haul", JobType.Haul),
        };

        private static readonly string[] TabLabels = { "Overview", "Build", "People", "Goals" };

        private readonly UiRenderer _ui;
        private VillagerManager _villagers;
        private VillageEntity? _village;
        private VillageManager? _villageManager;
        private VoxelWorld? _world;
        private Vector3 _playerPos;
        private IItemContainer? _playerPayer;
        private Core.Player? _guidePlayer;
        private bool _playerCreative;
        private string? _openingNote;
        private int _earlyGuideStage;
        private Action<Core.Player>? _takeRationsAction;
        private int _selectedTab;
        private int _selectedVillagerId = -1;
        private int _hoveredButton = -1;
        private bool _canClaimNearby;
        private bool _isFoundingMode;
        private float _buildScroll;
        private float _peopleScroll;
        private VillageViewModel? _viewModel;
        private int _refreshVillageStateCooldown;
        private bool _playWithAi;
        private int _selectedGoalBlockIndex;
        private int _selectedGoalCountIndex;
        private bool _isEditingName;
        private string _editingNameBuffer = "";

        private readonly IVillagePanel[] _panels =
        {
            new OverviewPanel(),
            new BuildPanel(),
            new PeoplePanel(),
            new GoalsPanel(),
        };

        private int _strandedCitizenCount;
        private int _actionFeedbackFrames;
        private readonly FoundingPanel _foundingPanel = new();

        public bool IsOpen { get; private set; }
        public bool IsFoundingMode => _isFoundingMode;
        public bool RecruitRequested { get; private set; }
        public bool ClaimRequested { get; private set; }
        public bool PlaceTownHeartRequested { get; private set; }
        public bool CloseRequested { get; private set; }
        public string? RequestedBlueprintId { get; private set; }
        public int RequestedAssignVillagerId { get; private set; } = -1;
        public JobType RequestedAssignJob { get; private set; } = JobType.Idle;
        public int RequestedChatVillagerId { get; private set; } = -1;
        public bool RequestedStewardChat { get; private set; }
        public bool RequestBlueprintPlacement { get; private set; }
        public bool RequestWorkZonePlacement { get; private set; }
        public string? AssignFeedback { get; private set; }
        public bool AssignSuccess { get; private set; }
        public string? RecruitFeedback { get; private set; }
        public bool RecruitSuccess { get; private set; }
        public VillageEntity? CurrentVillage => _village;

        public void SetAssignFeedback(JobAssignmentResult result)
        {
            if (result.Success)
            {
                AssignFeedback = "Job assigned successfully.";
                AssignSuccess = true;
            }
            else
            {
                AssignFeedback = string.IsNullOrEmpty(result.Remediation)
                    ? result.PlayerMessage
                    : $"{result.PlayerMessage} {result.Remediation}";
                AssignSuccess = false;
            }

            _actionFeedbackFrames = 180;
        }

        public void SetRecruitFeedback(RecruitResult result)
        {
            RecruitSuccess = result.Success;
            RecruitFeedback = result.Success
                ? result.PlayerMessage
                : string.IsNullOrEmpty(result.Remediation)
                    ? result.PlayerMessage
                    : $"{result.PlayerMessage} {result.Remediation}";
            _actionFeedbackFrames = 180;
        }

        public void RefreshAfterVillageAction()
        {
            _refreshVillageStateCooldown = 0;
            RefreshVillageState();
        }

        public void ClearActionFeedback()
        {
            AssignFeedback = null;
            RecruitFeedback = null;
            AssignSuccess = false;
            RecruitSuccess = false;
            _actionFeedbackFrames = 0;
        }

        public VillageScreen(UiRenderer ui, VillagerManager villagers)
        {
            _ui = ui;
            _villagers = villagers;
        }

        public void OpenFounding(
            VillageManager villageManager,
            VoxelWorld world,
            Vector3 playerPos,
            IItemContainer? playerPayer,
            bool playerCreative = false,
            bool playWithAi = true)
        {
            _village = null;
            _villageManager = villageManager;
            _villagers = villageManager.Villagers;
            _world = world;
            _playerPos = playerPos;
            _playerPayer = playerPayer;
            _playerCreative = playerCreative;
            _openingNote = null;
            _viewModel = null;
            _isFoundingMode = true;
            _selectedTab = 0;
            _selectedVillagerId = -1;
            _hoveredButton = -1;
            _playWithAi = playWithAi;
            _selectedGoalBlockIndex = 0;
            _selectedGoalCountIndex = 1;
            _isEditingName = false;
            _editingNameBuffer = "";
            _canClaimNearby = villageManager.TryFindClaimableStructure(world, playerPos, 24f, out _, out _, out _, quickScan: true);
            IsOpen = true;
        }

        public void Open(
            VillageEntity village,
            VillageManager villageManager,
            VoxelWorld world,
            Vector3 playerPos,
            IItemContainer? playerPayer,
            bool playerCreative = false,
            string? openingNote = null,
            bool playWithAi = true,
            int earlyGuideStage = 0,
            Core.Player? guidePlayer = null)
        {
            _village = village;
            _villageManager = villageManager;
            _villagers = villageManager.Villagers;
            _world = world;
            _playerPos = playerPos;
            _playerPayer = playerPayer;
            _guidePlayer = guidePlayer;
            _playerCreative = playerCreative;
            _openingNote = openingNote;
            _isFoundingMode = false;
            _playWithAi = playWithAi;
            _earlyGuideStage = earlyGuideStage;
            _selectedGoalBlockIndex = 0;
            _selectedGoalCountIndex = 1;
            _isEditingName = false;
            _editingNameBuffer = "";
            _selectedTab = 0;
            _selectedVillagerId = -1;
            if (VillageSettlementHealth.GetLivePopulation(village, _villagers) == 0 &&
                VillageSettlementHealth.HasEstablishedSettlement(village))
            {
                villageManager.EnsureStarterCitizens(village, world);
            }

            RefreshVillageState();
            foreach (var villager in _villagers.All)
            {
                if (villager.VillageId == village.Id)
                {
                    _selectedVillagerId = villager.Id;
                    break;
                }
            }

            if (_selectedVillagerId < 0 && village.VillagerIds.Count > 0)
            {
                _selectedVillagerId = village.VillagerIds[0];
            }

            _hoveredButton = -1;
            _buildScroll = 0f;
            _peopleScroll = 0f;
            _canClaimNearby = villageManager.TryFindClaimableStructure(world, playerPos, 24f, out _, out _, out _, quickScan: true);
            IsOpen = true;
        }

        public void OpenPeopleTab(int? villagerId = null)
        {
            _selectedTab = 2;
            if (villagerId.HasValue && CitizenExists(villagerId.Value))
            {
                _selectedVillagerId = villagerId.Value;
                return;
            }

            foreach (var villager in _villagers.All)
            {
                if (_village != null && villager.VillageId == _village.Id && villager.Role == VillagerRole.Lumberjack)
                {
                    _selectedVillagerId = villager.Id;
                    return;
                }
            }
        }

        public void SetTakeRationsAction(Action<Core.Player>? action) => _takeRationsAction = action;

        public void Close()
        {
            IsOpen = false;
            _village = null;
            _villageManager = null;
            _world = null;
            _playerPayer = null;
            _playerCreative = false;
            _openingNote = null;
            _isFoundingMode = false;
            _hoveredButton = -1;
            _selectedVillagerId = -1;
        }

        public void SetPlayerPosition(Vector3 playerPos) => _playerPos = playerPos;

        public void Update(
            Viewport viewport,
            KeyboardState kb,
            MouseState mouse,
            KeyboardState prevKb,
            MouseState prevMouse)
        {
            ResetRequests();

            if (_actionFeedbackFrames > 0)
            {
                _actionFeedbackFrames--;
                if (_actionFeedbackFrames == 0)
                {
                    ClearActionFeedback();
                }
            }

            if (!IsOpen)
            {
                return;
            }

            if (_isEditingName)
            {
                HandleRenameInput(kb, prevKb);
                if (mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
                {
                    var lay = CreateLayout(viewport);
                    float editTitleX = lay.Ui.CenterX - lay.S(200f);
                    float editTitleY = lay.PanelY + lay.S(10f);
                    var titleRect = new Rectangle((int)editTitleX, (int)editTitleY, (int)lay.S(400f), (int)lay.S(24f));
                    if (!titleRect.Contains(mouse.X, mouse.Y))
                    {
                        _isEditingName = false;
                    }
                }
                return;
            }

            if (_isFoundingMode)
            {
                UpdateFounding(viewport, kb, mouse, prevKb, prevMouse);
                return;
            }

            if (_village == null)
            {
                return;
            }

            if (--_refreshVillageStateCooldown > 0)
            {
                // Input handling below still runs; only skip expensive registry rebuilds.
            }
            else
            {
                _refreshVillageStateCooldown = 8;
                RefreshVillageState();
            }

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                CloseRequested = true;
                return;
            }

            if (kb.IsKeyDown(Keys.R) && !prevKb.IsKeyDown(Keys.R))
            {
                RefreshVillageState();
                RecruitRequested = true;
                return;
            }

            if (kb.IsKeyDown(Keys.Left) && !prevKb.IsKeyDown(Keys.Left))
            {
                _selectedTab = CycleTab(_selectedTab, -1);
            }

            if (kb.IsKeyDown(Keys.Right) && !prevKb.IsKeyDown(Keys.Right))
            {
                _selectedTab = CycleTab(_selectedTab, 1);
            }

            for (int i = 0; i < TabLabels.Length && i < 9; i++)
            {
                if (kb.IsKeyDown(Keys.D1 + i) && !prevKb.IsKeyDown(Keys.D1 + i))
                {
                    _selectedTab = i;
                }
            }

            var layout = CreateLayout(viewport);
            float panelX = layout.PanelX;
            float panelY = layout.PanelY;
            float left = layout.Left;
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);

            int scrollDelta = mouse.ScrollWheelValue - prevMouse.ScrollWheelValue;
            if (scrollDelta != 0 && PointInPanel(mouse.X, mouse.Y, layout))
            {
                float step = layout.S(28f) * Math.Sign(scrollDelta);
                if (_selectedTab == 1)
                {
                    _buildScroll = Math.Clamp(_buildScroll + step, 0f, GetMaxBuildScroll(layout));
                }
                else if (_selectedTab == 2)
                {
                    _peopleScroll = Math.Clamp(_peopleScroll + step, 0f, GetMaxPeopleScroll(layout));
                }
            }

            _hoveredButton = -1;
            float titleX = layout.Ui.CenterX - layout.S(200f);
            float titleY = panelY + layout.S(10f);
            HitRect(titleX, titleY, layout.S(400f), layout.S(24f), 90, mouse);

            HitTestTabs(layout, mouse);

            float footerY = panelY + layout.S(PanelHeight) - layout.S(FooterHeight);
            int displayedCitizens = CountDisplayedCitizens();
            bool starterOverview = _selectedTab == 0 && displayedCitizens == 0 && _viewModel != null;
            if (!starterOverview)
            {
                HitRect(left, footerY, buttonW, buttonH, 10, mouse);
            }

            float closeX = panelX + layout.S(PanelWidth) - layout.S(20f) - buttonW;
            float closeY = panelY + layout.S(PanelHeight) - layout.S(30f);
            HitRect(closeX, closeY, buttonW, buttonH, 11, mouse);

            if (_playWithAi)
            {
                float stewardX = closeX - buttonW - layout.S(10f);
                HitRect(stewardX, closeY, buttonW, buttonH, 17, mouse);
            }

            if (_selectedTab == 0)
            {
                if (_viewModel?.SuggestedTab != null && _viewModel.NextActionKind != SettlementActionKind.None &&
                    !starterOverview &&
                    OverviewPanel.TryGetNextActionCtaY(_viewModel, layout.Ui, panelY, layout.S(ContentTop), out float ctaY))
                {
                    float panelWidth = layout.S(PanelWidth);
                    float colW = (panelWidth - layout.S(40f) - layout.S(20f)) / 2f;
                    float rightColX = left + colW + layout.S(20f);
                    HitRect(rightColX, ctaY, layout.S(140f), layout.S(28f), 15, mouse);
                }

                if (!starterOverview && _canClaimNearby)
                {
                    HitRect(left + buttonW + layout.S(10f), footerY, buttonW, buttonH, 12, mouse);
                }

                if (!starterOverview)
                {
                    HitRect(left + (buttonW + layout.S(10f)) * (_canClaimNearby ? 2f : 1f), footerY, buttonW, buttonH, 13, mouse);
                    float rationX = left + (buttonW + layout.S(10f)) * (_canClaimNearby ? 3f : 2f);
                    HitRect(rationX, footerY, buttonW, buttonH, 16, mouse);
                }
            }

            if (_selectedTab == 1)
            {
                HitBuildCatalog(layout, mouse, buttonH);
            }

            if (_selectedTab == 2)
            {
                HitPeopleTab(layout, mouse, buttonH);
            }

            if (_selectedTab == 3 && !_playWithAi)
            {
                HitManualGoals(layout, mouse);
            }

            bool activate = (mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
                || ((kb.IsKeyDown(Keys.Enter) || kb.IsKeyDown(Keys.Space))
                    && !prevKb.IsKeyDown(Keys.Enter)
                    && !prevKb.IsKeyDown(Keys.Space));

            if (!activate)
            {
                return;
            }

            HandleActivate();
        }


        public void Draw(Viewport viewport, float alpha = 1f, float offsetY = 0f)
        {
            if (!IsOpen || alpha <= 0.01f)
            {
                return;
            }

            if (_isFoundingMode)
            {
                DrawFounding(viewport, alpha, offsetY);
                return;
            }

            if (_village == null)
            {
                return;
            }

            var layout = CreateLayout(viewport);
            layout.PanelY += offsetY;
            float panelX = layout.PanelX;
            float panelY = layout.PanelY;
            float panelW = layout.S(PanelWidth);
            float panelH = layout.S(PanelHeight);
            float left = layout.Left;
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            var accent = UiTheme.PanelBorder;

            _ui.DrawFullscreenBackground(UiTheme.OverlayScrim * (0.55f * alpha));
            _ui.DrawCard(panelX, panelY, panelW, panelH, alpha, UiTheme.RadiusXl);

            DrawHeader(layout, panelX, panelY, panelW, alpha, accent);
            DrawTabBar(layout, left, panelY, alpha, accent);

            float contentY = panelY + layout.S(ContentTop);
            float contentH = panelH - layout.S(ContentTop) - layout.S(FooterHeight);

            var panelContext = new VillagePanelContext
            {
                Ui = _ui,
                UiLayout = layout.Ui,
                Village = _village!,
                ViewModel = _viewModel,
                Villagers = _villagers,
                PlayerPosition = _playerPos,
                PlayerCreative = _playerCreative,
                PlayWithAi = _playWithAi,
                EarlyGuideStage = _earlyGuideStage,
                PanelX = panelX,
                PanelY = panelY,
                PanelWidth = panelW,
                PanelHeight = panelH,
                ContentLeft = left,
                ContentTop = layout.S(ContentTop),
                ContentHeight = contentH,
                FooterHeight = layout.S(FooterHeight),
                Alpha = alpha,
                Accent = accent,
                BuildScroll = _buildScroll,
                PeopleScroll = _peopleScroll,
                SelectedVillagerId = _selectedVillagerId,
                SelectedGoalBlockIndex = _selectedGoalBlockIndex,
                SelectedGoalCountIndex = _selectedGoalCountIndex,
                HoveredButton = _hoveredButton,
                PlayerPayer = _playerPayer,
                OpeningNote = _openingNote,
                AssignFeedback = AssignFeedback,
                AssignSuccess = AssignSuccess
            };

            if (_selectedTab >= 0 && _selectedTab < _panels.Length)
                _panels[_selectedTab].Draw(panelContext);

            float footerY = panelY + panelH - layout.S(FooterHeight);
            string recruitLabel = GetRecruitButtonLabel();
            int displayedCitizens = CountDisplayedCitizens();
            bool starterOverview = _selectedTab == 0 && displayedCitizens == 0 && _viewModel != null;
            bool canRecruit = displayedCitizens > 0
                ? _village.CanRecruit(_villagers, _playerCreative)
                : true;
            if (!starterOverview)
            {
                DrawStyledButton(left, footerY, buttonW, buttonH, recruitLabel, _hoveredButton == 10,
                    UiButtonStyle.Primary, layout.Ui, alpha, !canRecruit);
            }

            if (_selectedTab == 0 && !starterOverview)
            {
                if (_canClaimNearby)
                {
                    DrawStyledButton(left + buttonW + layout.S(10f), footerY, buttonW, buttonH, "Claim outpost",
                        _hoveredButton == 12, UiButtonStyle.Secondary, layout.Ui, alpha);
                }

                DrawStyledButton(left + (buttonW + layout.S(10f)) * (_canClaimNearby ? 2f : 1f), footerY, buttonW, buttonH,
                    "Paint zone", _hoveredButton == 13, UiButtonStyle.Secondary, layout.Ui, alpha);
                float rationX = left + (buttonW + layout.S(10f)) * (_canClaimNearby ? 3f : 2f);
                DrawStyledButton(rationX, footerY, buttonW, buttonH, "Take rations", _hoveredButton == 16,
                    UiButtonStyle.Secondary, layout.Ui, alpha);
            }

            float closeX = panelX + panelW - layout.S(20f) - buttonW;
            float closeY = panelY + panelH - layout.S(30f);
            if (_playWithAi)
            {
                float stewardX = closeX - buttonW - layout.S(10f);
                DrawStyledButton(stewardX, closeY, buttonW, buttonH, "Steward", _hoveredButton == 17,
                    UiButtonStyle.Secondary, layout.Ui, alpha);
            }

            DrawStyledButton(closeX, closeY, buttonW, buttonH, "Close", _hoveredButton == 11, UiButtonStyle.Ghost, layout.Ui, alpha);

            string footerHint;
            if (_hoveredButton == 10)
            {
                footerHint = _viewModel?.RecruitPreview
                    ?? "Recruit — bring a new peasant to the village (costs 4 oak planks)";
            }
            else if (_hoveredButton == 12)
            {
                footerHint = "Claim outpost — claim an abandoned structure to recruit a free citizen";
            }
            else if (_hoveredButton == 13)
            {
                footerHint = "Paint zone — define a gathering or clearing work zone for villagers";
            }
            else if (_hoveredButton == 11)
            {
                footerHint = "Close — exit the town board";
            }
            else if (_hoveredButton == 17)
            {
                footerHint = "Steward — chat with the village steward about priorities";
            }
            else if (_hoveredButton >= 20 && _hoveredButton < 30)
            {
                footerHint = "Click to initiate blueprint placement preview";
            }
            else
            {
                footerHint = _selectedTab switch
                {
                    1 => "Click a building card to place in the world",
                    2 => "Select a villager, then assign a job",
                    3 => _playWithAi ? "Steward goals track village priorities" : "Manual goals track local resource priorities",
                    _ => "Esc close · R recruit · 1–4 switch tabs · scroll lists"
                };
            }
            _ui.DrawCenteredText(footerHint, panelY + panelH - layout.S(12f), layout.S(UiTheme.FontSmall),
                UiTheme.Hint, 0.9f * alpha);

            if (!string.IsNullOrEmpty(RecruitFeedback))
            {
                Color feedbackColor = RecruitSuccess ? UiTheme.Success : UiTheme.Danger;
                _ui.DrawCenteredText(RecruitFeedback, panelY + panelH - layout.S(28f), layout.S(UiTheme.FontSmall),
                    feedbackColor, 0.95f * alpha);
            }
        }



    }
}

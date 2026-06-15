using System;
using System.Collections.Generic;
using System.Text;
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
using VillageEntity = Autonocraft.Village.Village;
using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.UI
{
    public sealed class VillageScreen
    {
        private const float PanelWidth = 900f;
        private const float PanelHeight = 600f;
        private const float ButtonWidth = 148f;
        private const float ButtonHeight = 34f;
        private const float TabWidth = 132f;
        private const float TabHeight = 30f;
        private const float ContentTop = 98f;
        private const float FooterHeight = 74f;
        private const int HitPadding = 6;

        private static readonly (string Label, JobType Job)[] AssignableJobs =
        {
            ("IDLE", JobType.Idle),
            ("LUMBER", JobType.Lumber),
            ("MINE", JobType.Mine),
            ("FARM", JobType.Farm),
            ("BUILD", JobType.Build),
            ("HAUL", JobType.Haul),
        };

        private static readonly string[] TabLabels = { "OVERVIEW", "BUILD", "PEOPLE", "GOALS" };

        private readonly UiRenderer _ui;
        private readonly VillagerManager _villagers;
        private VillageEntity? _village;
        private VillageManager? _villageManager;
        private VoxelWorld? _world;
        private Vector3 _playerPos;
        private IItemContainer? _playerPayer;
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
        private bool _playWithAi;
        private int _selectedGoalBlockIndex;
        private int _selectedGoalCountIndex;
        private bool _isEditingName;
        private string _editingNameBuffer = "";

        private static readonly BlockType[] GoalBlockTypes =
        {
            BlockType.OakPlank,
            BlockType.Cobblestone,
            BlockType.OakLog,
            BlockType.Wheat,
            BlockType.Carrot
        };

        private static readonly int[] GoalTargetCounts = { 10, 20, 32, 64 };

        public bool IsOpen { get; private set; }
        public bool IsFoundingMode => _isFoundingMode;
        public bool RecruitRequested { get; private set; }
        public bool SummonSettlersRequested { get; private set; }
        public bool ClaimRequested { get; private set; }
        public bool PlaceTownHeartRequested { get; private set; }
        public bool CloseRequested { get; private set; }
        public string? RequestedBlueprintId { get; private set; }
        public int RequestedAssignVillagerId { get; private set; } = -1;
        public JobType RequestedAssignJob { get; private set; } = JobType.Idle;
        public int RequestedChatVillagerId { get; private set; } = -1;
        public bool RequestBlueprintPlacement { get; private set; }
        public bool RequestWorkZonePlacement { get; private set; }

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
            _canClaimNearby = villageManager.TryFindClaimableStructure(world, playerPos, 24f, out _, out _, out _);
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
            int earlyGuideStage = 0)
        {
            _village = village;
            _villageManager = villageManager;
            _world = world;
            _playerPos = playerPos;
            _playerPayer = playerPayer;
            _playerCreative = playerCreative;
            _openingNote = openingNote;
            _isFoundingMode = false;
            _playWithAi = playWithAi;
            _earlyGuideStage = earlyGuideStage;
            _selectedGoalBlockIndex = 0;
            _selectedGoalCountIndex = 1;
            _isEditingName = false;
            _editingNameBuffer = "";
            village.ReconcileVillagerRegistry(_villagers.All);
            _villageManager.SyncCitizensForVillage(village);
            _viewModel = VillageViewModel.Build(village, villageManager, _villagers, playerCreative, playerPos);
            _selectedTab = 0;
            _selectedVillagerId = -1;
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
            _canClaimNearby = villageManager.TryFindClaimableStructure(world, playerPos, 24f, out _, out _, out _);
            IsOpen = true;
        }

        public void OpenPeopleTab()
        {
            _selectedTab = 2;
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

            RefreshVillageState();

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                CloseRequested = true;
                return;
            }

            if (kb.IsKeyDown(Keys.R) && !prevKb.IsKeyDown(Keys.R))
            {
                if (CountDisplayedCitizens() > 0)
                {
                    RecruitRequested = true;
                }
                else if (CanSummonSettlers())
                {
                    SummonSettlersRequested = true;
                }

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
            HitRect(left, footerY, buttonW, buttonH, 10, mouse);

            float closeX = panelX + layout.S(PanelWidth) - layout.S(20f) - buttonW;
            float closeY = panelY + layout.S(PanelHeight) - layout.S(30f);
            HitRect(closeX, closeY, buttonW, buttonH, 11, mouse);

            if (_selectedTab == 0)
            {
                if (_canClaimNearby)
                {
                    HitRect(left + buttonW + layout.S(10f), footerY, buttonW, buttonH, 12, mouse);
                }

                HitRect(left + (buttonW + layout.S(10f)) * (_canClaimNearby ? 2f : 1f), footerY, buttonW, buttonH, 13, mouse);
                float rationX = left + (buttonW + layout.S(10f)) * (_canClaimNearby ? 3f : 2f);
                HitRect(rationX, footerY, buttonW, buttonH, 16, mouse);
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

        private void UpdateFounding(
            Viewport viewport,
            KeyboardState kb,
            MouseState mouse,
            KeyboardState prevKb,
            MouseState prevMouse)
        {
            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                CloseRequested = true;
                return;
            }

            var layout = CreateLayout(viewport);
            float panelX = layout.PanelX;
            float panelY = layout.PanelY;
            float left = layout.Left;
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);
            float footerY = panelY + layout.S(PanelHeight) - layout.S(FooterHeight);

            _hoveredButton = -1;
            HitRect(left, footerY, buttonW, buttonH, 14, mouse);
            if (_canClaimNearby)
            {
                HitRect(left + buttonW + layout.S(10f), footerY, buttonW, buttonH, 12, mouse);
            }

            float closeX = panelX + layout.S(PanelWidth) - layout.S(20f) - buttonW;
            float closeY = panelY + layout.S(PanelHeight) - layout.S(30f);
            HitRect(closeX, closeY, buttonW, buttonH, 11, mouse);

            bool activate = (mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
                || ((kb.IsKeyDown(Keys.Enter) || kb.IsKeyDown(Keys.Space))
                    && !prevKb.IsKeyDown(Keys.Enter)
                    && !prevKb.IsKeyDown(Keys.Space));

            if (!activate)
            {
                return;
            }

            HandleFoundingActivate();
        }

        private void DrawFounding(Viewport viewport, float alpha, float offsetY)
        {
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

            _ui.DrawFullscreenBackground(UiTheme.PanelFill * 0.72f * alpha);
            _ui.DrawVignette(0.35f, alpha);
            _ui.DrawFramedPanel(panelX, panelY, panelW, panelH, UiTheme.PanelBgMuted, accent, alpha);

            _ui.DrawCenteredTitle("START A SETTLEMENT", panelY + layout.S(14f), layout.S(UiTheme.ScaleTitle),
                UiTheme.Title * alpha);
            _ui.DrawCenteredText("NO VILLAGE YET", panelY + layout.S(36f), layout.S(UiTheme.ScaleSmall),
                UiTheme.Subtitle * alpha);

            float contentY = panelY + layout.S(ContentTop);
            float contentH = panelH - layout.S(ContentTop) - layout.S(FooterHeight);
            _ui.DrawFramedPanel(left, contentY, panelW - layout.S(40f), contentH,
                UiTheme.PanelBgMuted, accent, alpha * 0.95f);

            float textY = contentY + layout.S(28f);
            _ui.DrawString("This save has no settlement yet.",
                left + layout.S(18f), textY, layout.S(UiTheme.ScaleNormal), UiTheme.StatValue * alpha);
            textY += layout.S(28f);
            _ui.DrawString("Place a Town Heart — one settler joins automatically and builds it.",
                left + layout.S(18f), textY, layout.S(UiTheme.ScaleSmall), UiTheme.Subtitle * alpha);
            textY += layout.S(28f);
            _ui.DrawString("Recruit is only for extra workers after you already have at least one villager.",
                left + layout.S(18f), textY, layout.S(UiTheme.ScaleSmall), UiTheme.Meta * alpha);

            if (_canClaimNearby)
            {
                textY += layout.S(28f);
                _ui.DrawString("Abandoned outpost nearby — claim it for a free settler.",
                    left + layout.S(18f), textY, layout.S(UiTheme.ScaleSmall), UiTheme.Subtitle * alpha);
            }
            else
            {
                textY += layout.S(28f);
                _ui.DrawString("Wild outposts (cottages, forest camps) are rare — roughly one every few hundred blocks.",
                    left + layout.S(18f), textY, layout.S(UiTheme.ScaleSmall), UiTheme.Meta * alpha);
            }

            if (PlayerStructureRegistry.TryGet("town_heart", out var blueprint))
            {
                textY += layout.S(36f);
                string costLine = _playerCreative
                    ? "TOWN HEART COST: FREE IN CREATIVE"
                    : $"TOWN HEART COST: {FormatCosts(blueprint)}";
                bool canAfford = _playerCreative || (_playerPayer != null && blueprint.CanAfford(_playerPayer));
                _ui.DrawString(costLine, left + layout.S(18f), textY, layout.S(UiTheme.ScaleSmall),
                    (canAfford ? UiTheme.Subtitle : UiTheme.Danger) * alpha);
            }

            float footerY = panelY + panelH - layout.S(FooterHeight);
            bool canPlace = CanAffordTownHeart();
            _ui.DrawButton(left, footerY, buttonW, buttonH, "PLACE TOWN HEART", _hoveredButton == 14, false,
                layout.S(0.9f), alpha, canPlace ? 1f : 0.55f);

            if (_canClaimNearby)
            {
                _ui.DrawButton(left + buttonW + layout.S(10f), footerY, buttonW, buttonH, "CLAIM OUTPOST",
                    _hoveredButton == 12, false, layout.S(0.9f), alpha);
            }

            float closeX = panelX + panelW - layout.S(20f) - buttonW;
            float closeY = panelY + panelH - layout.S(30f);
            _ui.DrawButton(closeX, closeY, buttonW, buttonH, "CLOSE", _hoveredButton == 11, false, layout.S(1.0f), alpha);

            _ui.DrawCenteredText("ESC CLOSE  |  ENTER CONFIRM", panelY + panelH - layout.S(8f), layout.S(0.88f),
                new Color(0.45f, 0.5f, 0.58f) * alpha);
        }

        private void HandleFoundingActivate()
        {
            if (_hoveredButton == 11)
            {
                CloseRequested = true;
                return;
            }

            if (_hoveredButton == 12)
            {
                ClaimRequested = true;
                return;
            }

            if (_hoveredButton == 14 && CanAffordTownHeart())
            {
                PlaceTownHeartRequested = true;
            }
        }

        private bool CanAffordTownHeart()
        {
            if (_playerCreative)
            {
                return true;
            }

            return PlayerStructureRegistry.TryGet("town_heart", out var blueprint)
                && _playerPayer != null
                && blueprint.CanAfford(_playerPayer);
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

            _ui.DrawFullscreenBackground(UiTheme.PanelFill * 0.72f * alpha);
            _ui.DrawVignette(0.35f, alpha);
            _ui.DrawFramedPanel(panelX, panelY, panelW, panelH, UiTheme.PanelBgMuted, accent, alpha);

            DrawHeader(layout, panelX, panelY, panelW, alpha, accent);
            DrawTabBar(layout, left, panelY, alpha, accent);

            float contentY = panelY + layout.S(ContentTop);
            float contentH = panelH - layout.S(ContentTop) - layout.S(FooterHeight);
            switch (_selectedTab)
            {
                case 1:
                    DrawBuildTab(layout, left, contentY, contentH, alpha, accent);
                    break;
                case 2:
                    DrawPeopleTab(layout, panelX, contentY, contentH, alpha, accent);
                    break;
                case 3:
                    DrawGoalsTab(layout, left, contentY, contentH, alpha, accent);
                    break;
                default:
                    DrawOverviewTab(layout, left, contentY, contentH, alpha, accent);
                    break;
            }

            float footerY = panelY + panelH - layout.S(FooterHeight);
            string recruitLabel = GetRecruitButtonLabel();
            bool canRecruit = CountDisplayedCitizens() > 0
                ? _village.CanRecruit(_villagers, _playerCreative)
                : CanSummonSettlers();
            _ui.DrawButton(left, footerY, buttonW, buttonH, recruitLabel, _hoveredButton == 10, false, layout.S(UiTheme.ScaleSmall), alpha,
                canRecruit ? 1f : 0.55f);

            if (_selectedTab == 0)
            {
                if (_canClaimNearby)
                {
                    _ui.DrawButton(left + buttonW + layout.S(10f), footerY, buttonW, buttonH, "CLAIM OUTPOST",
                        _hoveredButton == 12, false, layout.S(UiTheme.ScaleSmall), alpha);
                }

                _ui.DrawButton(left + (buttonW + layout.S(10f)) * (_canClaimNearby ? 2f : 1f), footerY, buttonW, buttonH,
                    "PAINT ZONE", _hoveredButton == 13, false, layout.S(UiTheme.ScaleSmall), alpha);
                float rationX = left + (buttonW + layout.S(10f)) * (_canClaimNearby ? 3f : 2f);
                _ui.DrawButton(rationX, footerY, buttonW, buttonH, "TAKE RATIONS", _hoveredButton == 16, false,
                    layout.S(UiTheme.ScaleSmall), alpha);
            }

            float closeX = panelX + panelW - layout.S(20f) - buttonW;
            float closeY = panelY + panelH - layout.S(30f);
            _ui.DrawButton(closeX, closeY, buttonW, buttonH, "CLOSE", _hoveredButton == 11, false, layout.S(UiTheme.ScaleNormal), alpha);

            string footerHint;
            if (_hoveredButton == 10)
            {
                footerHint = CountDisplayedCitizens() > 0
                    ? "RECRUIT - BRING A NEW PEASANT TO THE VILLAGE (COSTS 4 OAK PLANKS)"
                    : "SUMMON SETTLERS - RESTORE MISSING STARTER CITIZENS AT THE TOWN HEART";
            }
            else if (_hoveredButton == 12)
            {
                footerHint = "CLAIM OUTPOST - CLAIM AN ABANDONED OUTPOST STRUCTURE TO RECRUIT A FREE CITIZEN";
            }
            else if (_hoveredButton == 13)
            {
                footerHint = "PAINT ZONE - DEFINE A GATHERING OR CLEARING WORK ZONE FOR YOUR VILLAGERS";
            }
            else if (_hoveredButton == 11)
            {
                footerHint = "CLOSE - EXIT THE TOWN BOARD INTERFACE";
            }
            else if (_hoveredButton >= 20 && _hoveredButton < 30)
            {
                footerHint = "CLICK TO INITIATE BLUEPRINT PLACEMENT PREVIEW";
            }
            else
            {
                footerHint = _selectedTab switch
                {
                    1 => "CLICK A BUILDING CARD TO PLACE IN THE WORLD",
                    2 => "SELECT A VILLAGER THEN ASSIGN A JOB",
                    3 => _playWithAi ? "STEWARD GOALS TRACK VILLAGE PRIORITIES" : "MANUAL GOALS TRACK LOCAL RESOURCE PRIORITIES",
                    _ => "ESC CLOSE  |  R RECRUIT  |  KEYS 1-4 SWITCH TABS  |  SCROLL LISTS"
                };
            }
            _ui.DrawCenteredText(footerHint, panelY + panelH - layout.S(8f), layout.S(UiTheme.ScaleSmall),
                UiTheme.Hint * alpha);
        }

        private void DrawHeader(ScreenLayout layout, float panelX, float panelY, float panelW, float alpha, Color accent)
        {
            string name = _isEditingName ? _editingNameBuffer + "_" : _village!.Name;
            string title = name.ToUpperInvariant();
            Color titleColor = _hoveredButton == 90 ? Color.White : UiTheme.Title;
            _ui.DrawCenteredTitle(title, panelY + layout.S(14f), layout.S(UiTheme.ScaleTitle), titleColor * alpha);

            if (_hoveredButton == 90 || _isEditingName)
            {
                float titleW = layout.S(400f);
                float titleH = layout.S(24f);
                float titleX = layout.Ui.CenterX - titleW / 2f;
                float rectY = panelY + layout.S(10f);
                _ui.DrawPanel(titleX, rectY, titleW, titleH, Color.Transparent, accent * 0.5f, 0.5f * alpha);
            }

            string tier = _village!.Tier.ToString().ToUpperInvariant();
            int citizenCount = CountDisplayedCitizens();
            string subtitle = $"{tier}  |  {citizenCount}/{_village.PopulationCap} CITIZENS  |  TIER {_village.Tier}";
            if (_playerCreative)
            {
                subtitle += "  |  CREATIVE";
            }

            _ui.DrawCenteredText(subtitle, panelY + layout.S(36f), layout.S(UiTheme.ScaleSmall), UiTheme.Subtitle * alpha);

            float badgeW = layout.S(88f);
            float badgeH = layout.S(18f);
            float badgeX = panelX + panelW - layout.S(24f) - badgeW;
            float badgeY = panelY + layout.S(14f);
            _ui.DrawFramedPanel(badgeX, badgeY, badgeW, badgeH, new Color(0.08f, 0.12f, 0.1f), accent, alpha * 0.9f);
            _ui.DrawString(tier, badgeX + layout.S(10f), badgeY + layout.S(4f), layout.S(0.85f), accent * alpha);
        }

        private bool IsGoalsTabVisible() => _earlyGuideStage >= 3;

        private int VisibleTabCount => IsGoalsTabVisible() ? TabLabels.Length : TabLabels.Length - 1;

        private int MapVisibleTabToIndex(int visibleIndex)
        {
            if (visibleIndex < 3 || IsGoalsTabVisible())
            {
                return visibleIndex;
            }

            return 0;
        }

        private void DrawTabBar(ScreenLayout layout, float left, float panelY, float alpha, Color accent)
        {
            float tabY = panelY + layout.S(58f);
            float tabX = left;
            for (int i = 0; i < TabLabels.Length; i++)
            {
                if (i == 3 && !IsGoalsTabVisible())
                {
                    continue;
                }

                bool selected = i == _selectedTab;
                bool hovered = _hoveredButton == i;
                Color fill = selected ? UiTheme.PanelBgHighlight : UiTheme.PanelBgMuted;
                Color border = selected || hovered ? accent : UiTheme.Rule;
                _ui.DrawFramedPanel(tabX, tabY, layout.S(TabWidth), layout.S(TabHeight), fill, border, alpha);
                Color textColor = selected ? UiTheme.Title : UiTheme.Meta;
                string label = TabLabels[i];
                float textW = _ui.MeasureString(label, layout.S(UiTheme.ScaleSmall));
                _ui.DrawString(label, tabX + (layout.S(TabWidth) - textW) / 2f, tabY + layout.S(8f), layout.S(UiTheme.ScaleSmall),
                    textColor * alpha);
                tabX += layout.S(TabWidth) + layout.S(8f);
            }
        }

        private void DrawOverviewTab(ScreenLayout layout, float left, float y, float height, float alpha, Color accent)
        {
            float cardW = layout.S(198f);
            float cardH = layout.S(68f);
            float gap = layout.S(10f);
            float x = left;

            if (_viewModel != null)
            {
                _ui.DrawString(_viewModel.StatusLine, left, y - layout.S(18f), layout.S(UiTheme.ScaleSmall),
                    UiTheme.Subtitle * alpha);
                _ui.DrawString("Next: " + _viewModel.NextAction, left, y - layout.S(4f), layout.S(UiTheme.ScaleSmall),
                    UiTheme.Hint * alpha);
            }

            int citizens = CountDisplayedCitizens();
            if (citizens == 0)
            {
                float bannerH = layout.S(54f);
                _ui.DrawFramedPanel(left, y, layout.S(PanelWidth) - layout.S(40f), bannerH,
                    new Color(0.15f, 0.08f, 0.08f), UiTheme.Danger, alpha);
                string bannerTitle = VillageSettlementHealth.IsPlayerNearTownHeart(_village!, _playerPos)
                    ? "NO SETTLERS IN VILLAGE"
                    : "NO SETTLERS NEARBY";
                _ui.DrawString(bannerTitle, left + layout.S(14f), y + layout.S(10f), layout.S(UiTheme.ScaleNormal),
                    UiTheme.Title * alpha);
                _ui.DrawString(VillageGuidance.GetQuickStartSteps(_village!, _villagers, _playerPos), left + layout.S(14f),
                    y + layout.S(30f), layout.S(UiTheme.ScaleSmall), UiTheme.Subtitle * alpha);
                y += bannerH + layout.S(10f);
            }
            else if (citizens > 0 && citizens <= 2)
            {
                float bannerH = layout.S(40f);
                _ui.DrawFramedPanel(left, y, layout.S(PanelWidth) - layout.S(40f), bannerH,
                    UiTheme.PanelBgMuted, UiTheme.Accent, alpha);
                _ui.DrawString("QUICK START: " + VillageGuidance.GetQuickStartSteps(_village!, _villagers, _playerPos),
                    left + layout.S(14f), y + layout.S(12f), layout.S(UiTheme.ScaleSmall), UiTheme.Subtitle * alpha);
                y += bannerH + layout.S(10f);
            }

            DrawStatCard(layout, x, y, cardW, cardH, "POPULATION", $"{citizens}/{_village!.PopulationCap}",
                (float)citizens / Math.Max(1, _village.PopulationCap), new Color(0.45f, 0.78f, 0.55f), alpha, accent);
            x += cardW + gap;
            DrawStatCard(layout, x, y, cardW, cardH, "FOOD", $"{_village.FoodStock:F0}",
                Math.Clamp(_village.FoodStock / Math.Max(1f, Math.Max(1, citizens) * 2f), 0f, 1f),
                new Color(0.92f, 0.72f, 0.28f), alpha, accent);
            x += cardW + gap;
            DrawStatCard(layout, x, y, cardW, cardH, "HAPPINESS", $"{_village.Happiness:P0}", _village.Happiness,
                new Color(0.55f, 0.82f, 0.95f), alpha, accent);
            x += cardW + gap;
            DrawStatCard(layout, x, y, cardW, cardH, "HOUSING", $"{citizens}/{Math.Max(1, _village.HousingCapacity)}",
                Math.Clamp((float)citizens / Math.Max(1, _village.HousingCapacity), 0f, 1f),
                new Color(0.78f, 0.58f, 0.92f), alpha, accent);

            y += cardH + layout.S(14f);

            if (!string.IsNullOrWhiteSpace(_openingNote))
            {
                float bannerH = layout.S(42f);
                _ui.DrawFramedPanel(left, y, layout.S(PanelWidth) - layout.S(40f), bannerH,
                    UiTheme.PanelBgMuted, UiTheme.Accent, alpha);
                _ui.DrawString(_openingNote!, left + layout.S(14f), y + layout.S(12f), layout.S(UiTheme.ScaleSmall),
                    UiTheme.Subtitle * alpha);
                y += bannerH + layout.S(10f);
            }

            float colW = (layout.S(PanelWidth) - layout.S(40f) - layout.S(12f)) / 2f;
            float colH = height - (y - (layout.PanelY + layout.S(ContentTop))) - layout.S(8f);
            DrawStoragePanel(layout, left, y, colW, colH, alpha, accent);
            DrawActivityPanel(layout, left + colW + layout.S(12f), y, colW, colH, alpha, accent);
        }

        private void DrawStoragePanel(ScreenLayout layout, float x, float y, float w, float h, float alpha, Color accent)
        {
            _ui.DrawFramedPanel(x, y, w, h, UiTheme.PanelBgMuted, accent, alpha * 0.95f);
            _ui.DrawString("VILLAGE STORAGE", x + layout.S(12f), y + layout.S(10f), layout.S(UiTheme.ScaleSection),
                UiTheme.Section * alpha);

            float rowY = y + layout.S(28f);
            int shown = 0;
            for (int i = 0; i < _village!.Storage.SlotCount && shown < 10; i++)
            {
                var stack = _village.Storage.GetSlot(i);
                if (stack.IsEmpty)
                {
                    continue;
                }

                string label = FormatStack(stack);
                _ui.DrawString(label, x + layout.S(14f), rowY, layout.S(UiTheme.ScaleSmall), UiTheme.StatValue * alpha);
                rowY += layout.S(17f);
                shown++;
            }

            if (shown == 0)
            {
                _ui.DrawString("EMPTY — HAULERS DELIVER HERE", x + layout.S(14f), rowY, layout.S(UiTheme.ScaleSmall),
                    UiTheme.Hint * alpha);
            }

            int plankCount = _village.Storage.CountBlock(VillageEntity.RationBlock);
            float recruitY = y + h - layout.S(36f);
            string recruitHint = _playerCreative
                ? "RECRUIT COST: FREE IN CREATIVE"
                : $"RECRUIT COST: {VillageEntity.RecruitFoodCost} OAK PLANK ({plankCount} IN STORAGE)";
            _ui.DrawHorizontalRule(x + layout.S(10f), recruitY - layout.S(8f), w - layout.S(20f),
                UiTheme.Rule, layout.S(1f), alpha);
            _ui.DrawString(recruitHint, x + layout.S(14f), recruitY, layout.S(UiTheme.ScaleSmall),
                UiTheme.Meta * alpha);
        }

        private void DrawActivityPanel(ScreenLayout layout, float x, float y, float w, float h, float alpha, Color accent)
        {
            _ui.DrawFramedPanel(x, y, w, h, UiTheme.PanelBgMuted, accent, alpha * 0.95f);
            _ui.DrawString("ACTIVITY", x + layout.S(12f), y + layout.S(10f), layout.S(UiTheme.ScaleSection),
                UiTheme.Section * alpha);

            float rowY = y + layout.S(28f);
            int pendingSites = 0;
            foreach (var site in _village!.BuildingSites)
            {
                if (site.IsComplete)
                {
                    continue;
                }

                pendingSites++;
                string line = $"{site.BlueprintId.ToUpperInvariant()} {site.CompletionRatio:P0}";
                _ui.DrawString(line, x + layout.S(14f), rowY, layout.S(UiTheme.ScaleSmall), UiTheme.Subtitle * alpha);
                DrawMiniBar(x + layout.S(14f), rowY + layout.S(14f), w - layout.S(28f), layout.S(6f),
                    site.CompletionRatio, UiTheme.Accent, alpha);
                rowY += layout.S(28f);
            }

            if (pendingSites == 0)
            {
                _ui.DrawString("NO ACTIVE CONSTRUCTION", x + layout.S(14f), rowY, layout.S(UiTheme.ScaleSmall),
                    UiTheme.Hint * alpha);
                rowY += layout.S(20f);
            }

            rowY += layout.S(6f);
            _ui.DrawString("GATHER QUEUE", x + layout.S(10f), rowY, layout.S(UiTheme.ScaleSmall), UiTheme.Section * alpha);
            rowY += layout.S(18f);
            int queued = _village.WorkQueue.Count;
            string queueLine = queued == 0
                ? "SHIFT+CLICK BLOCKS OR PAINT ZONE"
                : $"{queued} BLOCK(S) MARKED FOR WORKERS";
            _ui.DrawString(queueLine, x + layout.S(14f), rowY, layout.S(UiTheme.ScaleSmall), UiTheme.Meta * alpha);
            rowY += layout.S(20f);

            _ui.DrawString("QUICK GUIDE", x + layout.S(10f), rowY, layout.S(UiTheme.ScaleSmall), UiTheme.Section * alpha);
            rowY += layout.S(18f);
            int guideLines = 0;
            foreach (string line in GetGuideLines())
            {
                if (guideLines >= 4)
                {
                    break;
                }

                _ui.DrawString($"• {line}", x + layout.S(14f), rowY, layout.S(UiTheme.ScaleSmall), UiTheme.Meta * alpha);
                rowY += layout.S(16f);
                guideLines++;
            }
        }

        private void DrawBuildTab(ScreenLayout layout, float left, float y, float height, float alpha, Color accent)
        {
            float sectionH = layout.S(130f);
            _ui.DrawFramedPanel(left, y, layout.S(PanelWidth) - layout.S(40f), sectionH,
                UiTheme.PanelBgMuted, accent, alpha * 0.95f);
            _ui.DrawString("CONSTRUCTION SITES", left + layout.S(12f), y + layout.S(10f), layout.S(UiTheme.ScaleSection),
                UiTheme.Section * alpha);

            float siteY = y + layout.S(30f);
            int drawn = 0;
            foreach (var site in _village!.BuildingSites)
            {
                if (site.IsComplete || drawn >= 3)
                {
                    continue;
                }

                string name = site.BlueprintId.ToUpperInvariant();
                _ui.DrawString(name, left + layout.S(16f), siteY, layout.S(UiTheme.ScaleNormal), UiTheme.StatValue * alpha);
                DrawMiniBar(left + layout.S(200f), siteY + layout.S(4f), layout.S(420f), layout.S(10f),
                    site.CompletionRatio, UiTheme.Accent, alpha);
                _ui.DrawString($"{site.CompletionRatio:P0}", left + layout.S(640f), siteY, layout.S(UiTheme.ScaleSmall),
                    UiTheme.Subtitle * alpha);
                siteY += layout.S(28f);
                drawn++;
            }

            if (drawn == 0)
            {
                _ui.DrawString("NO BUILD SITES — PICK A STRUCTURE BELOW", left + layout.S(16f), siteY, layout.S(UiTheme.ScaleSmall),
                    UiTheme.Hint * alpha);
            }

            float catalogY = y + sectionH + layout.S(12f);
            float catalogH = height - sectionH - layout.S(12f);
            _ui.DrawFramedPanel(left, catalogY, layout.S(PanelWidth) - layout.S(40f), catalogH,
                UiTheme.PanelBgMuted, accent, alpha * 0.95f);
            _ui.DrawString("BUILD CATALOG — CLICK TO PLACE", left + layout.S(12f), catalogY + layout.S(10f),
                layout.S(UiTheme.ScaleSection), UiTheme.Section * alpha);

            float cardY = catalogY + layout.S(34f) - _buildScroll;
            float cardH = layout.S(58f);
            float cardW = layout.S(PanelWidth) - layout.S(64f);
            int buildIndex = 0;
            foreach (var blueprint in PlayerStructureRegistry.All)
            {
                if (blueprint.Id == "town_heart")
                {
                    continue;
                }

                bool hovered = _hoveredButton == 20 + buildIndex;
                bool canAfford = CanAffordBlueprint(blueprint);
                Color cardBorder = hovered ? accent : (canAfford ? UiTheme.PanelBorder : UiTheme.Rule);
                Color cardFill = hovered ? UiTheme.PanelBgHighlight : UiTheme.PanelBgMuted;
                if (cardY + cardH >= catalogY + layout.S(30f) && cardY <= catalogY + catalogH - layout.S(6f))
                {
                    _ui.DrawFramedPanel(left + layout.S(12f), cardY, cardW, cardH, cardFill, cardBorder, alpha);
                    _ui.DrawString(blueprint.DisplayName.ToUpperInvariant(), left + layout.S(22f), cardY + layout.S(10f),
                        layout.S(1.02f), (canAfford ? UiTheme.Title : UiTheme.Meta) * alpha);
                    _ui.DrawString(GetBuildingBlurb(blueprint.Kind), left + layout.S(22f), cardY + layout.S(28f),
                        layout.S(UiTheme.ScaleSmall), UiTheme.Meta * alpha);
                    string costs = FormatCosts(blueprint);
                    float costW = _ui.MeasureString(costs, layout.S(UiTheme.ScaleSmall));
                    _ui.DrawString(costs, left + layout.S(12f) + cardW - costW - layout.S(12f), cardY + layout.S(18f),
                        layout.S(UiTheme.ScaleSmall), (canAfford ? UiTheme.Subtitle : UiTheme.Danger) * alpha);
                }

                cardY += cardH + layout.S(8f);
                buildIndex++;
            }
        }

        private void DrawPeopleTab(ScreenLayout layout, float panelX, float y, float height, float alpha, Color accent)
        {
            float left = layout.Left;
            float listW = layout.S(300f);
            float detailX = left + listW + layout.S(14f);
            float detailW = layout.S(PanelWidth) - layout.S(40f) - listW - layout.S(14f);

            _ui.DrawFramedPanel(left, y, listW, height, UiTheme.PanelBgMuted, accent, alpha * 0.95f);
            int citizenCount = CountDisplayedCitizens();
            _ui.DrawString($"CITIZENS ({citizenCount})", left + layout.S(12f), y + layout.S(10f), layout.S(UiTheme.ScaleSection),
                UiTheme.Section * alpha);

            float rowY = y + layout.S(32f) - _peopleScroll;
            float rowH = layout.S(42f);
            foreach (var villager in EnumerateCitizens())
            {
                int villagerId = villager.Id;
                bool isSelected = villagerId == _selectedVillagerId;
                bool hovered = _hoveredButton == 30 + villagerId;
                if (rowY + rowH >= y + layout.S(28f) && rowY <= y + height - layout.S(6f))
                {
                    if (isSelected || hovered)
                    {
                        _ui.DrawFilledRect(left + layout.S(8f), rowY, listW - layout.S(16f), rowH,
                            (isSelected ? UiTheme.PanelBgHighlight : new Color(0.08f, 0.12f, 0.15f)) * alpha);
                    }

                    var roleColor = VillagerVisuals.GetRoleColor(villager.Role);
                    _ui.DrawFilledRect(left + layout.S(14f), rowY + layout.S(12f), layout.S(10f), layout.S(10f), roleColor * alpha);
                    _ui.DrawString(villager.Name.ToUpperInvariant(), left + layout.S(30f), rowY + layout.S(8f),
                        layout.S(UiTheme.ScaleNormal), (isSelected ? UiTheme.Title : UiTheme.StatValue) * alpha);
                    string statusText = $"{villager.Role} · {villager.CurrentJob}";
                    Color textCol = roleColor;
                    if (_village!.ConsecutiveDaysWithoutFood >= 2)
                    {
                        statusText = "STARVING · " + statusText;
                        textCol = UiTheme.Danger;
                    }
                    _ui.DrawString(statusText.ToUpperInvariant(), left + layout.S(30f),
                        rowY + layout.S(24f), layout.S(UiTheme.ScaleSmall), textCol * alpha);
                }

                rowY += rowH + layout.S(4f);
            }

            if (!HasDisplayedCitizens())
            {
                string emptyHint = VillageSettlementHealth.IsPlayerNearTownHeart(_village!, _playerPos)
                    ? "NO VILLAGERS — CLICK SUMMON SETTLERS BELOW"
                    : "NO VILLAGERS — WALK TO TOWN HEART, THEN SUMMON SETTLERS";
                _ui.DrawString(emptyHint, left + layout.S(14f), y + layout.S(40f), layout.S(UiTheme.ScaleSmall),
                    UiTheme.Hint * alpha);
            }

            _ui.DrawFramedPanel(detailX, y, detailW, height, UiTheme.PanelBgMuted, accent, alpha * 0.95f);
            if (_selectedVillagerId >= 0 && _villagers.TryGet(_selectedVillagerId, out var selected))
            {
                DrawVillagerDetail(layout, detailX, y, detailW, height, selected, alpha, accent);
            }
            else
            {
                _ui.DrawString("SELECT A CITIZEN", detailX + layout.S(16f), y + layout.S(40f), layout.S(UiTheme.ScaleNormal),
                    UiTheme.Hint * alpha);
            }
        }

        private void DrawVillagerDetail(ScreenLayout layout, float x, float y, float w, float h, Villager villager, float alpha, Color accent)
        {
            float pad = layout.S(16f);
            float detailY = y + pad;
            var roleColor = VillagerVisuals.GetRoleColor(villager.Role);

            _ui.DrawString(villager.Name.ToUpperInvariant(), x + pad, detailY, layout.S(UiTheme.ScaleTitle),
                UiTheme.Title * alpha);
            detailY += layout.S(28f);
            string detailStatusText = $"{villager.Role}  ·  {villager.CurrentJob}";
            Color detailCol = roleColor;
            if (_village!.ConsecutiveDaysWithoutFood >= 2)
            {
                detailStatusText = "STARVING  ·  " + detailStatusText;
                detailCol = UiTheme.Danger;
            }
            _ui.DrawString(detailStatusText.ToUpperInvariant(), x + pad, detailY,
                layout.S(UiTheme.ScaleNormal), detailCol * alpha);
            detailY += layout.S(24f);
            _ui.DrawString($"TRAIT {villager.Persona.Trait.ToUpperInvariant()}", x + pad, detailY, layout.S(UiTheme.ScaleSmall),
                UiTheme.Meta * alpha);
            detailY += layout.S(28f);

            _ui.DrawString(
                $"SKILLS  MINING {villager.Skills.Mining.Level}  WOOD {villager.Skills.Woodcutting.Level}  FARM {villager.Skills.Farming.Level}",
                x + pad, detailY, layout.S(UiTheme.ScaleSmall), UiTheme.Meta * alpha);
            detailY += layout.S(28f);
            _ui.DrawProgressBar(x + pad, detailY, w - pad * 2f, layout.S(12f), villager.Happiness, "MORALE", UiTheme.ScaleSmall, alpha);
            detailY += layout.S(52f);

            _ui.DrawButton(x + pad, detailY, layout.S(96f), layout.S(ButtonHeight), "TALK", _hoveredButton == 50, false,
                layout.S(0.95f), alpha);
            detailY += layout.S(48f);
            _ui.DrawString("ASSIGN JOB", x + pad, detailY, layout.S(1.05f), new Color(0.55f, 0.75f, 0.65f) * alpha);
            detailY += layout.S(24f);

            float jobW = layout.S(96f);
            float jobH = layout.S(ButtonHeight);
            float jobGap = layout.S(10f);
            for (int i = 0; i < AssignableJobs.Length; i++)
            {
                int row = i / 3;
                int col = i % 3;
                float jobX = x + pad + col * (jobW + jobGap);
                float jobY = detailY + row * (jobH + jobGap);
                _ui.DrawButton(jobX, jobY, jobW, jobH, AssignableJobs[i].Label, _hoveredButton == 40 + i, false,
                    layout.S(0.88f), alpha);
            }
        }

        private void DrawGoalsTab(ScreenLayout layout, float left, float y, float height, float alpha, Color accent)
        {
            _ui.DrawFramedPanel(left, y, layout.S(PanelWidth) - layout.S(40f), height,
                new Color(0.06f, 0.08f, 0.11f), accent, alpha * 0.95f);

            if (_playWithAi)
            {
                _ui.DrawString("STEWARD GOALS", left + layout.S(12f), y + layout.S(10f), layout.S(UiTheme.ScaleSection),
                    UiTheme.Section * alpha);
                _ui.DrawString("PRIORITY TASKS SET BY THE VILLAGE AI STEWARD", left + layout.S(12f), y + layout.S(28f),
                    layout.S(UiTheme.ScaleSmall), UiTheme.Hint * alpha);

                float rowY = y + layout.S(52f);
                int shown = 0;
                foreach (var goal in _village!.Scheduler.Goals)
                {
                    if (shown >= 8)
                    {
                        break;
                    }

                    Color statusColor = goal.Completed ? UiTheme.Accent : UiTheme.StatValue;
                    string status = goal.Completed ? "DONE" : "ACTIVE";
                    _ui.DrawString($"[{status}] {goal.Description.ToUpperInvariant()}", left + layout.S(16f), rowY,
                        layout.S(UiTheme.ScaleSmall), statusColor * alpha);

                    if (!goal.Completed && goal.Kind == VillageGoalKind.Stock && goal.StockBlock.HasValue && goal.TargetCount > 0)
                    {
                        int have = _village.Scheduler.GetStockProgress(goal, _village);
                        float progress = Math.Clamp(have / (float)goal.TargetCount, 0f, 1f);
                        DrawMiniBar(left + layout.S(16f), rowY + layout.S(16f), layout.S(500f), layout.S(6f), progress,
                            UiTheme.Accent, alpha);
                        _ui.DrawString($"{have}/{goal.TargetCount} {goal.StockBlock.Value}".ToUpperInvariant(),
                            left + layout.S(530f), rowY + layout.S(12f), layout.S(UiTheme.ScaleSmall), UiTheme.Meta * alpha);
                        rowY += layout.S(34f);
                    }
                    else
                    {
                        rowY += layout.S(22f);
                    }

                    shown++;
                }

                if (shown == 0)
                {
                    _ui.DrawString("NO ACTIVE GOALS — CHAT WITH THE STEWARD (C) TO SET PRIORITIES", left + layout.S(16f),
                        rowY + layout.S(10f), layout.S(UiTheme.ScaleSmall), UiTheme.Hint * alpha);
                }
            }
            else
            {
                _ui.DrawString("MANUAL GOALS", left + layout.S(12f), y + layout.S(10f), layout.S(UiTheme.ScaleSection),
                    UiTheme.Section * alpha);
                _ui.DrawString("DEFINE LOCAL RESOURCE GATHERING PRIORITIES FOR YOUR VILLAGERS", left + layout.S(12f), y + layout.S(28f),
                    layout.S(UiTheme.ScaleSmall), UiTheme.Hint * alpha);

                float colLeft = left + layout.S(16f);
                _ui.DrawString("CREATE NEW GOAL", colLeft, y + layout.S(52f), layout.S(UiTheme.ScaleSmall), UiTheme.Subtitle * alpha);

                _ui.DrawString("RESOURCE:", colLeft, y + layout.S(78f), layout.S(UiTheme.ScaleSmall), UiTheme.Meta * alpha);
                for (int i = 0; i < GoalBlockTypes.Length; i++)
                {
                    float btnX = colLeft + i * layout.S(70f);
                    float btnY = y + layout.S(94f);
                    bool selected = i == _selectedGoalBlockIndex;
                    bool hovered = _hoveredButton == 60 + i;
                    Color btnAccent = selected ? UiTheme.Accent : accent;
                    _ui.DrawFramedPanel(btnX, btnY, layout.S(64f), layout.S(24f),
                        selected ? UiTheme.PanelBgHighlight * alpha : UiTheme.PanelBgMuted * alpha,
                        hovered ? UiTheme.Accent : btnAccent, alpha);
                    string label = GoalBlockTypes[i].ToString().ToUpperInvariant();
                    if (label.Length > 8) label = label.Substring(0, 8);
                    float labelW = _ui.MeasureString(label, layout.S(UiTheme.ScaleSmall));
                    _ui.DrawString(label, btnX + (layout.S(64f) - labelW) / 2f, btnY + layout.S(5f), layout.S(UiTheme.ScaleSmall),
                        (selected ? UiTheme.Title : UiTheme.Meta) * alpha);
                }

                _ui.DrawString("TARGET AMOUNT:", colLeft, y + layout.S(132f), layout.S(UiTheme.ScaleSmall), UiTheme.Meta * alpha);
                for (int j = 0; j < GoalTargetCounts.Length; j++)
                {
                    float btnX = colLeft + j * layout.S(70f);
                    float btnY = y + layout.S(148f);
                    bool selected = j == _selectedGoalCountIndex;
                    bool hovered = _hoveredButton == 70 + j;
                    Color btnAccent = selected ? UiTheme.Accent : accent;
                    _ui.DrawFramedPanel(btnX, btnY, layout.S(64f), layout.S(24f),
                        selected ? UiTheme.PanelBgHighlight * alpha : UiTheme.PanelBgMuted * alpha,
                        hovered ? UiTheme.Accent : btnAccent, alpha);
                    string label = GoalTargetCounts[j].ToString();
                    float labelW = _ui.MeasureString(label, layout.S(UiTheme.ScaleSmall));
                    _ui.DrawString(label, btnX + (layout.S(64f) - labelW) / 2f, btnY + layout.S(5f), layout.S(UiTheme.ScaleSmall),
                        (selected ? UiTheme.Title : UiTheme.Meta) * alpha);
                }

                float addX = colLeft;
                float addY = y + layout.S(196f);
                bool addHovered = _hoveredButton == 80;
                _ui.DrawButton(addX, addY, layout.S(140f), layout.S(30f), "ADD GOAL", addHovered, false, layout.S(UiTheme.ScaleSmall), alpha);

                float rightLeft = left + layout.S(420f);
                float rightW = layout.S(PanelWidth) - layout.S(40f) - layout.S(420f) - layout.S(16f);
                _ui.DrawString("ACTIVE GOALS", rightLeft, y + layout.S(52f), layout.S(UiTheme.ScaleSmall), UiTheme.Subtitle * alpha);

                float rowY = y + layout.S(78f);
                int shown = 0;
                foreach (var goal in _village!.Scheduler.Goals)
                {
                    if (shown >= 6)
                    {
                        break;
                    }

                    Color statusColor = goal.Completed ? UiTheme.Accent : UiTheme.StatValue;
                    string status = goal.Completed ? "DONE" : "ACTIVE";
                    string desc = goal.Description.ToUpperInvariant();
                    _ui.DrawString($"[{status}] {desc}", rightLeft, rowY, layout.S(UiTheme.ScaleSmall), statusColor * alpha);

                    float remW = layout.S(64f);
                    float remH = layout.S(20f);
                    float remX = rightLeft + rightW - remW;
                    bool remHovered = _hoveredButton == 100 + goal.Id;
                    _ui.DrawButton(remX, rowY - layout.S(2f), remW, remH, "REMOVE", remHovered, false, layout.S(UiTheme.ScaleSmall), alpha);

                    if (!goal.Completed && goal.Kind == VillageGoalKind.Stock && goal.StockBlock.HasValue && goal.TargetCount > 0)
                    {
                        int have = _village.Scheduler.GetStockProgress(goal, _village);
                        float progress = Math.Clamp(have / (float)goal.TargetCount, 0f, 1f);
                        DrawMiniBar(rightLeft, rowY + layout.S(16f), rightW - remW - layout.S(12f), layout.S(6f), progress,
                            UiTheme.Accent, alpha);
                        _ui.DrawString($"{have}/{goal.TargetCount} {goal.StockBlock.Value}".ToUpperInvariant(),
                            rightLeft + rightW - remW - layout.S(120f), rowY + layout.S(26f), layout.S(UiTheme.ScaleSmall), UiTheme.Meta * alpha);
                        rowY += layout.S(40f);
                    }
                    else
                    {
                        rowY += layout.S(26f);
                    }

                    shown++;
                }

                if (shown == 0)
                {
                    _ui.DrawString("NO ACTIVE GOALS SPECIFIED", rightLeft, rowY + layout.S(10f), layout.S(UiTheme.ScaleSmall),
                        UiTheme.Hint * alpha);
                }
            }
        }

        private void HitManualGoals(ScreenLayout layout, MouseState mouse)
        {
            float y = layout.PanelY + layout.S(ContentTop);
            float colLeft = layout.Left + layout.S(16f);

            for (int i = 0; i < GoalBlockTypes.Length; i++)
            {
                float btnX = colLeft + i * layout.S(70f);
                float btnY = y + layout.S(94f);
                HitRect(btnX, btnY, layout.S(64f), layout.S(24f), 60 + i, mouse);
            }

            for (int j = 0; j < GoalTargetCounts.Length; j++)
            {
                float btnX = colLeft + j * layout.S(70f);
                float btnY = y + layout.S(148f);
                HitRect(btnX, btnY, layout.S(64f), layout.S(24f), 70 + j, mouse);
            }

            HitRect(colLeft, y + layout.S(196f), layout.S(140f), layout.S(30f), 80, mouse);

            float rightLeft = layout.Left + layout.S(420f);
            float rightW = layout.S(PanelWidth) - layout.S(40f) - layout.S(420f) - layout.S(16f);
            float rowY = y + layout.S(78f);
            int shown = 0;
            foreach (var goal in _village!.Scheduler.Goals)
            {
                if (shown >= 6)
                {
                    break;
                }

                float remW = layout.S(64f);
                float remH = layout.S(20f);
                float remX = rightLeft + rightW - remW;
                HitRect(remX, rowY - layout.S(2f), remW, remH, 100 + goal.Id, mouse);

                if (!goal.Completed && goal.Kind == VillageGoalKind.Stock && goal.StockBlock.HasValue && goal.TargetCount > 0)
                {
                    rowY += layout.S(40f);
                }
                else
                {
                    rowY += layout.S(26f);
                }

                shown++;
            }
        }

        private void DrawStatCard(ScreenLayout layout, float x, float y, float w, float h, string label, string value, float ratio, Color barColor,
            float alpha, Color accent)
        {
            _ui.DrawFramedPanel(x, y, w, h, UiTheme.PanelBgMuted, accent, alpha * 0.92f);
            _ui.DrawString(label, x + layout.S(10f), y + layout.S(8f), layout.S(UiTheme.ScaleSmall), UiTheme.StatLabel * alpha);
            _ui.DrawString(value, x + layout.S(10f), y + layout.S(24f), layout.S(UiTheme.ScaleNormal), UiTheme.StatValue * alpha);
            DrawMiniBar(x + layout.S(10f), y + h - layout.S(14f), w - layout.S(20f), layout.S(6f), ratio, barColor, alpha);
        }

        private void DrawMiniBar(float x, float y, float w, float h, float ratio, Color fill, float alpha)
        {
            ratio = Math.Clamp(ratio, 0f, 1f);
            _ui.DrawFilledRect(x, y, w, h, UiTheme.PanelBgMuted * alpha);
            if (ratio > 0.01f)
            {
                _ui.DrawFilledRect(x, y, w * ratio, h, fill * alpha);
            }
        }

        private void HitTestTabs(ScreenLayout layout, MouseState mouse)
        {
            float tabY = layout.PanelY + layout.S(58f);
            float tabX = layout.Left;
            for (int i = 0; i < TabLabels.Length; i++)
            {
                if (i == 3 && !IsGoalsTabVisible())
                {
                    continue;
                }

                HitRect(tabX, tabY, layout.S(TabWidth), layout.S(TabHeight), i, mouse);
                tabX += layout.S(TabWidth) + layout.S(8f);
            }
        }

        private int CycleTab(int current, int delta)
        {
            int tab = current;
            for (int attempt = 0; attempt < TabLabels.Length; attempt++)
            {
                tab = (tab + delta + TabLabels.Length) % TabLabels.Length;
                if (tab != 3 || IsGoalsTabVisible())
                {
                    return tab;
                }
            }

            return current;
        }

        private void HitBuildCatalog(ScreenLayout layout, MouseState mouse, float buttonH)
        {
            float y = layout.PanelY + layout.S(ContentTop) + layout.S(142f);
            float cardH = layout.S(58f);
            float cardW = layout.S(PanelWidth) - layout.S(64f);
            float cardY = y - _buildScroll;
            int buildIndex = 0;
            foreach (var blueprint in PlayerStructureRegistry.All)
            {
                if (blueprint.Id == "town_heart")
                {
                    continue;
                }

                HitRect(layout.Left + layout.S(12f), cardY, cardW, cardH, 20 + buildIndex, mouse);
                cardY += cardH + layout.S(8f);
                buildIndex++;
            }
        }

        private void HitPeopleTab(ScreenLayout layout, MouseState mouse, float buttonH)
        {
            float y = layout.PanelY + layout.S(ContentTop);
            float listW = layout.S(300f);
            float rowY = y + layout.S(32f) - _peopleScroll;
            float rowH = layout.S(42f);
            foreach (var villager in EnumerateCitizens())
            {
                HitRect(layout.Left + layout.S(8f), rowY, listW - layout.S(16f), rowH, 30 + villager.Id, mouse);
                rowY += rowH + layout.S(4f);
            }

            if (_selectedVillagerId >= 0)
            {
                float detailX = layout.Left + listW + layout.S(14f);
                float detailW = layout.S(PanelWidth) - layout.S(40f) - listW - layout.S(14f);
                float pad = layout.S(16f);
                float talkY = y + pad + layout.S(152f);
                HitRect(detailX + pad, talkY, layout.S(96f), layout.S(ButtonHeight), 50, mouse);

                float jobY = talkY + layout.S(48f) + layout.S(24f);
                float jobW = layout.S(96f);
                float jobH = layout.S(ButtonHeight);
                float jobGap = layout.S(10f);
                for (int i = 0; i < AssignableJobs.Length; i++)
                {
                    int row = i / 3;
                    int col = i % 3;
                    HitRect(detailX + pad + col * (jobW + jobGap), jobY + row * (jobH + jobGap), jobW, jobH, 40 + i, mouse);
                }
            }
        }

        private void HandleActivate()
        {
            if (_hoveredButton >= 0 && _hoveredButton < TabLabels.Length)
            {
                _selectedTab = _hoveredButton;
                return;
            }

            if (_hoveredButton == 10)
            {
                if (CountDisplayedCitizens() > 0 && _village!.CanRecruit(_villagers, _playerCreative))
                {
                    RecruitRequested = true;
                }
                else if (CanSummonSettlers())
                {
                    SummonSettlersRequested = true;
                }

                return;
            }

            if (_hoveredButton == 11)
            {
                CloseRequested = true;
                return;
            }

            if (_hoveredButton == 12)
            {
                ClaimRequested = true;
                return;
            }

            if (_hoveredButton == 13)
            {
                RequestWorkZonePlacement = true;
                return;
            }

            if (_hoveredButton == 16 && _takeRationsAction != null && _playerPayer is Core.Player player)
            {
                _takeRationsAction(player);
                return;
            }

            if (_hoveredButton >= 20)
            {
                int buildIndex = 0;
                foreach (var blueprint in PlayerStructureRegistry.All)
                {
                    if (blueprint.Id == "town_heart")
                    {
                        continue;
                    }

                    if (_hoveredButton == 20 + buildIndex)
                    {
                        RequestedBlueprintId = blueprint.Id;
                        RequestBlueprintPlacement = true;
                        return;
                    }

                    buildIndex++;
                }
            }

            if (_hoveredButton >= 30 && _hoveredButton < 200)
            {
                _selectedVillagerId = _hoveredButton - 30;
                return;
            }

            if (_hoveredButton >= 40 && _hoveredButton < 40 + AssignableJobs.Length && _selectedVillagerId >= 0)
            {
                RequestedAssignVillagerId = _selectedVillagerId;
                RequestedAssignJob = AssignableJobs[_hoveredButton - 40].Job;
                return;
            }

            if (_hoveredButton == 50 && _selectedVillagerId >= 0)
            {
                RequestedChatVillagerId = _selectedVillagerId;
            }

            if (!_playWithAi)
            {
                if (_hoveredButton >= 60 && _hoveredButton < 60 + GoalBlockTypes.Length)
                {
                    _selectedGoalBlockIndex = _hoveredButton - 60;
                    return;
                }

                if (_hoveredButton >= 70 && _hoveredButton < 70 + GoalTargetCounts.Length)
                {
                    _selectedGoalCountIndex = _hoveredButton - 70;
                    return;
                }

                if (_hoveredButton == 80)
                {
                    var block = GoalBlockTypes[_selectedGoalBlockIndex];
                    int count = GoalTargetCounts[_selectedGoalCountIndex];
                    string desc = $"GATHER {count} {block.ToString().ToUpperInvariant()}";
                    _village!.Scheduler.AddStockGoal(block, count, 0, desc);
                    return;
                }

                if (_hoveredButton >= 101)
                {
                    int goalId = _hoveredButton - 100;
                    _village!.Scheduler.RemoveGoal(goalId);
                    return;
                }
            }
        }

        private void ResetRequests()
        {
            RecruitRequested = false;
            SummonSettlersRequested = false;
            ClaimRequested = false;
            PlaceTownHeartRequested = false;
            CloseRequested = false;
            RequestedBlueprintId = null;
            RequestedAssignVillagerId = -1;
            RequestedChatVillagerId = -1;
            RequestBlueprintPlacement = false;
            RequestWorkZonePlacement = false;
        }

        private bool CanAffordBlueprint(BuildingBlueprint blueprint)
        {
            if (_playerCreative || _village == null)
            {
                return _playerCreative;
            }

            if (blueprint.CanAfford(_village.Storage))
            {
                return true;
            }

            return _playerPayer != null && blueprint.CanAfford(_playerPayer);
        }

        private void RefreshVillageState()
        {
            if (_village == null || _villageManager == null)
            {
                return;
            }

            if (_villageManager.RepairVillageCitizens(_village, _world!))
            {
                _selectedTab = 2;
            }

            _villageManager.SyncCitizensForVillage(_village);
            _viewModel = VillageViewModel.Build(_village, _villageManager, _villagers, _playerCreative, _playerPos);

            if (_selectedVillagerId < 0 || !CitizenExists(_selectedVillagerId))
            {
                foreach (var villager in EnumerateCitizens())
                {
                    _selectedVillagerId = villager.Id;
                    break;
                }
            }
        }

        private bool CitizenExists(int villagerId)
        {
            foreach (var villager in EnumerateCitizens())
            {
                if (villager.Id == villagerId)
                {
                    return true;
                }
            }

            return false;
        }

        private string GetRecruitButtonLabel()
        {
            if (_village == null)
            {
                return "RECRUIT";
            }

            int citizens = CountDisplayedCitizens();
            if (citizens == 0)
            {
                return CanSummonSettlers() ? "SUMMON SETTLERS" : "SUMMON SETTLERS (GO TO HEART)";
            }

            if (citizens >= _village.PopulationCap)
            {
                return "RECRUIT (BUILD HOUSE)";
            }

            if (_playerCreative || _village.Storage.CountBlock(VillageEntity.RationBlock) >= VillageEntity.RecruitFoodCost)
            {
                return _playerCreative ? "RECRUIT (FREE)" : "RECRUIT VILLAGER";
            }

            return "RECRUIT (NEED 4 PLANKS)";
        }

        private IEnumerable<string> GetGuideLines()
        {
            if (_village == null)
            {
                yield break;
            }

            int citizens = CountDisplayedCitizens();
            if (citizens == 0)
            {
                yield return VillageGuidance.GetQuickStartSteps(_village, _villagers, _playerPos);
                yield break;
            }

            yield return "PEOPLE tab: pick a villager, click LUMBER / BUILD / FARM.";
            yield return "BUILD tab: queue farm plot or peasant house.";
            yield return "Shift+click a tree to mark it for lumberjacks.";
            if (_village.CanRecruit(_villagers, _playerCreative))
            {
                yield return "Press R to recruit another worker (4 oak planks).";
            }
        }

        private static string GetBuildingBlurb(BuildingKind kind) => kind switch
        {
            BuildingKind.House => "HOUSING FOR 2 CITIZENS & ADDS 2 TO POPULATION CAP",
            BuildingKind.FarmPlot => "GROWS FOOD OVER TIME FOR CITIZENS TO EAT",
            BuildingKind.LumberCamp => "BOOSTS WOODCUTTING SPEED FOR LUMBERJACKS",
            BuildingKind.Quarry => "BOOSTS STONE MINING SPEED FOR MINERS",
            BuildingKind.Workshop => "SMITH WORKERS CRAFT TOOLS & PLANKS AUTOMATICALLY",
            BuildingKind.Storage => "ADDS 18 SHARED SLOTS FOR STORAGE & HAULING",
            BuildingKind.Kitchen => "COOK WORKERS TRANSMUTE RAW FOOD MORE EFFICIENTLY",
            BuildingKind.Well => "BOOSTS FARM CROP GROWTH SPEED BY 15%",
            BuildingKind.Market => "KEEPS CITIZENS HAPPY & RAISES HAPPINESS LIMIT BY 10%",
            _ => "EXPANDS YOUR SETTLEMENT"
        };

        private static string FormatCosts(BuildingBlueprint blueprint)
        {
            if (blueprint.Costs.Length == 0)
            {
                return "FREE";
            }

            var sb = new StringBuilder();
            for (int i = 0; i < blueprint.Costs.Length; i++)
            {
                var cost = blueprint.Costs[i];
                if (i > 0)
                {
                    sb.Append(" + ");
                }

                sb.Append(cost.Count);
                sb.Append(' ');
                sb.Append(ShortBlockName(cost.BlockType));
            }

            return sb.ToString();
        }

        private static string ShortBlockName(BlockType type) => type switch
        {
            BlockType.OakPlank => "PLANK",
            BlockType.Cobblestone => "COBBLE",
            BlockType.Dirt => "DIRT",
            BlockType.Stone => "STONE",
            _ => type.ToString().ToUpperInvariant()
        };

        private static string FormatStack(ItemStack stack) => stack.Kind switch
        {
            ItemKind.Block => $"{stack.BlockType.ToString().ToUpperInvariant()} X{stack.Count}",
            ItemKind.Tool => $"{stack.ToolId.ToString().ToUpperInvariant()} ({stack.Durability}/{stack.MaxDurability})",
            _ => $"ITEM X{stack.Count}"
        };

        private IEnumerable<Villager> EnumerateCitizens()
        {
            if (_village == null)
            {
                yield break;
            }

            _villageManager?.SyncCitizensForVillage(_village);

            foreach (var villager in _villagers.All)
            {
                if (villager.VillageId == _village.Id)
                {
                    yield return villager;
                }
            }
        }

        private bool HasDisplayedCitizens()
        {
            foreach (var _ in EnumerateCitizens())
            {
                return true;
            }

            return false;
        }

        private bool CanSummonSettlers()
        {
            if (_village == null || _villageManager == null || _world == null)
            {
                return false;
            }

            if (CountDisplayedCitizens() > 0)
            {
                return false;
            }

            if (!VillageSettlementHealth.HasEstablishedSettlement(_village))
            {
                return false;
            }

            return VillageSettlementHealth.IsPlayerNearTownHeart(_village, _playerPos);
        }

        private int CountDisplayedCitizens()
        {
            if (_village == null)
            {
                return 0;
            }

            _villageManager?.SyncCitizensForVillage(_village);
            return VillageSettlementHealth.CountLiveCitizens(_village, _villagers);
        }

        private ScreenLayout CreateLayout(Viewport viewport)
        {
            var ui = new UiLayout(viewport);
            float panelW = ui.S(PanelWidth);
            float panelH = ui.S(PanelHeight);
            return new ScreenLayout
            {
                Ui = ui,
                PanelX = ui.CenterX - panelW / 2f,
                PanelY = ui.CenterY - panelH / 2f,
                Left = ui.CenterX - panelW / 2f + ui.S(20f)
            };
        }

        private static bool PointInPanel(int x, int y, ScreenLayout layout)
        {
            float panelW = layout.Ui.S(PanelWidth);
            float panelH = layout.Ui.S(PanelHeight);
            return x >= layout.PanelX && x <= layout.PanelX + panelW && y >= layout.PanelY && y <= layout.PanelY + panelH;
        }

        private float GetMaxBuildScroll(ScreenLayout layout)
        {
            float panelH = layout.S(PanelHeight);
            float contentH = panelH - layout.S(ContentTop) - layout.S(FooterHeight);
            float sectionH = layout.S(130f);
            float catalogH = contentH - sectionH - layout.S(12f);
            float cardH = layout.S(58f);
            int count = 0;
            foreach (var blueprint in PlayerStructureRegistry.All)
            {
                if (blueprint.Id != "town_heart")
                {
                    count++;
                }
            }

            float contentHeight = count * (cardH + layout.S(8f));
            float visibleHeight = catalogH - layout.S(40f);
            return Math.Max(0f, contentHeight - visibleHeight);
        }

        private float GetMaxPeopleScroll(ScreenLayout layout)
        {
            float panelH = layout.S(PanelHeight);
            float contentH = panelH - layout.S(ContentTop) - layout.S(FooterHeight);
            float rowH = layout.S(42f);
            int count = CountDisplayedCitizens();
            float contentHeight = count * (rowH + layout.S(4f));
            float visibleHeight = contentH - layout.S(38f);
            return Math.Max(0f, contentHeight - visibleHeight);
        }

        private void HitRect(float x, float y, float w, float h, int buttonId, MouseState mouse)
        {
            var rect = new Rectangle((int)x, (int)y, (int)w, (int)h);
            if (HitTest(rect, mouse.X, mouse.Y))
            {
                _hoveredButton = buttonId;
            }
        }

        private static bool HitTest(Rectangle rect, int x, int y)
        {
            if (rect.Contains(x, y))
            {
                return true;
            }

            return new Rectangle(
                rect.X - HitPadding,
                rect.Y - HitPadding,
                rect.Width + HitPadding * 2,
                rect.Height + HitPadding * 2).Contains(x, y);
        }

        private sealed class ScreenLayout
        {
            public required UiLayout Ui { get; init; }
            public float PanelX { get; init; }
            public float PanelY { get; set; }
            public float Left { get; init; }
            public float S(float v) => Ui.S(v);
        }

        private void HandleRenameInput(KeyboardState kb, KeyboardState prevKb)
        {
            if (!_isEditingName)
            {
                return;
            }

            if (kb.IsKeyDown(Keys.Enter) && !prevKb.IsKeyDown(Keys.Enter))
            {
                string trimmed = _editingNameBuffer.Trim();
                if (trimmed.Length > 0)
                {
                    _village!.Name = trimmed;
                }
                _isEditingName = false;
                return;
            }

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                _isEditingName = false;
                return;
            }

            if (kb.IsKeyDown(Keys.Back) && !prevKb.IsKeyDown(Keys.Back) && _editingNameBuffer.Length > 0)
            {
                _editingNameBuffer = _editingNameBuffer[..^1];
                return;
            }

            foreach (Keys key in Enum.GetValues<Keys>())
            {
                if (!kb.IsKeyDown(key) || prevKb.IsKeyDown(key))
                {
                    continue;
                }

                char? ch = KeyToChar(key, kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift));
                if (ch.HasValue && _editingNameBuffer.Length < 24)
                {
                    _editingNameBuffer += ch.Value;
                }
            }
        }

        private static char? KeyToChar(Keys key, bool shift)
        {
            if (key >= Keys.A && key <= Keys.Z)
            {
                char c = (char)('a' + (key - Keys.A));
                return shift ? char.ToUpper(c) : c;
            }

            if (key >= Keys.D0 && key <= Keys.D9)
            {
                return (char)('0' + (key - Keys.D0));
            }

            return key switch
            {
                Keys.OemMinus => shift ? '_' : '-',
                Keys.OemPeriod => '.',
                Keys.Space => ' ',
                _ => null
            };
        }
    }
}

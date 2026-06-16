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
            ("Idle", JobType.Idle),
            ("Lumber", JobType.Lumber),
            ("Mine", JobType.Mine),
            ("Farm", JobType.Farm),
            ("Build", JobType.Build),
            ("Haul", JobType.Haul),
        };

        private static readonly string[] TabLabels = { "Overview", "Build", "People", "Goals" };

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

            _ui.DrawFullscreenBackground(UiTheme.OverlayScrim * (0.55f * alpha));
            _ui.DrawCard(panelX, panelY, panelW, panelH, alpha, UiTheme.RadiusXl);

            _ui.DrawCenteredTitle("Start a settlement", panelY + layout.S(20f), layout.S(UiTheme.FontTitle),
                UiTheme.Title, alpha);
            _ui.DrawCenteredText("No village yet", panelY + layout.S(48f), layout.S(UiTheme.FontBody),
                UiTheme.Subtitle, alpha * 0.92f);

            float contentY = panelY + layout.S(ContentTop);
            float contentH = panelH - layout.S(ContentTop) - layout.S(FooterHeight);
            _ui.DrawPanel(left, contentY, panelW - layout.S(40f), contentH,
                UiTheme.PanelBgMuted, UiTheme.PanelBorder, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);

            float textY = contentY + layout.S(28f);
            _ui.DrawString("This save has no settlement yet.",
                left + layout.S(18f), textY, layout.S(UiTheme.FontBody), UiTheme.StatValue, alpha);
            textY += layout.S(28f);
            _ui.DrawString("Place a Town Heart — one settler joins automatically and builds it.",
                left + layout.S(18f), textY, layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
            textY += layout.S(28f);
            _ui.DrawString("Recruit is only for extra workers after you already have at least one villager.",
                left + layout.S(18f), textY, layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);

            if (_canClaimNearby)
            {
                textY += layout.S(28f);
                _ui.DrawString("Abandoned outpost nearby — claim it for a free settler.",
                    left + layout.S(18f), textY, layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
            }
            else
            {
                textY += layout.S(28f);
                _ui.DrawString("Wild outposts (cottages, forest camps) are rare — roughly one every few hundred blocks.",
                    left + layout.S(18f), textY, layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
            }

            if (PlayerStructureRegistry.TryGet("town_heart", out var blueprint))
            {
                textY += layout.S(36f);
                string costLine = _playerCreative
                    ? "Town Heart cost: free in creative"
                    : $"Town Heart cost: {FormatCosts(blueprint)}";
                bool canAfford = _playerCreative || (_playerPayer != null && blueprint.CanAfford(_playerPayer));
                _ui.DrawString(costLine, left + layout.S(18f), textY, layout.S(UiTheme.FontSmall),
                    canAfford ? UiTheme.Subtitle : UiTheme.Danger, alpha);
            }

            float footerY = panelY + panelH - layout.S(FooterHeight);
            bool canPlace = CanAffordTownHeart();
            DrawStyledButton(left, footerY, buttonW, buttonH, "Place Town Heart", _hoveredButton == 14,
                UiButtonStyle.Primary, layout.Ui, alpha, !canPlace);

            if (_canClaimNearby)
            {
                DrawStyledButton(left + buttonW + layout.S(10f), footerY, buttonW, buttonH, "Claim outpost",
                    _hoveredButton == 12, UiButtonStyle.Secondary, layout.Ui, alpha);
            }

            float closeX = panelX + panelW - layout.S(20f) - buttonW;
            float closeY = panelY + panelH - layout.S(30f);
            DrawStyledButton(closeX, closeY, buttonW, buttonH, "Close", _hoveredButton == 11, UiButtonStyle.Ghost, layout.Ui, alpha);

            _ui.DrawCenteredText("Esc close · Enter confirm", panelY + panelH - layout.S(12f), layout.S(UiTheme.FontSmall),
                UiTheme.Hint, 0.9f * alpha);
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

            _ui.DrawFullscreenBackground(UiTheme.OverlayScrim * (0.55f * alpha));
            _ui.DrawCard(panelX, panelY, panelW, panelH, alpha, UiTheme.RadiusXl);

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
            DrawStyledButton(left, footerY, buttonW, buttonH, recruitLabel, _hoveredButton == 10,
                UiButtonStyle.Primary, layout.Ui, alpha, !canRecruit);

            if (_selectedTab == 0)
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
            DrawStyledButton(closeX, closeY, buttonW, buttonH, "Close", _hoveredButton == 11, UiButtonStyle.Ghost, layout.Ui, alpha);

            string footerHint;
            if (_hoveredButton == 10)
            {
                footerHint = CountDisplayedCitizens() > 0
                    ? "Recruit — bring a new peasant to the village (costs 4 oak planks)"
                    : "Summon settlers — restore missing starter citizens at the Town Heart";
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
        }

        private void DrawHeader(ScreenLayout layout, float panelX, float panelY, float panelW, float alpha, Color accent)
        {
            string name = _isEditingName ? _editingNameBuffer + "_" : _village!.Name;
            Color titleColor = _hoveredButton == 90 ? UiTheme.Accent : UiTheme.Title;
            _ui.DrawCenteredTitle(name, panelY + layout.S(20f), layout.S(UiTheme.FontTitle), titleColor, alpha);

            if (_hoveredButton == 90 || _isEditingName)
            {
                float titleW = layout.S(400f);
                float titleH = layout.S(28f);
                float titleX = layout.Ui.CenterX - titleW / 2f;
                float rectY = panelY + layout.S(14f);
                _ui.DrawPanel(titleX, rectY, titleW, titleH, Color.Transparent, accent, 0.5f, alpha, UiTheme.RadiusSm);
            }

            string tier = _village!.Tier.ToString();
            int citizenCount = CountDisplayedCitizens();
            string subtitle = $"{tier} · {citizenCount}/{_village.PopulationCap} citizens · Tier {(int)_village.Tier}";
            if (_playerCreative)
            {
                subtitle += " · Creative";
            }

            _ui.DrawCenteredText(subtitle, panelY + layout.S(48f), layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha * 0.92f);

            float badgeW = layout.S(88f);
            float badgeH = layout.S(22f);
            float badgeX = panelX + panelW - layout.S(24f) - badgeW;
            float badgeY = panelY + layout.S(18f);
            _ui.DrawPanel(badgeX, badgeY, badgeW, badgeH, UiTheme.AccentSoft, UiTheme.Accent, 0.8f, alpha, UiTheme.RadiusSm);
            _ui.DrawString(tier, badgeX + layout.S(10f), badgeY + layout.S(4f), layout.S(UiTheme.FontSmall), UiTheme.Accent, alpha, semiBold: true);
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
            float tabY = panelY + layout.S(62f);
            float tabX = left;
            for (int i = 0; i < TabLabels.Length; i++)
            {
                if (i == 3 && !IsGoalsTabVisible())
                {
                    continue;
                }

                bool selected = i == _selectedTab;
                bool hovered = _hoveredButton == i;
                Color fill = selected ? UiTheme.AccentSoft : UiTheme.PanelBgMuted;
                Color border = selected || hovered ? UiTheme.Accent : UiTheme.PanelBorder;
                _ui.DrawPanel(tabX, tabY, layout.S(TabWidth), layout.S(TabHeight), fill, border, 0.8f, alpha, UiTheme.RadiusMd);
                Color textColor = selected ? UiTheme.Title : UiTheme.Meta;
                string label = TabLabels[i];
                float textW = _ui.MeasureString(label, layout.S(UiTheme.FontSmall), semiBold: selected);
                _ui.DrawString(label, tabX + (layout.S(TabWidth) - textW) / 2f, tabY + layout.S(8f), layout.S(UiTheme.FontSmall),
                    textColor, alpha, semiBold: selected);
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
                _ui.DrawString(_viewModel.StatusLine, left, y - layout.S(18f), layout.S(UiTheme.FontSmall),
                    UiTheme.Subtitle, alpha);
                _ui.DrawString("Next: " + _viewModel.NextAction, left, y - layout.S(4f), layout.S(UiTheme.FontSmall),
                    UiTheme.Hint, alpha);
            }

            int citizens = CountDisplayedCitizens();
            if (citizens == 0)
            {
                float bannerH = layout.S(54f);
                _ui.DrawPanel(left, y, layout.S(PanelWidth) - layout.S(40f), bannerH,
                    UiTheme.DangerSoft, UiTheme.Danger, 0.85f, alpha, UiTheme.RadiusMd);
                string bannerTitle = VillageSettlementHealth.IsPlayerNearTownHeart(_village!, _playerPos)
                    ? "No settlers in village"
                    : "No settlers nearby";
                _ui.DrawString(bannerTitle, left + layout.S(14f), y + layout.S(10f), layout.S(UiTheme.FontBody),
                    UiTheme.Title, alpha, semiBold: true);
                _ui.DrawString(VillageGuidance.GetQuickStartSteps(_village!, _villagers, _playerPos), left + layout.S(14f),
                    y + layout.S(30f), layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
                y += bannerH + layout.S(10f);
            }
            else if (citizens > 0 && citizens <= 2)
            {
                float bannerH = layout.S(40f);
                _ui.DrawPanel(left, y, layout.S(PanelWidth) - layout.S(40f), bannerH,
                    UiTheme.AccentSoft, UiTheme.Accent, 0.75f, alpha, UiTheme.RadiusMd);
                _ui.DrawString("Quick start: " + VillageGuidance.GetQuickStartSteps(_village!, _villagers, _playerPos),
                    left + layout.S(14f), y + layout.S(12f), layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
                y += bannerH + layout.S(10f);
            }

            DrawStatCard(layout, x, y, cardW, cardH, "Population", $"{citizens}/{_village!.PopulationCap}",
                (float)citizens / Math.Max(1, _village.PopulationCap), new Color(0.45f, 0.78f, 0.55f), alpha, accent);
            x += cardW + gap;
            DrawStatCard(layout, x, y, cardW, cardH, "Food", $"{_village.FoodStock:F0}",
                Math.Clamp(_village.FoodStock / Math.Max(1f, Math.Max(1, citizens) * 2f), 0f, 1f),
                new Color(0.92f, 0.72f, 0.28f), alpha, accent);
            x += cardW + gap;
            DrawStatCard(layout, x, y, cardW, cardH, "Happiness", $"{_village.Happiness:P0}", _village.Happiness,
                new Color(0.55f, 0.82f, 0.95f), alpha, accent);
            x += cardW + gap;
            DrawStatCard(layout, x, y, cardW, cardH, "Housing", $"{citizens}/{Math.Max(1, _village.HousingCapacity)}",
                Math.Clamp((float)citizens / Math.Max(1, _village.HousingCapacity), 0f, 1f),
                new Color(0.78f, 0.58f, 0.92f), alpha, accent);

            y += cardH + layout.S(14f);

            if (!string.IsNullOrWhiteSpace(_openingNote))
            {
                float bannerH = layout.S(42f);
                _ui.DrawPanel(left, y, layout.S(PanelWidth) - layout.S(40f), bannerH,
                    UiTheme.AccentSoft, UiTheme.Accent, 0.75f, alpha, UiTheme.RadiusMd);
                _ui.DrawString(_openingNote!, left + layout.S(14f), y + layout.S(12f), layout.S(UiTheme.FontSmall),
                    UiTheme.Subtitle, alpha);
                y += bannerH + layout.S(10f);
            }

            float colW = (layout.S(PanelWidth) - layout.S(40f) - layout.S(12f)) / 2f;
            float colH = height - (y - (layout.PanelY + layout.S(ContentTop))) - layout.S(8f);
            DrawStoragePanel(layout, left, y, colW, colH, alpha, accent);
            DrawActivityPanel(layout, left + colW + layout.S(12f), y, colW, colH, alpha, accent);
        }

        private void DrawStoragePanel(ScreenLayout layout, float x, float y, float w, float h, float alpha, Color accent)
        {
            _ui.DrawPanel(x, y, w, h, UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);
            UiTheme.DrawSectionHeader(_ui, "Village storage", x + layout.S(12f), y + layout.S(10f), layout.Ui, alpha);

            float rowY = y + layout.S(32f);
            int shown = 0;
            for (int i = 0; i < _village!.Storage.SlotCount && shown < 10; i++)
            {
                var stack = _village.Storage.GetSlot(i);
                if (stack.IsEmpty)
                {
                    continue;
                }

                string label = FormatStack(stack);
                _ui.DrawString(label, x + layout.S(14f), rowY, layout.S(UiTheme.FontSmall), UiTheme.StatValue, alpha);
                rowY += layout.S(18f);
                shown++;
            }

            if (shown == 0)
            {
                _ui.DrawString("Empty — haulers deliver here", x + layout.S(14f), rowY, layout.S(UiTheme.FontSmall),
                    UiTheme.Hint, alpha);
            }

            int plankCount = _village.Storage.CountBlock(VillageEntity.RationBlock);
            float recruitY = y + h - layout.S(36f);
            string recruitHint = _playerCreative
                ? "Recruit cost: free in creative"
                : $"Recruit cost: {VillageEntity.RecruitFoodCost} oak planks ({plankCount} in storage)";
            _ui.DrawHorizontalRule(x + layout.S(10f), recruitY - layout.S(8f), w - layout.S(20f),
                UiTheme.Rule, 1f, alpha * 0.7f);
            _ui.DrawString(recruitHint, x + layout.S(14f), recruitY, layout.S(UiTheme.FontSmall),
                UiTheme.Meta, alpha);
        }

        private void DrawActivityPanel(ScreenLayout layout, float x, float y, float w, float h, float alpha, Color accent)
        {
            _ui.DrawPanel(x, y, w, h, UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);
            UiTheme.DrawSectionHeader(_ui, "Activity", x + layout.S(12f), y + layout.S(10f), layout.Ui, alpha);

            float rowY = y + layout.S(32f);
            int pendingSites = 0;
            foreach (var site in _village!.BuildingSites)
            {
                if (site.IsComplete)
                {
                    continue;
                }

                pendingSites++;
                string line = $"{site.BlueprintId} {site.CompletionRatio:P0}";
                _ui.DrawString(line, x + layout.S(14f), rowY, layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
                DrawMiniBar(x + layout.S(14f), rowY + layout.S(14f), w - layout.S(28f), layout.S(6f),
                    site.CompletionRatio, UiTheme.Accent, alpha);
                rowY += layout.S(28f);
            }

            if (pendingSites == 0)
            {
                _ui.DrawString("No active construction", x + layout.S(14f), rowY, layout.S(UiTheme.FontSmall),
                    UiTheme.Hint, alpha);
                rowY += layout.S(20f);
            }

            rowY += layout.S(6f);
            _ui.DrawString("Gather queue", x + layout.S(12f), rowY, layout.S(UiTheme.FontSmall), UiTheme.Section, alpha, semiBold: true);
            rowY += layout.S(18f);
            int queued = _village.WorkQueue.Count;
            string queueLine = queued == 0
                ? "Shift+click blocks or paint zone"
                : $"{queued} block(s) marked for workers";
            _ui.DrawString(queueLine, x + layout.S(14f), rowY, layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
            rowY += layout.S(20f);

            _ui.DrawString("Quick guide", x + layout.S(12f), rowY, layout.S(UiTheme.FontSmall), UiTheme.Section, alpha, semiBold: true);
            rowY += layout.S(18f);
            int guideLines = 0;
            foreach (string line in GetGuideLines())
            {
                if (guideLines >= 4)
                {
                    break;
                }

                _ui.DrawString($"• {line}", x + layout.S(14f), rowY, layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
                rowY += layout.S(16f);
                guideLines++;
            }
        }

        private void DrawBuildTab(ScreenLayout layout, float left, float y, float height, float alpha, Color accent)
        {
            float sectionH = layout.S(130f);
            _ui.DrawPanel(left, y, layout.S(PanelWidth) - layout.S(40f), sectionH,
                UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);
            UiTheme.DrawSectionHeader(_ui, "Construction sites", left + layout.S(12f), y + layout.S(10f), layout.Ui, alpha);

            float siteY = y + layout.S(34f);
            int drawn = 0;
            foreach (var site in _village!.BuildingSites)
            {
                if (site.IsComplete || drawn >= 3)
                {
                    continue;
                }

                string name = site.BlueprintId;
                _ui.DrawString(name, left + layout.S(16f), siteY, layout.S(UiTheme.FontBody), UiTheme.StatValue, alpha, semiBold: true);
                DrawMiniBar(left + layout.S(200f), siteY + layout.S(4f), layout.S(420f), layout.S(10f),
                    site.CompletionRatio, UiTheme.Accent, alpha);
                _ui.DrawString($"{site.CompletionRatio:P0}", left + layout.S(640f), siteY, layout.S(UiTheme.FontSmall),
                    UiTheme.Subtitle, alpha);
                siteY += layout.S(28f);
                drawn++;
            }

            if (drawn == 0)
            {
                _ui.DrawString("No build sites — pick a structure below", left + layout.S(16f), siteY, layout.S(UiTheme.FontSmall),
                    UiTheme.Hint, alpha);
            }

            float catalogY = y + sectionH + layout.S(12f);
            float catalogH = height - sectionH - layout.S(12f);
            _ui.DrawPanel(left, catalogY, layout.S(PanelWidth) - layout.S(40f), catalogH,
                UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);
            UiTheme.DrawSectionHeader(_ui, "Build catalog", left + layout.S(12f), catalogY + layout.S(10f), layout.Ui, alpha);

            float cardY = catalogY + layout.S(38f) - _buildScroll;
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
                Color cardBorder = hovered ? UiTheme.Accent : (canAfford ? UiTheme.PanelBorder : UiTheme.Rule);
                Color cardFill = hovered ? UiTheme.PanelBgHighlight : UiTheme.PanelBgMuted;
                if (cardY + cardH >= catalogY + layout.S(34f) && cardY <= catalogY + catalogH - layout.S(6f))
                {
                    _ui.DrawPanel(left + layout.S(12f), cardY, cardW, cardH, cardFill, cardBorder, 0.8f, alpha, UiTheme.RadiusMd);
                    _ui.DrawString(blueprint.DisplayName, left + layout.S(22f), cardY + layout.S(10f),
                        layout.S(UiTheme.FontBody), canAfford ? UiTheme.Title : UiTheme.Meta, alpha, semiBold: true);
                    _ui.DrawString(GetBuildingBlurb(blueprint.Kind), left + layout.S(22f), cardY + layout.S(28f),
                        layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
                    string costs = FormatCosts(blueprint);
                    float costW = _ui.MeasureString(costs, layout.S(UiTheme.FontSmall));
                    _ui.DrawString(costs, left + layout.S(12f) + cardW - costW - layout.S(12f), cardY + layout.S(18f),
                        layout.S(UiTheme.FontSmall), canAfford ? UiTheme.Subtitle : UiTheme.Danger, alpha);
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

            _ui.DrawPanel(left, y, listW, height, UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);
            int citizenCount = CountDisplayedCitizens();
            UiTheme.DrawSectionHeader(_ui, $"Citizens ({citizenCount})", left + layout.S(12f), y + layout.S(10f), layout.Ui, alpha);

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
                    _ui.DrawRoundedRect(left + layout.S(14f), rowY + layout.S(12f), layout.S(10f), layout.S(10f), layout.S(3f), roleColor * alpha);
                    _ui.DrawString(villager.Name, left + layout.S(30f), rowY + layout.S(8f),
                        layout.S(UiTheme.FontBody), isSelected ? UiTheme.Title : UiTheme.StatValue, alpha, semiBold: isSelected);
                    string statusText = $"{villager.Role} · {villager.CurrentJob}";
                    Color textCol = roleColor;
                    if (_village!.ConsecutiveDaysWithoutFood >= 2)
                    {
                        statusText = "Starving · " + statusText;
                        textCol = UiTheme.Danger;
                    }
                    _ui.DrawString(statusText, left + layout.S(30f),
                        rowY + layout.S(24f), layout.S(UiTheme.FontSmall), textCol, alpha);
                }

                rowY += rowH + layout.S(4f);
            }

            if (!HasDisplayedCitizens())
            {
                string emptyHint = VillageSettlementHealth.IsPlayerNearTownHeart(_village!, _playerPos)
                    ? "No villagers — click Summon settlers below"
                    : "No villagers — walk to Town Heart, then summon settlers";
                _ui.DrawString(emptyHint, left + layout.S(14f), y + layout.S(40f), layout.S(UiTheme.FontSmall),
                    UiTheme.Hint, alpha);
            }

            _ui.DrawPanel(detailX, y, detailW, height, UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);
            if (_selectedVillagerId >= 0 && _villagers.TryGet(_selectedVillagerId, out var selected))
            {
                DrawVillagerDetail(layout, detailX, y, detailW, height, selected, alpha, accent);
            }
            else
            {
                _ui.DrawString("Select a citizen", detailX + layout.S(16f), y + layout.S(40f), layout.S(UiTheme.FontBody),
                    UiTheme.Hint, alpha);
            }
        }

        private void DrawVillagerDetail(ScreenLayout layout, float x, float y, float w, float h, Villager villager, float alpha, Color accent)
        {
            float pad = layout.S(16f);
            float detailY = y + pad;
            var roleColor = VillagerVisuals.GetRoleColor(villager.Role);

            _ui.DrawString(villager.Name, x + pad, detailY, layout.S(UiTheme.FontTitle),
                UiTheme.Title, alpha, semiBold: true);
            detailY += layout.S(28f);
            string detailStatusText = $"{villager.Role} · {villager.CurrentJob}";
            Color detailCol = roleColor;
            if (_village!.ConsecutiveDaysWithoutFood >= 2)
            {
                detailStatusText = "Starving · " + detailStatusText;
                detailCol = UiTheme.Danger;
            }
            _ui.DrawString(detailStatusText, x + pad, detailY,
                layout.S(UiTheme.FontBody), detailCol, alpha);
            detailY += layout.S(24f);
            _ui.DrawString($"Trait: {villager.Persona.Trait}", x + pad, detailY, layout.S(UiTheme.FontSmall),
                UiTheme.Meta, alpha);
            detailY += layout.S(28f);

            _ui.DrawString(
                $"Skills — Mining {villager.Skills.Mining.Level} · Wood {villager.Skills.Woodcutting.Level} · Farm {villager.Skills.Farming.Level}",
                x + pad, detailY, layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
            detailY += layout.S(28f);
            _ui.DrawProgressBar(x + pad, detailY, w - pad * 2f, layout.S(12f), villager.Happiness, "Morale", 1f, alpha);
            detailY += layout.S(52f);

            DrawStyledButton(x + pad, detailY, layout.S(96f), layout.S(ButtonHeight), "Talk", _hoveredButton == 50,
                UiButtonStyle.Secondary, layout.Ui, alpha);
            detailY += layout.S(48f);
            _ui.DrawString("Assign job", x + pad, detailY, layout.S(UiTheme.FontSection), UiTheme.Section, alpha, semiBold: true);
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
                DrawStyledButton(jobX, jobY, jobW, jobH, AssignableJobs[i].Label, _hoveredButton == 40 + i,
                    UiButtonStyle.Ghost, layout.Ui, alpha);
            }
        }

        private void DrawGoalsTab(ScreenLayout layout, float left, float y, float height, float alpha, Color accent)
        {
            _ui.DrawPanel(left, y, layout.S(PanelWidth) - layout.S(40f), height,
                UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);

            if (_playWithAi)
            {
                UiTheme.DrawSectionHeader(_ui, "Steward goals", left + layout.S(12f), y + layout.S(10f), layout.Ui, alpha);
                _ui.DrawString("Priority tasks set by the village AI steward", left + layout.S(12f), y + layout.S(32f),
                    layout.S(UiTheme.FontSmall), UiTheme.Hint, alpha);

                float rowY = y + layout.S(56f);
                int shown = 0;
                foreach (var goal in _village!.Scheduler.Goals)
                {
                    if (shown >= 8) break;

                    Color statusColor = goal.Completed ? UiTheme.Success : UiTheme.StatValue;
                    string status = goal.Completed ? "Done" : "Active";
                    _ui.DrawString($"[{status}] {goal.Description}", left + layout.S(16f), rowY,
                        layout.S(UiTheme.FontSmall), statusColor, alpha);

                    if (!goal.Completed && goal.Kind == VillageGoalKind.Stock && goal.StockBlock.HasValue && goal.TargetCount > 0)
                    {
                        int have = _village.Scheduler.GetStockProgress(goal, _village);
                        float progress = Math.Clamp(have / (float)goal.TargetCount, 0f, 1f);
                        DrawMiniBar(left + layout.S(16f), rowY + layout.S(16f), layout.S(500f), layout.S(6f), progress, UiTheme.Accent, alpha);
                        _ui.DrawString($"{have}/{goal.TargetCount} {goal.StockBlock.Value}",
                            left + layout.S(530f), rowY + layout.S(12f), layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
                        rowY += layout.S(34f);
                    }
                    else rowY += layout.S(22f);

                    shown++;
                }

                if (shown == 0)
                {
                    _ui.DrawString("No active goals — chat with the steward (C) to set priorities", left + layout.S(16f),
                        rowY + layout.S(10f), layout.S(UiTheme.FontSmall), UiTheme.Hint, alpha);
                }
            }
            else
            {
                UiTheme.DrawSectionHeader(_ui, "Manual goals", left + layout.S(12f), y + layout.S(10f), layout.Ui, alpha);
                _ui.DrawString("Define local resource gathering priorities for your villagers", left + layout.S(12f), y + layout.S(32f),
                    layout.S(UiTheme.FontSmall), UiTheme.Hint, alpha);

                float colLeft = left + layout.S(16f);
                _ui.DrawString("Create new goal", colLeft, y + layout.S(56f), layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
                _ui.DrawString("Resource:", colLeft, y + layout.S(82f), layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);

                for (int i = 0; i < GoalBlockTypes.Length; i++)
                {
                    float btnX = colLeft + i * layout.S(70f);
                    float btnY = y + layout.S(98f);
                    bool selected = i == _selectedGoalBlockIndex;
                    bool hovered = _hoveredButton == 60 + i;
                    _ui.DrawPanel(btnX, btnY, layout.S(64f), layout.S(26f),
                        selected ? UiTheme.PanelBgHighlight : UiTheme.PanelBgMuted,
                        hovered ? UiTheme.Accent : (selected ? UiTheme.Accent : accent), 0.8f, alpha, UiTheme.RadiusSm);
                    string label = GoalBlockTypes[i].ToString();
                    if (label.Length > 8) label = label[..8];
                    float labelW = _ui.MeasureString(label, layout.S(UiTheme.FontSmall));
                    _ui.DrawString(label, btnX + (layout.S(64f) - labelW) / 2f, btnY + layout.S(5f), layout.S(UiTheme.FontSmall),
                        selected ? UiTheme.Title : UiTheme.Meta, alpha);
                }

                _ui.DrawString("Target amount:", colLeft, y + layout.S(136f), layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
                for (int j = 0; j < GoalTargetCounts.Length; j++)
                {
                    float btnX = colLeft + j * layout.S(70f);
                    float btnY = y + layout.S(152f);
                    bool selected = j == _selectedGoalCountIndex;
                    bool hovered = _hoveredButton == 70 + j;
                    _ui.DrawPanel(btnX, btnY, layout.S(64f), layout.S(26f),
                        selected ? UiTheme.PanelBgHighlight : UiTheme.PanelBgMuted,
                        hovered ? UiTheme.Accent : (selected ? UiTheme.Accent : accent), 0.8f, alpha, UiTheme.RadiusSm);
                    string label = GoalTargetCounts[j].ToString();
                    float labelW = _ui.MeasureString(label, layout.S(UiTheme.FontSmall));
                    _ui.DrawString(label, btnX + (layout.S(64f) - labelW) / 2f, btnY + layout.S(5f), layout.S(UiTheme.FontSmall),
                        selected ? UiTheme.Title : UiTheme.Meta, alpha);
                }

                DrawStyledButton(colLeft, y + layout.S(200f), layout.S(140f), layout.S(32f), "Add goal", _hoveredButton == 80,
                    UiButtonStyle.Primary, layout.Ui, alpha);

                float rightLeft = left + layout.S(420f);
                float rightW = layout.S(PanelWidth) - layout.S(40f) - layout.S(420f) - layout.S(16f);
                _ui.DrawString("Active goals", rightLeft, y + layout.S(56f), layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha, semiBold: true);

                float rowY = y + layout.S(82f);
                int shown = 0;
                foreach (var goal in _village!.Scheduler.Goals)
                {
                    if (shown >= 6) break;

                    Color statusColor = goal.Completed ? UiTheme.Success : UiTheme.StatValue;
                    string status = goal.Completed ? "Done" : "Active";
                    _ui.DrawString($"[{status}] {goal.Description}", rightLeft, rowY, layout.S(UiTheme.FontSmall), statusColor, alpha);

                    float remW = layout.S(72f);
                    float remH = layout.S(26f);
                    DrawStyledButton(rightLeft + rightW - remW, rowY - layout.S(2f), remW, remH, "Remove", _hoveredButton == 100 + goal.Id,
                        UiButtonStyle.Danger, layout.Ui, alpha);

                    if (!goal.Completed && goal.Kind == VillageGoalKind.Stock && goal.StockBlock.HasValue && goal.TargetCount > 0)
                    {
                        int have = _village.Scheduler.GetStockProgress(goal, _village);
                        float progress = Math.Clamp(have / (float)goal.TargetCount, 0f, 1f);
                        DrawMiniBar(rightLeft, rowY + layout.S(16f), rightW - remW - layout.S(12f), layout.S(6f), progress, UiTheme.Accent, alpha);
                        _ui.DrawString($"{have}/{goal.TargetCount} {goal.StockBlock.Value}",
                            rightLeft + rightW - remW - layout.S(120f), rowY + layout.S(26f), layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
                        rowY += layout.S(40f);
                    }
                    else rowY += layout.S(26f);

                    shown++;
                }

                if (shown == 0)
                {
                    _ui.DrawString("No active goals specified", rightLeft, rowY + layout.S(10f), layout.S(UiTheme.FontSmall), UiTheme.Hint, alpha);
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
                float btnY = y + layout.S(98f);
                HitRect(btnX, btnY, layout.S(64f), layout.S(26f), 60 + i, mouse);
            }

            for (int j = 0; j < GoalTargetCounts.Length; j++)
            {
                float btnX = colLeft + j * layout.S(70f);
                float btnY = y + layout.S(152f);
                HitRect(btnX, btnY, layout.S(64f), layout.S(26f), 70 + j, mouse);
            }

            HitRect(colLeft, y + layout.S(200f), layout.S(140f), layout.S(32f), 80, mouse);

            float rightLeft = layout.Left + layout.S(420f);
            float rightW = layout.S(PanelWidth) - layout.S(40f) - layout.S(420f) - layout.S(16f);
            float rowY = y + layout.S(82f);
            int shown = 0;
            foreach (var goal in _village!.Scheduler.Goals)
            {
                if (shown >= 6)
                {
                    break;
                }

                float remW = layout.S(72f);
                float remH = layout.S(26f);
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
            _ui.DrawPanel(x, y, w, h, UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.92f, UiTheme.RadiusMd);
            _ui.DrawString(label, x + layout.S(10f), y + layout.S(8f), layout.S(UiTheme.FontSmall), UiTheme.StatLabel, alpha);
            _ui.DrawString(value, x + layout.S(10f), y + layout.S(24f), layout.S(UiTheme.FontBody), UiTheme.StatValue, alpha, semiBold: true);
            DrawMiniBar(x + layout.S(10f), y + h - layout.S(14f), w - layout.S(20f), layout.S(6f), ratio, barColor, alpha);
        }

        private void DrawStyledButton(float x, float y, float w, float h, string label, bool hovered, UiButtonStyle style, UiLayout layout, float alpha, bool disabled = false)
        {
            _ui.DrawButton(x, y, w, h, label, hovered && !disabled, false, style, layout.S(UiTheme.FontBody), alpha, hovered ? 1f : 0f, disabled);
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
            float tabY = layout.PanelY + layout.S(62f);
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
                float detailY = y + pad + layout.S(28f) + layout.S(24f) + layout.S(28f) + layout.S(28f) + layout.S(52f);
                HitRect(detailX + pad, detailY, layout.S(96f), layout.S(ButtonHeight), 50, mouse);

                float jobY = detailY + layout.S(48f) + layout.S(24f);
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
                    string desc = $"Gather {count} {block}";
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
                return "Recruit";
            }

            int citizens = CountDisplayedCitizens();
            if (citizens == 0)
            {
                return CanSummonSettlers() ? "Summon settlers" : "Summon settlers (go to heart)";
            }

            if (citizens >= _village.PopulationCap)
            {
                return "Recruit (build house)";
            }

            if (_playerCreative || _village.Storage.CountBlock(VillageEntity.RationBlock) >= VillageEntity.RecruitFoodCost)
            {
                return _playerCreative ? "Recruit (free)" : "Recruit villager";
            }

            return "Recruit (need 4 planks)";
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

            yield return "People tab: pick a villager, click Lumber / Build / Farm.";
            yield return "BUILD tab: queue farm plot or peasant house.";
            yield return "Shift+click a tree to mark it for lumberjacks.";
            if (_village.CanRecruit(_villagers, _playerCreative))
            {
                yield return "Press R to recruit another worker (4 oak planks).";
            }
        }

        private static string GetBuildingBlurb(BuildingKind kind) => kind switch
        {
            BuildingKind.House => "Housing for 2 citizens, adds 2 to population cap",
            BuildingKind.FarmPlot => "Grows food over time for citizens to eat",
            BuildingKind.LumberCamp => "Boosts woodcutting speed for lumberjacks",
            BuildingKind.Quarry => "Boosts stone mining speed for miners",
            BuildingKind.Workshop => "Smith workers craft tools and planks automatically",
            BuildingKind.Storage => "Adds 18 shared slots for storage and hauling",
            BuildingKind.Kitchen => "Cook workers transmute raw food more efficiently",
            BuildingKind.Well => "Boosts farm crop growth speed by 15%",
            BuildingKind.Market => "Keeps citizens happy, raises happiness limit by 10%",
            _ => "Expands your settlement"
        };

        private static string FormatCosts(BuildingBlueprint blueprint)
        {
            if (blueprint.Costs.Length == 0)
            {
                return "Free";
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
            BlockType.OakPlank => "plank",
            BlockType.Cobblestone => "cobble",
            BlockType.Dirt => "dirt",
            BlockType.Stone => "stone",
            _ => type.ToString()
        };

        private static string FormatStack(ItemStack stack) => stack.Kind switch
        {
            ItemKind.Block => $"{stack.BlockType} ×{stack.Count}",
            ItemKind.Tool => $"{stack.ToolId} ({stack.Durability}/{stack.MaxDurability})",
            _ => $"Item ×{stack.Count}"
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

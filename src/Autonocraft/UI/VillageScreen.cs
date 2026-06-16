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
    public sealed class VillageScreen
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

        private readonly IVillagePanel[] _panels =
        {
            new OverviewPanel(),
            new BuildPanel(),
            new PeoplePanel(),
            new GoalsPanel(),
        };

        private readonly FoundingPanel _foundingPanel = new();

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
            var foundingContext = new FoundingPanelContext
            {
                Ui = _ui,
                UiLayout = layout.Ui,
                PlayerPayer = _playerPayer,
                PlayerCreative = _playerCreative,
                CanClaimNearby = _canClaimNearby,
                HoveredButton = _hoveredButton,
                PanelX = layout.PanelX,
                PanelY = layout.PanelY,
                PanelWidth = layout.S(PanelWidth),
                PanelHeight = layout.S(PanelHeight),
                ContentLeft = layout.Left,
                Alpha = alpha
            };
            _foundingPanel.Draw(foundingContext);
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

            if (_hoveredButton == 14 && FoundingPanel.CanAffordTownHeart(_playerPayer, _playerCreative))
            {
                PlaceTownHeartRequested = true;
            }
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
                OpeningNote = _openingNote
            };

            if (_selectedTab >= 0 && _selectedTab < _panels.Length)
                _panels[_selectedTab].Draw(panelContext);

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

        private void HitManualGoals(ScreenLayout layout, MouseState mouse)
        {
            float y = layout.PanelY + layout.S(ContentTop);
            float colLeft = layout.Left + layout.S(16f);

            for (int i = 0; i < GoalsPanel.GoalBlockTypes.Length; i++)
            {
                float btnX = colLeft + i * layout.S(70f);
                float btnY = y + layout.S(98f);
                HitRect(btnX, btnY, layout.S(64f), layout.S(26f), 60 + i, mouse);
            }

            for (int j = 0; j < GoalsPanel.GoalTargetCounts.Length; j++)
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

        private void DrawStyledButton(float x, float y, float w, float h, string label, bool hovered, UiButtonStyle style, UiLayout layout, float alpha, bool disabled = false)
        {
            _ui.DrawButton(x, y, w, h, label, hovered && !disabled, false, style, layout.S(UiTheme.FontBody), alpha, hovered ? 1f : 0f, disabled);
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
                if (_hoveredButton >= 60 && _hoveredButton < 60 + GoalsPanel.GoalBlockTypes.Length)
                {
                    _selectedGoalBlockIndex = _hoveredButton - 60;
                    return;
                }

                if (_hoveredButton >= 70 && _hoveredButton < 70 + GoalsPanel.GoalTargetCounts.Length)
                {
                    _selectedGoalCountIndex = _hoveredButton - 70;
                    return;
                }

                if (_hoveredButton == 80)
                {
                    var block = GoalsPanel.GoalBlockTypes[_selectedGoalBlockIndex];
                    int count = GoalsPanel.GoalTargetCounts[_selectedGoalCountIndex];
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

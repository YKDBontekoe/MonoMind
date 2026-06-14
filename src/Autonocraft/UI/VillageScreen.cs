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
        private int _selectedTab;
        private int _selectedVillagerId = -1;
        private int _hoveredButton = -1;
        private bool _canClaimNearby;
        private float _buildScroll;
        private float _peopleScroll;
        private VillageViewModel? _viewModel;

        public bool IsOpen { get; private set; }
        public bool RecruitRequested { get; private set; }
        public bool ClaimRequested { get; private set; }
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

        public void Open(
            VillageEntity village,
            VillageManager villageManager,
            VoxelWorld world,
            Vector3 playerPos,
            IItemContainer? playerPayer,
            bool playerCreative = false,
            string? openingNote = null)
        {
            _village = village;
            _villageManager = villageManager;
            _world = world;
            _playerPos = playerPos;
            _playerPayer = playerPayer;
            _playerCreative = playerCreative;
            _openingNote = openingNote;
            _selectedTab = 0;
            _selectedVillagerId = village.VillagerIds.Count > 0 ? village.VillagerIds[0] : -1;
            _hoveredButton = -1;
            _buildScroll = 0f;
            _peopleScroll = 0f;
            _canClaimNearby = villageManager.TryFindClaimableStructure(world, playerPos, 24f, out _, out _, out _);
            _viewModel = VillageViewModel.Build(village, villageManager, _villagers, playerCreative);
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
            _village = null;
            _villageManager = null;
            _world = null;
            _playerPayer = null;
            _playerCreative = false;
            _openingNote = null;
            _hoveredButton = -1;
            _selectedVillagerId = -1;
        }

        public void Update(
            Viewport viewport,
            KeyboardState kb,
            MouseState mouse,
            KeyboardState prevKb,
            MouseState prevMouse)
        {
            ResetRequests();

            if (!IsOpen || _village == null)
            {
                return;
            }

            if (kb.IsKeyDown(Keys.Escape) && !prevKb.IsKeyDown(Keys.Escape))
            {
                CloseRequested = true;
                return;
            }

            if (kb.IsKeyDown(Keys.R) && !prevKb.IsKeyDown(Keys.R))
            {
                RecruitRequested = true;
                return;
            }

            if (kb.IsKeyDown(Keys.Left) && !prevKb.IsKeyDown(Keys.Left))
            {
                _selectedTab = (_selectedTab + TabLabels.Length - 1) % TabLabels.Length;
            }

            if (kb.IsKeyDown(Keys.Right) && !prevKb.IsKeyDown(Keys.Right))
            {
                _selectedTab = (_selectedTab + 1) % TabLabels.Length;
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
            }

            if (_selectedTab == 1)
            {
                HitBuildCatalog(layout, mouse, buttonH);
            }

            if (_selectedTab == 2)
            {
                HitPeopleTab(layout, mouse, buttonH);
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
            if (!IsOpen || _village == null || alpha <= 0.01f)
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
            var accent = new Color(0.35f, 0.55f, 0.45f);

            _ui.DrawFullscreenBackground(new Color(0.02f, 0.03f, 0.06f) * (0.72f * alpha));
            _ui.DrawVignette(0.35f, alpha);
            _ui.DrawFramedPanel(panelX, panelY, panelW, panelH, new Color(0.05f, 0.07f, 0.11f), accent, alpha);

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
            bool canRecruit = _village.CanRecruit(_playerCreative);
            _ui.DrawButton(left, footerY, buttonW, buttonH, recruitLabel, _hoveredButton == 10, false, layout.S(0.95f), alpha,
                canRecruit ? 1f : 0.55f);

            if (_selectedTab == 0)
            {
                if (_canClaimNearby)
                {
                    _ui.DrawButton(left + buttonW + layout.S(10f), footerY, buttonW, buttonH, "CLAIM OUTPOST",
                        _hoveredButton == 12, false, layout.S(0.9f), alpha);
                }

                _ui.DrawButton(left + (buttonW + layout.S(10f)) * (_canClaimNearby ? 2f : 1f), footerY, buttonW, buttonH,
                    "PAINT ZONE", _hoveredButton == 13, false, layout.S(0.9f), alpha);
            }

            float closeX = panelX + panelW - layout.S(20f) - buttonW;
            float closeY = panelY + panelH - layout.S(30f);
            _ui.DrawButton(closeX, closeY, buttonW, buttonH, "CLOSE", _hoveredButton == 11, false, layout.S(1.0f), alpha);

            string footerHint = _selectedTab switch
            {
                1 => "CLICK A BUILDING CARD TO PLACE IN THE WORLD",
                2 => "SELECT A VILLAGER THEN ASSIGN A JOB",
                3 => "STEWARD GOALS TRACK VILLAGE PRIORITIES",
                _ => "ESC CLOSE  |  R RECRUIT  |  1-4 TABS  |  SCROLL LISTS"
            };
            _ui.DrawCenteredText(footerHint, panelY + panelH - layout.S(8f), layout.S(0.88f),
                new Color(0.45f, 0.5f, 0.58f) * alpha);
        }

        private void DrawHeader(ScreenLayout layout, float panelX, float panelY, float panelW, float alpha, Color accent)
        {
            string title = _village!.Name.ToUpperInvariant();
            _ui.DrawCenteredTitle(title, panelY + layout.S(14f), layout.S(1.55f), new Color(0.8f, 0.95f, 0.85f) * alpha);

            string tier = _village.Tier.ToString().ToUpperInvariant();
            string subtitle = $"{tier}  |  {_village.Population}/{_village.PopulationCap} CITIZENS  |  TIER {_village.Tier}";
            if (_playerCreative)
            {
                subtitle += "  |  CREATIVE";
            }

            _ui.DrawCenteredText(subtitle, panelY + layout.S(36f), layout.S(0.92f), new Color(0.55f, 0.72f, 0.62f) * alpha);

            float badgeW = layout.S(88f);
            float badgeH = layout.S(18f);
            float badgeX = panelX + panelW - layout.S(24f) - badgeW;
            float badgeY = panelY + layout.S(14f);
            _ui.DrawFramedPanel(badgeX, badgeY, badgeW, badgeH, new Color(0.08f, 0.12f, 0.1f), accent, alpha * 0.9f);
            _ui.DrawString(tier, badgeX + layout.S(10f), badgeY + layout.S(4f), layout.S(0.85f), accent * alpha);
        }

        private void DrawTabBar(ScreenLayout layout, float left, float panelY, float alpha, Color accent)
        {
            float tabY = panelY + layout.S(58f);
            float tabX = left;
            for (int i = 0; i < TabLabels.Length; i++)
            {
                bool selected = i == _selectedTab;
                bool hovered = _hoveredButton == i;
                Color fill = selected
                    ? new Color(0.1f, 0.16f, 0.14f)
                    : new Color(0.06f, 0.08f, 0.11f);
                Color border = selected || hovered ? accent : new Color(0.22f, 0.3f, 0.34f);
                _ui.DrawFramedPanel(tabX, tabY, layout.S(TabWidth), layout.S(TabHeight), fill, border, alpha);
                Color textColor = selected ? new Color(0.65f, 0.92f, 0.75f) : new Color(0.5f, 0.55f, 0.62f);
                string label = $"{i + 1} {TabLabels[i]}";
                float textW = _ui.MeasureString(label, layout.S(0.95f));
                _ui.DrawString(label, tabX + (layout.S(TabWidth) - textW) / 2f, tabY + layout.S(8f), layout.S(0.95f),
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
                _ui.DrawString(_viewModel.StatusLine, left, y - layout.S(18f), layout.S(0.95f),
                    new Color(0.72f, 0.82f, 0.88f) * alpha);
                _ui.DrawString("Next: " + _viewModel.NextAction, left, y - layout.S(4f), layout.S(0.85f),
                    new Color(0.55f, 0.75f, 0.65f) * alpha);
            }

            DrawStatCard(layout, x, y, cardW, cardH, "POPULATION", $"{_village!.Population}/{_village.PopulationCap}",
                (float)_village.Population / Math.Max(1, _village.PopulationCap), new Color(0.45f, 0.78f, 0.55f), alpha, accent);
            x += cardW + gap;
            DrawStatCard(layout, x, y, cardW, cardH, "FOOD", $"{_village.FoodStock:F0}",
                Math.Clamp(_village.FoodStock / Math.Max(1f, _village.Population * 2f), 0f, 1f),
                new Color(0.92f, 0.72f, 0.28f), alpha, accent);
            x += cardW + gap;
            DrawStatCard(layout, x, y, cardW, cardH, "HAPPINESS", $"{_village.Happiness:P0}", _village.Happiness,
                new Color(0.55f, 0.82f, 0.95f), alpha, accent);
            x += cardW + gap;
            DrawStatCard(layout, x, y, cardW, cardH, "HOUSING", $"{_village.HousingCapacity}",
                Math.Clamp((float)_village.Population / Math.Max(1, _village.HousingCapacity), 0f, 1f),
                new Color(0.78f, 0.58f, 0.92f), alpha, accent);

            y += cardH + layout.S(14f);

            if (!string.IsNullOrWhiteSpace(_openingNote))
            {
                float bannerH = layout.S(42f);
                _ui.DrawFramedPanel(left, y, layout.S(PanelWidth) - layout.S(40f), bannerH,
                    new Color(0.08f, 0.11f, 0.14f), new Color(0.5f, 0.75f, 0.6f), alpha);
                _ui.DrawString(_openingNote!, left + layout.S(14f), y + layout.S(12f), layout.S(0.9f),
                    new Color(0.78f, 0.9f, 0.82f) * alpha);
                y += bannerH + layout.S(10f);
            }

            float colW = (layout.S(PanelWidth) - layout.S(40f) - layout.S(12f)) / 2f;
            float colH = height - (y - (layout.PanelY + layout.S(ContentTop))) - layout.S(8f);
            DrawStoragePanel(layout, left, y, colW, colH, alpha, accent);
            DrawActivityPanel(layout, left + colW + layout.S(12f), y, colW, colH, alpha, accent);
        }

        private void DrawStoragePanel(ScreenLayout layout, float x, float y, float w, float h, float alpha, Color accent)
        {
            _ui.DrawFramedPanel(x, y, w, h, new Color(0.06f, 0.08f, 0.11f), accent, alpha * 0.95f);
            _ui.DrawString("VILLAGE STORAGE", x + layout.S(12f), y + layout.S(10f), layout.S(1.05f),
                new Color(0.55f, 0.75f, 0.65f) * alpha);

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
                _ui.DrawString(label, x + layout.S(14f), rowY, layout.S(0.92f), new Color(0.85f, 0.88f, 0.92f) * alpha);
                rowY += layout.S(17f);
                shown++;
            }

            if (shown == 0)
            {
                _ui.DrawString("EMPTY — HAULERS DELIVER HERE", x + layout.S(14f), rowY, layout.S(0.88f),
                    new Color(0.45f, 0.5f, 0.58f) * alpha);
            }

            int plankCount = _village.Storage.CountBlock(VillageEntity.RationBlock);
            float recruitY = y + h - layout.S(36f);
            string recruitHint = _playerCreative
                ? "RECRUIT COST: FREE IN CREATIVE"
                : $"RECRUIT COST: {VillageEntity.RecruitFoodCost} OAK PLANK ({plankCount} IN STORAGE)";
            _ui.DrawHorizontalRule(x + layout.S(10f), recruitY - layout.S(8f), w - layout.S(20f),
                new Color(0.2f, 0.28f, 0.32f), layout.S(1f), alpha);
            _ui.DrawString(recruitHint, x + layout.S(14f), recruitY, layout.S(0.82f),
                new Color(0.58f, 0.68f, 0.74f) * alpha);
        }

        private void DrawActivityPanel(ScreenLayout layout, float x, float y, float w, float h, float alpha, Color accent)
        {
            _ui.DrawFramedPanel(x, y, w, h, new Color(0.06f, 0.08f, 0.11f), accent, alpha * 0.95f);
            _ui.DrawString("ACTIVITY", x + layout.S(12f), y + layout.S(10f), layout.S(1.05f),
                new Color(0.55f, 0.75f, 0.65f) * alpha);

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
                _ui.DrawString(line, x + layout.S(14f), rowY, layout.S(0.9f), new Color(0.78f, 0.84f, 0.9f) * alpha);
                DrawMiniBar(x + layout.S(14f), rowY + layout.S(14f), w - layout.S(28f), layout.S(6f),
                    site.CompletionRatio, new Color(0.35f, 0.72f, 0.48f), alpha);
                rowY += layout.S(28f);
            }

            if (pendingSites == 0)
            {
                _ui.DrawString("NO ACTIVE CONSTRUCTION", x + layout.S(14f), rowY, layout.S(0.88f),
                    new Color(0.45f, 0.5f, 0.58f) * alpha);
                rowY += layout.S(20f);
            }

            rowY += layout.S(6f);
            _ui.DrawString("GATHER QUEUE", x + layout.S(10f), rowY, layout.S(0.95f), new Color(0.5f, 0.68f, 0.58f) * alpha);
            rowY += layout.S(18f);
            int queued = _village.WorkQueue.Count;
            string queueLine = queued == 0
                ? "SHIFT+CLICK BLOCKS OR PAINT ZONE"
                : $"{queued} BLOCK(S) MARKED FOR WORKERS";
            _ui.DrawString(queueLine, x + layout.S(14f), rowY, layout.S(0.88f), new Color(0.72f, 0.78f, 0.86f) * alpha);
            rowY += layout.S(20f);

            _ui.DrawString("QUICK GUIDE", x + layout.S(10f), rowY, layout.S(0.95f), new Color(0.5f, 0.68f, 0.58f) * alpha);
            rowY += layout.S(18f);
            int guideLines = 0;
            foreach (string line in GetGuideLines())
            {
                if (guideLines >= 4)
                {
                    break;
                }

                _ui.DrawString($"• {line}", x + layout.S(14f), rowY, layout.S(0.82f), new Color(0.62f, 0.72f, 0.78f) * alpha);
                rowY += layout.S(16f);
                guideLines++;
            }
        }

        private void DrawBuildTab(ScreenLayout layout, float left, float y, float height, float alpha, Color accent)
        {
            float sectionH = layout.S(130f);
            _ui.DrawFramedPanel(left, y, layout.S(PanelWidth) - layout.S(40f), sectionH,
                new Color(0.06f, 0.08f, 0.11f), accent, alpha * 0.95f);
            _ui.DrawString("CONSTRUCTION SITES", left + layout.S(12f), y + layout.S(10f), layout.S(1.0f),
                new Color(0.55f, 0.75f, 0.65f) * alpha);

            float siteY = y + layout.S(30f);
            int drawn = 0;
            foreach (var site in _village!.BuildingSites)
            {
                if (site.IsComplete || drawn >= 3)
                {
                    continue;
                }

                string name = site.BlueprintId.ToUpperInvariant();
                _ui.DrawString(name, left + layout.S(16f), siteY, layout.S(0.95f), new Color(0.85f, 0.88f, 0.92f) * alpha);
                DrawMiniBar(left + layout.S(200f), siteY + layout.S(4f), layout.S(420f), layout.S(10f),
                    site.CompletionRatio, new Color(0.3f, 0.7f, 0.95f), alpha);
                _ui.DrawString($"{site.CompletionRatio:P0}", left + layout.S(640f), siteY, layout.S(0.9f),
                    new Color(0.62f, 0.72f, 0.78f) * alpha);
                siteY += layout.S(28f);
                drawn++;
            }

            if (drawn == 0)
            {
                _ui.DrawString("NO BUILD SITES — PICK A STRUCTURE BELOW", left + layout.S(16f), siteY, layout.S(0.9f),
                    new Color(0.45f, 0.5f, 0.58f) * alpha);
            }

            float catalogY = y + sectionH + layout.S(12f);
            float catalogH = height - sectionH - layout.S(12f);
            _ui.DrawFramedPanel(left, catalogY, layout.S(PanelWidth) - layout.S(40f), catalogH,
                new Color(0.06f, 0.08f, 0.11f), accent, alpha * 0.95f);
            _ui.DrawString("BUILD CATALOG — CLICK TO PLACE", left + layout.S(12f), catalogY + layout.S(10f),
                layout.S(1.0f), new Color(0.55f, 0.75f, 0.65f) * alpha);

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
                Color cardBorder = hovered ? accent : (canAfford ? new Color(0.28f, 0.38f, 0.34f) : new Color(0.22f, 0.24f, 0.28f));
                Color cardFill = hovered ? new Color(0.1f, 0.14f, 0.13f) : new Color(0.07f, 0.09f, 0.12f);
                if (cardY + cardH >= catalogY + layout.S(30f) && cardY <= catalogY + catalogH - layout.S(6f))
                {
                    _ui.DrawFramedPanel(left + layout.S(12f), cardY, cardW, cardH, cardFill, cardBorder, alpha);
                    _ui.DrawString(blueprint.DisplayName.ToUpperInvariant(), left + layout.S(22f), cardY + layout.S(10f),
                        layout.S(1.02f), (canAfford ? new Color(0.88f, 0.92f, 0.96f) : new Color(0.5f, 0.54f, 0.6f)) * alpha);
                    _ui.DrawString(GetBuildingBlurb(blueprint.Kind), left + layout.S(22f), cardY + layout.S(28f),
                        layout.S(0.82f), new Color(0.55f, 0.62f, 0.7f) * alpha);
                    string costs = FormatCosts(blueprint);
                    float costW = _ui.MeasureString(costs, layout.S(0.82f));
                    _ui.DrawString(costs, left + layout.S(12f) + cardW - costW - layout.S(12f), cardY + layout.S(18f),
                        layout.S(0.82f), (canAfford ? new Color(0.5f, 0.82f, 0.62f) : new Color(0.72f, 0.45f, 0.4f)) * alpha);
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

            _ui.DrawFramedPanel(left, y, listW, height, new Color(0.06f, 0.08f, 0.11f), accent, alpha * 0.95f);
            _ui.DrawString("CITIZENS", left + layout.S(12f), y + layout.S(10f), layout.S(1.0f),
                new Color(0.55f, 0.75f, 0.65f) * alpha);

            float rowY = y + layout.S(32f) - _peopleScroll;
            float rowH = layout.S(42f);
            foreach (int villagerId in _village!.VillagerIds)
            {
                if (!_villagers.TryGet(villagerId, out var villager))
                {
                    continue;
                }

                bool isSelected = villagerId == _selectedVillagerId;
                bool hovered = _hoveredButton == 30 + villagerId;
                if (rowY + rowH >= y + layout.S(28f) && rowY <= y + height - layout.S(6f))
                {
                    if (isSelected || hovered)
                    {
                        _ui.DrawFilledRect(left + layout.S(8f), rowY, listW - layout.S(16f), rowH,
                            (isSelected ? new Color(0.14f, 0.22f, 0.18f) : new Color(0.1f, 0.14f, 0.16f)) * alpha);
                    }

                    var roleColor = VillagerVisuals.GetRoleColor(villager.Role);
                    _ui.DrawFilledRect(left + layout.S(14f), rowY + layout.S(12f), layout.S(10f), layout.S(10f), roleColor * alpha);
                    _ui.DrawString(villager.Name.ToUpperInvariant(), left + layout.S(30f), rowY + layout.S(8f),
                        layout.S(0.98f), (isSelected ? new Color(0.7f, 0.92f, 0.78f) : new Color(0.85f, 0.88f, 0.92f)) * alpha);
                    _ui.DrawString($"{villager.Role} · {villager.CurrentJob}".ToUpperInvariant(), left + layout.S(30f),
                        rowY + layout.S(24f), layout.S(0.8f), roleColor * alpha);
                }

                rowY += rowH + layout.S(4f);
            }

            if (_village.VillagerIds.Count == 0)
            {
                _ui.DrawString("NO VILLAGERS — RECRUIT BELOW", left + layout.S(14f), y + layout.S(40f), layout.S(0.9f),
                    new Color(0.45f, 0.5f, 0.58f) * alpha);
            }

            _ui.DrawFramedPanel(detailX, y, detailW, height, new Color(0.06f, 0.08f, 0.11f), accent, alpha * 0.95f);
            if (_selectedVillagerId >= 0 && _villagers.TryGet(_selectedVillagerId, out var selected))
            {
                DrawVillagerDetail(layout, detailX, y, detailW, height, selected, alpha, accent);
            }
            else
            {
                _ui.DrawString("SELECT A CITIZEN", detailX + layout.S(16f), y + layout.S(40f), layout.S(1.1f),
                    new Color(0.45f, 0.5f, 0.58f) * alpha);
            }
        }

        private void DrawVillagerDetail(ScreenLayout layout, float x, float y, float w, float h, Villager villager, float alpha, Color accent)
        {
            float pad = layout.S(16f);
            float detailY = y + pad;
            var roleColor = VillagerVisuals.GetRoleColor(villager.Role);

            _ui.DrawString(villager.Name.ToUpperInvariant(), x + pad, detailY, layout.S(1.35f),
                new Color(0.88f, 0.92f, 0.96f) * alpha);
            detailY += layout.S(28f);
            _ui.DrawString($"{villager.Role}  ·  {villager.CurrentJob}".ToUpperInvariant(), x + pad, detailY,
                layout.S(1.0f), roleColor * alpha);
            detailY += layout.S(24f);
            _ui.DrawString($"TRAIT {villager.Persona.Trait.ToUpperInvariant()}", x + pad, detailY, layout.S(0.9f),
                new Color(0.58f, 0.68f, 0.74f) * alpha);
            detailY += layout.S(28f);

            _ui.DrawString(
                $"SKILLS  MINING {villager.Skills.Mining.Level}  WOOD {villager.Skills.Woodcutting.Level}  FARM {villager.Skills.Farming.Level}",
                x + pad, detailY, layout.S(0.88f), new Color(0.58f, 0.68f, 0.74f) * alpha);
            detailY += layout.S(28f);
            _ui.DrawProgressBar(x + pad, detailY, w - pad * 2f, layout.S(12f), villager.Happiness, "MORALE", 0.7f, alpha);
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
            _ui.DrawString("STEWARD GOALS", left + layout.S(12f), y + layout.S(10f), layout.S(1.05f),
                new Color(0.55f, 0.75f, 0.65f) * alpha);
            _ui.DrawString("PRIORITY TASKS SET BY THE VILLAGE AI STEWARD", left + layout.S(12f), y + layout.S(28f),
                layout.S(0.85f), new Color(0.45f, 0.52f, 0.58f) * alpha);

            float rowY = y + layout.S(52f);
            int shown = 0;
            foreach (var goal in _village!.Scheduler.Goals)
            {
                if (shown >= 8)
                {
                    break;
                }

                Color statusColor = goal.Completed ? new Color(0.45f, 0.82f, 0.55f) : new Color(0.85f, 0.88f, 0.92f);
                string status = goal.Completed ? "DONE" : "ACTIVE";
                _ui.DrawString($"[{status}] {goal.Description.ToUpperInvariant()}", left + layout.S(16f), rowY,
                    layout.S(0.9f), statusColor * alpha);

                if (!goal.Completed && goal.Kind == VillageGoalKind.Stock && goal.StockBlock.HasValue && goal.TargetCount > 0)
                {
                    int have = _village.Scheduler.GetStockProgress(goal, _village);
                    float progress = Math.Clamp(have / (float)goal.TargetCount, 0f, 1f);
                    DrawMiniBar(left + layout.S(16f), rowY + layout.S(16f), layout.S(500f), layout.S(6f), progress,
                        new Color(0.35f, 0.72f, 0.48f), alpha);
                    _ui.DrawString($"{have}/{goal.TargetCount} {goal.StockBlock.Value}".ToUpperInvariant(),
                        left + layout.S(530f), rowY + layout.S(12f), layout.S(0.82f), new Color(0.58f, 0.68f, 0.74f) * alpha);
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
                    rowY + layout.S(10f), layout.S(0.9f), new Color(0.45f, 0.5f, 0.58f) * alpha);
            }
        }

        private void DrawStatCard(ScreenLayout layout, float x, float y, float w, float h, string label, string value, float ratio, Color barColor,
            float alpha, Color accent)
        {
            _ui.DrawFramedPanel(x, y, w, h, new Color(0.07f, 0.09f, 0.12f), accent, alpha * 0.92f);
            _ui.DrawString(label, x + layout.S(10f), y + layout.S(8f), layout.S(0.82f), new Color(0.5f, 0.62f, 0.68f) * alpha);
            _ui.DrawString(value, x + layout.S(10f), y + layout.S(24f), layout.S(1.1f), new Color(0.88f, 0.92f, 0.96f) * alpha);
            DrawMiniBar(x + layout.S(10f), y + h - layout.S(14f), w - layout.S(20f), layout.S(6f), ratio, barColor, alpha);
        }

        private void DrawMiniBar(float x, float y, float w, float h, float ratio, Color fill, float alpha)
        {
            ratio = Math.Clamp(ratio, 0f, 1f);
            _ui.DrawFilledRect(x, y, w, h, new Color(0.08f, 0.1f, 0.14f) * alpha);
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
                HitRect(tabX, tabY, layout.S(TabWidth), layout.S(TabHeight), i, mouse);
                tabX += layout.S(TabWidth) + layout.S(8f);
            }
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
            foreach (int villagerId in _village!.VillagerIds)
            {
                HitRect(layout.Left + layout.S(8f), rowY, listW - layout.S(16f), rowH, 30 + villagerId, mouse);
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
                RecruitRequested = true;
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
        }

        private void ResetRequests()
        {
            RecruitRequested = false;
            ClaimRequested = false;
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

        private string GetRecruitButtonLabel()
        {
            if (_village == null)
            {
                return "RECRUIT";
            }

            if (_village.Population >= _village.PopulationCap)
            {
                return "RECRUIT (AT CAP)";
            }

            if (_playerCreative || _village.Storage.CountBlock(VillageEntity.RationBlock) >= VillageEntity.RecruitFoodCost)
            {
                return _playerCreative ? "RECRUIT (FREE)" : "RECRUIT VILLAGER";
            }

            return "RECRUIT (NEED PLANKS)";
        }

        private IEnumerable<string> GetGuideLines()
        {
            if (_village == null)
            {
                yield break;
            }

            if (_village.Population == 0)
            {
                yield return _playerCreative
                    ? "Recruit your first villager (free in creative)."
                    : "Stock 4 oak planks, then recruit.";
            }

            yield return "Build tab: queue farms, houses, workshops.";
            yield return "People tab: assign Lumber, Farm, Build jobs.";
            yield return "Shift+click blocks to mark gather work.";
        }

        private static string GetBuildingBlurb(BuildingKind kind) => kind switch
        {
            BuildingKind.House => "HOUSING FOR 2 CITIZENS",
            BuildingKind.FarmPlot => "GROWS FOOD OVER TIME",
            BuildingKind.LumberCamp => "BOOSTS WOODCUTTING",
            BuildingKind.Quarry => "BOOSTS MINING",
            BuildingKind.Workshop => "CRAFTS TOOLS AND PLANKS",
            BuildingKind.Storage => "MORE SHARED STORAGE SLOTS",
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
            int count = _village?.VillagerIds.Count ?? 0;
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
    }
}

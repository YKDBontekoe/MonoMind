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
using VillageEntity = Autonocraft.Village.Village;
using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.UI
{
    public sealed class VillageScreen
    {
        private const float PanelWidth = 640f;
        private const float PanelHeight = 520f;
        private const float ButtonWidth = 140f;
        private const float ButtonHeight = 34f;
        private const int HitPadding = 6;

        private readonly UiRenderer _ui;
        private readonly VillagerManager _villagers;
        private VillageEntity? _village;
        private VillageManager? _villageManager;
        private VoxelWorld? _world;
        private Vector3 _playerPos;
        private IItemContainer? _playerPayer;
        private int _selectedTab;
        private int _selectedVillagerId = -1;
        private int _hoveredButton = -1;
        private bool _canClaimNearby;

        public bool IsOpen { get; private set; }
        public bool RecruitRequested { get; private set; }
        public bool ClaimRequested { get; private set; }
        public bool CloseRequested { get; private set; }
        public string? RequestedBlueprintId { get; private set; }
        public int RequestedAssignVillagerId { get; private set; } = -1;
        public JobType RequestedAssignJob { get; private set; } = JobType.Idle;

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
            IItemContainer? playerPayer)
        {
            _village = village;
            _villageManager = villageManager;
            _world = world;
            _playerPos = playerPos;
            _playerPayer = playerPayer;
            _selectedTab = 0;
            _selectedVillagerId = -1;
            _hoveredButton = -1;
            _canClaimNearby = villageManager.TryFindClaimableStructure(world, playerPos, 24f, out _, out _, out _);
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
            _village = null;
            _villageManager = null;
            _world = null;
            _playerPayer = null;
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
            RecruitRequested = false;
            ClaimRequested = false;
            CloseRequested = false;
            RequestedBlueprintId = null;
            RequestedAssignVillagerId = -1;

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
                _selectedTab = (_selectedTab + 2) % 3;
            }

            if (kb.IsKeyDown(Keys.Right) && !prevKb.IsKeyDown(Keys.Right))
            {
                _selectedTab = (_selectedTab + 1) % 3;
            }

            var layout = new UiLayout(viewport);
            float panelW = layout.S(PanelWidth);
            float panelH = layout.S(PanelHeight);
            float panelX = layout.CenterX - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f;
            float left = panelX + layout.S(20f);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);

            _hoveredButton = -1;
            int buttonIndex = 0;

            float tabY = panelY + layout.S(44f);
            string[] tabs = { "OVERVIEW", "BUILDINGS", "VILLAGERS" };
            float tabX = left;
            for (int i = 0; i < tabs.Length; i++)
            {
                float tabW = layout.S(120f);
                var tabRect = new Rectangle((int)tabX, (int)tabY, (int)tabW, (int)layout.S(24f));
                if (HitTest(tabRect, mouse.X, mouse.Y))
                {
                    _hoveredButton = buttonIndex;
                }

                tabX += tabW + layout.S(8f);
                buttonIndex++;
            }

            float footerY = panelY + panelH - layout.S(72f);
            var recruitRect = new Rectangle((int)left, (int)footerY, (int)buttonW, (int)buttonH);
            var closeRect = new Rectangle((int)(panelX + panelW - layout.S(20f) - buttonW), (int)(panelY + panelH - layout.S(28f)), (int)buttonW, (int)buttonH);

            if (HitTest(recruitRect, mouse.X, mouse.Y))
            {
                _hoveredButton = 10;
            }
            else if (HitTest(closeRect, mouse.X, mouse.Y))
            {
                _hoveredButton = 11;
            }

            if (_selectedTab == 0 && _canClaimNearby)
            {
                var claimRect = new Rectangle((int)(left + buttonW + layout.S(12f)), (int)footerY, (int)buttonW, (int)buttonH);
                if (HitTest(claimRect, mouse.X, mouse.Y))
                {
                    _hoveredButton = 12;
                }
            }

            if (_selectedTab == 1)
            {
                float buildY = panelY + layout.S(120f);
                foreach (var blueprint in PlayerStructureRegistry.All)
                {
                    if (blueprint.Id == "town_heart")
                    {
                        continue;
                    }

                    var buildRect = new Rectangle((int)left, (int)buildY, (int)layout.S(220f), (int)buttonH);
                    if (HitTest(buildRect, mouse.X, mouse.Y))
                    {
                        _hoveredButton = 20 + buttonIndex;
                    }

                    buildY += buttonH + layout.S(8f);
                    buttonIndex++;
                }
            }

            if (_selectedTab == 2)
            {
                float villagerY = panelY + layout.S(120f);
                foreach (int villagerId in _village.VillagerIds)
                {
                    var rowRect = new Rectangle((int)left, (int)villagerY, (int)layout.S(280f), (int)layout.S(22f));
                    if (HitTest(rowRect, mouse.X, mouse.Y))
                    {
                        _hoveredButton = 30 + villagerId;
                    }

                    villagerY += layout.S(24f);
                }

                if (_selectedVillagerId >= 0)
                {
                    float jobY = panelY + panelH - layout.S(120f);
                    string[] jobs = { "IDLE", "GATHER", "BUILD", "HAUL" };
                    float jobX = left;
                    for (int i = 0; i < jobs.Length; i++)
                    {
                        var jobRect = new Rectangle((int)jobX, (int)jobY, (int)layout.S(90f), (int)buttonH);
                        if (HitTest(jobRect, mouse.X, mouse.Y))
                        {
                            _hoveredButton = 40 + i;
                        }

                        jobX += layout.S(98f);
                    }
                }
            }

            bool click = mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released;
            bool activate = click
                || ((kb.IsKeyDown(Keys.Enter) || kb.IsKeyDown(Keys.Space))
                    && !prevKb.IsKeyDown(Keys.Enter)
                    && !prevKb.IsKeyDown(Keys.Space));

            if (!activate)
            {
                return;
            }

            if (_hoveredButton >= 0 && _hoveredButton <= 2)
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
                        return;
                    }

                    buildIndex++;
                }
            }

            if (_hoveredButton >= 30 && _hoveredButton < 40)
            {
                _selectedVillagerId = _hoveredButton - 30;
                return;
            }

            if (_hoveredButton >= 40 && _hoveredButton < 44 && _selectedVillagerId >= 0)
            {
                RequestedAssignVillagerId = _selectedVillagerId;
                RequestedAssignJob = (JobType)(_hoveredButton - 40);
            }
        }

        public void Draw(Viewport viewport, float alpha = 1f, float offsetY = 0f)
        {
            if (!IsOpen || _village == null || alpha <= 0.01f)
            {
                return;
            }

            var layout = new UiLayout(viewport);
            float panelW = layout.S(PanelWidth);
            float panelH = layout.S(PanelHeight);
            float panelX = layout.CenterX - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f + offsetY;
            float left = panelX + layout.S(20f);
            float buttonW = layout.S(ButtonWidth);
            float buttonH = layout.S(ButtonHeight);

            _ui.DrawFullscreenBackground(new Color(0.02f, 0.03f, 0.06f) * (0.65f * alpha));
            _ui.DrawPanel(panelX, panelY, panelW, panelH, new Color(0.05f, 0.07f, 0.11f) * alpha, new Color(0.35f, 0.55f, 0.45f) * alpha);
            _ui.DrawCenteredText(_village.Name.ToUpperInvariant(), panelY + layout.S(16f), layout.S(1.6f), new Color(0.8f, 0.95f, 0.85f) * alpha);

            DrawTabs(layout, panelX, panelY, panelW, left, alpha);

            switch (_selectedTab)
            {
                case 1:
                    DrawBuildingsTab(layout, panelX, panelY, left, alpha);
                    break;
                case 2:
                    DrawVillagersTab(layout, panelX, panelY, panelH, left, buttonH, alpha);
                    break;
                default:
                    DrawOverviewTab(layout, panelY, left, alpha);
                    break;
            }

            float footerY = panelY + panelH - layout.S(72f);
            _ui.DrawButton(left, footerY, buttonW, buttonH, "RECRUIT", _hoveredButton == 10, false, layout.S(1.1f), alpha);
            if (_selectedTab == 0 && _canClaimNearby)
            {
                _ui.DrawButton(left + buttonW + layout.S(12f), footerY, buttonW, buttonH, "CLAIM OUTPOST", _hoveredButton == 12, false, layout.S(1.0f), alpha);
            }

            float closeX = panelX + panelW - layout.S(20f) - buttonW;
            float closeY = panelY + panelH - layout.S(28f);
            _ui.DrawButton(closeX, closeY, buttonW, buttonH, "CLOSE", _hoveredButton == 11, false, layout.S(1.2f), alpha);
            _ui.DrawCenteredText("ESC / R RECRUIT  |  LEFT/RIGHT TAB", panelY + panelH - layout.S(8f), layout.S(0.95f), new Color(0.45f, 0.5f, 0.58f) * alpha);
        }

        private void DrawTabs(UiLayout layout, float panelX, float panelY, float panelW, float left, float alpha)
        {
            string[] tabs = { "OVERVIEW", "BUILDINGS", "VILLAGERS" };
            float tabY = panelY + layout.S(44f);
            float tabX = left;
            for (int i = 0; i < tabs.Length; i++)
            {
                bool selected = i == _selectedTab;
                Color color = selected ? new Color(0.55f, 0.85f, 0.7f) : new Color(0.45f, 0.5f, 0.58f);
                _ui.DrawString(tabs[i], tabX, tabY, layout.S(1.05f), color * alpha);
                tabX += layout.S(128f);
            }
        }

        private void DrawOverviewTab(UiLayout layout, float panelY, float left, float alpha)
        {
            float y = panelY + layout.S(84f);
            _ui.DrawString(
                $"POP {_village!.Population}/{_village.PopulationCap}  TIER {_village.Tier.ToString().ToUpperInvariant()}",
                left,
                y,
                layout.S(1.1f),
                new Color(0.85f, 0.88f, 0.92f) * alpha);
            y += layout.S(22f);
            _ui.DrawString(
                $"FOOD {_village.FoodStock:F1}  HAPPINESS {_village.Happiness:P0}  HOUSING {_village.HousingCapacity}",
                left,
                y,
                layout.S(1.05f),
                new Color(0.72f, 0.78f, 0.86f) * alpha);
            y += layout.S(28f);

            _ui.DrawString("STORAGE", left, y, layout.S(1.3f), new Color(0.55f, 0.75f, 0.65f) * alpha);
            y += layout.S(24f);
            int storageLines = 0;
            for (int i = 0; i < _village.Storage.SlotCount && storageLines < 6; i++)
            {
                var stack = _village.Storage.GetSlot(i);
                if (stack.IsEmpty)
                {
                    continue;
                }

                string label = stack.Kind switch
                {
                    ItemKind.Block => stack.BlockType.ToString().ToUpperInvariant(),
                    ItemKind.Tool => stack.ToolId.ToString().ToUpperInvariant(),
                    _ => "ITEM"
                };
                _ui.DrawString($"{label} x{stack.Count}", left + layout.S(12f), y, layout.S(1.0f), new Color(0.85f, 0.88f, 0.92f) * alpha);
                y += layout.S(18f);
                storageLines++;
            }

            if (storageLines == 0)
            {
                _ui.DrawString("EMPTY", left + layout.S(12f), y, layout.S(1.0f), new Color(0.45f, 0.5f, 0.58f) * alpha);
            }
        }

        private void DrawBuildingsTab(UiLayout layout, float panelX, float panelY, float left, float alpha)
        {
            float y = panelY + layout.S(84f);
            _ui.DrawString("COMPLETED", left, y, layout.S(1.2f), new Color(0.55f, 0.75f, 0.65f) * alpha);
            y += layout.S(24f);
            foreach (var building in _village!.Buildings)
            {
                _ui.DrawString($"{building.BlueprintId.ToUpperInvariant()}", left + layout.S(12f), y, layout.S(1.0f), new Color(0.85f, 0.88f, 0.92f) * alpha);
                y += layout.S(18f);
            }

            y += layout.S(8f);
            _ui.DrawString("BUILD QUEUE", left, y, layout.S(1.2f), new Color(0.55f, 0.75f, 0.65f) * alpha);
            y += layout.S(24f);
            foreach (var site in _village.BuildingSites)
            {
                if (site.IsComplete)
                {
                    continue;
                }

                string line = $"{site.BlueprintId.ToUpperInvariant()} {site.CompletionRatio:P0}";
                _ui.DrawString(line, left + layout.S(12f), y, layout.S(1.0f), new Color(0.72f, 0.78f, 0.86f) * alpha);
                y += layout.S(18f);
            }

            y += layout.S(12f);
            _ui.DrawString("QUEUE NEW", left, y, layout.S(1.2f), new Color(0.55f, 0.75f, 0.65f) * alpha);
            y += layout.S(24f);
            foreach (var blueprint in PlayerStructureRegistry.All)
            {
                if (blueprint.Id == "town_heart")
                {
                    continue;
                }

                bool canAfford = _playerPayer != null && blueprint.CanAfford(_playerPayer);
                Color color = canAfford ? new Color(0.85f, 0.88f, 0.92f) : new Color(0.45f, 0.5f, 0.58f);
                _ui.DrawString($"+ {blueprint.DisplayName.ToUpperInvariant()}", left + layout.S(12f), y + layout.S(4f), layout.S(1.0f), color * alpha);
                y += buttonH(layout) + layout.S(8f);
            }
        }

        private void DrawVillagersTab(UiLayout layout, float panelX, float panelY, float panelH, float left, float buttonH, float alpha)
        {
            float y = panelY + layout.S(84f);
            foreach (int villagerId in _village!.VillagerIds)
            {
                if (!_villagers.TryGet(villagerId, out var villager))
                {
                    continue;
                }

                bool selected = villagerId == _selectedVillagerId;
                Color color = selected ? new Color(0.55f, 0.85f, 0.7f) : new Color(0.85f, 0.88f, 0.92f);
                string line = $"{villager.Name.ToUpperInvariant()} - {villager.Role} / {villager.CurrentJob}";
                _ui.DrawString(line, left + layout.S(12f), y, layout.S(1.05f), color * alpha);
                y += layout.S(24f);
            }

            if (_selectedVillagerId >= 0)
            {
                float jobY = panelY + panelH - layout.S(120f);
                _ui.DrawString("ASSIGN JOB", left, jobY - layout.S(22f), layout.S(1.1f), new Color(0.55f, 0.75f, 0.65f) * alpha);
                string[] jobs = { "IDLE", "GATHER", "BUILD", "HAUL" };
                float jobX = left;
                for (int i = 0; i < jobs.Length; i++)
                {
                    _ui.DrawButton(jobX, jobY, layout.S(90f), buttonH, jobs[i], _hoveredButton == 40 + i, false, layout.S(0.95f), alpha);
                    jobX += layout.S(98f);
                }
            }
        }

        private static float buttonH(UiLayout layout) => layout.S(ButtonHeight);

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
    }
}

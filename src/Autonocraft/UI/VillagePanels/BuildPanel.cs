using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Autonocraft.Domain.Village;
using Autonocraft.Engine;
using Autonocraft.Items;
using Autonocraft.Village;
using Autonocraft.World;

namespace Autonocraft.UI.VillagePanels
{
    /// <summary>
    /// Build tab: active construction sites and blueprint catalog.
    /// </summary>
    public sealed class BuildPanel : IVillagePanel
    {
        private const float PanelWidth = 900f;

        public int TabIndex => 1;
        public string Label => "Build";

        public bool IsVisible(VillagePanelContext context) => true;

        public void Draw(VillagePanelContext context)
        {
            var ui = context.Ui;
            var layout = context.UiLayout;
            float left = context.ContentLeft;
            float y = context.PanelY + layout.S(VillageScreen.ContentTop);
            float height = context.ContentHeight;
            float alpha = context.Alpha;
            Color accent = context.Accent;
            var village = context.Village;

            float sectionH = layout.S(130f);
            ui.DrawPanel(left, y, layout.S(PanelWidth) - layout.S(40f), sectionH,
                UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);
            UiTheme.DrawSectionHeader(ui, "Construction sites", left + layout.S(12f), y + layout.S(10f), layout, alpha);

            float siteY = y + layout.S(34f);
            int drawn = 0;
            foreach (var site in village.BuildingSites)
            {
                if (site.IsComplete || drawn >= 3)
                {
                    continue;
                }

                string name = site.BlueprintId;
                ui.DrawString(name, left + layout.S(16f), siteY, layout.S(UiTheme.FontBody), UiTheme.StatValue, alpha, semiBold: true);
                DrawMiniBar(ui, left + layout.S(200f), siteY + layout.S(4f), layout.S(420f), layout.S(10f),
                    site.CompletionRatio, UiTheme.Accent, alpha);
                ui.DrawString($"{site.CompletionRatio:P0}", left + layout.S(640f), siteY, layout.S(UiTheme.FontSmall),
                    UiTheme.Subtitle, alpha);
                siteY += layout.S(28f);
                drawn++;
            }

            if (drawn == 0)
            {
                ui.DrawString("No build sites — pick a structure below", left + layout.S(16f), siteY, layout.S(UiTheme.FontSmall),
                    UiTheme.Hint, alpha);
            }

            float catalogY = y + sectionH + layout.S(12f);
            float catalogH = height - sectionH - layout.S(12f);
            ui.DrawPanel(left, catalogY, layout.S(PanelWidth) - layout.S(40f), catalogH,
                UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);
            UiTheme.DrawSectionHeader(ui, "Build catalog", left + layout.S(12f), catalogY + layout.S(10f), layout, alpha);

            float cardY = catalogY + layout.S(38f) - context.BuildScroll;
            float cardH = layout.S(58f);
            float cardW = layout.S(PanelWidth) - layout.S(64f);
            int buildIndex = 0;
            foreach (var blueprint in GetOrderedBlueprints(context))
            {
                if (blueprint.Id == "town_heart")
                {
                    continue;
                }

                bool hovered = context.HoveredButton == 20 + buildIndex;
                bool canAfford = CanAffordBlueprint(context, blueprint);
                var recommendation = GetRecommendation(context, blueprint);
                Color cardBorder = hovered ? UiTheme.Accent : (canAfford ? UiTheme.PanelBorder : UiTheme.Rule);
                Color cardFill = hovered ? UiTheme.PanelBgHighlight : UiTheme.PanelBgMuted;
                if (cardY + cardH >= catalogY + layout.S(34f) && cardY <= catalogY + catalogH - layout.S(6f))
                {
                    ui.DrawPanel(left + layout.S(12f), cardY, cardW, cardH, cardFill, cardBorder, 0.8f, alpha, UiTheme.RadiusMd);
                    ui.DrawString(blueprint.DisplayName, left + layout.S(22f), cardY + layout.S(10f),
                        layout.S(UiTheme.FontBody), canAfford ? UiTheme.Title : UiTheme.Meta, alpha, semiBold: true);
                    string blurb = recommendation.Priority > 0
                        ? recommendation.Reason
                        : GetBuildingBlurb(blueprint.Kind);
                    ui.DrawString(TrimToWidth(ui, blurb, layout.S(UiTheme.FontSmall), cardW - layout.S(220f)),
                        left + layout.S(22f), cardY + layout.S(28f),
                        layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
                    if (recommendation.Priority > 0)
                    {
                        DrawBadge(ui, layout, left + layout.S(170f), cardY + layout.S(12f),
                            recommendation.Label, canAfford ? UiTheme.Accent : UiTheme.Meta, alpha);
                    }

                    string costs = FormatCosts(blueprint);
                    float costW = ui.MeasureString(costs, layout.S(UiTheme.FontSmall));
                    ui.DrawString(costs, left + layout.S(12f) + cardW - costW - layout.S(12f), cardY + layout.S(18f),
                        layout.S(UiTheme.FontSmall), canAfford ? UiTheme.Subtitle : UiTheme.Danger, alpha);
                }

                cardY += cardH + layout.S(8f);
                buildIndex++;
            }
        }

        public static IReadOnlyList<BuildingBlueprint> GetOrderedBlueprints(VillagePanelContext context)
        {
            var blueprints = new List<BuildingBlueprint>();
            foreach (var blueprint in PlayerStructureRegistry.All)
            {
                if (blueprint.Id != "town_heart")
                {
                    blueprints.Add(blueprint);
                }
            }

            blueprints.Sort((a, b) =>
            {
                int priority = GetRecommendation(context, b).Priority.CompareTo(GetRecommendation(context, a).Priority);
                return priority != 0
                    ? priority
                    : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });
            return blueprints;
        }

        private static (int Priority, string Label, string Reason) GetRecommendation(
            VillagePanelContext context,
            BuildingBlueprint blueprint)
        {
            var village = context.Village;
            var vm = context.ViewModel;
            int citizens = vm?.Population ?? village.Population;

            if (blueprint.Kind == BuildingKind.House && citizens >= village.PopulationCap)
            {
                return (100, "needed", "Housing is full; add beds before more families can join.");
            }

            if (blueprint.Kind == BuildingKind.FarmPlot &&
                (vm?.FoodRiskLevel is FoodRiskLevel.Low or FoodRiskLevel.Critical || village.FoodStock <= Math.Max(1, citizens)))
            {
                return (95, "food", "Food is tight; farms keep recruiting and families moving.");
            }

            if (blueprint.Kind == BuildingKind.Market &&
                citizens >= 3 &&
                !village.HasBuilding(BuildingKind.Market))
            {
                return (80, "trade", "Turns surplus into favor for trades and future agent contracts.");
            }

            if (blueprint.Kind == BuildingKind.Workshop &&
                citizens >= 4 &&
                !village.HasBuilding(BuildingKind.Workshop))
            {
                return (78, "agents", "Crafts tools and opens the path to delegated work orders.");
            }

            if (blueprint.Kind == BuildingKind.Storage && village.Storage.SlotCount <= 9 && citizens >= 3)
            {
                return (70, "logistics", "Adds shared storage so large builds and hauling do not jam.");
            }

            if (blueprint.Kind == BuildingKind.LumberCamp && citizens > 0 && !village.HasBuilding(BuildingKind.LumberCamp))
            {
                return (55, "wood", "Useful early: faster planks feed houses, tools, and recruits.");
            }

            if (blueprint.Kind == BuildingKind.Quarry && citizens >= 2 && !village.HasBuilding(BuildingKind.Quarry))
            {
                return (45, "stone", "Secures cobble and stone for bigger civic buildings.");
            }

            return (0, string.Empty, string.Empty);
        }

        private static void DrawBadge(
            UiRenderer ui,
            UiLayout layout,
            float x,
            float y,
            string label,
            Color accent,
            float alpha)
        {
            float textW = ui.MeasureString(label, layout.S(UiTheme.FontCaption), semiBold: true);
            float w = textW + layout.S(16f);
            float h = layout.S(18f);
            ui.DrawRoundedRect(x, y, w, h, h * 0.5f, accent * (0.14f * alpha));
            ui.DrawRoundedRectOutline(x, y, w, h, h * 0.5f, accent * (0.45f * alpha), 1f, alpha);
            ui.DrawString(label, x + layout.S(8f), y + layout.S(4f), layout.S(UiTheme.FontCaption),
                accent, alpha, semiBold: true);
        }

        private static bool CanAffordBlueprint(VillagePanelContext context, BuildingBlueprint blueprint)
        {
            if (context.PlayerCreative)
            {
                return true;
            }

            if (blueprint.CanAfford(context.Village.Storage))
            {
                return true;
            }

            return context.PlayerPayer != null && blueprint.CanAfford(context.PlayerPayer);
        }

        private static void DrawMiniBar(UiRenderer ui, float x, float y, float w, float h, float ratio, Color fill, float alpha)
        {
            ratio = Math.Clamp(ratio, 0f, 1f);
            ui.DrawFilledRect(x, y, w, h, UiTheme.PanelBgMuted * alpha);
            if (ratio > 0.01f)
            {
                ui.DrawFilledRect(x, y, w * ratio, h, fill * alpha);
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

        private static string TrimToWidth(UiRenderer ui, string text, float fontSize, float maxWidth)
        {
            if (ui.MeasureString(text, fontSize) <= maxWidth)
            {
                return text;
            }

            const string ellipsis = "...";
            for (int len = text.Length - 1; len > 0; len--)
            {
                string candidate = text[..len].TrimEnd() + ellipsis;
                if (ui.MeasureString(candidate, fontSize) <= maxWidth)
                {
                    return candidate;
                }
            }

            return ellipsis;
        }

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
    }
}

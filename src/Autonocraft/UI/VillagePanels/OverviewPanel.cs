using System;
using Microsoft.Xna.Framework;
using Autonocraft.Domain.Village;
using Autonocraft.Engine;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.UI.Village;
using Autonocraft.Village;
using VillageEntity = Autonocraft.Village.Village;
using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.UI.VillagePanels
{
    /// <summary>
    /// Overview tab: high-level village stats, storage, and activity.
    /// </summary>
    public sealed class OverviewPanel : IVillagePanel
    {
        public int TabIndex => 0;
        public string Label => "Overview";

        public bool IsVisible(VillagePanelContext context) => true;

        public static bool TryGetNextActionCtaY(
            VillageViewModel? viewModel,
            UiLayout layout,
            float panelY,
            float contentTop,
            out float ctaY)
        {
            ctaY = 0f;
            if (viewModel == null ||
                viewModel.NextActionKind == SettlementActionKind.None ||
                !viewModel.SuggestedTab.HasValue)
            {
                return false;
            }

            float y = panelY + contentTop;
            y += layout.S(4f);
            y += layout.S(20f);

            if (!string.IsNullOrEmpty(viewModel.HudContextNote))
            {
                y += layout.S(28f) + layout.S(8f);
            }

            if (viewModel.FoodRiskLevel is FoodRiskLevel.Low or FoodRiskLevel.Critical ||
                viewModel.IdleWorkerCount > 0)
            {
                y = GetWellBeingBannerBottomY(y, layout, viewModel) + layout.S(6f);
            }

            if (viewModel.IdleWorkerCount > 0 || viewModel.FoodRiskLevel != FoodRiskLevel.Ok)
            {
                y += layout.S(28f);
            }

            if (!string.IsNullOrEmpty(viewModel.ActiveWorkSummary))
            {
                y += layout.S(18f);
            }

            ctaY = y;
            return true;
        }

        public void Draw(VillagePanelContext context)
        {
            var ui = context.Ui;
            var layout = context.UiLayout;

            float left = context.ContentLeft;
            float y = context.PanelY + context.ContentTop;
            float height = context.ContentHeight;
            float panelWidth = context.PanelWidth;
            float alpha = context.Alpha;
            Color accent = context.Accent;

            float cardW = layout.S(198f);
            float cardH = layout.S(68f);
            float gap = layout.S(10f);
            float x = left;

            var viewModel = context.ViewModel;
            if (viewModel != null)
            {
                y += layout.S(4f);
                ui.DrawString("Next: " + viewModel.NextAction, left, y, layout.S(UiTheme.FontSmall),
                    UiTheme.Hint, alpha);
                y += layout.S(20f);

                if (!string.IsNullOrEmpty(viewModel.HudContextNote))
                {
                    float noteH = layout.S(28f);
                    ui.DrawPanel(left, y, panelWidth - layout.S(40f), noteH, UiTheme.AccentSoft, UiTheme.Accent, 0.55f, alpha, UiTheme.RadiusSm);
                    ui.DrawString(viewModel.HudContextNote, left + layout.S(10f), y + layout.S(8f),
                        layout.S(UiTheme.FontSmall), UiTheme.Hint, alpha);
                    y += noteH + layout.S(8f);
                }

                if (viewModel.FoodRiskLevel is FoodRiskLevel.Low or FoodRiskLevel.Critical ||
                    viewModel.IdleWorkerCount > 0)
                {
                    y = DrawWellBeingBanner(ui, layout, left, y, panelWidth, viewModel, alpha) + layout.S(6f);
                }

                if (viewModel.IdleWorkerCount > 0)
                {
                    float badgeH = layout.S(22f);
                    ui.DrawPanel(left, y, layout.S(120f), badgeH, UiTheme.AccentSoft, UiTheme.Accent, 0.8f, alpha, UiTheme.RadiusSm);
                    ui.DrawString($"{viewModel.IdleWorkerCount} idle", left + layout.S(10f), y + layout.S(4f),
                        layout.S(UiTheme.FontSmall), UiTheme.Accent, alpha, semiBold: true);
                    x = left + layout.S(130f);
                }

                if (viewModel.FoodRiskLevel != FoodRiskLevel.Ok)
                {
                    var foodColor = viewModel.FoodRiskLevel == FoodRiskLevel.Critical
                        ? UiTheme.Danger
                        : new Color(0.92f, 0.72f, 0.28f);
                    float badgeW = layout.S(viewModel.IdleWorkerCount > 0 ? 100f : 120f);
                    float badgeH = layout.S(22f);
                    float badgeX = viewModel.IdleWorkerCount > 0 ? left + layout.S(130f) : left;
                    ui.DrawPanel(badgeX, y, badgeW, badgeH, UiTheme.DangerSoft, foodColor, 0.8f, alpha, UiTheme.RadiusSm);
                    string foodLabel = viewModel.FoodRiskLevel == FoodRiskLevel.Critical ? "Food critical" : "Food low";
                    ui.DrawString(foodLabel, badgeX + layout.S(10f), y + layout.S(4f),
                        layout.S(UiTheme.FontSmall), foodColor, alpha, semiBold: true);
                }

                if (viewModel.IdleWorkerCount > 0 || viewModel.FoodRiskLevel != FoodRiskLevel.Ok)
                {
                    y += layout.S(28f);
                    x = left;
                }

                if (!string.IsNullOrEmpty(viewModel.ActiveWorkSummary))
                {
                    ui.DrawString("Active: " + viewModel.ActiveWorkSummary, left, y, layout.S(UiTheme.FontSmall),
                        UiTheme.Meta, alpha);
                    y += layout.S(18f);
                }

                y = DrawVillagePulse(ui, layout, left, y, panelWidth, viewModel, alpha) + layout.S(10f);

                if (viewModel.NextActionKind != SettlementActionKind.None && viewModel.SuggestedTab.HasValue)
                {
                    string ctaLabel = viewModel.SuggestedTab switch
                    {
                        SettlementTab.People => "Go to People →",
                        SettlementTab.Build => "Go to Build →",
                        _ => "View details →"
                    };
                    float ctaW = layout.S(140f);
                    float ctaH = layout.S(28f);
                    bool hovered = context.HoveredButton == 15;
                    ui.DrawButton(left, y, ctaW, ctaH, ctaLabel, hovered, false, UiButtonStyle.Secondary,
                        layout.S(UiTheme.FontSmall), alpha, hovered ? 1f : 0f);
                    y += ctaH + layout.S(8f);
                }
            }

            var village = context.Village;
            int citizens = CountDisplayedCitizens(village, context.Villagers);
            int stranded = VillageSettlementHealth.CountStrandedCitizens(village, context.Villagers);
            if (citizens == 0 && stranded > 0)
            {
                float bannerH = layout.S(54f);
                var warningColor = new Color(0.92f, 0.72f, 0.28f);
                ui.DrawPanel(left, y, panelWidth - layout.S(40f), bannerH,
                    UiTheme.AccentSoft, warningColor, 0.85f, alpha, UiTheme.RadiusMd);
                ui.DrawString($"{stranded} settler(s) nearby are not linked to this town",
                    left + layout.S(14f), y + layout.S(10f), layout.S(UiTheme.FontBody), warningColor, alpha, semiBold: true);
                ui.DrawString("Click Link nearby settlers in the footer — they are already walking around.",
                    left + layout.S(14f), y + layout.S(30f), layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
                y += bannerH + layout.S(10f);
            }

            bool showMissingSettlersBanner = citizens == 0 && stranded == 0 &&
                viewModel?.NextActionKind != SettlementActionKind.SummonSettlers;
            if (showMissingSettlersBanner)
            {
                float bannerH = layout.S(54f);
                ui.DrawPanel(left, y, panelWidth - layout.S(40f), bannerH,
                    UiTheme.DangerSoft, UiTheme.Danger, 0.85f, alpha, UiTheme.RadiusMd);
                string bannerTitle = VillageSettlementHealth.IsPlayerNearTownHeart(village, context.PlayerPosition)
                    ? "No settlers in village"
                    : "No settlers nearby";
                ui.DrawString(bannerTitle, left + layout.S(14f), y + layout.S(10f), layout.S(UiTheme.FontBody),
                    UiTheme.Title, alpha, semiBold: true);
                ui.DrawString(VillageGuidance.GetQuickStartSteps(village, context.Villagers, context.PlayerPosition),
                    left + layout.S(14f), y + layout.S(30f), layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
                y += bannerH + layout.S(10f);
            }
            else if (citizens > 0 && citizens <= 2)
            {
                float bannerH = layout.S(40f);
                ui.DrawPanel(left, y, panelWidth - layout.S(40f), bannerH,
                    UiTheme.AccentSoft, UiTheme.Accent, 0.75f, alpha, UiTheme.RadiusMd);
                ui.DrawString("Quick start: " + VillageGuidance.GetQuickStartSteps(village, context.Villagers, context.PlayerPosition),
                    left + layout.S(14f), y + layout.S(12f), layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
                y += bannerH + layout.S(10f);
            }

            x = left;
            DrawStatCard(ui, layout, x, y, cardW, cardH, "Population", $"{citizens}/{village.PopulationCap}",
                (float)citizens / Math.Max(1, village.PopulationCap), new Color(0.45f, 0.78f, 0.55f), alpha, accent);
            x += cardW + gap;
            DrawStatCard(ui, layout, x, y, cardW, cardH, "Food", $"{village.FoodStock:F0}",
                Math.Clamp(village.FoodStock / Math.Max(1f, Math.Max(1, citizens) * 2f), 0f, 1f),
                new Color(0.92f, 0.72f, 0.28f), alpha, accent);
            x += cardW + gap;
            DrawStatCard(ui, layout, x, y, cardW, cardH, "Happiness", $"{village.Happiness:P0}", village.Happiness,
                new Color(0.55f, 0.82f, 0.95f), alpha, accent);
            x += cardW + gap;
            DrawStatCard(ui, layout, x, y, cardW, cardH, "Housing", $"{citizens}/{Math.Max(1, village.HousingCapacity)}",
                Math.Clamp((float)citizens / Math.Max(1, village.HousingCapacity), 0f, 1f),
                new Color(0.78f, 0.58f, 0.92f), alpha, accent);

            y += cardH + layout.S(14f);

            if (!string.IsNullOrWhiteSpace(context.OpeningNote))
            {
                float bannerH = layout.S(42f);
                ui.DrawPanel(left, y, panelWidth - layout.S(40f), bannerH,
                    UiTheme.AccentSoft, UiTheme.Accent, 0.75f, alpha, UiTheme.RadiusMd);
                ui.DrawString(context.OpeningNote!, left + layout.S(14f), y + layout.S(12f), layout.S(UiTheme.FontSmall),
                    UiTheme.Subtitle, alpha);
                y += bannerH + layout.S(10f);
            }

            float colW = (panelWidth - layout.S(40f) - layout.S(12f)) / 2f;
            float colH = height - (y - (context.PanelY + context.ContentTop)) - layout.S(8f);
            DrawStoragePanel(context, left, y, colW, colH, alpha, accent);
            DrawActivityPanel(context, left + colW + layout.S(12f), y, colW, colH, alpha, accent);
        }

        private static float GetWellBeingBannerBottomY(float y, UiLayout layout, VillageViewModel viewModel)
        {
            if (viewModel.FoodRiskLevel == FoodRiskLevel.Critical ||
                viewModel.FoodRiskLevel == FoodRiskLevel.Low ||
                viewModel.IdleWorkerCount >= 2)
            {
                return y + layout.S(36f);
            }

            return y;
        }

        private static float DrawWellBeingBanner(
            UiRenderer ui,
            UiLayout layout,
            float left,
            float y,
            float panelWidth,
            VillageViewModel viewModel,
            float alpha)
        {
            if (viewModel.FoodRiskLevel == FoodRiskLevel.Critical)
            {
                float bannerH = layout.S(36f);
                ui.DrawPanel(left, y, panelWidth - layout.S(40f), bannerH,
                    UiTheme.DangerSoft, UiTheme.Danger, 0.85f, alpha, UiTheme.RadiusMd);
                ui.DrawString("Food crisis — assign Farm jobs or queue a farm plot",
                    left + layout.S(14f), y + layout.S(10f), layout.S(UiTheme.FontSmall), UiTheme.Danger, alpha);
                return y + bannerH;
            }

            if (viewModel.FoodRiskLevel == FoodRiskLevel.Low)
            {
                var warningColor = new Color(0.92f, 0.72f, 0.28f);
                float bannerH = layout.S(36f);
                ui.DrawPanel(left, y, panelWidth - layout.S(40f), bannerH,
                    UiTheme.AccentSoft, warningColor, 0.75f, alpha, UiTheme.RadiusMd);
                ui.DrawString("Food running low — grow crops or take rations",
                    left + layout.S(14f), y + layout.S(10f), layout.S(UiTheme.FontSmall), warningColor, alpha);
                return y + bannerH;
            }

            if (viewModel.IdleWorkerCount >= 2)
            {
                float bannerH = layout.S(36f);
                ui.DrawPanel(left, y, panelWidth - layout.S(40f), bannerH,
                    UiTheme.AccentSoft, UiTheme.Accent, 0.75f, alpha, UiTheme.RadiusMd);
                ui.DrawString($"{viewModel.IdleWorkerCount} idle workers — assign jobs on People tab",
                    left + layout.S(14f), y + layout.S(10f), layout.S(UiTheme.FontSmall), UiTheme.Accent, alpha);
                return y + bannerH;
            }

            return y;
        }

        private static void DrawStatCard(
            UiRenderer ui,
            UiLayout layout,
            float x,
            float y,
            float w,
            float h,
            string label,
            string value,
            float ratio,
            Color barColor,
            float alpha,
            Color accent)
        {
            ui.DrawPanel(x, y, w, h, UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.92f, UiTheme.RadiusMd);
            ui.DrawString(label, x + layout.S(10f), y + layout.S(8f), layout.S(UiTheme.FontSmall), UiTheme.StatLabel, alpha);
            ui.DrawString(value, x + layout.S(10f), y + layout.S(24f), layout.S(UiTheme.FontBody), UiTheme.StatValue, alpha, semiBold: true);
            DrawMiniBar(ui, x + layout.S(10f), y + h - layout.S(14f), w - layout.S(20f), layout.S(6f), ratio, barColor, alpha);
        }

        private static float DrawVillagePulse(
            UiRenderer ui,
            UiLayout layout,
            float left,
            float y,
            float panelWidth,
            VillageViewModel viewModel,
            float alpha)
        {
            var pulse = viewModel.Pulse;
            float w = panelWidth - layout.S(40f);
            float h = layout.S(104f);
            ui.DrawPanel(left, y, w, h, UiTheme.PanelBgMuted, UiTheme.Accent, 0.8f, alpha * 0.95f, UiTheme.RadiusMd);

            ui.DrawString(pulse.Mood, left + layout.S(14f), y + layout.S(10f),
                layout.S(UiTheme.FontBody), UiTheme.Title, alpha, semiBold: true);
            ui.DrawString(pulse.Focus, left + layout.S(14f), y + layout.S(30f),
                layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);

            float barX = left + layout.S(14f);
            float barY = y + layout.S(52f);
            DrawMiniBar(ui, barX, barY, w - layout.S(28f), layout.S(6f), pulse.Momentum, UiTheme.Accent, alpha);

            float colY = y + layout.S(68f);
            float colW = (w - layout.S(42f)) / 4f;
            DrawPulseLine(ui, layout, left + layout.S(14f), colY, colW, "Next", pulse.Opportunity, alpha);
            DrawPulseLine(ui, layout, left + layout.S(18f) + colW, colY, colW, "Growth", pulse.GrowthHook, alpha);
            DrawPulseLine(ui, layout, left + layout.S(22f) + colW * 2f, colY, colW, "Trade", pulse.TradeHook, alpha);
            DrawPulseLine(ui, layout, left + layout.S(26f) + colW * 3f, colY, colW, "Agent", pulse.DelegationHook, alpha);

            return y + h;
        }

        private static void DrawPulseLine(
            UiRenderer ui,
            UiLayout layout,
            float x,
            float y,
            float w,
            string label,
            string text,
            float alpha)
        {
            ui.DrawString(label, x, y, layout.S(UiTheme.FontCaption), UiTheme.StatLabel, alpha, semiBold: true);
            ui.DrawString(TrimToWidth(ui, text, layout.S(UiTheme.FontSmall), w), x, y + layout.S(13f),
                layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
        }

        private static string TrimToWidth(UiRenderer ui, string text, float fontSize, float maxWidth)
        {
            if (ui.MeasureString(text, fontSize) <= maxWidth)
            {
                return text;
            }

            const string ellipsis = "...";
            for (int len = Math.Max(0, text.Length - 1); len > 0; len--)
            {
                string candidate = text[..len].TrimEnd() + ellipsis;
                if (ui.MeasureString(candidate, fontSize) <= maxWidth)
                {
                    return candidate;
                }
            }

            return ellipsis;
        }

        private static void DrawStoragePanel(
            VillagePanelContext context,
            float x,
            float y,
            float w,
            float h,
            float alpha,
            Color accent)
        {
            var ui = context.Ui;
            var layout = context.UiLayout;
            var village = context.Village;

            ui.DrawPanel(x, y, w, h, UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);
            UiTheme.DrawSectionHeader(ui, "Village storage", x + layout.S(12f), y + layout.S(10f), layout, alpha);

            float rowY = y + layout.S(32f);
            int shown = 0;
            for (int i = 0; i < village.Storage.SlotCount && shown < 10; i++)
            {
                var stack = village.Storage.GetSlot(i);
                if (stack.IsEmpty)
                {
                    continue;
                }

                string label = FormatStack(stack);
                ui.DrawString(label, x + layout.S(14f), rowY, layout.S(UiTheme.FontSmall), UiTheme.StatValue, alpha);
                rowY += layout.S(18f);
                shown++;
            }

            if (shown == 0)
            {
                ui.DrawString("Empty — haulers deliver here", x + layout.S(14f), rowY, layout.S(UiTheme.FontSmall),
                    UiTheme.Hint, alpha);
            }

            int plankCount = village.Storage.CountBlock(VillageEntity.RationBlock);
            float recruitY = y + h - layout.S(36f);
            string recruitHint = context.ViewModel?.RecruitPreview
                ?? (context.PlayerCreative
                    ? "Recruit cost: free in creative"
                    : $"Recruit cost: {VillageEntity.RecruitFoodCost} oak planks ({plankCount} in storage)");
            string costLine = context.ViewModel != null
                ? $"{recruitHint}  |  Favor {context.ViewModel.Pulse.FavorBalance}  |  Agent {context.ViewModel.Pulse.AgentWorkOrderCost} favor"
                : recruitHint;
            ui.DrawHorizontalRule(x + layout.S(10f), recruitY - layout.S(8f), w - layout.S(20f),
                UiTheme.Rule, 1f, alpha * 0.7f);
            ui.DrawString(TrimToWidth(ui, costLine, layout.S(UiTheme.FontSmall), w - layout.S(28f)),
                x + layout.S(14f), recruitY, layout.S(UiTheme.FontSmall),
                UiTheme.Meta, alpha);
        }

        private static void DrawActivityPanel(
            VillagePanelContext context,
            float x,
            float y,
            float w,
            float h,
            float alpha,
            Color accent)
        {
            var ui = context.Ui;
            var layout = context.UiLayout;
            var village = context.Village;

            ui.DrawPanel(x, y, w, h, UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);
            UiTheme.DrawSectionHeader(ui, "Activity", x + layout.S(12f), y + layout.S(10f), layout, alpha);

            float rowY = y + layout.S(32f);
            int pendingSites = 0;
            foreach (var site in village.BuildingSites)
            {
                if (site.IsComplete)
                {
                    continue;
                }

                pendingSites++;
                string line = $"{site.BlueprintId} {site.CompletionRatio:P0}";
                ui.DrawString(line, x + layout.S(14f), rowY, layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
                DrawMiniBar(ui, x + layout.S(14f), rowY + layout.S(14f), w - layout.S(28f), layout.S(6f),
                    site.CompletionRatio, UiTheme.Accent, alpha);
                rowY += layout.S(28f);
            }

            if (pendingSites == 0)
            {
                ui.DrawString("No active construction", x + layout.S(14f), rowY, layout.S(UiTheme.FontSmall),
                    UiTheme.Hint, alpha);
                rowY += layout.S(20f);
            }

            rowY += layout.S(6f);
            ui.DrawString("Gather queue", x + layout.S(12f), rowY, layout.S(UiTheme.FontSmall), UiTheme.Section, alpha, semiBold: true);
            rowY += layout.S(18f);
            int queued = village.WorkQueue.Count;
            string queueLine = queued == 0
                ? "Shift+click blocks or paint zone"
                : $"{queued} block(s) marked for workers";
            ui.DrawString(queueLine, x + layout.S(14f), rowY, layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
            rowY += layout.S(20f);

            ui.DrawString("Quick guide", x + layout.S(12f), rowY, layout.S(UiTheme.FontSmall), UiTheme.Section, alpha, semiBold: true);
            rowY += layout.S(18f);
            int guideLines = 0;
            foreach (string line in GetGuideLines(village, context.Villagers, context.PlayerPosition, context.PlayerCreative))
            {
                if (guideLines >= 4)
                {
                    break;
                }

                ui.DrawString($"• {line}", x + layout.S(14f), rowY, layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
                rowY += layout.S(16f);
                guideLines++;
            }
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

        private static int CountDisplayedCitizens(VillageEntity village, VillagerManager villagers)
        {
            return VillageSettlementHealth.CountLiveCitizens(village, villagers);
        }

        private static System.Collections.Generic.IEnumerable<string> GetGuideLines(
            VillageEntity village,
            VillagerManager villagers,
            Vector3 playerPos,
            bool playerCreative)
        {
            int citizens = CountDisplayedCitizens(village, villagers);
            if (citizens == 0)
            {
                yield return VillageGuidance.GetQuickStartSteps(village, villagers, playerPos);
                yield break;
            }

            yield return "People tab: pick a villager, click Lumber / Build / Farm.";
            yield return "Build tab: queue farm plot or peasant house.";
            yield return "Shift+click a tree to mark it for lumberjacks.";
            if (village.CanRecruit(villagers, playerCreative))
            {
                yield return "Press R to recruit another worker (4 oak planks).";
            }
        }

        private static string FormatStack(ItemStack stack) => stack.Kind switch
        {
            ItemKind.Block => $"{stack.BlockType} ×{stack.Count}",
            ItemKind.Tool => $"{stack.ToolId} ({stack.Durability}/{stack.MaxDurability})",
            _ => $"Item ×{stack.Count}"
        };
    }
}

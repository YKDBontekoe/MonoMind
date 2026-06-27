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

            ctaY = panelY + contentTop + layout.S(104f) + layout.S(10f);
            return true;
        }

        public void Draw(VillagePanelContext context)
        {
            var ui = context.Ui;
            var layout = context.UiLayout;

            float left = context.ContentLeft;
            float yStart = context.PanelY + context.ContentTop;
            float panelH = layout.S(VillageScreen.PanelHeight);
            float panelWidth = context.PanelWidth;
            float alpha = context.Alpha;
            Color accent = context.Accent;
            var village = context.Village;
            int citizens = CountDisplayedCitizens(village, context.Villagers);

            float colW = (panelWidth - layout.S(40f) - layout.S(20f)) / 2f;
            float rightColX = left + colW + layout.S(20f);
            float bottomY = context.PanelY + panelH - layout.S(VillageScreen.FooterHeight) - layout.S(10f);

            var viewModel = context.ViewModel;
            bool emptyRoster = citizens == 0;

            // ------------------ LEFT COLUMN ------------------
            float leftY = yStart;

            if (viewModel != null)
            {
                // 1. Priority Card
                leftY = DrawPriorityCard(context, left, leftY + layout.S(2f), colW, viewModel, emptyRoster) + layout.S(10f);

                // 2. Hud Context Note (if present)
                if (!string.IsNullOrEmpty(viewModel.HudContextNote))
                {
                    float noteH = layout.S(32f);
                    ui.DrawPanel(left, leftY, colW, noteH, UiTheme.AccentSoft, UiTheme.Accent, 0.55f, alpha, UiTheme.RadiusSm);
                    ui.DrawString(viewModel.HudContextNote, left + layout.S(10f), leftY + layout.S(8f),
                        layout.S(UiTheme.FontSmall), UiTheme.Hint, alpha);
                    leftY += noteH + layout.S(8f);
                }

                // 3. Well-Being Banner (if present and not empty roster)
                if (!emptyRoster &&
                    (viewModel.FoodRiskLevel is FoodRiskLevel.Low or FoodRiskLevel.Critical ||
                    viewModel.IdleWorkerCount > 0))
                {
                    leftY = DrawWellBeingBanner(ui, layout, left, leftY, colW, viewModel, alpha) + layout.S(8f);
                }
            }

            // 4. Stranded Citizens Banner (if empty roster and stranded > 0)
            int stranded = VillageSettlementHealth.CountStrandedCitizens(village, context.Villagers);
            if (citizens == 0 && stranded > 0)
            {
                float bannerH = layout.S(54f);
                var warningColor = new Color(0.92f, 0.72f, 0.28f);
                ui.DrawPanel(left, leftY, colW, bannerH,
                    UiTheme.AccentSoft, warningColor, 0.85f, alpha, UiTheme.RadiusMd);
                ui.DrawString($"{stranded} villager(s) nearby are not linked",
                    left + layout.S(10f), leftY + layout.S(10f), layout.S(UiTheme.FontBody), warningColor, alpha, semiBold: true);
                ui.DrawString("Close and reopen board to relink them.",
                    left + layout.S(10f), leftY + layout.S(30f), layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
                leftY += bannerH + layout.S(10f);
            }

            // 5. Empty Roster Banner
            bool showMissingSettlersBanner = citizens == 0 && stranded == 0;
            if (showMissingSettlersBanner)
            {
                float bannerH = layout.S(54f);
                ui.DrawPanel(left, leftY, colW, bannerH,
                    UiTheme.DangerSoft, UiTheme.Danger, 0.85f, alpha, UiTheme.RadiusMd);
                ui.DrawString("Village roster is empty", left + layout.S(10f), leftY + layout.S(10f), layout.S(UiTheme.FontBody),
                    UiTheme.Title, alpha, semiBold: true);
                ui.DrawString("Close and reopen board to repair starter villagers.",
                    left + layout.S(10f), leftY + layout.S(30f), layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
                leftY += bannerH + layout.S(10f);
            }
            else if (citizens > 0 && citizens <= 2)
            {
                // Quick start banner
                float bannerH = layout.S(40f);
                ui.DrawPanel(left, leftY, colW, bannerH,
                    UiTheme.AccentSoft, UiTheme.Accent, 0.75f, alpha, UiTheme.RadiusMd);
                ui.DrawString("Quick start: " + VillageGuidance.GetQuickStartSteps(village, context.Villagers, context.PlayerPosition),
                    left + layout.S(10f), leftY + layout.S(12f), layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
                leftY += bannerH + layout.S(10f);
            }

            // 6. Stat Cards (2x2 Grid)
            float cardW = (colW - layout.S(10f)) / 2f;
            float cardH = layout.S(62f);
            float cardGap = layout.S(10f);

            // Row 0
            DrawStatCard(ui, layout, left, leftY, cardW, cardH, "Population", $"{citizens}/{village.PopulationCap}",
                (float)citizens / Math.Max(1, village.PopulationCap), new Color(0.45f, 0.78f, 0.55f), alpha, accent);
            
            DrawStatCard(ui, layout, left + cardW + cardGap, leftY, cardW, cardH, "Food", $"{village.FoodStock:F0}",
                Math.Clamp(village.FoodStock / Math.Max(1f, Math.Max(1, citizens) * 2f), 0f, 1f),
                new Color(0.92f, 0.72f, 0.28f), alpha, accent);

            leftY += cardH + cardGap;

            // Row 1
            DrawStatCard(ui, layout, left, leftY, cardW, cardH, "Happiness", $"{village.Happiness:P0}", village.Happiness,
                new Color(0.55f, 0.82f, 0.95f), alpha, accent);

            DrawStatCard(ui, layout, left + cardW + cardGap, leftY, cardW, cardH, "Housing", $"{citizens}/{Math.Max(1, village.HousingCapacity)}",
                Math.Clamp((float)citizens / Math.Max(1, village.HousingCapacity), 0f, 1f),
                new Color(0.78f, 0.58f, 0.92f), alpha, accent);

            leftY += cardH + layout.S(14f);

            // 7. Storage Panel (fills remaining space)
            float storageH = Math.Max(layout.S(78f), bottomY - leftY);
            DrawStoragePanel(context, left, leftY, colW, storageH, alpha, accent);


            // ------------------ RIGHT COLUMN ------------------
            float rightY = yStart;

            // 1. Village Pulse (if not empty roster)
            if (viewModel != null && !emptyRoster)
            {
                rightY = DrawVillagePulse(ui, layout, rightColX, rightY, colW, viewModel, alpha) + layout.S(10f);
            }

            // 2. CTA Button (if not empty roster and CTA is present)
            if (viewModel != null && !emptyRoster && viewModel.NextActionKind != SettlementActionKind.None && viewModel.SuggestedTab.HasValue)
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
                ui.DrawButton(rightColX, rightY, ctaW, ctaH, ctaLabel, hovered, false, UiButtonStyle.Secondary,
                    layout.S(UiTheme.FontSmall), alpha, hovered ? 1f : 0f);
                rightY += ctaH + layout.S(10f);
            }

            // 3. Activity Panel (fills remaining space in right column)
            float activityH = Math.Max(layout.S(78f), bottomY - rightY);
            DrawActivityPanel(context, rightColX, rightY, colW, activityH, alpha, accent);
        }

        private static float DrawPriorityCard(
            VillagePanelContext context,
            float left,
            float y,
            float width,
            VillageViewModel viewModel,
            bool emptyRoster)
        {
            var ui = context.Ui;
            var layout = context.UiLayout;
            float w = width;
            float h = layout.S(72f);
            Color border = viewModel.IsBlocked ? UiTheme.Danger : UiTheme.Accent;
            Color fill = viewModel.IsBlocked ? UiTheme.DangerSoft : UiTheme.AccentSoft;

            ui.DrawPanel(left, y, w, h, fill, border, 0.9f, context.Alpha, UiTheme.RadiusMd);

            string eyebrow = emptyRoster ? "Village status" : "Next priority";
            ui.DrawString(eyebrow, left + layout.S(16f), y + layout.S(12f), layout.S(UiTheme.FontCaption),
                UiTheme.StatLabel, context.Alpha, semiBold: true);

            string title = string.IsNullOrWhiteSpace(viewModel.StarterStep)
                ? "Manage settlement"
                : viewModel.StarterStep;
            ui.DrawString(title, left + layout.S(16f), y + layout.S(30f), layout.S(UiTheme.FontBody),
                viewModel.IsBlocked ? UiTheme.Danger : UiTheme.Title, context.Alpha, semiBold: true);

            string detail = viewModel.IsBlocked
                ? string.IsNullOrWhiteSpace(viewModel.Remediation)
                    ? viewModel.BlockedReason
                    : $"{viewModel.BlockedReason} {viewModel.Remediation}"
                : viewModel.NextAction;
            ui.DrawString(TrimToWidth(ui, detail, layout.S(UiTheme.FontSmall), w - layout.S(32f)),
                left + layout.S(16f), y + layout.S(54f), layout.S(UiTheme.FontSmall), UiTheme.Subtitle, context.Alpha);

            return y + h;
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
            float width,
            VillageViewModel viewModel,
            float alpha)
        {
            float w = width;
            if (viewModel.FoodRiskLevel == FoodRiskLevel.Critical)
            {
                float bannerH = layout.S(36f);
                ui.DrawPanel(left, y, w, bannerH,
                    UiTheme.DangerSoft, UiTheme.Danger, 0.85f, alpha, UiTheme.RadiusMd);
                ui.DrawString("Food crisis — assign Farm jobs or queue a farm plot",
                    left + layout.S(14f), y + layout.S(10f), layout.S(UiTheme.FontSmall), UiTheme.Danger, alpha);
                return y + bannerH;
            }

            if (viewModel.FoodRiskLevel == FoodRiskLevel.Low)
            {
                var warningColor = new Color(0.92f, 0.72f, 0.28f);
                float bannerH = layout.S(36f);
                ui.DrawPanel(left, y, w, bannerH,
                    UiTheme.AccentSoft, warningColor, 0.75f, alpha, UiTheme.RadiusMd);
                ui.DrawString("Food running low — grow crops or take rations",
                    left + layout.S(14f), y + layout.S(10f), layout.S(UiTheme.FontSmall), warningColor, alpha);
                return y + bannerH;
            }

            if (viewModel.IdleWorkerCount >= 2)
            {
                float bannerH = layout.S(36f);
                ui.DrawPanel(left, y, w, bannerH,
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
            float width,
            VillageViewModel viewModel,
            float alpha)
        {
            var pulse = viewModel.Pulse;
            float w = width;
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
            float footerReserve = layout.S(44f);
            float rowBottom = y + h - footerReserve;
            int shown = 0;
            int hidden = 0;
            for (int i = 0; i < village.Storage.SlotCount && shown < 10; i++)
            {
                var stack = village.Storage.GetSlot(i);
                if (stack.IsEmpty)
                {
                    continue;
                }

                if (rowY > rowBottom)
                {
                    hidden++;
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
            else if (hidden > 0)
            {
                ui.DrawString($"+{hidden} more", x + layout.S(14f), Math.Min(rowY, rowBottom - layout.S(16f)),
                    layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
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

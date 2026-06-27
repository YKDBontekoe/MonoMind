using System;
using Microsoft.Xna.Framework;
using Autonocraft.Domain.Village;
using Autonocraft.Engine;
using Autonocraft.Village;
using Autonocraft.World;

namespace Autonocraft.UI.VillagePanels
{
    /// <summary>
    /// Goals tab: steward AI goals or manual resource priorities.
    /// </summary>
    public sealed class GoalsPanel : IVillagePanel
    {
        private const float PanelWidth = 900f;

        public static readonly BlockType[] GoalBlockTypes =
        {
            BlockType.OakPlank,
            BlockType.Cobblestone,
            BlockType.OakLog,
            BlockType.Wheat,
            BlockType.Carrot
        };

        public static readonly int[] GoalTargetCounts = { 10, 20, 32, 64 };

        public int TabIndex => 3;
        public string Label => "Goals";

        public bool IsVisible(VillagePanelContext context) => context.EarlyGuideStage >= 3;

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

            ui.DrawPanel(left, y, layout.S(PanelWidth) - layout.S(40f), height,
                UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);

            if (context.PlayWithAi)
            {
                UiTheme.DrawSectionHeader(ui, "Steward goals", left + layout.S(12f), y + layout.S(10f), layout, alpha);
                ui.DrawString($"Priority tasks set by the steward · Favor {village.Favor}", left + layout.S(12f), y + layout.S(32f),
                    layout.S(UiTheme.FontSmall), UiTheme.Hint, alpha);

                float contractY = y + layout.S(56f);
                int contractIndex = 0;
                foreach (var contract in VillageAgentContracts.Suggest(village, context.Villagers))
                {
                    if (contractIndex >= 3)
                    {
                        break;
                    }

                    float cardX = left + layout.S(16f) + contractIndex * layout.S(270f);
                    float cardW = layout.S(254f);
                    bool hovered = context.HoveredButton == 81 + contractIndex;
                    ui.DrawPanel(cardX, contractY, cardW, layout.S(64f),
                        hovered ? UiTheme.PanelBgHighlight : UiTheme.PanelBgMuted,
                        hovered ? UiTheme.Accent : (contract.CanAccept ? UiTheme.PanelBorder : UiTheme.Rule),
                        0.8f, alpha, UiTheme.RadiusMd);
                    ui.DrawString(contract.Label, cardX + layout.S(10f), contractY + layout.S(8f),
                        layout.S(UiTheme.FontSmall), contract.CanAccept ? UiTheme.Title : UiTheme.Meta, alpha, semiBold: true);
                    ui.DrawString(TrimToWidth(ui, contract.Description, layout.S(UiTheme.FontCaption), cardW - layout.S(20f)),
                        cardX + layout.S(10f), contractY + layout.S(26f), layout.S(UiTheme.FontCaption), UiTheme.Meta, alpha);
                    Color statusColor = contract.AlreadyActive
                        ? UiTheme.Success
                        : contract.CanAfford ? UiTheme.Accent : UiTheme.Danger;
                    ui.DrawString(contract.StatusText, cardX + layout.S(10f), contractY + layout.S(44f),
                        layout.S(UiTheme.FontCaption), statusColor, alpha, semiBold: true);
                    contractIndex++;
                }

                float rowY = y + layout.S(138f);
                int shown = 0;
                foreach (var goal in village.Scheduler.Goals)
                {
                    if (shown >= 8) break;

                    Color statusColor = goal.Completed ? UiTheme.Success : UiTheme.StatValue;
                    string status = goal.Completed ? "Done" : "Active";
                    ui.DrawString($"[{status}] {goal.Description}", left + layout.S(16f), rowY,
                        layout.S(UiTheme.FontSmall), statusColor, alpha);

                    if (!goal.Completed && goal.Kind == VillageGoalKind.Stock && goal.StockBlock.HasValue && goal.TargetCount > 0)
                    {
                        int have = village.Scheduler.GetStockProgress(goal, village);
                        float progress = Math.Clamp(have / (float)goal.TargetCount, 0f, 1f);
                        DrawMiniBar(ui, left + layout.S(16f), rowY + layout.S(16f), layout.S(500f), layout.S(6f), progress, UiTheme.Accent, alpha);
                        ui.DrawString($"{have}/{goal.TargetCount} {goal.StockBlock.Value}",
                            left + layout.S(530f), rowY + layout.S(12f), layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
                        rowY += layout.S(34f);
                    }
                    else rowY += layout.S(22f);

                    shown++;
                }

                if (shown == 0)
                {
                    ui.DrawString("No active goals — chat with the steward (C) to set priorities", left + layout.S(16f),
                        rowY + layout.S(10f), layout.S(UiTheme.FontSmall), UiTheme.Hint, alpha);
                }
            }
            else
            {
                UiTheme.DrawSectionHeader(ui, "Manual goals", left + layout.S(12f), y + layout.S(10f), layout, alpha);
                ui.DrawString("Define local resource gathering priorities for your villagers", left + layout.S(12f), y + layout.S(32f),
                    layout.S(UiTheme.FontSmall), UiTheme.Hint, alpha);

                float colLeft = left + layout.S(16f);
                ui.DrawString("Create new goal", colLeft, y + layout.S(56f), layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
                ui.DrawString("Resource:", colLeft, y + layout.S(82f), layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);

                for (int i = 0; i < GoalBlockTypes.Length; i++)
                {
                    float btnX = colLeft + i * layout.S(70f);
                    float btnY = y + layout.S(98f);
                    bool selected = i == context.SelectedGoalBlockIndex;
                    bool hovered = context.HoveredButton == 60 + i;
                    ui.DrawPanel(btnX, btnY, layout.S(64f), layout.S(26f),
                        selected ? UiTheme.PanelBgHighlight : UiTheme.PanelBgMuted,
                        hovered ? UiTheme.Accent : (selected ? UiTheme.Accent : accent), 0.8f, alpha, UiTheme.RadiusSm);
                    string label = GoalBlockTypes[i].ToString();
                    if (label.Length > 8) label = label[..8];
                    float labelW = ui.MeasureString(label, layout.S(UiTheme.FontSmall));
                    ui.DrawString(label, btnX + (layout.S(64f) - labelW) / 2f, btnY + layout.S(5f), layout.S(UiTheme.FontSmall),
                        selected ? UiTheme.Title : UiTheme.Meta, alpha);
                }

                ui.DrawString("Target amount:", colLeft, y + layout.S(136f), layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
                for (int j = 0; j < GoalTargetCounts.Length; j++)
                {
                    float btnX = colLeft + j * layout.S(70f);
                    float btnY = y + layout.S(152f);
                    bool selected = j == context.SelectedGoalCountIndex;
                    bool hovered = context.HoveredButton == 70 + j;
                    ui.DrawPanel(btnX, btnY, layout.S(64f), layout.S(26f),
                        selected ? UiTheme.PanelBgHighlight : UiTheme.PanelBgMuted,
                        hovered ? UiTheme.Accent : (selected ? UiTheme.Accent : accent), 0.8f, alpha, UiTheme.RadiusSm);
                    string label = GoalTargetCounts[j].ToString();
                    float labelW = ui.MeasureString(label, layout.S(UiTheme.FontSmall));
                    ui.DrawString(label, btnX + (layout.S(64f) - labelW) / 2f, btnY + layout.S(5f), layout.S(UiTheme.FontSmall),
                        selected ? UiTheme.Title : UiTheme.Meta, alpha);
                }

                VillagePanelChrome.DrawButton(ui, colLeft, y + layout.S(200f), layout.S(140f), layout.S(32f), "Add goal", context.HoveredButton == 80,
                    UiButtonStyle.Primary, layout, alpha);

                float rightLeft = left + layout.S(420f);
                float rightW = layout.S(PanelWidth) - layout.S(40f) - layout.S(420f) - layout.S(16f);
                ui.DrawString("Active goals", rightLeft, y + layout.S(56f), layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha, semiBold: true);

                float rowY = y + layout.S(82f);
                int shown = 0;
                foreach (var goal in village.Scheduler.Goals)
                {
                    if (shown >= 6) break;

                    Color statusColor = goal.Completed ? UiTheme.Success : UiTheme.StatValue;
                    string status = goal.Completed ? "Done" : "Active";
                    ui.DrawString($"[{status}] {goal.Description}", rightLeft, rowY, layout.S(UiTheme.FontSmall), statusColor, alpha);

                    float remW = layout.S(72f);
                    float remH = layout.S(26f);
                    VillagePanelChrome.DrawButton(ui, rightLeft + rightW - remW, rowY - layout.S(2f), remW, remH, "Remove", context.HoveredButton == 3000 + goal.Id,
                        UiButtonStyle.Danger, layout, alpha);

                    if (!goal.Completed && goal.Kind == VillageGoalKind.Stock && goal.StockBlock.HasValue && goal.TargetCount > 0)
                    {
                        int have = village.Scheduler.GetStockProgress(goal, village);
                        float progress = Math.Clamp(have / (float)goal.TargetCount, 0f, 1f);
                        DrawMiniBar(ui, rightLeft, rowY + layout.S(16f), rightW - remW - layout.S(12f), layout.S(6f), progress, UiTheme.Accent, alpha);
                        ui.DrawString($"{have}/{goal.TargetCount} {goal.StockBlock.Value}",
                            rightLeft + rightW - remW - layout.S(120f), rowY + layout.S(26f), layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
                        rowY += layout.S(40f);
                    }
                    else rowY += layout.S(26f);

                    shown++;
                }

                if (shown == 0)
                {
                    ui.DrawString("No active goals specified", rightLeft, rowY + layout.S(10f), layout.S(UiTheme.FontSmall), UiTheme.Hint, alpha);
                }
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
    }
}

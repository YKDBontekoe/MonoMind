using Microsoft.Xna.Framework;
using Autonocraft.Domain.Village;
using Autonocraft.Engine;
using Autonocraft.Entities;
using Autonocraft.Village;
using VillageEntity = Autonocraft.Village.Village;
using Vector3 = System.Numerics.Vector3;

namespace Autonocraft.UI.VillagePanels
{
    /// <summary>
    /// People tab: citizen list and job assignment detail.
    /// </summary>
    public sealed class PeoplePanel : IVillagePanel
    {
        private const float PanelWidth = 900f;
        private const float ButtonHeight = 34f;

        private static readonly (string Label, JobType Job)[] AssignableJobs =
        {
            ("Idle", JobType.Idle),
            ("Lumber", JobType.Lumber),
            ("Mine", JobType.Mine),
            ("Farm", JobType.Farm),
            ("Build", JobType.Build),
            ("Haul", JobType.Haul),
        };

        public int TabIndex => 2;
        public string Label => "People";

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

            float listW = layout.S(300f);
            float detailX = left + listW + layout.S(14f);
            float detailW = layout.S(PanelWidth) - layout.S(40f) - listW - layout.S(14f);

            ui.DrawPanel(left, y, listW, height, UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);
            int citizenCount = CountDisplayedCitizens(village, context.Villagers);
            UiTheme.DrawSectionHeader(ui, $"Citizens ({citizenCount})", left + layout.S(12f), y + layout.S(10f), layout, alpha);

            float rowY = y + layout.S(32f) - context.PeopleScroll;
            float rowH = layout.S(42f);
            foreach (var villager in EnumerateCitizens(village, context.Villagers))
            {
                int villagerId = villager.Id;
                bool isSelected = villagerId == context.SelectedVillagerId;
                bool hovered = context.HoveredButton == 30 + villagerId;
                bool needsAttention = VillagerActivityText.NeedsAttention(villager, village);
                if (rowY + rowH >= y + layout.S(28f) && rowY <= y + height - layout.S(6f))
                {
                    if (isSelected || hovered)
                    {
                        ui.DrawFilledRect(left + layout.S(8f), rowY, listW - layout.S(16f), rowH,
                            (isSelected ? UiTheme.PanelBgHighlight : new Color(0.08f, 0.12f, 0.15f)) * alpha);
                    }

                    var roleColor = VillagerVisuals.GetRoleColor(villager.Role);
                    ui.DrawRoundedRect(left + layout.S(14f), rowY + layout.S(12f), layout.S(10f), layout.S(10f), layout.S(3f), roleColor * alpha);
                    if (needsAttention)
                    {
                        ui.DrawRoundedRect(left + layout.S(8f), rowY + layout.S(8f), layout.S(4f), rowH - layout.S(16f),
                            layout.S(2f), UiTheme.Danger * alpha);
                    }

                    ui.DrawString(villager.Name, left + layout.S(30f), rowY + layout.S(6f),
                        layout.S(UiTheme.FontBody), isSelected ? UiTheme.Title : UiTheme.StatValue, alpha, semiBold: isSelected);
                    string activity = VillagerActivityText.Describe(villager, village, null);
                    string progress = VillagerActivityText.DescribeProgress(villager, village);
                    string statusText = string.IsNullOrEmpty(progress) ? activity : $"{activity} · {progress}";
                    Color textCol = needsAttention ? UiTheme.Danger : roleColor;
                    ui.DrawString(statusText, left + layout.S(30f),
                        rowY + layout.S(24f), layout.S(UiTheme.FontSmall), textCol, alpha);
                }

                rowY += rowH + layout.S(4f);
            }

            if (!HasDisplayedCitizens(village, context.Villagers))
            {
                int stranded = VillageSettlementHealth.CountStrandedCitizens(village, context.Villagers);
                string emptyHint = stranded > 0
                    ? $"{stranded} settler(s) nearby — go to Overview and click Link nearby settlers"
                    : VillageSettlementHealth.IsPlayerManagingSettlement(village, context.PlayerPosition)
                        ? "No villagers linked — click Summon settlers on Overview"
                        : "No villagers linked — stand at the Town Heart, then summon settlers";
                ui.DrawString(emptyHint, left + layout.S(14f), y + layout.S(40f), layout.S(UiTheme.FontSmall),
                    UiTheme.Hint, alpha);
            }

            ui.DrawPanel(detailX, y, detailW, height, UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);
            if (context.SelectedVillagerId >= 0 && context.Villagers.TryGet(context.SelectedVillagerId, out var selected))
            {
                DrawVillagerDetail(context, detailX, y, detailW, height, selected, alpha, accent);
            }
            else
            {
                ui.DrawString("Select a citizen", detailX + layout.S(16f), y + layout.S(40f), layout.S(UiTheme.FontBody),
                    UiTheme.Hint, alpha);
            }
        }

        private static void DrawVillagerDetail(
            VillagePanelContext context,
            float x,
            float y,
            float w,
            float h,
            Villager villager,
            float alpha,
            Color accent)
        {
            var ui = context.Ui;
            var layout = context.UiLayout;
            var village = context.Village;
            float pad = layout.S(16f);
            float detailY = y + pad;
            var roleColor = VillagerVisuals.GetRoleColor(villager.Role);

            ui.DrawString(villager.Name, x + pad, detailY, layout.S(UiTheme.FontTitle),
                UiTheme.Title, alpha, semiBold: true);
            detailY += layout.S(28f);
            ui.DrawString($"{villager.Role} · {villager.CurrentJob}", x + pad, detailY,
                layout.S(UiTheme.FontBody), roleColor, alpha);
            detailY += layout.S(22f);

            string activity = VillagerActivityText.Describe(villager, village, null);
            string progress = VillagerActivityText.DescribeProgress(villager, village);
            ui.DrawString(activity, x + pad, detailY, layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
            detailY += layout.S(18f);
            if (!string.IsNullOrEmpty(progress))
            {
                ui.DrawString(progress, x + pad, detailY, layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
                detailY += layout.S(18f);
            }

            detailY += layout.S(4f);
            ui.DrawString($"Trait: {villager.Persona.Trait}", x + pad, detailY, layout.S(UiTheme.FontSmall),
                UiTheme.Accent, alpha);
            detailY += layout.S(22f);

            ui.DrawString(
                $"Skills — Mining {villager.Skills.Mining.Level} · Wood {villager.Skills.Woodcutting.Level} · Farm {villager.Skills.Farming.Level}",
                x + pad, detailY, layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
            detailY += layout.S(28f);
            ui.DrawProgressBar(x + pad, detailY, w - pad * 2f, layout.S(12f), villager.Happiness, "Morale", 1f, alpha);
            detailY += layout.S(52f);

            bool talkEnabled = context.PlayWithAi;
            string talkHint = talkEnabled
                ? $"Talk to {villager.Name}"
                : "Enable Play with AI in settings to chat";
            DrawStyledButton(ui, x + pad, detailY, layout.S(96f), layout.S(ButtonHeight), "Talk", context.HoveredButton == 50,
                UiButtonStyle.Secondary, layout, alpha, !talkEnabled);
            ui.DrawString(talkHint, x + pad + layout.S(106f), detailY + layout.S(8f), layout.S(UiTheme.FontSmall),
                UiTheme.Hint, alpha);
            detailY += layout.S(48f);

            if (!string.IsNullOrEmpty(context.AssignFeedback))
            {
                Color feedbackColor = context.AssignSuccess ? UiTheme.Success : UiTheme.Danger;
                ui.DrawString(context.AssignFeedback, x + pad, detailY, layout.S(UiTheme.FontSmall), feedbackColor, alpha);
                detailY += layout.S(22f);
            }

            ui.DrawString("Assign job", x + pad, detailY, layout.S(UiTheme.FontSection), UiTheme.Section, alpha, semiBold: true);
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
                DrawStyledButton(ui, jobX, jobY, jobW, jobH, AssignableJobs[i].Label, context.HoveredButton == 40 + i,
                    UiButtonStyle.Ghost, layout, alpha);
            }
        }

        private static void DrawStyledButton(
            UiRenderer ui,
            float x,
            float y,
            float w,
            float h,
            string label,
            bool hovered,
            UiButtonStyle style,
            UiLayout layout,
            float alpha,
            bool disabled = false)
        {
            ui.DrawButton(x, y, w, h, label, hovered && !disabled, false, style, layout.S(UiTheme.FontBody), alpha, hovered ? 1f : 0f, disabled);
        }

        private static int CountDisplayedCitizens(VillageEntity village, VillagerManager villagers)
        {
            return VillageSettlementHealth.CountLiveCitizens(village, villagers);
        }

        private static bool HasDisplayedCitizens(VillageEntity village, VillagerManager villagers)
        {
            foreach (var _ in EnumerateCitizens(village, villagers))
            {
                return true;
            }

            return false;
        }

        private static System.Collections.Generic.IEnumerable<Villager> EnumerateCitizens(VillageEntity village, VillagerManager villagers)
        {
            foreach (var villager in villagers.All)
            {
                if (villager.VillageId == village.Id)
                {
                    yield return villager;
                }
            }
        }
    }
}

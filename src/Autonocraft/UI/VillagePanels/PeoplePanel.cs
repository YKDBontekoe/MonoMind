using Microsoft.Xna.Framework;
using Autonocraft.Domain.Village;
using Autonocraft.Engine;
using Autonocraft.Entities;
using Autonocraft.Village;
using VillageEntity = Autonocraft.Village.Village;

namespace Autonocraft.UI.VillagePanels
{
    /// <summary>
    /// People tab: citizen list on the left, villager detail + job assignment on the right.
    /// </summary>
    public sealed class PeoplePanel : IVillagePanel
    {
        internal const float ListWidth = 300f;
        internal const float ButtonHeight = 34f;

        internal static readonly (string Label, JobType Job)[] AssignableJobs =
        {
            ("Idle",   JobType.Idle),
            ("Lumber", JobType.Lumber),
            ("Mine",   JobType.Mine),
            ("Farm",   JobType.Farm),
            ("Build",  JobType.Build),
            ("Haul",   JobType.Haul),
        };

        public int TabIndex => 2;
        public string Label => "People";
        public bool IsVisible(VillagePanelContext context) => true;

        // ---------------------------------------------------------------
        // Shared layout helper
        //
        // Returns the ABSOLUTE screen-Y coordinates of the Talk button and
        // the job-button grid so both Draw and HitPeopleTab use exactly the
        // same positions — no more dual-maintenance drift.
        // ---------------------------------------------------------------

        /// <summary>
        /// Computes the Y-positions of the interactive buttons in the detail
        /// pane for <paramref name="villager"/>.  All coordinates are absolute
        /// screen pixels (not relative to any sub-rect).
        /// </summary>
        internal static void GetDetailButtonYs(
            UiLayout layout,
            float panelY,
            Villager villager,
            VillageEntity village,
            bool hasFeedback,
            out float talkButtonY,
            out float jobGridY)
        {
            float dy = panelY + layout.S(VillageScreen.ContentTop) + layout.S(16f); // y + pad

            dy += layout.S(28f);   // name (title)
            dy += layout.S(22f);   // role · job
            dy += layout.S(18f);   // activity

            if (!string.IsNullOrEmpty(VillagerActivityText.DescribeProgress(villager, village)))
                dy += layout.S(18f);   // progress

            dy += layout.S(4f);    // gap before trait
            dy += layout.S(22f);   // trait
            dy += layout.S(28f);   // skills
            dy += layout.S(52f);   // morale bar (bar 12 + label + gaps)

            talkButtonY = dy;
            dy += layout.S(48f);   // talk button (34) + gap (14)

            if (hasFeedback)
                dy += layout.S(22f);   // feedback text

            dy += layout.S(24f);   // "Assign job" section header

            jobGridY = dy;
        }

        // ---------------------------------------------------------------
        // Draw
        // ---------------------------------------------------------------

        public void Draw(VillagePanelContext context)
        {
            var ui = context.Ui;
            var layout = context.UiLayout;
            float left = context.ContentLeft;
            float y = context.PanelY + layout.S(VillageScreen.ContentTop);
            float h = context.ContentHeight;
            float alpha = context.Alpha;
            Color accent = context.Accent;
            var village = context.Village;

            float listW = layout.S(ListWidth);
            float detailX = left + listW + layout.S(14f);
            float detailW = layout.S(VillageScreen.PanelWidth) - layout.S(40f) - listW - layout.S(14f);

            // ---- Left column: citizen list ----
            ui.DrawPanel(left, y, listW, h, UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);

            int citizenCount = VillageSettlementHealth.CountLiveCitizens(village, context.Villagers);
            UiTheme.DrawSectionHeader(ui, $"Citizens ({citizenCount})", left + layout.S(12f), y + layout.S(10f), layout, alpha);

            float rowY = y + layout.S(32f) - context.PeopleScroll;
            float rowH = layout.S(42f);

            bool anyRow = false;
            foreach (var villager in EnumerateCitizens(village, context.Villagers))
            {
                anyRow = true;
                int vid = villager.Id;
                bool isSelected = vid == context.SelectedVillagerId;
                bool hovered = context.HoveredButton == VillageScreen.VillagerRowButtonBase + vid;
                bool needsAttn = VillagerActivityText.NeedsAttention(villager, village);

                if (rowY + rowH >= y + layout.S(28f) && rowY <= y + h - layout.S(6f))
                {
                    // Row background
                    if (isSelected || hovered)
                    {
                        Color hlColor = isSelected
                            ? UiTheme.PanelBgHighlight
                            : new Color(0.08f, 0.12f, 0.15f);
                        ui.DrawFilledRect(left + layout.S(8f), rowY, listW - layout.S(16f), rowH, hlColor * alpha);
                    }

                    // Attention stripe
                    if (needsAttn)
                    {
                        ui.DrawRoundedRect(left + layout.S(8f), rowY + layout.S(8f),
                            layout.S(4f), rowH - layout.S(16f), layout.S(2f), UiTheme.Danger * alpha);
                    }

                    // Role colour dot
                    var roleColor = VillagerVisuals.GetRoleColor(villager.Role);
                    ui.DrawRoundedRect(left + layout.S(14f), rowY + layout.S(13f),
                        layout.S(10f), layout.S(10f), layout.S(3f), roleColor * alpha);

                    // Name
                    ui.DrawString(villager.Name, left + layout.S(30f), rowY + layout.S(6f),
                        layout.S(UiTheme.FontBody),
                        isSelected ? UiTheme.Title : UiTheme.StatValue, alpha, semiBold: isSelected);

                    // Status line
                    string activity = VillagerActivityText.Describe(villager, village, null);
                    string progress = VillagerActivityText.DescribeProgress(villager, village);
                    string statusLine = string.IsNullOrEmpty(progress) ? activity : $"{activity} · {progress}";
                    Color statusCol = needsAttn ? UiTheme.Danger : roleColor;
                    ui.DrawString(
                        TrimToWidth(ui, statusLine, layout.S(UiTheme.FontSmall), listW - layout.S(44f)),
                        left + layout.S(30f), rowY + layout.S(24f),
                        layout.S(UiTheme.FontSmall), statusCol, alpha);
                }

                rowY += rowH + layout.S(4f);
            }

            if (!anyRow)
            {
                int stranded = VillageSettlementHealth.CountStrandedCitizens(village, context.Villagers);
                string hint = stranded > 0
                    ? $"{stranded} villager(s) nearby — close and reopen the board to relink them"
                    : "No villagers linked — close and reopen the Town Board to repair this village";
                ui.DrawString(hint, left + layout.S(14f), y + layout.S(40f),
                    layout.S(UiTheme.FontSmall), UiTheme.Hint, alpha);
            }

            // ---- Right column: detail pane ----
            ui.DrawPanel(detailX, y, detailW, h, UiTheme.PanelBgMuted, accent, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);

            if (context.SelectedVillagerId >= 0 &&
                context.Villagers.TryGet(context.SelectedVillagerId, out var selected))
            {
                DrawVillagerDetail(context, detailX, y, detailW, selected, alpha);
            }
            else
            {
                ui.DrawString("← Select a citizen to manage them",
                    detailX + layout.S(16f), y + layout.S(40f),
                    layout.S(UiTheme.FontBody), UiTheme.Hint, alpha);
            }
        }

        // ---------------------------------------------------------------
        // Villager detail
        // ---------------------------------------------------------------

        private static void DrawVillagerDetail(
            VillagePanelContext context,
            float x, float y, float w,
            Villager villager,
            float alpha)
        {
            var ui = context.Ui;
            var layout = context.UiLayout;
            var village = context.Village;
            float pad = layout.S(16f);

            bool hasFeedback = !string.IsNullOrEmpty(context.AssignFeedback);

            // Get the canonical button Y positions from the shared calculator
            GetDetailButtonYs(
                layout, context.PanelY, villager, village, hasFeedback,
                out float talkButtonY, out float jobGridY);

            var roleColor = VillagerVisuals.GetRoleColor(villager.Role);

            // ---- Walk detailY from the top, same increments as GetDetailButtonYs ----
            float detailY = y + pad;

            // Name with role-colour dot
            ui.DrawRoundedRect(x + pad, detailY + layout.S(6f),
                layout.S(10f), layout.S(10f), layout.S(3f), roleColor * alpha);
            ui.DrawString(villager.Name, x + pad + layout.S(16f), detailY,
                layout.S(UiTheme.FontTitle), UiTheme.Title, alpha, semiBold: true);
            detailY += layout.S(28f);

            // Role · Current job
            ui.DrawString($"{villager.Role} · {villager.CurrentJob}", x + pad, detailY,
                layout.S(UiTheme.FontBody), roleColor, alpha);
            detailY += layout.S(22f);

            // Activity
            string activity = VillagerActivityText.Describe(villager, village, null);
            string progress = VillagerActivityText.DescribeProgress(villager, village);
            ui.DrawString(activity, x + pad, detailY,
                layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
            detailY += layout.S(18f);

            // Progress (conditional)
            if (!string.IsNullOrEmpty(progress))
            {
                ui.DrawString(progress, x + pad, detailY,
                    layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
                detailY += layout.S(18f);
            }

            // Trait
            detailY += layout.S(4f);
            ui.DrawString($"Trait: {villager.Persona.Trait}", x + pad, detailY,
                layout.S(UiTheme.FontSmall), UiTheme.Accent, alpha);
            detailY += layout.S(22f);

            // Skills
            ui.DrawString(
                $"Skills — Mining {villager.Skills.Mining.Level} · " +
                $"Wood {villager.Skills.Woodcutting.Level} · " +
                $"Farm {villager.Skills.Farming.Level}",
                x + pad, detailY, layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
            detailY += layout.S(28f);

            // Morale bar
            ui.DrawProgressBar(x + pad, detailY, w - pad * 2f,
                layout.S(12f), villager.Happiness, "Morale", 1f, alpha);
            detailY += layout.S(52f);

            // detailY should now equal talkButtonY (verified by shared calculator)
            // ---- Talk button ----
            bool talkEnabled = context.PlayWithAi;
            string talkHint = talkEnabled
                ? $"Talk to {villager.Name}"
                : "Enable Play with AI in settings to chat";
            VillagePanelChrome.DrawButton(ui, x + pad, detailY, layout.S(96f), layout.S(ButtonHeight),
                "Talk", context.HoveredButton == 50, UiButtonStyle.Secondary, layout, alpha, !talkEnabled);
            ui.DrawString(
                TrimToWidth(ui, talkHint, layout.S(UiTheme.FontSmall), w - pad * 2f - layout.S(112f)),
                x + pad + layout.S(106f), detailY + layout.S(8f),
                layout.S(UiTheme.FontSmall), UiTheme.Hint, alpha);
            detailY += layout.S(48f);

            // Feedback line (conditional)
            if (hasFeedback)
            {
                Color fc = context.AssignSuccess ? UiTheme.Success : UiTheme.Danger;
                ui.DrawString(context.AssignFeedback!, x + pad, detailY,
                    layout.S(UiTheme.FontSmall), fc, alpha);
                detailY += layout.S(22f);
            }

            // ---- "Assign job" header ----
            ui.DrawString("Assign job", x + pad, detailY,
                layout.S(UiTheme.FontSection), UiTheme.Section, alpha, semiBold: true);
            detailY += layout.S(24f);

            // ---- Job buttons (3 rows × 2 cols) ----
            float jobW = layout.S(240f);
            float jobH = layout.S(ButtonHeight);
            float jobGap = layout.S(10f);

            for (int i = 0; i < AssignableJobs.Length; i++)
            {
                int row = i / 2;
                int col = i % 2;
                float bx = x + pad + col * (jobW + jobGap);
                float by = detailY + row * (jobH + jobGap);

                // Highlight the villager's current job assignment
                bool isCurrentJob = villager.CurrentJob == AssignableJobs[i].Job;
                bool isHovered = context.HoveredButton == 40 + i;
                UiButtonStyle style = isCurrentJob ? UiButtonStyle.Primary : UiButtonStyle.Ghost;

                VillagePanelChrome.DrawButton(ui, bx, by, jobW, jobH,
                    AssignableJobs[i].Label, isHovered, style, layout, alpha);
            }
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private static string TrimToWidth(UiRenderer ui, string text, float fontSize, float maxWidth)
        {
            if (ui.MeasureString(text, fontSize) <= maxWidth)
                return text;
            const string ellipsis = "...";
            for (int len = text.Length - 1; len > 0; len--)
            {
                string c = text[..len].TrimEnd() + ellipsis;
                if (ui.MeasureString(c, fontSize) <= maxWidth)
                    return c;
            }
            return ellipsis;
        }

        private static System.Collections.Generic.IEnumerable<Villager> EnumerateCitizens(
            VillageEntity village, VillagerManager villagers)
        {
            foreach (var v in villagers.All)
                if (v.VillageId == village.Id)
                    yield return v;
        }
    }
}

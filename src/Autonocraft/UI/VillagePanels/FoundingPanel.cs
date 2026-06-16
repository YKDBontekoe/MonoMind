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
    /// Founding mode: shown when no settlement exists yet.
    /// </summary>
    public sealed class FoundingPanel
    {
        public void Draw(FoundingPanelContext context)
        {
            var ui = context.Ui;
            var layout = context.UiLayout;
            float panelX = context.PanelX;
            float panelY = context.PanelY;
            float panelW = context.PanelWidth;
            float panelH = context.PanelHeight;
            float left = context.ContentLeft;
            float buttonW = layout.S(VillageScreen.ButtonWidth);
            float buttonH = layout.S(VillageScreen.ButtonHeight);
            float alpha = context.Alpha;

            ui.DrawFullscreenBackground(UiTheme.OverlayScrim * (0.55f * alpha));
            ui.DrawCard(panelX, panelY, panelW, panelH, alpha, UiTheme.RadiusXl);

            ui.DrawCenteredTitle("Start a settlement", panelY + layout.S(20f), layout.S(UiTheme.FontTitle),
                UiTheme.Title, alpha);
            ui.DrawCenteredText("No village yet", panelY + layout.S(48f), layout.S(UiTheme.FontBody),
                UiTheme.Subtitle, alpha * 0.92f);

            float contentY = panelY + layout.S(VillageScreen.ContentTop);
            float contentH = panelH - layout.S(VillageScreen.ContentTop) - layout.S(VillageScreen.FooterHeight);
            ui.DrawPanel(left, contentY, panelW - layout.S(40f), contentH,
                UiTheme.PanelBgMuted, UiTheme.PanelBorder, 0.75f, alpha * 0.95f, UiTheme.RadiusMd);

            float textY = contentY + layout.S(28f);
            ui.DrawString("This save has no settlement yet.",
                left + layout.S(18f), textY, layout.S(UiTheme.FontBody), UiTheme.StatValue, alpha);
            textY += layout.S(28f);
            ui.DrawString("Place a Town Heart — one settler joins automatically and builds it.",
                left + layout.S(18f), textY, layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
            textY += layout.S(28f);
            ui.DrawString("Recruit is only for extra workers after you already have at least one villager.",
                left + layout.S(18f), textY, layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);

            if (context.CanClaimNearby)
            {
                textY += layout.S(28f);
                ui.DrawString("Abandoned outpost nearby — claim it for a free settler.",
                    left + layout.S(18f), textY, layout.S(UiTheme.FontSmall), UiTheme.Subtitle, alpha);
            }
            else
            {
                textY += layout.S(28f);
                ui.DrawString("Wild outposts (cottages, forest camps) are rare — roughly one every few hundred blocks.",
                    left + layout.S(18f), textY, layout.S(UiTheme.FontSmall), UiTheme.Meta, alpha);
            }

            if (PlayerStructureRegistry.TryGet("town_heart", out var blueprint))
            {
                textY += layout.S(36f);
                string costLine = context.PlayerCreative
                    ? "Town Heart cost: free in creative"
                    : $"Town Heart cost: {FormatCosts(blueprint)}";
                bool canAfford = context.PlayerCreative || (context.PlayerPayer != null && blueprint.CanAfford(context.PlayerPayer));
                ui.DrawString(costLine, left + layout.S(18f), textY, layout.S(UiTheme.FontSmall),
                    canAfford ? UiTheme.Subtitle : UiTheme.Danger, alpha);
            }

            float footerY = panelY + panelH - layout.S(VillageScreen.FooterHeight);
            bool canPlace = CanAffordTownHeart(context);
            DrawStyledButton(ui, left, footerY, buttonW, buttonH, "Place Town Heart", context.HoveredButton == 14,
                UiButtonStyle.Primary, layout, alpha, !canPlace);

            if (context.CanClaimNearby)
            {
                DrawStyledButton(ui, left + buttonW + layout.S(10f), footerY, buttonW, buttonH, "Claim outpost",
                    context.HoveredButton == 12, UiButtonStyle.Secondary, layout, alpha);
            }

            float closeX = panelX + panelW - layout.S(20f) - buttonW;
            float closeY = panelY + panelH - layout.S(30f);
            DrawStyledButton(ui, closeX, closeY, buttonW, buttonH, "Close", context.HoveredButton == 11, UiButtonStyle.Ghost, layout, alpha);

            ui.DrawCenteredText("Esc close · Enter confirm", panelY + panelH - layout.S(12f), layout.S(UiTheme.FontSmall),
                UiTheme.Hint, 0.9f * alpha);
        }

        public static bool CanAffordTownHeart(IItemContainer? playerPayer, bool playerCreative)
        {
            if (playerCreative)
            {
                return true;
            }

            return PlayerStructureRegistry.TryGet("town_heart", out var blueprint)
                && playerPayer != null
                && blueprint.CanAfford(playerPayer);
        }

        private static bool CanAffordTownHeart(FoundingPanelContext context) =>
            CanAffordTownHeart(context.PlayerPayer, context.PlayerCreative);

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

using System;
using Autonocraft.Core;
using Autonocraft.Items;
using Autonocraft.Village;

namespace Autonocraft.Tests.Integration;

public static class EarlyGameTests
{
    public static void RunOpeningGoalDisplayDismissAndProgression()
    {
        Console.Write("Running Opening Goal Display, Dismiss, and Progression Test... ");

        var session = EarlyGameTestHelpers.CreateStarterSession();
        var objective = session.GetOpeningObjective();
        var village = EarlyGameTestHelpers.RequireStarterVillage(session);
        string expectedHeadline = SettlementGuidance.Compute(village, session.Villagers, session.Player.Position).Headline;
        if (!objective.Active || objective.Headline != expectedHeadline || !objective.Dismissible)
        {
            throw new Exception($"Expected active opening objective, got '{objective.Headline}' active={objective.Active}.");
        }

        session.UpdateEarlyGuide(0.1f, 0.15f, villageScreenOpen: false);
        if (!session.CurrentToastMessage.Contains("Town Board", StringComparison.Ordinal) &&
            !session.CurrentToastMessage.Contains("settlers", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exception($"Expected settlement-first toast, got '{session.CurrentToastMessage}'.");
        }

        session.DismissOpeningGuidance();
        if (!string.IsNullOrEmpty(session.CurrentToastMessage))
        {
            throw new Exception("Dismissed opening guidance should clear the current toast.");
        }

        if (session.GetOpeningObjective().Active)
        {
            throw new Exception("Dismissed opening objective should not remain active.");
        }

        session.Player.Hotbar[0] = ItemStack.CreateBlock(BlockType.OakLog, 1);
        session.UpdateEarlyGuide(0.1f, 0.15f, villageScreenOpen: false);

        if (session.Player.Stats.EarlyGuideStage != 1)
        {
            throw new Exception($"Expected early guide stage 1 after gathering wood, got {session.Player.Stats.EarlyGuideStage}.");
        }

        if (!session.CurrentToastMessage.Contains("First logs gathered", StringComparison.Ordinal))
        {
            throw new Exception($"Expected completion toast, got '{session.CurrentToastMessage}'.");
        }

        Console.WriteLine("PASSED");
    }
}

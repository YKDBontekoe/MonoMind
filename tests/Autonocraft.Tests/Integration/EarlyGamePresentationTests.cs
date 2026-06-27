using System;
using Autonocraft.Core;
using Autonocraft.Village;

namespace Autonocraft.Tests.Integration;

public static class EarlyGamePresentationTests
{
    public static void RunReturningPlayerSuppressesOpeningPrompt()
    {
        Console.Write("Running Returning Player Suppresses Opening Prompt Test... ");

        var session = EarlyGameTestHelpers.CreateStarterSession();
        session.Player.Stats.EarlyGuideStage = 5;
        session.HudToast.Clear();

        var objective = session.GetOpeningObjective();
        if (objective.Active)
        {
            throw new Exception("Completed opening guidance should not show an active objective.");
        }

        session.UpdateEarlyGuide(1f, 0.15f, villageScreenOpen: false);
        if (!string.IsNullOrEmpty(session.CurrentToastMessage))
        {
            throw new Exception($"Returning player should not receive opening toast, got '{session.CurrentToastMessage}'.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunOpeningCopyStaysCompact()
    {
        Console.Write("Running Opening Copy Stays Compact Test... ");

        if (EarlyGameGuide.OpeningGoalHeadline.Length > 32)
        {
            throw new Exception("Opening headline is too long for compact HUD presentation.");
        }

        if (EarlyGameGuide.OpeningGoalToast.Length > 96)
        {
            throw new Exception("Opening toast is too long for compact HUD presentation.");
        }

        if (EarlyGameGuide.StarterSettlementToast.Length > 110)
        {
            throw new Exception("Starter settlement toast is too long for compact HUD presentation.");
        }

        if (!EarlyGameGuide.StarterSettlementToast.Contains("Town Board", StringComparison.Ordinal))
        {
            throw new Exception("Starter settlement toast should point players at the Town Board.");
        }

        Console.WriteLine("PASSED");
    }
}

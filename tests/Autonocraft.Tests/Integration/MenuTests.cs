using System;
using System.IO;
using Autonocraft.Core;
using Autonocraft.Domain.Persistence;
using Autonocraft.UI;
using Autonocraft.UI.Menu;
using Autonocraft.World;

namespace Autonocraft.Tests.Integration;

public static class MenuTests
{
    public static void RunMenuInitialLayerIsRootHub()
    {
        Console.Write("Running Menu Initial Layer Is Root Hub Test... ");

        var nav = new MenuNavigationState();
        if (nav.Layer != MenuLayer.RootHub)
        {
            throw new Exception($"Expected initial menu layer RootHub, got {nav.Layer}.");
        }

        if (nav.IsOverlayActive)
        {
            throw new Exception("Root hub launch must not start with an overlay active.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunMainMenuRootHubLayoutBounds()
    {
        Console.Write("Running Main Menu Root Hub Layout Bounds Test... ");

        AssertLayoutWithinViewport(1280, 720, hasContinueSave: true);
        AssertLayoutWithinViewport(800, 600, hasContinueSave: false);

        Console.WriteLine("PASSED");
    }

    public static void RunSaveBrowserBackReturnsToRootHub()
    {
        Console.Write("Running Save Browser Back Returns To Root Hub Test... ");

        var nav = new MenuNavigationState();
        nav.NavigateTo(MenuLayer.SaveBrowser);
        if (nav.Layer != MenuLayer.SaveBrowser)
        {
            throw new Exception("Failed to navigate to save browser.");
        }

        nav.NavigateTo(MenuLayer.RootHub);
        if (nav.Layer != MenuLayer.RootHub)
        {
            throw new Exception("Back navigation must return to root hub.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunDeleteRequiresTwoStepConfirmation()
    {
        Console.Write("Running Delete Requires Two Step Confirmation Test... ");

        bool confirming = false;
        bool deleted = false;

        void OnDeletePressed()
        {
            if (confirming)
            {
                deleted = true;
                confirming = false;
            }
            else
            {
                confirming = true;
            }
        }

        OnDeletePressed();
        if (deleted)
        {
            throw new Exception("First delete press must arm confirmation, not delete.");
        }

        if (!confirming)
        {
            throw new Exception("First delete press must enter confirming state.");
        }

        OnDeletePressed();
        if (!deleted)
        {
            throw new Exception("Second delete press must delete the save.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunSettingsCancelDoesNotPersist()
    {
        Console.Write("Running Settings Cancel Does Not Persist Test... ");

        using var host = new TestHost();
        var baseline = new GameSettings { RenderDistance = 6, MasterVolume = 0.5f };
        GameSettingsManager.Save(baseline);

        var working = GameSettingsManager.Load();
        working.RenderDistance = 11;
        working.MasterVolume = 0.1f;
        if (working.RenderDistance != 11 || Math.Abs(working.MasterVolume - 0.1f) > 0.001f)
        {
            throw new Exception("In-memory settings edits were not applied to working copy.");
        }

        var reloaded = GameSettingsManager.Load();
        if (reloaded.RenderDistance != 6 || Math.Abs(reloaded.MasterVolume - 0.5f) > 0.001f)
        {
            throw new Exception("Cancel path must not persist unsaved settings edits.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunSettingsOverlayBlocksBaseInput()
    {
        Console.Write("Running Settings Overlay Blocks Base Input Test... ");

        var nav = new MenuNavigationState();
        nav.NavigateTo(MenuLayer.SaveBrowser);
        nav.OpenOverlay(MenuLayer.SettingsOverlay);

        if (!nav.IsOverlayActive)
        {
            throw new Exception("Settings overlay must be active.");
        }

        if (nav.BaseLayer != MenuLayer.SaveBrowser)
        {
            throw new Exception("Settings overlay must preserve save browser as base layer.");
        }

        nav.CloseOverlay();
        if (nav.Layer != MenuLayer.SaveBrowser)
        {
            throw new Exception("Closing settings must restore save browser layer.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunContinueUsesMostRecentSave()
    {
        Console.Write("Running Continue Uses Most Recent Save Test... ");

        using var host = new TestHost();
        string olderSlot = WorldSaveManager.CreateSlotId("older-world");
        string newerSlot = WorldSaveManager.CreateSlotId("newer-world");

        WorldSaveManager.Save(BuildMinimalSave(olderSlot, "Older World", DateTime.UtcNow.AddHours(-2)));
        WorldSaveManager.Save(BuildMinimalSave(newerSlot, "Newer World", DateTime.UtcNow.AddHours(-1)));

        var recent = WorldSaveManager.GetMostRecentSaveSlot();
        if (recent == null || recent.SlotId != newerSlot)
        {
            throw new Exception("Continue must target the most recently saved slot.");
        }

        Console.WriteLine("PASSED");
    }

    private static void AssertLayoutWithinViewport(int width, int height, bool hasContinueSave)
    {
        var metrics = MainMenuScreen.ComputeLayoutMetrics(width, height, hasContinueSave);
        foreach (var rect in metrics.ButtonRects)
        {
            if (rect.Left < 0 || rect.Top < 0 || rect.Right > width || rect.Bottom > height)
            {
                throw new Exception($"Button rect {rect} clips viewport {width}x{height}.");
            }
        }
    }

    private static WorldSaveData BuildMinimalSave(string slotId, string slotName, DateTime savedAt)
    {
        return new WorldSaveData
        {
            SlotId = slotId,
            SlotName = slotName,
            Seed = 4242,
            SavedAt = savedAt,
            Spawn = new SpawnSaveData { X = 16, Z = 16 }
        };
    }
}

using Autonocraft.Core;
using Autonocraft.Ai;
using Autonocraft.Domain.Village;
using Autonocraft.Entities;
using Autonocraft.Items;
using Autonocraft.Village;
using VillageEntity = Autonocraft.Village.Village;
using Autonocraft.World;
using Autonocraft.World.Structures;

namespace Autonocraft.Tests.Integration;

public static class VillageTests
{
    public static void RunInventoryStacking()
    {
        Console.Write("Running Inventory Stacking Test... ");
        var inv = new Inventory(9);
        if (!inv.AddItem(ItemStack.CreateBlock(BlockType.OakLog, 32)))
        {
            throw new Exception("Failed to add blocks.");
        }

        if (inv.CountBlock(BlockType.OakLog) != 32)
        {
            throw new Exception("Block count mismatch.");
        }

        if (!inv.TryConsumeBlock(BlockType.OakLog, 10))
        {
            throw new Exception("Consume failed.");
        }

        if (inv.CountBlock(BlockType.OakLog) != 22)
        {
            throw new Exception("Remaining count wrong.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunBlockActionService()
    {
        Console.Write("Running Block Action Service Test... ");
        var world = new VoxelWorld(4242);
        world.UpdateChunksAround(null, new System.Numerics.Vector3(16.5f, 64f, 16.5f), 1);
        int x = 16;
        int z = 16;
        int y = world.GetHighestSolidY(x, z) + 1;
        world.SetBlock(x, y, z, BlockType.Stone);
        var inv = new Inventory(4);
        if (world.GetBlock(x, y, z) != BlockType.Stone)
        {
            throw new Exception("Setup block missing.");
        }

        if (!BlockActionService.TryBreakBlock(world, x, y, z, inv))
        {
            throw new Exception("Break failed.");
        }

        if (world.GetBlock(x, y, z) != BlockType.Air || inv.CountBlock(BlockType.Stone) != 1)
        {
            throw new Exception("Break result wrong.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunStarterSettlementOnNewWorld(AutonocraftGame game)
    {
        Console.Write("Running Starter Settlement On New World Test... ");
        var session = game.Session;
        var world = session.Grid;
        world.UpdateChunksAround(null, new System.Numerics.Vector3(GameConstants.DefaultSpawnX + 4.5f, 64f, GameConstants.DefaultSpawnZ + 0.5f), 2);

        session.Villages.InitializeStarterSettlement(world, GameConstants.DefaultSpawnX, GameConstants.DefaultSpawnZ);

        var village = session.Villages.GetPrimaryVillage();
        if (village == null)
        {
            throw new Exception("Starter village missing.");
        }

        if (village.Population < 2)
        {
            throw new Exception("Expected at least 2 starter villagers.");
        }

        if (village.Storage.CountBlock(BlockType.OakPlank) < 8)
        {
            throw new Exception("Starter storage not seeded.");
        }

        if (!PlayerStructureRegistry.TryGet("town_heart", out var heart))
        {
            throw new Exception("Town heart blueprint missing.");
        }

        bool hasHeartBlock = false;
        foreach (var block in heart.Template.Blocks)
        {
            if (world.GetBlock(village.AnchorX + block.Dx, village.AnchorY + block.Dy, village.AnchorZ + block.Dz) == block.Type)
            {
                hasHeartBlock = true;
                break;
            }
        }

        if (!hasHeartBlock)
        {
            throw new Exception("Town Heart blocks not placed.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunClaimWorldStructure()
    {
        Console.Write("Running Claim World Structure Test... ");
        var villagers = new VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(5555);
        world.UpdateChunksAround(null, new System.Numerics.Vector3(32.5f, 64f, 32.5f), 2);

        int ax = 32;
        int az = 32;
        world.UpdateChunksAround(null, new System.Numerics.Vector3(ax + 0.5f, 64f, az + 0.5f), 2);
        int ay = StructureFingerprint.FindSurfaceAnchorY(world, ax, az) - 1;
        var cottage = StructureRegistry.All.First(s => s.Id == "PlainsCottage");
        foreach (var block in cottage.Template.Blocks)
        {
            world.SetBlock(ax + block.Dx, ay + block.Dy, az + block.Dz, BlockType.Air);
        }

        foreach (var block in cottage.Template.Blocks)
        {
            world.SetBlock(ax + block.Dx, ay + block.Dy, az + block.Dz, block.Type);
        }

        if (!StructureFingerprint.TryMatchWorldStructure(world, ax, ay, az, out var matched, out float ratio))
        {
            throw new Exception($"Structure fingerprint failed before claim (ratio={ratio:F2}).");
        }

        if (!villages.TryClaimStructure(world, ax, az, out var village) || village == null)
        {
            throw new Exception("Claim structure failed.");
        }

        if (village.Population < 1)
        {
            throw new Exception("Claimed village should spawn a villager.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunFarmFoodProduction()
    {
        Console.Write("Running Farm Food Production Test... ");
        var village = new VillageEntity("Farm Test", 0, 64, 0);
        village.RestoreBuilding(new BuildingSaveData
        {
            Id = 1,
            BlueprintId = "farm_plot",
            Kind = (int)BuildingKind.FarmPlot,
            AnchorX = 0,
            AnchorY = 64,
            AnchorZ = 0,
            IsComplete = true
        });

        float initialFood = village.FoodStock;
        for (int i = 0; i < 70; i++)
        {
            village.UpdateSimulation(1f, 0.5f);
        }

        if (village.FoodStock <= initialFood)
        {
            throw new Exception("Farm plot did not increase food stock.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageFoundAndRecruit(AutonocraftGame game)
    {
        Console.Write("Running Village Found And Recruit Test... ");
        var session = game.Session;
        var world = session.Grid;
        world.UpdateChunksAround(null, session.Player.Position, 2);

        int ax = (int)session.Player.Position.X + 12;
        int az = (int)session.Player.Position.Z + 12;
        int ay = StructureFingerprint.FindSurfaceAnchorY(world, ax, az);
        if (PlayerStructureRegistry.TryGet("town_heart", out var heart))
        {
            foreach (var block in heart.Template.Blocks)
            {
                world.SetBlock(ax + block.Dx, ay + block.Dy, az + block.Dz, BlockType.Air);
            }
        }

        if (!session.Villages.TryFoundVillage(world, "Test Hamlet", ax, az, out var village) || village == null)
        {
            throw new Exception("Could not found village.");
        }

        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 16));

        if (!session.Villages.TryRecruit(village))
        {
            throw new Exception("Recruit failed.");
        }

        if (village.Population < 1)
        {
            throw new Exception("Population not updated.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageSaveRoundTrip(AutonocraftGame game)
    {
        Console.Write("Running Village Save Round-Trip Test... ");
        RunVillageSaveRoundTripV6(game);
    }

    public static void RunVillageSaveRoundTripV6(AutonocraftGame game)
    {
        Console.Write("Running Village Save Round-Trip V6 Test... ");
        var session = game.Session;
        int ax = (int)session.Player.Position.X + 8;
        int az = (int)session.Player.Position.Z + 8;
        session.Villages.TryFoundVillage(session.Grid, "Save Test", ax, az, out var village);
        if (village != null)
        {
            village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 8));
            village.Storage.AddItem(ItemStack.CreateBlock(BlockType.Dirt, 32));
            village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 8));
            session.Villages.TryRecruit(village);
            if (!session.Villages.TryQueueBlueprint(session.Grid, village, "farm_plot", ax + 4, az, village.Storage))
            {
                throw new Exception("Failed to queue farm_plot for save test.");
            }
        }

        int expectedSites = village?.BuildingSites.Count ?? 0;

        var exportedVillages = session.Villages.ExportVillages();
        var exportedVillagers = session.Villagers.ExportVillagers();
        var exportedAnchors = session.Villages.ExportClaimedAnchors();
        if (exportedVillages.Count == 0)
        {
            throw new Exception("No villages exported.");
        }

        var exportedSave = exportedVillages.Find(v => v.Name == "Save Test");
        if (exportedSave == null)
        {
            throw new Exception("Save Test village missing from export.");
        }

        if (exportedSave.BuildingSites.Count == 0 && expectedSites > 0)
        {
            throw new Exception("Building sites missing from export.");
        }

        if (exportedSave.PopulationCap <= 0)
        {
            throw new Exception("Population cap not exported.");
        }

        session.Villages.LoadFromSave(exportedVillages, exportedVillagers, exportedAnchors);
        if (session.Villages.Villages.Count == 0)
        {
            throw new Exception("Villages not loaded.");
        }

        var loaded = session.Villages.GetVillage(exportedSave.Id);
        if (loaded == null)
        {
            throw new Exception("Save Test village not loaded.");
        }

        if (expectedSites > 0 && loaded.BuildingSites.Count == 0)
        {
            throw new Exception("Building sites not persisted.");
        }

        if (exportedVillagers.Count > 0)
        {
            var exportedVillager = exportedVillagers[0];
            session.Villagers.TryGet(exportedVillager.Id, out var loadedVillager);
            if (loadedVillager == null)
            {
                throw new Exception("Villager not loaded after save round-trip.");
            }

            if (string.IsNullOrEmpty(exportedVillager.Trait))
            {
                throw new Exception("Exported villager trait missing.");
            }

            if (!string.Equals(loadedVillager.Persona.Trait, exportedVillager.Trait, StringComparison.Ordinal))
            {
                throw new Exception($"Villager trait not restored: expected {exportedVillager.Trait}, got {loadedVillager.Persona.Trait}.");
            }
        }

        Console.WriteLine("PASSED");
    }

    public static void RunCanPlaceBlueprint()
    {
        Console.Write("Running Can Place Blueprint Test... ");
        var villagers = new Entities.VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(4242);
        int ax = 32;
        int az = 32;
        if (!villages.TryFoundVillage(world, "Placement Test", ax, az, out var village) || village == null)
        {
            throw new Exception("Could not found village.");
        }

        if (!PlayerStructureRegistry.TryGet("farm_plot", out var blueprint))
        {
            throw new Exception("farm_plot blueprint missing.");
        }

        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.Dirt, 32));
        village.Storage.AddItem(ItemStack.CreateBlock(BlockType.OakPlank, 8));

        if (villages.CanPlaceBlueprint(world, village, blueprint, ax + 80, az + 80, village.Storage))
        {
            throw new Exception("Expected out-of-radius blueprint placement to fail.");
        }

        int candidateX = ax + 8;
        int candidateZ = az + 8;
        int anchorY = StructureFingerprint.FindSurfaceAnchorY(world, candidateX, candidateZ);
        world.SetBlock(candidateX, anchorY, candidateZ, BlockType.Stone);
        if (villages.CanPlaceBlueprint(world, village, blueprint, candidateX, candidateZ, village.Storage))
        {
            throw new Exception("Expected overlapping blueprint placement to fail.");
        }

        if (blueprint.CanAfford(village.Storage)
            && villages.CanPlaceBlueprint(world, village, blueprint, ax + 80, az, village.Storage))
        {
            throw new Exception("Expected out-of-radius placement to fail even when affordable.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageAiToolsMock()
    {
        Console.Write("Running Village AI Tools Test... ");
        var villagers = new Entities.VillagerManager();
        var villages = new VillageManager(villagers);
        var world = new VoxelWorld(999);
        int ax = 16;
        int az = 16;
        villages.TryFoundVillage(world, "AI Test", ax, az, out var village);
        if (village == null)
        {
            throw new Exception("Village missing.");
        }

        var (ok, msg) = VillageAiTools.ExecuteTool("get_village_summary", "{}", villages, villagers, village);
        if (!ok || string.IsNullOrEmpty(msg))
        {
            throw new Exception("Summary tool failed.");
        }

        Console.WriteLine("PASSED");
    }

    public static void RunVillageScreenInputLayout()
    {
        Console.Write("Running Village Screen Input Layout Test... ");
        const float panelWidth = 640f;
        const float panelHeight = 520f;
        const float buttonWidth = 140f;
        const float buttonHeight = 34f;

        var layout = new Autonocraft.Engine.UiLayout(1280, 720);
        float panelW = layout.S(panelWidth);
        float panelH = layout.S(panelHeight);
        float panelX = layout.CenterX - panelW / 2f;
        float panelY = layout.CenterY - panelH / 2f;
        float left = panelX + layout.S(20f);
        float footerY = panelY + panelH - layout.S(72f);
        float buttonW = layout.S(buttonWidth);
        float buttonH = layout.S(buttonHeight);

        var recruitRect = new Microsoft.Xna.Framework.Rectangle((int)left, (int)footerY, (int)buttonW, (int)buttonH);
        if (!recruitRect.Contains(recruitRect.Center.X, recruitRect.Center.Y))
        {
            throw new Exception("Recruit button center must be inside recruit hit box.");
        }

        var closeRect = new Microsoft.Xna.Framework.Rectangle(
            (int)(panelX + panelW - layout.S(20f) - buttonW),
            (int)(panelY + panelH - layout.S(28f)),
            (int)buttonW,
            (int)buttonH);
        if (!closeRect.Contains(closeRect.Center.X, closeRect.Center.Y))
        {
            throw new Exception("Close button center must be inside close hit box.");
        }

        float detailX = panelX + panelW / 2f;
        float jobY = panelY + panelH - layout.S(120f);
        float talkY = jobY - layout.S(34f) - layout.S(16f);
        var talkRect = new Microsoft.Xna.Framework.Rectangle((int)detailX, (int)talkY, (int)layout.S(90f), (int)layout.S(34f));
        if (!talkRect.Contains(talkRect.Center.X, talkRect.Center.Y))
        {
            throw new Exception("Talk button center must be inside talk hit box.");
        }

        Console.WriteLine("PASSED");
    }
}

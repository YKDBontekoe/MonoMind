using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Autonocraft.Core;
using Autonocraft.Crafting;
using Autonocraft.Engine;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.UI
{
    public sealed class InventoryScreen
    {
        private const float SlotSize = 40f;
        private const float SlotGap = 4f;
        private const float PanelPad = 16f;

        private readonly UiRenderer _ui;
        private readonly RecipeBookPanel _recipeBook;
        private readonly UiItemStackRenderer _itemStacks;
        private int _hoveredSlot = -1;
        private bool _leftClicked;
        private bool _rightClicked;
        private int _clickedSlot = -1;

        public InventoryScreen(UiRenderer ui)
        {
            _ui = ui;
            _recipeBook = new RecipeBookPanel(ui);
            _itemStacks = new UiItemStackRenderer(ui.Device);
        }

        public void Update(
            Viewport viewport,
            Player player,
            CraftingSystem crafting,
            KeyboardState kb,
            MouseState mouse,
            KeyboardState prevKb,
            MouseState prevMouse)
        {
            _hoveredSlot = -1;
            _leftClicked = false;
            _rightClicked = false;
            _clickedSlot = -1;
            _recipeBook.ResetInteraction();

            if (!crafting.InventoryOpen)
            {
                return;
            }

            var layout = new UiLayout(viewport.Width, viewport.Height);
            var bounds = BuildSlotBounds(layout, out int totalSlots);
            Point mousePt = new Point(mouse.X, mouse.Y);
            Rectangle? recipeBookRect = null;

            if (crafting.RecipeBookOpen)
            {
                float panelW = layout.S(520f);
                float panelH = layout.S(300f);
                float panelX = layout.CenterX - panelW / 2f;
                float panelY = layout.CenterY - panelH / 2f;
                recipeBookRect = RecipeBookPanel.BuildPanelRect(layout, panelX, panelX + panelW, panelY, panelH);
            }

            bool skipSlotClicks = recipeBookRect.HasValue && recipeBookRect.Value.Contains(mousePt);

            for (int i = 0; i < totalSlots; i++)
            {
                if (!bounds[i].Contains(mousePt))
                {
                    continue;
                }

                _hoveredSlot = i;
                if (skipSlotClicks)
                {
                    continue;
                }

                if (mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
                {
                    _leftClicked = true;
                    _clickedSlot = i;
                }

                if (mouse.RightButton == ButtonState.Pressed && prevMouse.RightButton == ButtonState.Released)
                {
                    _rightClicked = true;
                    _clickedSlot = i;
                }
            }

            if (crafting.RecipeBookOpen)
            {
                float panelW = layout.S(520f);
                float panelH = layout.S(300f);
                float panelX = layout.CenterX - panelW / 2f;
                float panelY = layout.CenterY - panelH / 2f;
                var bookRect = recipeBookRect ?? RecipeBookPanel.BuildPanelRect(layout, panelX, panelX + panelW, panelY, panelH);
                var recipes = RecipeBookResolver.GetVisibleRecipes(
                    BlockType.StationBench,
                    CraftGridSize.TwoByTwo,
                    crafting.Journal);
                _recipeBook.Update(bookRect, recipes, player, CraftGridSize.TwoByTwo, mouse, prevMouse);
            }
        }

        public void HandleRecipeBookClick(CraftingSystem crafting, Player player)
        {
            var recipe = _recipeBook.ConsumeClickedRecipe();
            if (recipe == null)
            {
                return;
            }

            if (!crafting.TryApplyRecipeBookSelection(recipe, player))
            {
                player.ShowToast?.Invoke("Missing ingredients for recipe");
            }
        }

        public void HandleClicks(Player player, CraftingSystem crafting)
        {
            if (_clickedSlot < 0)
            {
                return;
            }

            var cursor = crafting.CraftCursor;
            int craftSlots = crafting.PlayerCraftGrid.SlotCount;
            int storageSlots = Player.StorageSlotCount;
            int slot = _clickedSlot;

            if (slot == craftSlots)
            {
                if (_leftClicked)
                {
                    var result = crafting.TryPlayerCraft(player);
                    if (!result.Succeeded && !string.IsNullOrEmpty(result.Message))
                    {
                        player.ShowToast?.Invoke(result.Message);
                    }
                }

                return;
            }

            if (slot < craftSlots)
            {
                crafting.PlayerCraftGrid.HandleSlotClick(slot, ref cursor, _rightClicked);
                crafting.CraftCursor = cursor;
                return;
            }

            slot -= craftSlots + 1;
            if (slot < storageSlots)
            {
                var storage = player.Storage;
                var stack = storage.GetSlot(slot);
                if (_rightClicked)
                {
                    InventorySlotInteraction.HandleRightClick(ref cursor, ref stack);
                }
                else
                {
                    InventorySlotInteraction.HandleLeftClick(ref cursor, ref stack);
                }

                storage.SetSlot(slot, stack);
                crafting.CraftCursor = cursor;
                return;
            }

            slot -= storageSlots;
            if (slot < player.Hotbar.Length)
            {
                ref var hotbarSlot = ref player.Hotbar[slot];
                if (_rightClicked)
                {
                    InventorySlotInteraction.HandleRightClick(ref cursor, ref hotbarSlot);
                }
                else
                {
                    InventorySlotInteraction.HandleLeftClick(ref cursor, ref hotbarSlot);
                }

                crafting.CraftCursor = cursor;
            }
        }

        public void Draw(Viewport viewport, Player player, CraftingSystem crafting, Texture2D atlas)
        {
            if (!crafting.InventoryOpen)
            {
                return;
            }

            var layout = new UiLayout(viewport.Width, viewport.Height);
            var bounds = BuildSlotBounds(layout, out int totalSlots);
            var preview = crafting.GetPlayerCraftPreview();

            float panelW = layout.S(520f);
            float panelH = layout.S(300f);
            float panelX = layout.CenterX - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f;

            _ui.DrawFullscreenBackground(UiTheme.OverlayScrim * 0.55f);
            _ui.DrawCard(panelX, panelY, panelW, panelH, 1f, UiTheme.RadiusXl);
            _ui.DrawCenteredTitle("Inventory", panelY + layout.S(16f), layout.S(UiTheme.FontTitle), UiTheme.Title);

            int craftSlots = crafting.PlayerCraftGrid.SlotCount;
            for (int i = 0; i < totalSlots; i++)
            {
                var rect = bounds[i];
                bool hovered = _hoveredSlot == i;
                _ui.DrawPanel(rect.X, rect.Y, rect.Width, rect.Height,
                    hovered ? UiTheme.PanelBgHighlight : UiTheme.HudSlotFill,
                    hovered ? UiTheme.Accent : UiTheme.HudSlotBorder);

                if (i < craftSlots)
                {
                    _itemStacks.DrawStack(atlas, _ui, crafting.PlayerCraftGrid.GetSlot(i), rect, layout);
                }
                else if (i == craftSlots)
                {
                    _itemStacks.DrawStack(atlas, _ui, preview.Result, rect, layout, dimmed: !preview.HasMatch);
                }
                else
                {
                    int dataIndex = i - craftSlots - 1;
                    int storageSlots = Player.StorageSlotCount;
                    if (dataIndex < storageSlots)
                    {
                        _itemStacks.DrawStack(atlas, _ui, player.Storage.GetSlot(dataIndex), rect, layout);
                    }
                    else
                    {
                        _itemStacks.DrawStack(atlas, _ui, player.Hotbar[dataIndex - storageSlots], rect, layout);
                    }
                }
            }

            if (!crafting.CraftCursor.IsEmpty)
            {
                var mouse = Mouse.GetState();
                float size = layout.S(SlotSize);
                _itemStacks.DrawStack(
                    atlas,
                    _ui,
                    crafting.CraftCursor,
                    new Rectangle(mouse.X, mouse.Y, (int)size, (int)size),
                    layout);
            }

            _ui.DrawCenteredText("I or Esc close · click slots to move · click output to craft",
                panelY + panelH + layout.S(10f), layout.S(UiTheme.FontSmall), UiTheme.Hint);

            if (crafting.RecipeBookOpen)
            {
                var bookRect = RecipeBookPanel.BuildPanelRect(layout, panelX, panelX + panelW, panelY, panelH);
                var recipes = RecipeBookResolver.GetVisibleRecipes(
                    BlockType.StationBench,
                    CraftGridSize.TwoByTwo,
                    crafting.Journal);
                _recipeBook.Draw(layout, bookRect, recipes, crafting.Journal, player, CraftGridSize.TwoByTwo);
            }
            else
            {
                _ui.DrawString("B — recipe book", panelX + panelW + layout.S(12f), panelY + layout.S(14f),
                    layout.S(UiTheme.FontSmall), UiTheme.Hint);
            }
        }

        private Rectangle[] BuildSlotBounds(UiLayout layout, out int totalSlots)
        {
            int craftSlots = 4;
            int storageSlots = Player.StorageSlotCount;
            int hotbarSlots = 9;
            totalSlots = craftSlots + 1 + storageSlots + hotbarSlots;

            float size = layout.S(SlotSize);
            float gap = layout.S(SlotGap);
            float panelW = layout.S(520f);
            float panelH = layout.S(300f);
            float panelX = layout.CenterX - panelW / 2f;
            float panelY = layout.CenterY - panelH / 2f;

            var bounds = new Rectangle[totalSlots];
            float craftX = panelX + layout.S(PanelPad);
            float craftY = panelY + layout.S(48f);
            int gridDim = 2;

            for (int i = 0; i < craftSlots; i++)
            {
                int row = i / gridDim;
                int col = i % gridDim;
                bounds[i] = new Rectangle(
                    (int)(craftX + col * (size + gap)),
                    (int)(craftY + row * (size + gap)),
                    (int)size,
                    (int)size);
            }

            float resultX = craftX + gridDim * (size + gap) + layout.S(24f);
            float resultY = craftY + (size + gap) * 0.5f;
            bounds[craftSlots] = new Rectangle((int)resultX, (int)resultY, (int)size, (int)size);

            float storageX = panelX + layout.S(PanelPad);
            float storageY = craftY + gridDim * (size + gap) + layout.S(20f);
            int storageCols = 9;
            for (int i = 0; i < storageSlots; i++)
            {
                int row = i / storageCols;
                int col = i % storageCols;
                bounds[craftSlots + 1 + i] = new Rectangle(
                    (int)(storageX + col * (size + gap)),
                    (int)(storageY + row * (size + gap)),
                    (int)size,
                    (int)size);
            }

            float hotbarY = storageY + 3 * (size + gap) + layout.S(16f);
            for (int i = 0; i < hotbarSlots; i++)
            {
                bounds[craftSlots + 1 + storageSlots + i] = new Rectangle(
                    (int)(storageX + i * (size + gap)),
                    (int)hotbarY,
                    (int)size,
                    (int)size);
            }

            return bounds;
        }
    }
}

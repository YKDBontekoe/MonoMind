using System;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Core
{
    public partial class Player
    {
        public ItemStack[] Hotbar { get; } = new ItemStack[9];
        public int SelectedSlot { get; set; } = 0;

        public const int StorageSlotCount = 27;
        public Inventory Storage { get; } = new(StorageSlotCount);

        public ItemStack GetSelectedStack() => Hotbar[SelectedSlot];

        public bool IsHoldingTool() => GetSelectedStack().IsTool();

        public bool CanPlaceFromSelected()
        {
            var stack = GetSelectedStack();
            if (CreativeMode && stack.IsBlock())
            {
                return true;
            }

            return stack.IsBlock() && stack.Count > 0;
        }

        public BlockType GetSelectedBlockType()
        {
            var stack = GetSelectedStack();
            return stack.IsBlock() ? stack.BlockType : BlockType.Air;
        }

        public bool UseSelectedBlock() => UseSelectedStack(stack => stack.IsBlock());

        public bool UseSelectedStack(Func<ItemStack, bool>? predicate = null)
        {
            ref var slot = ref Hotbar[SelectedSlot];
            if (slot.IsEmpty || predicate != null && !predicate(slot))
            {
                return false;
            }

            if (CreativeMode)
            {
                return true;
            }

            slot.Count--;
            if (slot.Count <= 0)
            {
                slot = ItemStack.Empty;
            }

            return true;
        }

        public ItemStack DropOneFromSelectedSlot()
        {
            ref var slot = ref Hotbar[SelectedSlot];
            if (slot.IsEmpty)
            {
                return ItemStack.Empty;
            }

            ItemStack dropped = slot;
            dropped.Count = 1;

            if (!CreativeMode)
            {
                slot.Count--;
                if (slot.Count <= 0)
                {
                    slot = ItemStack.Empty;
                }
            }

            return dropped;
        }

        public bool DamageSelectedTool(int amount)
        {
            if (amount <= 0 || CreativeMode)
            {
                return false;
            }

            ref var slot = ref Hotbar[SelectedSlot];
            if (!slot.IsTool())
            {
                return false;
            }

            slot.Durability -= amount;
            if (slot.Durability <= 0)
            {
                string toolName = slot.GetDisplayName();
                slot = ItemStack.Empty;
                Stats.RecordToolBroken();
                Notify($"{toolName} broke!");
                return true;
            }

            return false;
        }

        public float GetSelectedMeleeDamage()
        {
            var stack = GetSelectedStack();
            if (stack.IsTool() && ToolRegistry.TryGet(stack.ToolId, out var def) && def.ToolType == ToolType.Sword)
            {
                return def.MeleeDamage * Skills.GetBonus(PlayerSkill.Combat);
            }

            return CombatSystem.BareHandDamage;
        }

        public void GiveBlocks(BlockType blockType, int count)
        {
            if (!AddBlockStack(blockType, count))
            {
                return;
            }

            if (count > 0 && blockType != BlockType.Air)
            {
                OnItemAdded?.Invoke(ItemStack.CreateBlock(blockType, count));
            }
        }

        public bool HasSpaceFor(ItemStack item)
        {
            if (item.IsEmpty || CreativeMode)
            {
                return true;
            }

            var adapter = new Inventory(StorageSlotCount + Hotbar.Length);
            CopyToInventory(adapter);
            return adapter.HasSpaceFor(item);
        }

        private void CopyToInventory(Inventory proxy)
        {
            for (int i = 0; i < Hotbar.Length; i++)
            {
                proxy.SetSlot(i, Hotbar[i]);
            }

            for (int i = 0; i < StorageSlotCount; i++)
            {
                proxy.SetSlot(Hotbar.Length + i, Storage.GetSlot(i));
            }
        }

        private void CopyFromInventory(Inventory proxy)
        {
            for (int i = 0; i < Hotbar.Length; i++)
            {
                Hotbar[i] = proxy.GetSlot(i);
            }

            for (int i = 0; i < StorageSlotCount; i++)
            {
                Storage.SetSlot(i, proxy.GetSlot(Hotbar.Length + i));
            }
        }

        public bool AddItem(ItemStack item)
        {
            if (item.IsEmpty)
            {
                return false;
            }

            bool added = item.IsBlock() ? AddBlockStack(item.BlockType, item.Count)
                : item.IsTool() ? AddToolStack(item)
                : item.IsFluidContainer() ? AddFluidContainerStack(item)
                : item.IsFood() ? AddFoodStack(item)
                : item.IsMaterial() ? AddMaterialStack(item)
                : false;

            if (added)
            {
                OnItemAdded?.Invoke(item);
            }

            return added;
        }

        public void AddToInventory(BlockType blockType)
        {
            if (blockType == BlockType.Air)
            {
                return;
            }

            AddBlockStack(blockType, 1);
        }

        private bool AddBlockStack(BlockType blockType, int count)
        {
            if (blockType == BlockType.Air || count <= 0)
            {
                return false;
            }

            return TryAddViaInventoryProxy(ItemStack.CreateBlock(blockType, count));
        }

        private bool AddToolStack(ItemStack tool) => TryAddViaInventoryProxy(tool, logCollection: true);

        private bool AddFoodStack(ItemStack food) => TryAddViaInventoryProxy(food);

        private bool AddMaterialStack(ItemStack material) => TryAddViaInventoryProxy(material);

        private bool AddFluidContainerStack(ItemStack container) => TryAddViaInventoryProxy(container, logCollection: true);

        private bool TryAddViaInventoryProxy(ItemStack item, bool logCollection = false)
        {
            var proxy = new Inventory(StorageSlotCount + Hotbar.Length);
            CopyToInventory(proxy);
            if (logCollection)
            {
                proxy.OnOverflow = msg =>
                {
                    Console.WriteLine($"[Inventory] {msg}");
                    Notify(msg);
                };
            }
            else
            {
                proxy.OnOverflow = Notify;
            }

            if (!proxy.AddItem(item))
            {
                if (logCollection)
                {
                    Console.WriteLine($"[Inventory] Inventory full! Cannot collect {item.GetDisplayName()}.");
                    Notify($"Inventory full! Cannot collect {item.GetDisplayName()}.");
                }

                return false;
            }

            CopyFromInventory(proxy);
            if (logCollection)
            {
                Console.WriteLine($"[Inventory] Added {item.GetDisplayName()}");
            }

            return true;
        }

        public string GetInventoryHUD()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 9; i++)
            {
                string marker = (i == SelectedSlot) ? "*" : "";
                if (Hotbar[i].IsEmpty)
                {
                    sb.Append($"[{marker}{i + 1}: -]");
                }
                else if (Hotbar[i].IsTool())
                {
                    sb.Append($"[{marker}{i + 1}: {Hotbar[i].GetDisplayName()} ({Hotbar[i].Durability})]");
                }
                else if (Hotbar[i].IsFood())
                {
                    sb.Append($"[{marker}{i + 1}: {Hotbar[i].GetDisplayName()} ({Hotbar[i].Count})]");
                }
                else
                {
                    sb.Append($"[{marker}{i + 1}: {Hotbar[i].BlockType} ({Hotbar[i].Count})]");
                }

                if (i < 8) sb.Append(" ");
            }

            return sb.ToString();
        }

        int IItemContainer.SlotCount => StorageSlotCount + Hotbar.Length;

        ItemStack IItemContainer.GetSlot(int index)
        {
            if (index < Hotbar.Length)
            {
                return Hotbar[index];
            }

            return Storage.GetSlot(index - Hotbar.Length);
        }

        void IItemContainer.SetSlot(int index, ItemStack stack)
        {
            if (index < Hotbar.Length)
            {
                Hotbar[index] = stack;
                return;
            }

            Storage.SetSlot(index - Hotbar.Length, stack);
        }

        bool IItemContainer.TryConsumeBlock(BlockType blockType, int count)
        {
            if (CreativeMode)
            {
                return true;
            }

            int available = ((IItemContainer)this).CountBlock(blockType);
            if (available < count)
            {
                return false;
            }

            int remaining = count;
            for (int i = 0; i < ((IItemContainer)this).SlotCount && remaining > 0; i++)
            {
                var slot = ((IItemContainer)this).GetSlot(i);
                if (!slot.IsBlock() || slot.BlockType != blockType)
                {
                    continue;
                }

                int take = Math.Min(slot.Count, remaining);
                slot.Count -= take;
                remaining -= take;
                if (slot.Count <= 0)
                {
                    slot = ItemStack.Empty;
                }

                ((IItemContainer)this).SetSlot(i, slot);
            }

            return remaining == 0;
        }

        int IItemContainer.CountBlock(BlockType blockType)
        {
            if (CreativeMode)
            {
                return int.MaxValue / 2;
            }

            int total = 0;
            for (int i = 0; i < ((IItemContainer)this).SlotCount; i++)
            {
                var slot = ((IItemContainer)this).GetSlot(i);
                if (slot.IsBlock() && slot.BlockType == blockType)
                {
                    total += slot.Count;
                }
            }

            return total;
        }

        bool IItemContainer.AddItem(ItemStack item) => AddItem(item);

        bool IItemContainer.HasSpaceFor(ItemStack item) => HasSpaceFor(item);

        void ICraftingPlayer.RecordItemCrafted() => Stats.RecordItemCrafted();

        bool ICraftingPlayer.TryConsumeFood(ItemId foodId, int count)
        {
            int remaining = count;
            for (int i = 0; i < Hotbar.Length && remaining > 0; i++)
            {
                ref var slot = ref Hotbar[i];
                if (!slot.IsFood() || slot.FoodId != foodId)
                {
                    continue;
                }

                int take = Math.Min(slot.Count, remaining);
                slot.Count -= take;
                if (slot.Count <= 0)
                {
                    slot = ItemStack.Empty;
                }

                remaining -= take;
            }

            return remaining <= 0;
        }

        bool ICraftingPlayer.TryTakeOneFromHotbar(int hotbarIndex, out ItemStack taken)
        {
            taken = ItemStack.Empty;
            if (hotbarIndex < 0 || hotbarIndex >= Hotbar.Length)
            {
                return false;
            }

            ref var slot = ref Hotbar[hotbarIndex];
            if (slot.IsEmpty || (!slot.IsBlock() && !slot.IsMaterial()))
            {
                return false;
            }

            if (slot.IsBlock())
            {
                taken = ItemStack.CreateBlock(slot.BlockType, 1);
                slot.Count--;
                if (slot.Count <= 0)
                {
                    slot = ItemStack.Empty;
                }

                return true;
            }

            taken = ItemStack.CreateMaterial(slot.MaterialId, 1);
            slot.Count--;
            if (slot.Count <= 0)
            {
                slot = ItemStack.Empty;
            }

            return true;
        }

        public ItemStack GetHotbarSlot(int index) => Hotbar[index];
    }
}

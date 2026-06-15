using System;
using System.Collections.Generic;
using System.Text;
using Autonocraft.Items;

namespace Autonocraft.Core
{
    public static class DeathConsequences
    {
        public static string ApplyInventoryLoss(Player player, Random? rng = null)
        {
            if (player.FlyingMode)
            {
                return string.Empty;
            }

            rng ??= new Random();
            var occupied = new List<int>();
            for (int i = 0; i < player.Hotbar.Length; i++)
            {
                if (!player.Hotbar[i].IsEmpty)
                {
                    occupied.Add(i);
                }
            }

            if (occupied.Count == 0)
            {
                ApplyToolWear(player, rng);
                return string.Empty;
            }

            int toLose = Math.Min(SurvivalConstants.DeathLostSlotCount, occupied.Count);
            var lostNames = new List<string>();
            for (int n = 0; n < toLose && occupied.Count > 0; n++)
            {
                int pick = rng.Next(occupied.Count);
                int slotIndex = occupied[pick];
                occupied.RemoveAt(pick);
                lostNames.Add(DescribeSlot(player.Hotbar[slotIndex]));
                player.Hotbar[slotIndex] = ItemStack.Empty;
            }

            ApplyToolWear(player, rng);
            return lostNames.Count == 0 ? string.Empty : $"Lost: {string.Join(", ", lostNames)}";
        }

        private static void ApplyToolWear(Player player, Random rng)
        {
            for (int i = 0; i < player.Hotbar.Length; i++)
            {
                ref var slot = ref player.Hotbar[i];
                if (!slot.IsTool() || slot.MaxDurability <= 0)
                {
                    continue;
                }

                int loss = Math.Max(1, (int)MathF.Ceiling(slot.MaxDurability * SurvivalConstants.DeathToolDurabilityLossFraction));
                slot.Durability = Math.Max(1, slot.Durability - loss);
            }
        }

        private static string DescribeSlot(in ItemStack stack)
        {
            if (stack.IsBlock())
            {
                return $"{stack.Count} {stack.BlockType}";
            }

            return stack.GetDisplayName();
        }
    }
}

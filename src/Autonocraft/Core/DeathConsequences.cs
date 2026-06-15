using Autonocraft.Items;

namespace Autonocraft.Core
{
    public enum DeathCause
    {
        Unknown,
        Fall,
        Drown,
        Animal,
        Starvation,
        Wolf
    }

    public static class DeathConsequences
    {
        public static void ApplyOnDeath(Player player)
        {
            int usedSlots = 0;
            for (int i = 0; i < player.Hotbar.Length; i++)
            {
                if (!player.Hotbar[i].IsEmpty)
                {
                    usedSlots++;
                }
            }

            if (usedSlots <= 0)
            {
                return;
            }

            int dropCount = (usedSlots + 1) / 2;
            for (int i = player.Hotbar.Length - 1; i >= 0 && dropCount > 0; i--)
            {
                if (!player.Hotbar[i].IsEmpty)
                {
                    player.Hotbar[i] = ItemStack.Empty;
                    dropCount--;
                }
            }
        }

        public static void ApplyOnRespawn(Player player)
        {
            player.Hunger = SurvivalConstants.MaxHunger * SurvivalConstants.RespawnHungerFraction;
        }
    }
}

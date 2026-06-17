using System;
using Autonocraft.Items;

namespace Autonocraft.Core.DevCommands
{
    /// <summary>
    /// Shared helpers used by multiple dev commands.
    /// </summary>
    internal static class DevCommandHelpers
    {
        public static string FormatStack(ItemStack stack)
        {
            if (stack.IsTool())
            {
                return $"{stack.GetDisplayName()} ({stack.Durability}/{stack.MaxDurability})";
            }

            if (stack.IsFluidContainer())
            {
                return stack.GetDisplayName();
            }

            return $"{stack.BlockType} x{stack.Count}";
        }

        public static string GetTimeLabel(float time)
        {
            if (time < 0.2f || time > 0.9f) return "NIGHT";
            if (time < 0.3f) return "DAWN";
            if (time < 0.7f) return "DAY";
            if (time < 0.8f) return "DUSK";
            return "NIGHT";
        }
    }
}


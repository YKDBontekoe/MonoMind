using System;
using Autonocraft.Items;

using Autonocraft.Domain.Core;

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
            return DayNightCycle.GetHudTimeLabel(time);
        }
    }
}


using System;
using Autonocraft.Items;
using Autonocraft.World;

namespace Autonocraft.Core.DevCommands.Commands
{
    internal sealed class GiveCommand : IDevCommand
    {
        public string Name => "give";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var remaining = args;

            if (!DevCommandParser.TryReadNextToken(ref remaining, out var firstSpan))
            {
                return "Usage: give <block|tool> [count]";
            }

            // bucket / bucket water
            if (DevCommandParser.EqualsIgnoreCase(firstSpan, "bucket"))
            {
                bool filled = false;
                var tmp = remaining;
                if (DevCommandParser.TryReadNextToken(ref tmp, out var secondSpan) &&
                    DevCommandParser.EqualsIgnoreCase(secondSpan, "water"))
                {
                    filled = true;
                }

                session.Player.AddItem(ItemStack.CreateFluidContainer(
                    filled ? ItemId.WaterBucket : ItemId.EmptyBucket));
                return filled ? "Gave Water Bucket" : "Gave Empty Bucket";
            }

            // give tool <type> [tier]
            if (DevCommandParser.EqualsIgnoreCase(firstSpan, "tool"))
            {
                if (!DevCommandParser.TryReadNextToken(ref remaining, out var typeSpan))
                {
                    return "Usage: give tool <pickaxe|axe|shovel|sword> [wood|stone|iron|gold]";
                }

                var typeString = typeSpan.ToString();
                if (!Enum.TryParse<ToolType>(typeString, true, out var toolType))
                {
                    return "Usage: give tool <pickaxe|axe|shovel|sword> [wood|stone|iron|gold]";
                }

                ToolTier tier = ToolTier.Wood;
                if (DevCommandParser.TryReadNextToken(ref remaining, out var tierSpan))
                {
                    var tierString = tierSpan.ToString();
                    if (!Enum.TryParse<ToolTier>(tierString, true, out tier))
                    {
                        return "Invalid tier";
                    }
                }

                var itemId = ToolRegistry.GetItemId(toolType, tier);
                if (itemId == ItemId.None)
                {
                    return $"Tool {toolType}/{tier} is not available";
                }

                session.Player.AddItem(ToolRegistry.CreateStack(itemId));
                return $"Gave {ToolRegistry.Get(itemId).DisplayName}";
            }

            // give <BlockType> [count]
            var blockName = firstSpan.ToString();
            if (!Enum.TryParse<BlockType>(blockName, true, out var blockType) ||
                blockType == BlockType.Air ||
                !Enum.IsDefined(typeof(BlockType), blockType))
            {
                return $"Unknown block: {blockName}";
            }

            int count = 64;
            if (DevCommandParser.TryReadNextToken(ref remaining, out var countSpan))
            {
                if (!DevCommandParser.TryParseInt(countSpan, out count) || count <= 0)
                {
                    return "Invalid count";
                }
            }

            session.Player.GiveBlocks(blockType, count);
            return $"Gave {count}x {blockType}";
        }
    }
}


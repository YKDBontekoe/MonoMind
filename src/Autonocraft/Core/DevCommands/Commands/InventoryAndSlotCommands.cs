using System;

namespace Autonocraft.Core.DevCommands.Commands
{
    internal sealed class SlotCommand : IDevCommand
    {
        public string Name => "slot";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var remaining = args.Trim();

            if (!DevCommandParser.TryReadNextToken(ref remaining, out var slotSpan) ||
                !DevCommandParser.TryParseInt(slotSpan, out int slot) ||
                slot < 0 || slot > 8)
            {
                return "Usage: slot <0-8>";
            }

            session.Player.SelectedSlot = slot;
            var item = session.Player.Hotbar[slot];
            return item.IsEmpty
                ? $"Selected slot {slot + 1} (empty)"
                : $"Selected slot {slot + 1}: {DevCommandHelpers.FormatStack(item)}";
        }
    }

    internal sealed class InventoryCommand : IDevCommand
    {
        public string Name => "inv";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = new[] { "hotbar" };

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var lines = new string[9];
            for (int i = 0; i < 9; i++)
            {
                var item = session.Player.Hotbar[i];
                string marker = i == session.Player.SelectedSlot ? "*" : " ";
                lines[i] = item.IsEmpty
                    ? $"{marker} [{i}] empty"
                    : $"{marker} [{i}] {DevCommandHelpers.FormatStack(item)}";
            }

            return "Hotbar:\n" + string.Join("\n", lines);
        }
    }
}


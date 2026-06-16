using System;
using Autonocraft.Crafting;
using Autonocraft.Entities;

namespace Autonocraft.Core.DevCommands.Commands
{
    internal sealed class SpawnCommand : IDevCommand
    {
        public string Name => "spawn";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var remaining = args.Trim();

            if (!DevCommandParser.TryReadNextToken(ref remaining, out var typeSpan))
            {
                return "Usage: spawn <sheep|pig|chicken> [count]";
            }

            var typeString = typeSpan.ToString();
            if (!Enum.TryParse<AnimalType>(typeString, true, out var type))
            {
                return "Usage: spawn <sheep|pig|chicken> [count]";
            }

            int count = 1;
            if (DevCommandParser.TryReadNextToken(ref remaining, out var countSpan))
            {
                if (!DevCommandParser.TryParseInt(countSpan, out count) || count <= 0)
                {
                    return "Invalid count";
                }
            }

            int spawned = session.Animals.SpawnInFrontOfPlayer(session.Player, session.Grid, type, count);
            return spawned == 0
                ? $"Failed to spawn {type}"
                : $"Spawned {spawned}x {type}";
        }
    }

    internal sealed class RecipesCommand : IDevCommand
    {
        public string Name => "recipes";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var ids = session.Crafting.Journal.Export();
            if (ids.Count == 0)
            {
                return "No crafting discoveries yet.";
            }

            return "Unlocked:\n" + string.Join("\n", ids);
        }
    }

    internal sealed class UnlockCommand : IDevCommand
    {
        public string Name => "unlock";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var remaining = args.Trim();

            if (!DevCommandParser.TryReadNextToken(ref remaining, out var idSpan))
            {
                return "Usage: unlock <sigil:bench|recipe:plank|...>";
            }

            var id = idSpan.ToString();
            session.Crafting.Journal.Unlock(id);
            return $"Unlocked '{id}'.";
        }
    }
}


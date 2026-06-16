using System;
using System.Collections.Generic;

namespace Autonocraft.Core.DevCommands
{
    /// <summary>
    /// Registry mapping command names and aliases to implementations.
    /// </summary>
    internal static class DevCommandRegistry
    {
        private static readonly Dictionary<string, IDevCommand> _commands = new(StringComparer.OrdinalIgnoreCase);

        static DevCommandRegistry()
        {
            // Core gameplay / utility commands.
            Register(new Commands.TimeCommand());
            Register(new Commands.TeleportCommand());
            Register(new Commands.PosCommand());
            Register(new Commands.CreativeCommand());
            Register(new Commands.GiveCommand());
            Register(new Commands.HealthCommand());
            Register(new Commands.DamageCommand());
            Register(new Commands.SpeedCommand());
            Register(new Commands.SlotCommand());
            Register(new Commands.InventoryCommand());
            Register(new Commands.SeedCommand());
            Register(new Commands.PerfCommand());
            Register(new Commands.PerfHudCommand());
            Register(new Commands.ChunksCommand());
            Register(new Commands.SpawnCommand());
            Register(new Commands.AnimalsCommand());
            Register(new Commands.RecipesCommand());
            Register(new Commands.UnlockCommand());
            Register(new Commands.VillageCommand());
            Register(new Commands.WeatherCommand());
            Register(new Commands.RecruitCommand());
            Register(new Commands.RationsCommand());
            Register(new Commands.AssignJobCommand());
        }

        private static void Register(IDevCommand command)
        {
            _commands[command.Name] = command;

            if (command.Aliases == null)
                return;

            foreach (var alias in command.Aliases)
            {
                if (string.IsNullOrWhiteSpace(alias))
                    continue;

                _commands[alias] = command;
            }
        }

        public static bool TryGet(string name, out IDevCommand command)
        {
            return _commands.TryGetValue(name, out command!);
        }
    }
}


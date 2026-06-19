using System;
using Autonocraft.Domain.Village;
using Autonocraft.Village;

namespace Autonocraft.Core.DevCommands.Commands
{
    internal sealed class VillageCommand : IDevCommand
    {
        public string Name => "village";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var village = session.Villages.GetActiveVillage(session.Player.Position);
            if (village == null)
            {
                return "No village. Use 'recruit' after founding (press V in-game).";
            }

            return $"Village '{village.Name}' at ({village.AnchorX}, {village.AnchorY}, {village.AnchorZ}) " +
                   $"pop {village.Population}/{village.PopulationCap} tier {village.Tier} happiness {village.Happiness:F2}";
        }
    }

    internal sealed class RecruitCommand : IDevCommand
    {
        public string Name => "recruit";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var village = session.Villages.GetActiveVillage(session.Player.Position);
            if (village == null)
            {
                int ax = (int)MathF.Floor(session.Player.Position.X);
                int az = (int)MathF.Floor(session.Player.Position.Z);
                if (!session.Villages.TryFoundVillage(session.Grid, "Dev Village", ax, az, out village))
                {
                    return "Could not found village.";
                }
            }

            var recruitResult = session.Villages.TryRecruit(village!, session.Grid);
            return recruitResult.Success
                ? "Recruited villager."
                : recruitResult.PlayerMessage;
        }
    }

    internal sealed class RationsCommand : IDevCommand
    {
        public string Name => "ration";

        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = new[] { "rations" };

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var village = session.Villages.GetActiveVillage(session.Player.Position);
            if (village == null)
            {
                return "No village nearby.";
            }

            return FoodConsumption.TryTakeRations(session.Player, village)
                ? $"Took rations. Food stock: {village.FoodStock:0.#}"
                : "Could not take rations.";
        }
    }

    internal sealed class AssignJobCommand : IDevCommand
    {
        public string Name => "assign";
        public System.Collections.Generic.IEnumerable<string> Aliases { get; } = Array.Empty<string>();

        public string Execute(GameHostContext host, ReadOnlySpan<char> args)
        {
            var session = host.Session;
            var remaining = args.Trim();

            if (!DevCommandParser.TryReadNextToken(ref remaining, out var idSpan) ||
                !DevCommandParser.TryReadNextToken(ref remaining, out var jobSpan))
            {
                return "Usage: assign <villager_id> <Idle|Lumber|Mine|Farm|Build|Haul>";
            }

            if (!DevCommandParser.TryParseInt(idSpan, out int vid))
            {
                return "Invalid villager id or job.";
            }

            var jobString = jobSpan.ToString();
            if (!Enum.TryParse<JobType>(jobString, true, out var job))
            {
                return "Invalid villager id or job.";
            }

            var village = session.Villages.GetActiveVillage(session.Player.Position);
            if (village == null || !session.Villagers.TryGet(vid, out var villager))
            {
                return "Village or villager not found.";
            }

            session.Villages.SyncCitizensForVillage(village);
            var assignResult = session.Villages.TryAssignJob(village, villager, job);
            return assignResult.Success
                ? $"Assigned {job}."
                : assignResult.PlayerMessage;
        }
    }
}


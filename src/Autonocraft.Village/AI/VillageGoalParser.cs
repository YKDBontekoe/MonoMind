using System.Text.RegularExpressions;
using Autonocraft.Domain.Village;
using Autonocraft.World;

namespace Autonocraft.Village
{
    public readonly struct ParsedVillageGoal
    {
        public VillageGoalKind Kind { get; init; }
        public string Description { get; init; }
        public BlockType? StockBlock { get; init; }
        public int TargetCount { get; init; }
        public string? BlueprintId { get; init; }
    }

    public static class VillageGoalParser
    {
        private static readonly Regex StockPattern = new(
            @"\bstock\s+(\d+)\s+([a-z_]+)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex BuildPattern = new(
            @"\bbuild\s+([a-z_ ]+)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static bool TryParseDescription(string description, out ParsedVillageGoal parsed)
        {
            parsed = default;
            if (string.IsNullOrWhiteSpace(description))
            {
                return false;
            }

            var stockMatch = StockPattern.Match(description);
            if (stockMatch.Success &&
                int.TryParse(stockMatch.Groups[1].Value, out int count) &&
                count > 0 &&
                TryParseBlockType(stockMatch.Groups[2].Value, out var blockType))
            {
                parsed = new ParsedVillageGoal
                {
                    Kind = VillageGoalKind.Stock,
                    Description = description.Trim(),
                    StockBlock = blockType,
                    TargetCount = count
                };
                return true;
            }

            var buildMatch = BuildPattern.Match(description);
            if (buildMatch.Success)
            {
                string raw = buildMatch.Groups[1].Value.Trim().Replace(' ', '_');
                if (TryResolveBlueprintId(raw, out string? blueprintId))
                {
                    parsed = new ParsedVillageGoal
                    {
                        Kind = VillageGoalKind.Build,
                        Description = description.Trim(),
                        BlueprintId = blueprintId
                    };
                    return true;
                }
            }

            return false;
        }

        public static bool TryParseBlockType(string raw, out BlockType blockType)
        {
            blockType = BlockType.Air;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string normalized = raw.Trim().Replace(' ', '_');
            return Enum.TryParse(normalized, ignoreCase: true, out blockType) && blockType != BlockType.Air;
        }

        private static bool TryResolveBlueprintId(string raw, out string? blueprintId)
        {
            blueprintId = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string normalized = raw.Trim().ToLowerInvariant().Replace(' ', '_');
            foreach (var blueprint in PlayerStructureRegistry.All)
            {
                if (string.Equals(blueprint.Id, normalized, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(blueprint.DisplayName.Replace(' ', '_'), normalized, StringComparison.OrdinalIgnoreCase))
                {
                    blueprintId = blueprint.Id;
                    return true;
                }
            }

            return PlayerStructureRegistry.TryGet(normalized, out _) && (blueprintId = normalized) != null;
        }
    }
}

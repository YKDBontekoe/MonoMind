using Autonocraft.Items;
using Autonocraft.Domain.Core;
using Autonocraft.World;

namespace Autonocraft.Crafting
{
    public readonly struct CraftInput
    {
        public BlockType? ExactBlock { get; init; }
        public MaterialTag? Tag { get; init; }
        public int Count { get; init; }

        public bool Matches(BlockType type)
        {
            if (ExactBlock.HasValue)
            {
                return type == ExactBlock.Value;
            }

            if (Tag == MaterialTag.Wood)
            {
                return type.IsAnyLog();
            }

            if (Tag == MaterialTag.Organic)
            {
                return type.IsAnyLeaves() || type.MatchesTag(MaterialTag.Organic);
            }

            if (Tag.HasValue)
            {
                return type.MatchesTag(Tag.Value);
            }

            return false;
        }
    }

    public sealed class CraftRecipe
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public BlockType StationType { get; init; }
        public IReadOnlyList<CraftInput> Inputs { get; init; } = Array.Empty<CraftInput>();
        public BlockType Output { get; init; }
        public int OutputCount { get; init; } = 1;
        public ItemKind OutputKind { get; init; } = ItemKind.Block;
        public ItemId OutputItem { get; init; } = ItemId.None;
        public ItemId InputFood { get; init; } = ItemId.None;
        public int InputFoodCount { get; init; } = 1;
        public bool RequiresUnlock { get; init; }
        public bool IsToolOutput => OutputKind == ItemKind.Tool && OutputItem != ItemId.None;
        public bool IsFoodOutput => OutputKind == ItemKind.Food && OutputItem != ItemId.None;
        public bool IsMaterialOutput => OutputKind == ItemKind.Material && OutputItem != ItemId.None;
        public bool IsFoodInput => InputFood != ItemId.None;
        public bool RequiresHeat { get; init; }
        public bool RequiresWater { get; init; }
        public TimePhase? RequiredTimePhase { get; init; }
        public BiomeType? RequiredBiome { get; init; }
        public IReadOnlyList<BiomeType>? AllowedBiomes { get; init; }

        /// <summary>Minimum grid size (2×2 player craft or 3×3 bench). Defaults to 2×2.</summary>
        public CraftGridSize GridSize { get; init; } = CraftGridSize.TwoByTwo;

        /// <summary>Optional shaped pattern rows (P=plank, L=log, S=stone, C=cobble, I=iron, G=gold).</summary>
        public IReadOnlyList<string>? ShapedPattern { get; init; }

        public bool EnvironmentMatches(CraftEnvironment env)
        {
            if (RequiresHeat && !env.HasAdjacentHeat && !env.HasFuelInInputs)
            {
                return false;
            }

            if (RequiresWater && !env.HasAdjacentWater)
            {
                return false;
            }

            if (RequiredTimePhase.HasValue && env.TimePhase != RequiredTimePhase.Value)
            {
                return false;
            }

            if (RequiredBiome.HasValue && env.Biome != RequiredBiome.Value)
            {
                return false;
            }

            if (AllowedBiomes != null && AllowedBiomes.Count > 0 && !AllowedBiomes.Contains(env.Biome))
            {
                return false;
            }

            return true;
        }

        public bool TryMatchInputs(IReadOnlyList<BlockType> slotTypes, out Dictionary<int, int> slotConsumption) =>
            TryMatchGrid(slotTypes, InferGridSize(slotTypes.Count), out slotConsumption);

        public bool TryMatchGrid(IReadOnlyList<BlockType> slotTypes, int gridDimension, out Dictionary<int, int> slotConsumption) =>
            TryMatchItemGrid(ToItemStacks(slotTypes), gridDimension, out slotConsumption);

        public bool TryMatchItemGrid(IReadOnlyList<ItemStack> slots, int gridDimension, out Dictionary<int, int> slotConsumption)
        {
            int activeSlots = gridDimension * gridDimension;
            if (ShapedPattern is { Count: > 0 })
            {
                return CraftPatternMatcher.TryMatch(ShapedPattern, gridDimension, slots, out slotConsumption);
            }

            return TryMatchShapelessItems(slots, activeSlots, out slotConsumption);
        }

        private static ItemStack[] ToItemStacks(IReadOnlyList<BlockType> slotTypes)
        {
            var stacks = new ItemStack[slotTypes.Count];
            for (int i = 0; i < slotTypes.Count; i++)
            {
                stacks[i] = slotTypes[i] == BlockType.Air
                    ? ItemStack.Empty
                    : ItemStack.CreateBlock(slotTypes[i], 1);
            }

            return stacks;
        }

        private static int InferGridSize(int slotCount) =>
            slotCount >= 9 ? 3 : 2;

        private bool TryMatchShapeless(IReadOnlyList<BlockType> slotTypes, int activeSlots, out Dictionary<int, int> slotConsumption)
        {
            slotConsumption = new Dictionary<int, int>();
            var slotUsed = new bool[activeSlots];

            foreach (var input in Inputs)
            {
                int needed = input.Count;
                for (int slot = 0; slot < activeSlots && needed > 0; slot++)
                {
                    if (slotUsed[slot] || slotTypes[slot] == BlockType.Air)
                    {
                        continue;
                    }

                    if (!input.Matches(slotTypes[slot]))
                    {
                        continue;
                    }

                    slotConsumption[slot] = 1;
                    slotUsed[slot] = true;
                    needed--;
                }

                if (needed > 0)
                {
                    slotConsumption.Clear();
                    return false;
                }
            }

            return true;
        }

        private bool TryMatchShapelessItems(IReadOnlyList<ItemStack> slots, int activeSlots, out Dictionary<int, int> slotConsumption)
        {
            slotConsumption = new Dictionary<int, int>();
            var slotUsed = new bool[activeSlots];

            foreach (var input in Inputs)
            {
                int needed = input.Count;
                for (int slot = 0; slot < activeSlots && needed > 0; slot++)
                {
                    if (slotUsed[slot] || slots[slot].IsEmpty)
                    {
                        continue;
                    }

                    if (!slots[slot].IsBlock() || !input.Matches(slots[slot].BlockType))
                    {
                        continue;
                    }

                    slotConsumption[slot] = 1;
                    slotUsed[slot] = true;
                    needed--;
                }

                if (needed > 0)
                {
                    slotConsumption.Clear();
                    return false;
                }
            }

            return true;
        }
    }
}

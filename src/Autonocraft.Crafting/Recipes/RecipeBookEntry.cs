namespace Autonocraft.Crafting
{
    public enum RecipeBookCraftability
    {
        Craftable,
        MissingMaterials,
        NeedsLargerGrid,
        NeedsEnvironment
    }

    public sealed class RecipeBookEntry
    {
        public CraftRecipe Recipe { get; init; } = null!;
        public string DisplayName { get; init; } = string.Empty;
        public string IngredientSummary { get; init; } = string.Empty;
        public string OutputSummary { get; init; } = string.Empty;
        public RecipeBookCraftability Craftability { get; init; }
        public string? MissingHint { get; init; }

        public bool IsCraftable => Craftability == RecipeBookCraftability.Craftable;
    }
}

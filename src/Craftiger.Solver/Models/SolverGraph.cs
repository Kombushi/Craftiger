namespace Craftiger.Solver.Models;

/// <summary>The recipe graph the solver works on, with producer and consumer indices built
/// once so every solve walks dictionaries rather than lists.</summary>
public sealed class SolverGraph
{
    private SolverGraph(
        IReadOnlyDictionary<string, SolverItem> items,
        IReadOnlyList<SolverRecipe> recipes,
        IReadOnlyDictionary<string, SolverRecipe> recipesById,
        IReadOnlyDictionary<string, IReadOnlyList<SolverRecipe>> producers,
        IReadOnlyDictionary<string, IReadOnlyList<SolverRecipe>> consumers)
    {
        Items = items;
        Recipes = recipes;
        RecipesById = recipesById;
        Producers = producers;
        Consumers = consumers;
    }

    public IReadOnlyDictionary<string, SolverItem> Items { get; }

    public IReadOnlyList<SolverRecipe> Recipes { get; }

    public IReadOnlyDictionary<string, SolverRecipe> RecipesById { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<SolverRecipe>> Producers { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<SolverRecipe>> Consumers { get; }

    /// <summary>An item absent from the item set is simply not a leaf.</summary>
    public bool IsLeaf(string itemId) =>
        Items.TryGetValue(itemId, out var item) && item.IsLeaf;

    public static SolverGraph Build(IEnumerable<SolverItem> items, IEnumerable<SolverRecipe> recipes)
    {
        var recipeList = recipes.ToList();
        var producers = new Dictionary<string, List<SolverRecipe>>();
        var consumers = new Dictionary<string, List<SolverRecipe>>();
        foreach (var recipe in recipeList)
        {
            foreach (var itemId in recipe.Outputs.Select(o => o.ItemId).Distinct())
            {
                Index(producers, itemId, recipe);
            }
            foreach (var itemId in recipe.Slots
                .SelectMany(slot => slot.Alternatives)
                .Select(a => a.ItemId)
                .Distinct())
            {
                Index(consumers, itemId, recipe);
            }
        }

        return new SolverGraph(
            items.ToDictionary(item => item.Id),
            recipeList,
            recipeList.ToDictionary(recipe => recipe.Id),
            producers.ToDictionary(p => p.Key, p => (IReadOnlyList<SolverRecipe>)p.Value),
            consumers.ToDictionary(c => c.Key, c => (IReadOnlyList<SolverRecipe>)c.Value));
    }

    private static void Index(Dictionary<string, List<SolverRecipe>> index, string itemId, SolverRecipe recipe)
    {
        if (!index.TryGetValue(itemId, out var list))
        {
            index[itemId] = list = [];
        }
        list.Add(recipe);
    }
}

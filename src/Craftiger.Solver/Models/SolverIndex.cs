namespace Craftiger.Solver.Models;

/// <summary>The recipe graph as integer positions and compressed-row arrays — the only form
/// the runtime keeps. A cold solve evaluates millions of recipes, and hashing long string ids
/// at every one of those steps was what made it slow; every reader, from the fixpoint to the
/// item detail, addresses items and recipes by position here and translates to ids only at
/// the edge. Built once, by <see cref="SolverIndexBuilder"/>.</summary>
public sealed class SolverIndex
{
    internal SolverIndex(
        string[] itemIds,
        IReadOnlyDictionary<string, int> itemIndex,
        string?[] leafClass,
        string[] recipeIds,
        IReadOnlyDictionary<string, int> recipeIndex,
        string[] machine,
        int[] tier,
        int[] multiTier,
        int[] heat,
        int[] slotStart,
        int[] alternativeStart,
        int[] alternativeItem,
        long[] alternativeAmount,
        int maxSlotCount,
        int[] outputStart,
        int[] outputItem,
        long[] outputAmount,
        double[] outputChance,
        double[] outputYield,
        int[] consumerStart,
        int[] consumerRecipe,
        int[] producerStart,
        int[] producerRecipe)
    {
        ItemIds = itemIds;
        ItemIndex = itemIndex;
        LeafClass = leafClass;
        RecipeIds = recipeIds;
        RecipeIndex = recipeIndex;
        Machine = machine;
        Tier = tier;
        MultiTier = multiTier;
        Heat = heat;
        SlotStart = slotStart;
        AlternativeStart = alternativeStart;
        AlternativeItem = alternativeItem;
        AlternativeAmount = alternativeAmount;
        MaxSlotCount = maxSlotCount;
        OutputStart = outputStart;
        OutputItem = outputItem;
        OutputAmount = outputAmount;
        OutputChance = outputChance;
        OutputYield = outputYield;
        ConsumerStart = consumerStart;
        ConsumerRecipe = consumerRecipe;
        ProducerStart = producerStart;
        ProducerRecipe = producerRecipe;
    }

    /// <summary>Item id per position: every leaf, then every id a recipe references.</summary>
    public string[] ItemIds { get; }

    public IReadOnlyDictionary<string, int> ItemIndex { get; }

    /// <summary>Leaf class per item position; null where the item is not a leaf.</summary>
    public string?[] LeafClass { get; }

    /// <summary>Recipe id per position, in graph order — the order the fixpoint evaluates.</summary>
    public string[] RecipeIds { get; }

    public IReadOnlyDictionary<string, int> RecipeIndex { get; }

    /// <summary>The machine (recipe map) per recipe, one shared string per map.</summary>
    public string[] Machine { get; }

    /// <summary>The tier a single block needs per recipe.</summary>
    public int[] Tier { get; }

    /// <summary>The tier the map's multiblock needs, or -1 where owning one lowers nothing.</summary>
    public int[] MultiTier { get; }

    /// <summary>The heat a coil-gated recipe needs, or -1.</summary>
    public int[] Heat { get; }

    /// <summary>Recipe <c>r</c> owns slots <c>SlotStart[r]</c> to <c>SlotStart[r + 1]</c>.</summary>
    public int[] SlotStart { get; }

    /// <summary>Slot <c>s</c> owns alternatives <c>AlternativeStart[s]</c> to <c>AlternativeStart[s + 1]</c>.</summary>
    public int[] AlternativeStart { get; }

    public int[] AlternativeItem { get; }

    /// <summary>Units for items, mB for fluids.</summary>
    public long[] AlternativeAmount { get; }

    /// <summary>The widest recipe's slot count — the size of a scratch pick buffer.</summary>
    public int MaxSlotCount { get; }

    /// <summary>Recipe <c>r</c> owns outputs <c>OutputStart[r]</c> to <c>OutputStart[r + 1]</c>.</summary>
    public int[] OutputStart { get; }

    public int[] OutputItem { get; }

    public long[] OutputAmount { get; }

    /// <summary>In (0, 1].</summary>
    public double[] OutputChance { get; }

    /// <summary>Expected units per run: amount × chance.</summary>
    public double[] OutputYield { get; }

    /// <summary>Item <c>i</c> is consumed by recipes <c>ConsumerRecipe[ConsumerStart[i]..ConsumerStart[i + 1])</c>,
    /// each once, in graph order.</summary>
    public int[] ConsumerStart { get; }

    public int[] ConsumerRecipe { get; }

    /// <summary>Item <c>i</c> is produced by recipes <c>ProducerRecipe[ProducerStart[i]..ProducerStart[i + 1])</c>,
    /// each once, in graph order.</summary>
    public int[] ProducerStart { get; }

    public int[] ProducerRecipe { get; }

    public int ItemCount => ItemIds.Length;

    public int RecipeCount => RecipeIds.Length;

    public bool IsLeaf(int item) => LeafClass[item] is not null;

    public bool TryGetItem(string itemId, out int item) => ItemIndex.TryGetValue(itemId, out item);

    public bool TryGetRecipe(string recipeId, out int recipe) => RecipeIndex.TryGetValue(recipeId, out recipe);

    /// <summary>Whether any recipe at all produces the item.</summary>
    public bool IsProduced(int item) => ProducerStart[item + 1] > ProducerStart[item];

    public int SlotCount(int recipe) => SlotStart[recipe + 1] - SlotStart[recipe];

    public int AlternativeCount(int recipe, int slot)
    {
        var s = SlotStart[recipe] + slot;
        return AlternativeStart[s + 1] - AlternativeStart[s];
    }

    /// <summary>The flat position of one alternative, indexing <see cref="AlternativeItem"/> and
    /// <see cref="AlternativeAmount"/>.</summary>
    public int AlternativeAt(int recipe, int slot, int alternative) => AlternativeStart[SlotStart[recipe] + slot] + alternative;

    public int OutputCount(int recipe) => OutputStart[recipe + 1] - OutputStart[recipe];

    /// <summary>The expected amount one run of the recipe yields of the item, summing chanced
    /// twin rows.</summary>
    public double Yield(int recipe, int item)
    {
        var yield = 0.0;
        for (var o = OutputStart[recipe]; o < OutputStart[recipe + 1]; o++)
        {
            if (OutputItem[o] == item)
            {
                yield += OutputYield[o];
            }
        }
        return yield;
    }

    /// <summary>The index of a graph given as records — the fixtures' way in; a loader streams
    /// rows through the builder instead.</summary>
    public static SolverIndex Build(IEnumerable<SolverItem> leaves, IEnumerable<SolverRecipe> recipes)
    {
        var builder = new SolverIndexBuilder(leaves);
        foreach (var recipe in recipes)
        {
            builder.BeginRecipe(recipe.Id, recipe.Machine, recipe.Tier, recipe.MultiTier, recipe.Heat);
            foreach (var slot in recipe.Slots)
            {
                builder.BeginSlot();
                foreach (var alternative in slot.Alternatives)
                {
                    builder.AddAlternative(alternative.ItemId, alternative.Amount);
                }
            }
            foreach (var output in recipe.Outputs)
            {
                builder.AddOutput(output.ItemId, output.Amount, output.Chance);
            }
        }
        return builder.Build();
    }
}

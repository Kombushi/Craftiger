namespace Craftiger.Solver.Models;

/// <summary>The recipe graph flattened to integer ids and compressed-row arrays, built once per
/// graph. A cold solve evaluates millions of recipes, and hashing long string ids at every one
/// of those steps was what made it slow; the fixpoint reads these arrays instead and only
/// translates back to ids when it hands out the table.</summary>
public sealed class SolverIndex
{
    private SolverIndex(
        string[] itemIds,
        IReadOnlyDictionary<string, int> itemIndex,
        string?[] leafClass,
        SolverRecipe[] recipes,
        IReadOnlyDictionary<string, int> recipeIndex,
        int[] slotStart,
        int[] alternativeStart,
        int[] alternativeItem,
        double[] alternativeAmount,
        int[] outputStart,
        int[] outputItem,
        double[] outputYield,
        int[] consumerStart,
        int[] consumerRecipe,
        int[] producerStart,
        int[] producerRecipe)
    {
        ItemIds = itemIds;
        ItemIndex = itemIndex;
        LeafClass = leafClass;
        Recipes = recipes;
        RecipeIndex = recipeIndex;
        SlotStart = slotStart;
        AlternativeStart = alternativeStart;
        AlternativeItem = alternativeItem;
        AlternativeAmount = alternativeAmount;
        OutputStart = outputStart;
        OutputItem = outputItem;
        OutputYield = outputYield;
        ConsumerStart = consumerStart;
        ConsumerRecipe = consumerRecipe;
        ProducerStart = producerStart;
        ProducerRecipe = producerRecipe;
    }

    /// <summary>Item id per index: every item of the graph plus every id a recipe references.</summary>
    public string[] ItemIds { get; }

    public IReadOnlyDictionary<string, int> ItemIndex { get; }

    /// <summary>Leaf class per item index; null where the item is not a leaf.</summary>
    public string?[] LeafClass { get; }

    /// <summary>Recipes in graph order; the recipe index is the position here.</summary>
    public SolverRecipe[] Recipes { get; }

    public IReadOnlyDictionary<string, int> RecipeIndex { get; }

    /// <summary>Recipe <c>r</c> owns slots <c>SlotStart[r]</c> to <c>SlotStart[r + 1]</c>.</summary>
    public int[] SlotStart { get; }

    /// <summary>Slot <c>s</c> owns alternatives <c>AlternativeStart[s]</c> to <c>AlternativeStart[s + 1]</c>.</summary>
    public int[] AlternativeStart { get; }

    public int[] AlternativeItem { get; }

    public double[] AlternativeAmount { get; }

    /// <summary>Recipe <c>r</c> owns outputs <c>OutputStart[r]</c> to <c>OutputStart[r + 1]</c>.</summary>
    public int[] OutputStart { get; }

    public int[] OutputItem { get; }

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

    /// <summary>The widest recipe's slot count — the size of a scratch pick buffer.</summary>
    public int MaxSlotCount { get; private set; }

    public bool IsLeaf(int item) => LeafClass[item] is not null;

    public bool TryGetItem(string itemId, out int item) => ItemIndex.TryGetValue(itemId, out item);

    public int SlotCount(int recipe) => SlotStart[recipe + 1] - SlotStart[recipe];

    public int AlternativeCount(int recipe, int slot)
    {
        var s = SlotStart[recipe] + slot;
        return AlternativeStart[s + 1] - AlternativeStart[s];
    }

    /// <summary>The flat position of one alternative, indexing <see cref="AlternativeItem"/> and
    /// <see cref="AlternativeAmount"/>.</summary>
    public int AlternativeAt(int recipe, int slot, int alternative) =>
        AlternativeStart[SlotStart[recipe] + slot] + alternative;

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

    public static SolverIndex Build(
        IReadOnlyDictionary<string, SolverItem> items, IReadOnlyList<SolverRecipe> recipeList)
    {
        var itemIndex = new Dictionary<string, int>(items.Count);
        var itemIds = new List<string>(items.Count);
        foreach (var id in items.Keys)
        {
            itemIndex[id] = itemIds.Count;
            itemIds.Add(id);
        }
        int IndexOf(string id)
        {
            if (!itemIndex.TryGetValue(id, out var index))
            {
                itemIndex[id] = index = itemIds.Count;
                itemIds.Add(id);
            }
            return index;
        }

        var recipes = recipeList.ToArray();
        var recipeIndex = new Dictionary<string, int>(recipes.Length);
        var slotStart = new int[recipes.Length + 1];
        var outputStart = new int[recipes.Length + 1];
        var alternativeStart = new List<int> { 0 };
        var alternativeItem = new List<int>();
        var alternativeAmount = new List<double>();
        var outputItem = new List<int>();
        var outputYield = new List<double>();
        var maxSlots = 0;
        for (var r = 0; r < recipes.Length; r++)
        {
            recipeIndex[recipes[r].Id] = r;
            foreach (var slot in recipes[r].Slots)
            {
                // Solved tables record the chosen alternative per slot as a ushort.
                if (slot.Alternatives.Count > ushort.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"recipe '{recipes[r].Id}' has a slot with {slot.Alternatives.Count} alternatives; at most {ushort.MaxValue} are supported");
                }
                foreach (var alternative in slot.Alternatives)
                {
                    alternativeItem.Add(IndexOf(alternative.ItemId));
                    alternativeAmount.Add(alternative.Amount);
                }
                alternativeStart.Add(alternativeItem.Count);
            }
            slotStart[r + 1] = alternativeStart.Count - 1;
            maxSlots = Math.Max(maxSlots, recipes[r].Slots.Count);
            foreach (var output in recipes[r].Outputs)
            {
                outputItem.Add(IndexOf(output.ItemId));
                outputYield.Add(output.Amount * output.Chance);
            }
            outputStart[r + 1] = outputItem.Count;
        }

        var leafClass = new string?[itemIds.Count];
        foreach (var (id, item) in items)
        {
            leafClass[itemIndex[id]] = item.LeafClass;
        }

        var (consumerStart, consumerRecipe) = Adjacency(
            itemIds.Count, recipes.Length, r => DistinctRange(alternativeItem, alternativeStart[slotStart[r]], alternativeStart[slotStart[r + 1]]));
        var (producerStart, producerRecipe) = Adjacency(
            itemIds.Count, recipes.Length, r => DistinctRange(outputItem, outputStart[r], outputStart[r + 1]));

        return new SolverIndex(
            itemIds.ToArray(), itemIndex, leafClass, recipes, recipeIndex,
            slotStart, alternativeStart.ToArray(), alternativeItem.ToArray(), alternativeAmount.ToArray(),
            outputStart, outputItem.ToArray(), outputYield.ToArray(),
            consumerStart, consumerRecipe, producerStart, producerRecipe)
        {
            MaxSlotCount = maxSlots,
        };
    }

    private static IEnumerable<int> DistinctRange(List<int> values, int start, int end)
    {
        var seen = new HashSet<int>();
        for (var i = start; i < end; i++)
        {
            if (seen.Add(values[i]))
            {
                yield return values[i];
            }
        }
    }

    /// <summary>Item → recipes in recipe order, as a compressed row layout: a counting pass
    /// sizes each row, a second pass fills it.</summary>
    private static (int[] Start, int[] Recipe) Adjacency(
        int itemCount, int recipeCount, Func<int, IEnumerable<int>> itemsOf)
    {
        var start = new int[itemCount + 1];
        for (var r = 0; r < recipeCount; r++)
        {
            foreach (var item in itemsOf(r))
            {
                start[item + 1]++;
            }
        }
        for (var i = 0; i < itemCount; i++)
        {
            start[i + 1] += start[i];
        }
        var fill = (int[])start.Clone();
        var recipe = new int[start[itemCount]];
        for (var r = 0; r < recipeCount; r++)
        {
            foreach (var item in itemsOf(r))
            {
                recipe[fill[item]++] = r;
            }
        }
        return (start, recipe);
    }
}

using System.Collections.Immutable;

namespace Craftiger.Solver.Models.Graph;

/// <summary>The recipe graph as integer positions over compressed-row arrays: every reader addresses items and recipes by position and translates to ids only at the edge.</summary>
public sealed record SolverIndex
{
    internal SolverIndex(
        ImmutableArray<string> itemIds,
        IReadOnlyDictionary<string, int> itemIndex,
        ImmutableArray<string?> leafClass,
        ImmutableArray<string> recipeIds,
        IReadOnlyDictionary<string, int> recipeIndex,
        ImmutableArray<string> machine,
        ImmutableArray<int> tier,
        ImmutableArray<int> multiTier,
        ImmutableArray<int> heat,
        ImmutableArray<int> toolSlots,
        ImmutableArray<int> slotStart,
        ImmutableArray<int> alternativeStart,
        ImmutableArray<int> alternativeItem,
        ImmutableArray<long> alternativeAmount,
        int maxSlotCount,
        ImmutableArray<int> outputStart,
        ImmutableArray<int> outputItem,
        ImmutableArray<long> outputAmount,
        ImmutableArray<double> outputChance,
        ImmutableArray<double> outputYield,
        ImmutableArray<int> consumerStart,
        ImmutableArray<int> consumerRecipe,
        ImmutableArray<int> producerStart,
        ImmutableArray<int> producerRecipe)
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
        ToolSlots = toolSlots;
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

    /// <summary>Item id per position: every leaf first, then every id a recipe references.</summary>
    public ImmutableArray<string> ItemIds { get; }

    public IReadOnlyDictionary<string, int> ItemIndex { get; }

    /// <summary>Leaf class per item position; null where the item is not a leaf.</summary>
    public ImmutableArray<string?> LeafClass { get; }

    /// <summary>Recipe id per position, in graph order — the order the fixpoint evaluates.</summary>
    public ImmutableArray<string> RecipeIds { get; }

    public IReadOnlyDictionary<string, int> RecipeIndex { get; }

    /// <summary>The machine (recipe map) per recipe, one shared string per map.</summary>
    public ImmutableArray<string> Machine { get; }

    /// <summary>The tier a single block needs per recipe.</summary>
    public ImmutableArray<int> Tier { get; }

    /// <summary>The tier the map's multiblock needs, or -1 where owning one lowers nothing.</summary>
    public ImmutableArray<int> MultiTier { get; }

    /// <summary>The heat a coil-gated recipe needs, or -1.</summary>
    public ImmutableArray<int> Heat { get; }

    /// <summary>Catalyst slots holding a wearing tool per recipe, read only to break exact ties.</summary>
    public ImmutableArray<int> ToolSlots { get; }

    /// <summary>Recipe r owns slots SlotStart[r] to SlotStart[r + 1].</summary>
    public ImmutableArray<int> SlotStart { get; }

    /// <summary>Slot s owns alternatives AlternativeStart[s] to AlternativeStart[s + 1].</summary>
    public ImmutableArray<int> AlternativeStart { get; }

    public ImmutableArray<int> AlternativeItem { get; }

    /// <summary>Units for items, mB for fluids.</summary>
    public ImmutableArray<long> AlternativeAmount { get; }

    /// <summary>The widest recipe's slot count — the size of a scratch pick buffer.</summary>
    public int MaxSlotCount { get; }

    /// <summary>Recipe r owns outputs OutputStart[r] to OutputStart[r + 1].</summary>
    public ImmutableArray<int> OutputStart { get; }

    public ImmutableArray<int> OutputItem { get; }

    public ImmutableArray<long> OutputAmount { get; }

    /// <summary>In (0, 1].</summary>
    public ImmutableArray<double> OutputChance { get; }

    /// <summary>Expected units per run: amount × chance.</summary>
    public ImmutableArray<double> OutputYield { get; }

    /// <summary>Item i is consumed by ConsumerRecipe[ConsumerStart[i]..ConsumerStart[i + 1]), each once, in graph order.</summary>
    public ImmutableArray<int> ConsumerStart { get; }

    public ImmutableArray<int> ConsumerRecipe { get; }

    /// <summary>Item i is produced by ProducerRecipe[ProducerStart[i]..ProducerStart[i + 1]), each once, in graph order.</summary>
    public ImmutableArray<int> ProducerStart { get; }

    public ImmutableArray<int> ProducerRecipe { get; }

    public int ItemCount => ItemIds.Length;

    public int RecipeCount => RecipeIds.Length;

    public bool IsLeaf(int item) => LeafClass[item] is not null;

    public bool TryGetItem(string itemId, out int item) => ItemIndex.TryGetValue(itemId, out item);

    public bool TryGetRecipe(string recipeId, out int recipe) => RecipeIndex.TryGetValue(recipeId, out recipe);

    /// <summary>Whether any recipe at all produces the item.</summary>
    public bool IsProduced(int item) => ProducerStart[item + 1] > ProducerStart[item];

    public int? MultiTierOf(int recipe) => MultiTier[recipe] < 0 ? null : MultiTier[recipe];

    public int? HeatOf(int recipe) => Heat[recipe] < 0 ? null : Heat[recipe];

    public int SlotCount(int recipe) => SlotStart[recipe + 1] - SlotStart[recipe];

    public int AlternativeCount(int recipe, int slot)
    {
        var s = SlotStart[recipe] + slot;
        return AlternativeStart[s + 1] - AlternativeStart[s];
    }

    /// <summary>The flat position of one alternative, indexing AlternativeItem and AlternativeAmount.</summary>
    public int AlternativeAt(int recipe, int slot, int alternative) => AlternativeStart[SlotStart[recipe] + slot] + alternative;

    /// <summary>The first flat alternative position of the recipe, over every slot.</summary>
    public int FirstAlternative(int recipe) => AlternativeStart[SlotStart[recipe]];

    /// <summary>One past the recipe's last flat alternative position.</summary>
    public int EndAlternative(int recipe) => AlternativeStart[SlotStart[recipe + 1]];

    public int OutputCount(int recipe) => OutputStart[recipe + 1] - OutputStart[recipe];

    /// <summary>The expected amount one run yields of the item, summing chanced twin rows.</summary>
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

    public bool Produces(int recipe, int item)
    {
        for (var o = OutputStart[recipe]; o < OutputStart[recipe + 1]; o++)
        {
            if (OutputItem[o] == item)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Whether the recipe yields the item on every run, not merely by chance.</summary>
    public bool ProducesDeterministically(int recipe, int item)
    {
        for (var o = OutputStart[recipe]; o < OutputStart[recipe + 1]; o++)
        {
            if (OutputItem[o] == item && OutputChance[o] >= 1)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Whether the slot lists the item among its alternatives.</summary>
    public bool SlotHolds(int recipe, int slot, int item)
    {
        for (var alt = 0; alt < AlternativeCount(recipe, slot); alt++)
        {
            if (AlternativeItem[AlternativeAt(recipe, slot, alt)] == item)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The index of a graph given as records — the fixtures' way in; a loader streams rows through the builder instead.</summary>
    public static SolverIndex Build(IEnumerable<SolverItem> leaves, IEnumerable<SolverRecipe> recipes)
    {
        var builder = new SolverIndexBuilder(leaves);
        foreach (var recipe in recipes)
        {
            builder.BeginRecipe(recipe.Id, recipe.Machine, recipe.Tier, recipe.MultiTier, recipe.Heat);
            for (var tool = 0; tool < recipe.ToolSlots; tool++)
            {
                builder.AddToolSlot();
            }
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

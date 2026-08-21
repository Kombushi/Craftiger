namespace Craftiger.Solver.Models;

/// <summary>Builds a <see cref="SolverIndex"/> row by row — a recipe, its slots and
/// alternatives, its outputs — so a loader can stream straight from its source without ever
/// materializing recipe objects. Items get positions in order of first sight, leaves first;
/// every id is kept once.</summary>
public sealed class SolverIndexBuilder
{
    private readonly Dictionary<string, int> _itemIndex = new();
    private readonly List<string> _itemIds = [];
    private readonly List<string?> _leafClass = [];
    private readonly List<string> _recipeIds = [];
    private readonly Dictionary<string, int> _recipeIndex = new();
    private readonly Dictionary<string, string> _machines = new();
    private readonly List<string> _machine = [];
    private readonly List<int> _tier = [];
    private readonly List<int> _multiTier = [];
    private readonly List<int> _heat = [];
    private readonly List<int> _slotStart = [0];
    private readonly List<int> _alternativeStart = [0];
    private readonly List<int> _alternativeItem = [];
    private readonly List<long> _alternativeAmount = [];
    private readonly List<int> _outputStart = [0];
    private readonly List<int> _outputItem = [];
    private readonly List<long> _outputAmount = [];
    private readonly List<double> _outputChance = [];
    private readonly List<double> _outputYield = [];
    private int _maxSlots;
    private bool _recipeOpen;
    private bool _slotOpen;
    private int _slotAlternatives;

    public SolverIndexBuilder(IEnumerable<SolverItem> leaves)
    {
        foreach (var leaf in leaves)
        {
            var item = Item(leaf.Id);
            _leafClass[item] = leaf.LeafClass;
        }
    }

    /// <summary>The position of an item, assigned on first sight.</summary>
    private int Item(string itemId)
    {
        if (!_itemIndex.TryGetValue(itemId, out var item))
        {
            item = _itemIds.Count;
            _itemIndex[itemId] = item;
            _itemIds.Add(itemId);
            _leafClass.Add(null);
        }
        return item;
    }

    /// <summary>Opens the next recipe, closing the previous one; recipes take the positions in
    /// the order they are opened.</summary>
    public void BeginRecipe(string id, string machine, int tier, int? multiTier, int? heat)
    {
        CloseRecipe();
        if (!_recipeIndex.TryAdd(id, _recipeIds.Count))
        {
            throw new InvalidOperationException($"recipe '{id}' was added twice");
        }
        _recipeIds.Add(id);
        if (!_machines.TryGetValue(machine, out var shared))
        {
            _machines[machine] = shared = machine;
        }
        _machine.Add(shared);
        _tier.Add(tier);
        _multiTier.Add(multiTier ?? -1);
        _heat.Add(heat ?? -1);
        _recipeOpen = true;
    }

    /// <summary>Opens the next input slot of the open recipe.</summary>
    public void BeginSlot()
    {
        if (!_recipeOpen)
        {
            throw new InvalidOperationException("a slot needs an open recipe");
        }
        CloseSlot();
        _slotOpen = true;
    }

    public void AddAlternative(string itemId, long amount)
    {
        if (!_slotOpen)
        {
            throw new InvalidOperationException("an alternative needs an open slot");
        }
        // Solved tables record the chosen alternative per slot as a ushort.
        if (++_slotAlternatives > ushort.MaxValue)
        {
            throw new InvalidOperationException(
                $"recipe '{_recipeIds[^1]}' has a slot with more than {ushort.MaxValue} alternatives");
        }
        _alternativeItem.Add(Item(itemId));
        _alternativeAmount.Add(amount);
    }

    public void AddOutput(string itemId, long amount, double chance)
    {
        if (!_recipeOpen)
        {
            throw new InvalidOperationException("an output needs an open recipe");
        }
        CloseSlot();
        _outputItem.Add(Item(itemId));
        _outputAmount.Add(amount);
        _outputChance.Add(chance);
        _outputYield.Add(amount * chance);
    }

    public SolverIndex Build()
    {
        CloseRecipe();
        var itemCount = _itemIds.Count;
        var recipeCount = _recipeIds.Count;
        var alternativeItem = _alternativeItem.ToArray();
        var alternativeStart = _alternativeStart.ToArray();
        var slotStart = _slotStart.ToArray();
        var outputItem = _outputItem.ToArray();
        var outputStart = _outputStart.ToArray();
        var (consumerStart, consumerRecipe) = Adjacency(
            itemCount, recipeCount, r => DistinctRange(alternativeItem, alternativeStart[slotStart[r]], alternativeStart[slotStart[r + 1]]));
        var (producerStart, producerRecipe) = Adjacency(
            itemCount, recipeCount, r => DistinctRange(outputItem, outputStart[r], outputStart[r + 1]));

        return new SolverIndex(
            [.. _itemIds],
            _itemIndex,
            [.. _leafClass],
            [.. _recipeIds],
            _recipeIndex,
            [.. _machine],
            [.. _tier],
            [.. _multiTier],
            [.. _heat],
            slotStart,
            alternativeStart,
            alternativeItem,
            [.. _alternativeAmount],
            _maxSlots,
            outputStart,
            outputItem,
            [.. _outputAmount],
            [.. _outputChance],
            [.. _outputYield],
            consumerStart,
            consumerRecipe,
            producerStart,
            producerRecipe);
    }

    private void CloseSlot()
    {
        if (_slotOpen)
        {
            _alternativeStart.Add(_alternativeItem.Count);
            _slotOpen = false;
            _slotAlternatives = 0;
        }
    }

    private void CloseRecipe()
    {
        if (!_recipeOpen)
        {
            return;
        }
        CloseSlot();
        var slots = _alternativeStart.Count - 1;
        _maxSlots = Math.Max(_maxSlots, slots - _slotStart[^1]);
        _slotStart.Add(slots);
        _outputStart.Add(_outputItem.Count);
        _recipeOpen = false;
    }

    private static IEnumerable<int> DistinctRange(int[] values, int start, int end)
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
    private static (int[] Start, int[] Recipe) Adjacency(int itemCount, int recipeCount, Func<int, IEnumerable<int>> itemsOf)
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

using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Models.Eras;

/// <summary>The era solve's working state: seeds are fixed first, drops may lower them, and recipes then only ever lower what is not a seed.</summary>
public sealed class EraTable
{
    private readonly Dictionary<string, int> _era = new();
    private readonly Dictionary<string, PlannerRecipe> _best = new();
    private readonly HashSet<string> _seeds = new();

    public int Count => _era.Count;

    public int SeedCount => _seeds.Count;

    public IReadOnlyDictionary<string, int> Eras => _era;

    public bool TryGetEra(string itemId, out int era) => _era.TryGetValue(itemId, out era);

    public bool IsReachable(string itemId) => _era.ContainsKey(itemId);

    public bool IsSeed(string itemId) => _seeds.Contains(itemId);

    /// <summary>Fixes a world-origin item at an era recipes can never raise; the first seed of an id wins.</summary>
    public void Seed(string itemId, int era)
    {
        if (_era.TryAdd(itemId, era))
        {
            _seeds.Add(itemId);
        }
    }

    /// <summary>Lowers an item's era without a recipe behind it, as a mined drop does.</summary>
    public void Lower(string itemId, int era)
    {
        if (!_era.TryGetValue(itemId, out var current) || era < current)
        {
            _era[itemId] = era;
        }
    }

    /// <summary>Records a recipe reaching the item at an era, unless the item is a seed or already cheaper.</summary>
    public bool Reach(string itemId, int era, PlannerRecipe via)
    {
        if (_seeds.Contains(itemId) || (_era.TryGetValue(itemId, out var current) && current <= era))
        {
            return false;
        }
        _era[itemId] = era;
        _best[itemId] = via;
        return true;
    }

    /// <summary>The lowest era among the ids, or int.MaxValue while none is reachable.</summary>
    public int CheapestEra(IEnumerable<string> itemIds)
    {
        var cheapest = int.MaxValue;
        foreach (var itemId in itemIds)
        {
            if (_era.TryGetValue(itemId, out var era) && era < cheapest)
            {
                cheapest = era;
            }
        }
        return cheapest;
    }

    public EraSolve ToSolve(
        IReadOnlyDictionary<string, int> tiers,
        IReadOnlyDictionary<string, int?> machineEras,
        PlannerEnvironment environment,
        IReadOnlyList<LadderCoil> coils) =>
        new(tiers, _era, _best, _seeds, machineEras, environment, coils);
}

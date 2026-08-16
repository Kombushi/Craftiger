using Craftiger.Solver.Models;
using Craftiger.Solver.Services;

namespace Craftiger.Solver.UnitTests;

/// <summary>Terse factories for hand-built solver fixtures.</summary>
internal static class Fx
{
    public static readonly GarageRules Rules = new(
        AlwaysOwnedMachines: new HashSet<string> { "Crafting Table", "Furnace", "Mining" },
        HeatExemptMachines: new HashSet<string> { "Helioflux Melting Core" },
        HeatBonusMachines: new HashSet<string> { "Blast Furnace" });

    public static readonly SolverPreferences Preferences = new(
        DeprioritizedLeafClasses: new HashSet<string> { "dust", "dust_small", "dust_tiny" });

    public static SolverItem Leaf(
        string id, int? tier = null, double? weight = null, string? parent = null,
        double divisor = 1, string leafClass = "ingot") =>
        new(id, leafClass, tier, weight, parent is null ? null : new ItemParentLink(parent, divisor));

    public static SolverRecipe Recipe(
        string id, string machine = "Crafting Table", int tier = 0, int? multiTier = null,
        int? heat = null,
        (string ItemId, long Amount)[]? inputs = null,
        (string ItemId, long Amount)[][]? slots = null,
        params (string ItemId, long Amount, double Chance)[] outputs)
    {
        var slotList = new List<SolverSlot>();
        foreach (var (itemId, amount) in inputs ?? [])
        {
            slotList.Add(new SolverSlot([new SolverStack(itemId, amount)]));
        }
        foreach (var alternatives in slots ?? [])
        {
            slotList.Add(new SolverSlot(
                alternatives.Select(a => new SolverStack(a.ItemId, a.Amount)).ToList()));
        }
        return new SolverRecipe(
            id, machine, tier, multiTier, heat, slotList,
            outputs.Select(o => new SolverOutput(o.ItemId, o.Amount, o.Chance)).ToList());
    }

    public static SolverGraph Graph(IEnumerable<SolverItem> items, params SolverRecipe[] recipes) =>
        SolverGraph.Build(items, recipes);

    public static Garage Garage(
        int defaultTier = 0,
        Dictionary<string, int?>? tiers = null,
        IEnumerable<string>? built = null,
        Dictionary<string, int>? coils = null) =>
        new(defaultTier, tiers ?? new(), (built ?? []).ToHashSet(), coils ?? new());

    public static WeightSettings Weights(double b = 4, Dictionary<string, double>? items = null) =>
        new(b, items ?? new());

    public static GarageLegalityService Legality() => new(Rules);

    public static CostSolverService Solver() => new(new LeafWeightService(), Legality(), Preferences);

    public static BomService Bom() => new(Legality());
}

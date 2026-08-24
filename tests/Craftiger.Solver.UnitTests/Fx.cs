using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;
using Craftiger.Solver.Services;

namespace Craftiger.Solver.UnitTests;

/// <summary>Terse factories for hand-built solver fixtures.</summary>
internal static class Fx
{
    public static readonly GarageRules Rules = new(
        AlwaysOwnedMachines: new HashSet<string> { "Crafting Table", "Furnace", "Mining" },
        HeatExemptMachines: new HashSet<string> { "Helioflux Melting Core" },
        HeatBonusMachines: new HashSet<string> { "Electric Blast Furnace" });

    public static readonly SolverPreferences Preferences = new(
        LeafClassPriority: ["ingot", "gem", "dust", "nugget", "dust_small", "dust_tiny"]);

    public static SolverItem Leaf(
        string id, int? tier = null, double? weight = null, string? parent = null,
        double divisor = 1, string leafClass = "ingot") =>
        new(id, leafClass, tier, weight, parent is null ? null : new ItemParentLink(parent, divisor));

    public static SolverRecipe Recipe(
        string id, string machine = "Crafting Table", int tier = 0, int? multiTier = null,
        int? heat = null, int toolSlots = 0,
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
            outputs.Select(o => new SolverOutput(o.ItemId, o.Amount, o.Chance)).ToList(),
            toolSlots);
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

    /// <summary>Whether the garage can run a single recipe, indexed on its own.</summary>
    public static bool Legal(SolverRecipe recipe, Garage garage) =>
        Legality().IsLegal(Graph([], recipe).Index, 0, garage);

    public static CostSolverService Solver() => new(new LeafWeightService(), Legality(), Preferences);

    public static BomService Bom() => new(Legality());

    public static PipelineSolverService Pipeline(ILinearProgramSolver lp) =>
        new(new LeafWeightService(), Legality(), Solver(), lp);

    public static CostTable Costs(SolverGraph graph, Garage? garage = null, WeightSettings? weights = null) =>
        Solver().Solve(graph, garage ?? Garage(), weights ?? Weights());

    public static FactoryRecipeData Data(
        SolverGraph graph, Dictionary<string, (long DurationTicks, long EuT, long Amps)>? recipes = null) =>
        FactoryRecipeData.Build(graph.Index, recipes);

    public static FactoryMachineData Machines(
        Dictionary<string, IReadOnlyList<FactoryMachineBlock>> blocks,
        IReadOnlyList<FactoryCoil>? coils = null,
        IReadOnlyList<FactoryFuel>? fuels = null) =>
        new(blocks, coils ?? [], fuels ?? []);

    public static FactoryMachineBlock Block(
        string itemId, int? tier = null, bool multiblock = false, bool steam = false, int? era = 0,
        long maxParallel = 1, params FactoryMachineBonus[] bonuses) =>
        new(itemId, tier, multiblock, steam, era, maxParallel, bonuses);

    public static FactorySeedData Seeds(params (string ItemId, string Kind)[] seeds) =>
        seeds.Length == 0
            ? FactorySeedData.Empty
            : new(seeds.ToDictionary(s => s.ItemId, s => s.Kind));

    public static FactoryRequest Request(
        (string ItemId, double Rate)[] produce,
        FactoryObjective[]? priority = null,
        Dictionary<string, string>? pins = null,
        bool mobFarms = false) =>
        new(
            produce.Select(p => new FactoryTarget(FactoryTargetKind.Produce, p.ItemId, p.Rate)).ToList(),
            priority ?? [],
            pins ?? new(),
            mobFarms);
}

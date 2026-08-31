using Craftiger.Solver.Interfaces.Lp;
using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using Craftiger.Solver.Models.Options;
using Craftiger.Solver.Services.Bom;
using Craftiger.Solver.Services.Costs;
using Craftiger.Solver.Services.Factory;
using Microsoft.Extensions.Options;

namespace Craftiger.Solver.UnitTests;

/// <summary>Terse factories for hand-built solver fixtures and the services composed over them.</summary>
internal static class Fx
{
    public static readonly GarageRules Rules = new()
    {
        AlwaysOwnedMachines = ["Crafting Table", "Furnace", "Mining"],
        HeatExemptMachines = ["Helioflux Melting Core"],
        HeatBonusMachines = ["Electric Blast Furnace"],
    };

    public static readonly SolverPreferences Preferences = new()
    {
        LeafClassPriority = ["ingot", "gem", "dust", "nugget", "dust_small", "dust_tiny"],
    };

    public static SolverItem Leaf(
        string id, int? tier = null, double? weight = null, string? parent = null,
        double divisor = 1, string leafClass = "ingot") =>
        new(id, leafClass, tier, weight, parent is null ? null : new ItemParentLink(parent, divisor));

    public static SolverRecipe Recipe(
        string id, string machine = "Crafting Table", int tier = 0, int? multiTier = null,
        int? heat = null, int toolSlots = 0, RecipeScope scope = RecipeScope.None,
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
            toolSlots, scope);
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

    public static GarageLegalityService Legality() => new(Options.Create(Rules));

    /// <summary>Whether the garage can run a single recipe, indexed on its own.</summary>
    public static bool Legal(SolverRecipe recipe, Garage garage) =>
        Legality().IsLegal(Graph([], recipe).Index, 0, garage);

    public static CostSolverService Solver()
    {
        var options = Options.Create(new CostSolverOptions());
        return new(
            new LeafWeightService(), Legality(),
            new RoutePreferenceService(Options.Create(Preferences), options), options);
    }

    public static BomService Bom()
    {
        var options = Options.Create(new BomOptions());
        var graph = new ChosenEdgeGraphService(options);
        return new(Legality(), graph, new LoopSeedService(Legality(), graph), options);
    }

    public static FactorySolverService Factory(ILinearProgramSolver lp)
    {
        var options = Options.Create(new FactorySolverOptions());
        var legality = Legality();
        return new(
            new FactoryTargetService(),
            new GeneratorCatalogService(options),
            new CandidateWalkService(legality, options),
            new FactoryModelService(new LeafWeightService(), new RunVariantService(legality), options),
            new AutoInfiniteService(legality),
            new FactoryDiagnosisService(lp, options),
            new FactoryPlanInterpreter(options),
            lp);
    }

    public static CostTable Costs(SolverGraph graph, Garage? garage = null, WeightSettings? weights = null) =>
        Solver().Solve(graph, garage ?? Garage(), weights ?? Weights());

    /// <summary>A factory context over the graph, its cost table solved on the way in.</summary>
    public static FactoryContext Context(
        SolverGraph graph,
        Dictionary<string, (long DurationTicks, long EuT, long Amps)>? data = null,
        FactoryMachineData? machines = null,
        FactorySeedData? seeds = null,
        Garage? garage = null,
        WeightSettings? weights = null,
        FactoryEnvironment? environment = null)
    {
        garage ??= Garage();
        weights ??= Weights();
        return new FactoryContext(
            graph, Data(graph, data), machines ?? FactoryMachineData.Empty, seeds ?? FactorySeedData.Empty,
            FactorySteamRules.Empty with { SteamFluidIds = ["f~IC2~ic2steam", "f~Railcraft~steam"] },
            environment ?? FactoryEnvironment.None,
            Costs(graph, garage, weights), garage, weights);
    }

    public static FactoryRecipeData Data(
        SolverGraph graph,
        Dictionary<string, (long DurationTicks, long EuT, long Amps)>? recipes = null,
        IEnumerable<string>? treeFarms = null) =>
        FactoryRecipeData.Build(graph.Index, recipes, treeFarms);

    public static FactoryMachineData Machines(
        Dictionary<string, IReadOnlyList<FactoryMachineBlock>> blocks,
        IReadOnlyList<FactoryCoil>? coils = null,
        IReadOnlyList<FactoryFuel>? fuels = null,
        IReadOnlyList<FactoryRotorStats>? rotors = null,
        IReadOnlyList<FactoryDynamo>? dynamos = null) =>
        new(blocks, coils ?? [], fuels ?? [], rotors ?? [], dynamos ?? []);

    public static FactoryMachineBlock Block(
        string itemId, int? tier = null, bool multiblock = false, bool steam = false, int? era = 0,
        long maxParallel = 1, string? rotorFuel = null, params FactoryMachineBonus[] bonuses) =>
        new(itemId, tier, multiblock, steam, era, maxParallel, bonuses, RotorFuel: rotorFuel);

    public static FactorySeedData Seeds(params (string ItemId, SeedKind Kind)[] seeds) =>
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

    public static FactoryRequest Targets(params FactoryTarget[] targets) =>
        new(targets, [], new Dictionary<string, string>());
}

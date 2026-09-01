using Craftiger.Solver.Highs.Models;
using Craftiger.Solver.Highs.Services;
using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Graph;
using Craftiger.Solver.Models.Options;
using Craftiger.Solver.Services.Costs;
using Craftiger.Solver.Services.Factory;
using Microsoft.Extensions.Options;

namespace Craftiger.Solver.Highs.UnitTests;

/// <summary>Factory solves over hand-built fixtures, composed over the real HiGHS adapter.</summary>
internal static class FactoryHarness
{
    public const double Tolerance = 1e-6;

    private static readonly GarageRules Rules = new() { AlwaysOwnedMachines = ["Crafting Table"] };

    private static readonly SolverPreferences Preferences = new()
    {
        LeafClassPriority = ["ingot", "gem", "dust", "nugget", "dust_small", "dust_tiny"],
    };

    public static HighsLinearProgramSolver Highs()
    {
        var highsOptions = Options.Create(new HighsOptions());
        return new(new HighsModelLoader(highsOptions), new LexicographicLayerRunner(highsOptions), highsOptions);
    }

    public static SolverItem Leaf(string id, double? weight = null) => new(id, "dust", null, weight, null);

    public static SolverRecipe Recipe(
        string id,
        (string ItemId, long Amount)[]? inputs = null,
        (string ItemId, long Amount)[][]? slots = null,
        int tier = 0,
        string machine = "Crafting Table",
        RecipeScope scope = RecipeScope.None,
        params (string ItemId, long Amount, double Chance)[] outputs)
    {
        var slotList = new List<SolverSlot>();
        foreach (var (itemId, amount) in inputs ?? [])
        {
            slotList.Add(new SolverSlot([new SolverStack(itemId, amount)]));
        }
        foreach (var alternatives in slots ?? [])
        {
            slotList.Add(new SolverSlot(alternatives.Select(a => new SolverStack(a.ItemId, a.Amount)).ToList()));
        }
        return new SolverRecipe(
            id, machine, tier, null, null, slotList,
            outputs.Select(o => new SolverOutput(o.ItemId, o.Amount, o.Chance)).ToList(),
            Scope: scope);
    }

    public static FactoryPlan Solve(
        SolverGraph graph,
        FactoryRequest request,
        Dictionary<string, (long DurationTicks, long EuT, long Amps)>? data = null,
        FactoryMachineData? machines = null,
        int garageTier = 0,
        FactorySeedData? seeds = null,
        IEnumerable<string>? treeFarms = null,
        IReadOnlyDictionary<string, OverclockMode>? overclocks = null,
        IEnumerable<string>? cleanroom = null,
        IEnumerable<string>? lowGravity = null,
        FactoryEnvironment? environment = null)
    {
        var legality = new GarageLegalityService(Options.Create(Rules));
        var costOptions = Options.Create(new CostSolverOptions());
        var costSolver = new CostSolverService(
            new LeafWeightService(), legality,
            new RoutePreferenceService(Options.Create(Preferences), costOptions), costOptions);
        var options = Options.Create(new FactorySolverOptions());
        var lp = Highs();
        var service = new FactorySolverService(
            new FactoryTargetService(),
            new GeneratorCatalogService(options),
            new CandidateWalkService(legality, options),
            new FactoryModelService(new LeafWeightService(), new RunVariantService(legality), options),
            new AutoInfiniteService(legality),
            new FactoryDiagnosisService(lp, options),
            new FactoryPlanInterpreter(options),
            legality,
            lp);
        var garage = new Garage(garageTier, new Dictionary<string, int?>(), new HashSet<string>(), new Dictionary<string, int>());
        var weights = new WeightSettings(4, new Dictionary<string, double>());
        var context = new FactoryContext(
            graph,
            FactoryRecipeData.Build(graph.Index, data, treeFarms, overclocks, cleanroom, lowGravity),
            machines ?? FactoryMachineData.Empty,
            seeds ?? FactorySeedData.Empty,
            new FactorySteamRules(["f~IC2~ic2steam", "f~Railcraft~steam"], "f~IC2~ic2distilledwater", 0.5, 160),
            environment ?? FactoryEnvironment.None,
            costSolver.Solve(graph, garage, weights),
            garage,
            weights);
        return service.Solve(context, request);
    }

    public static FactoryRequest Produce(
        (string ItemId, double Rate)[] targets,
        FactoryObjective[]? priority = null,
        Dictionary<string, string>? pins = null,
        bool mobFarms = false,
        bool bredSeeds = false,
        FactoryStep[]? steps = null,
        string[]? supplies = null) =>
        new(
            targets.Select(t => new FactoryTarget(FactoryTargetKind.Produce, t.ItemId, t.Rate)).ToList(),
            priority ?? [],
            pins ?? new Dictionary<string, string>(),
            mobFarms,
            bredSeeds,
            Steps: steps,
            Supplies: supplies);

    public static FactoryRequest Energy(
        double euT, int? generatorTier = null, FactoryObjective[]? priority = null, FactoryStep[]? steps = null) =>
        new(
            [new FactoryTarget(FactoryTargetKind.Energy, null, euT, generatorTier)], priority ?? [],
            new Dictionary<string, string>(), Steps: steps);

    public static FactoryRequest Consume(string itemId, double rate) =>
        new([new FactoryTarget(FactoryTargetKind.Consume, itemId, rate)], [], new Dictionary<string, string>());

    public static FactoryMachineData Generators(
        string map,
        FactoryMachineBlock block,
        FactoryFuel fuel,
        FactoryDynamo[]? dynamos = null,
        GeneratorMode[]? modes = null) =>
        new(
            new Dictionary<string, IReadOnlyList<FactoryMachineBlock>> { [map] = [block] },
            [], [fuel], [], dynamos ?? [],
            modes is null
                ? null
                : new Dictionary<string, IReadOnlyList<GeneratorMode>> { [block.ItemId] = modes });

    public static FactoryMachineData Turbines(
        FactoryMachineBlock block,
        FactoryDynamo dynamo,
        params FactoryRotorStats[] rotors) =>
        new(
            new Dictionary<string, IReadOnlyList<FactoryMachineBlock>> { ["Gas Turbine Fuel"] = [block] },
            [],
            [new FactoryFuel("Gas Turbine Fuel", "benzene", 1, 360, null, null)],
            rotors,
            [dynamo]);

    public static FactoryRotorStats Rotor(
        string itemId,
        double efficiency, double flow, double looseEfficiency = 0.4, double looseFlow = 0) =>
        new(
            itemId, "GAS", efficiency, looseEfficiency, flow, looseFlow,
            efficiency * flow, looseEfficiency * looseFlow);

    /// <summary>The layer corridor legitimately leaves a sub-percent sliver on a losing route; the winner must carry effectively all of the flow.</summary>
    public static FactoryLine Dominant(FactoryPlan plan)
    {
        var dominant = plan.Lines.MaxBy(line => line.RunsPerSecond)!;
        Assert.True(dominant.RunsPerSecond >= plan.Lines.Sum(line => line.RunsPerSecond) * 0.99);
        return dominant;
    }
}

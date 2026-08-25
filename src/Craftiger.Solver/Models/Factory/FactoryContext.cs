using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Models.Factory;

/// <summary>Everything a factory solve reads besides the request: the artifact's graph and rate data, the user's garage and weights, and the cost table solved for them.</summary>
public sealed record FactoryContext
{
    public FactoryContext(
        SolverGraph graph,
        FactoryRecipeData recipes,
        FactoryMachineData machines,
        FactorySeedData seeds,
        FactorySteamRules steam,
        CostTable costs,
        Garage garage,
        WeightSettings weights)
    {
        if (!ReferenceEquals(graph.Index, costs.Index))
        {
            throw new ArgumentException("the cost table was solved on a different graph", nameof(costs));
        }
        Graph = graph;
        Recipes = recipes;
        Machines = machines;
        Seeds = seeds;
        Steam = steam;
        Costs = costs;
        Garage = garage;
        Weights = weights;
    }

    public SolverGraph Graph { get; }

    public FactoryRecipeData Recipes { get; }

    public FactoryMachineData Machines { get; }

    public FactorySeedData Seeds { get; }

    public FactorySteamRules Steam { get; }

    public CostTable Costs { get; }

    public Garage Garage { get; }

    public WeightSettings Weights { get; }

    public SolverIndex Index => Graph.Index;

    /// <summary>The steam fluids the index knows, in the artifact's order.</summary>
    public IEnumerable<int> SteamItems()
    {
        foreach (var id in Steam.SteamFluidIds)
        {
            if (Index.TryGetItem(id, out var item))
            {
                yield return item;
            }
        }
    }

    /// <summary>The distilled water position, when the artifact names it and the index knows it.</summary>
    public int? DistilledWaterItem() =>
        Steam.DistilledWaterId is { } id && Index.TryGetItem(id, out var item) ? item : null;
}

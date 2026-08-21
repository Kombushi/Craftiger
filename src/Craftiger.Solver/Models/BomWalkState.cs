namespace Craftiger.Solver.Models;

/// <summary>Everything one BOM walk accumulates, shared by the item and loop expansions.</summary>
internal sealed record BomWalkState(
    SolverGraph Graph, CostTable Costs, Garage Garage, Dictionary<string, SolverRecipe> Pins,
    HashSet<string> Roots, Dictionary<string, double> Demand, Dictionary<string, long> WholeDemand,
    List<BomWarning> Warnings)
{
    public Dictionary<string, (double Amount, long Whole)> Leaves { get; } = new();

    public List<BomNode> Nodes { get; } = [];

    public int Loops { get; set; }

    public void Add(string itemId, double amount, long whole)
    {
        Demand[itemId] = Demand.GetValueOrDefault(itemId) + amount;
        WholeDemand[itemId] = WholeDemand.GetValueOrDefault(itemId) + whole;
    }
}

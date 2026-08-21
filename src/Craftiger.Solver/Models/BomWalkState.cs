namespace Craftiger.Solver.Models;

/// <summary>Everything one BOM walk accumulates, shared by the item and loop expansions. Items
/// are index positions; a target the index does not know gets a position past the end and its
/// id in <paramref name="ExtraIds"/>, so it walks like any other unproducible item.</summary>
internal sealed record BomWalkState(
    SolverIndex Index,
    CostTable Costs,
    Garage Garage,
    Dictionary<int, int> Pins,
    HashSet<int> Roots,
    Dictionary<int, double> Demand,
    Dictionary<int, long> WholeDemand,
    List<BomWarning> Warnings,
    IReadOnlyList<string> ExtraIds)
{
    public Dictionary<int, (double Amount, long Whole)> Leaves { get; } = new();

    public List<BomNode> Nodes { get; } = [];

    public int Loops { get; set; }

    public void Add(int item, double amount, long whole)
    {
        Demand[item] = Demand.GetValueOrDefault(item) + amount;
        WholeDemand[item] = WholeDemand.GetValueOrDefault(item) + whole;
    }

    public string IdOf(int item) => item < Index.ItemCount ? Index.ItemIds[item] : ExtraIds[item - Index.ItemCount];
}

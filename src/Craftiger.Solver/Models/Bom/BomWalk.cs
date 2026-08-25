using Craftiger.Solver.Models.Costs;

namespace Craftiger.Solver.Models.Bom;

/// <summary>Everything one BOM walk accumulates, shared by the item and loop expansions; demands are index positions.</summary>
public sealed class BomWalk(
    BomItems items,
    CostTable costs,
    BomPins pins,
    IReadOnlySet<int> roots,
    Dictionary<int, double> demand,
    Dictionary<int, long> wholeDemand,
    List<BomWarning> warnings)
{
    private readonly Dictionary<int, (double Amount, long Whole)> _leaves = new();
    private readonly List<BomNode> _nodes = [];
    private int _loops;

    public BomItems Items { get; } = items;

    public CostTable Costs { get; } = costs;

    public BomPins Pins { get; } = pins;

    public IReadOnlySet<int> Roots { get; } = roots;

    public IReadOnlyList<BomNode> Nodes => _nodes;

    public IReadOnlyDictionary<int, (double Amount, long Whole)> Leaves => _leaves;

    public double Demanded(int item) => demand.GetValueOrDefault(item);

    public long WholeDemanded(int item) => wholeDemand.GetValueOrDefault(item);

    public void Add(int item, double amount, long whole)
    {
        demand[item] = Demanded(item) + amount;
        wholeDemand[item] = WholeDemanded(item) + whole;
    }

    public void AddLeaf(int item, double amount, long whole)
    {
        var (current, currentWhole) = _leaves.GetValueOrDefault(item);
        _leaves[item] = (current + amount, currentWhole + whole);
    }

    public void AddNode(BomNode node) => _nodes.Add(node);

    public void Warn(BomWarning warning) => warnings.Add(warning);

    /// <summary>The number of the next loop to expand.</summary>
    public int NextLoop() => _loops++;

    public string IdOf(int item) => Items.IdOf(item);

    /// <summary>The recipe's input stacks under the given picks, by id, for a chain node.</summary>
    public List<BomStack> Stacks(int recipe, int[] picks)
    {
        var index = Items.Index;
        var stacks = new List<BomStack>(picks.Length);
        for (var s = 0; s < picks.Length; s++)
        {
            var at = index.AlternativeAt(recipe, s, picks[s]);
            stacks.Add(new BomStack(IdOf(index.AlternativeItem[at]), index.AlternativeAmount[at]));
        }
        return stacks;
    }
}

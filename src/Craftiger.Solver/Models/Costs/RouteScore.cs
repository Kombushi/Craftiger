namespace Craftiger.Solver.Models.Costs;

/// <summary>The tie-break key of a recipe over its chosen inputs: worst form rank, deepest chain, heaviest chosen leaf, then catalyst slots wearing a tool; lexicographically smaller is better.</summary>
public readonly record struct RouteScore(int Rank, int Depth, double Weight, int Tools) : IComparable<RouteScore>
{
    public int CompareTo(RouteScore other)
    {
        var byRank = Rank.CompareTo(other.Rank);
        if (byRank != 0)
        {
            return byRank;
        }
        var byDepth = Depth.CompareTo(other.Depth);
        if (byDepth != 0)
        {
            return byDepth;
        }
        var byWeight = Weight.CompareTo(other.Weight);
        return byWeight != 0 ? byWeight : Tools.CompareTo(other.Tools);
    }

    public bool Beats(RouteScore other) => CompareTo(other) < 0;
}

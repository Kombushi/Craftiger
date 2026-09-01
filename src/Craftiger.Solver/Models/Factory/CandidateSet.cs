namespace Craftiger.Solver.Models.Factory;

/// <summary>The recipes entering the model, in position order; Pruned says the cost band dropped something.</summary>
public sealed record CandidateSet(IReadOnlyList<int> Candidates, bool Pruned)
{
    public bool Contains(int recipe) => Candidates.Contains(recipe);
}

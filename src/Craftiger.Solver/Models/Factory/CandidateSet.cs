namespace Craftiger.Solver.Models.Factory;

/// <summary>The recipes entering the model, in position order; Cone is the consume targets' downstream recipes; Pruned says the cost band dropped something.</summary>
public sealed record CandidateSet(IReadOnlyList<int> Candidates, IReadOnlySet<int> Cone, bool Pruned)
{
    public bool Contains(int recipe) => Candidates.Contains(recipe);
}

using System.Collections.Immutable;

namespace Craftiger.Solver.Models.Factory;

/// <summary>Per item position, whether the garage-legal fixpoint reaches it from the seeds.</summary>
public sealed record AutoInfiniteItems(ImmutableArray<bool> Flags)
{
    public bool Contains(int item) => Flags[item];
}

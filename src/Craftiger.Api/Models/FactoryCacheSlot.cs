using Craftiger.Solver.Models.Factory;

namespace Craftiger.Api.Models;

/// <summary>One slot of the in-process factory cache: the lazily started work every concurrent request awaits, and the tick of its last use for eviction.</summary>
internal sealed class FactoryCacheSlot(Lazy<Task<FactoryPlan>> plan)
{
    public Lazy<Task<FactoryPlan>> Plan { get; } = plan;

    public long LastUsed { get; set; }

    /// <summary>Whether the work has finished, whatever its outcome — only such a slot may be evicted.</summary>
    public bool IsSettled => Plan.IsValueCreated && Plan.Value.IsCompleted;
}

namespace Craftiger.Api.Models;

/// <summary>One slot of the in-process solve cache: the lazily started work every concurrent request awaits, and the tick of its last use for eviction.</summary>
internal sealed class SolveCacheSlot(Lazy<Task<SolveEntry>> entry)
{
    public Lazy<Task<SolveEntry>> Entry { get; } = entry;

    public long LastUsed { get; set; }

    /// <summary>Whether the work has finished, whatever its outcome — only such a slot may be evicted.</summary>
    public bool IsSettled => Entry.IsValueCreated && Entry.Value.IsCompleted;
}

namespace Craftiger.Api.Models;

/// <summary>One slot of the solve cache: the lazily computed entry — so a solve runs once per
/// id while every concurrent request for it waits on the same computation — and the tick of
/// its last use, for eviction.</summary>
internal sealed class SolveCacheSlot(Lazy<SolveEntry> entry)
{
    public Lazy<SolveEntry> Entry { get; } = entry;

    public long LastUsed { get; set; }
}

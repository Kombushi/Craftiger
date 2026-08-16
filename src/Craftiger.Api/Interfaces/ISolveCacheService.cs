using Craftiger.Api.Models;

namespace Craftiger.Api.Interfaces;

/// <summary>Runs or reuses cost solves, keyed by the settings that shape them — pins never
/// enter the key. A missing solveId means the entry was evicted and the client re-posts.</summary>
public interface ISolveCacheService
{
    SolveResponse Solve(SolveRequest request);

    SolveEntry? Get(string solveId);
}

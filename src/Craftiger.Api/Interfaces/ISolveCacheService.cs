using Craftiger.Api.Models;

namespace Craftiger.Api.Interfaces;

/// <summary>Runs or reuses cost solves, keyed by the settings that shape them — pins never
/// enter the key. Entries live in this process and in the solve store behind it; a solveId
/// neither knows means the entry is gone and the client re-posts.</summary>
public interface ISolveCacheService
{
    Task<SolveResponse> SolveAsync(SolveRequest request);

    Task<SolveEntry?> GetAsync(string solveId);
}

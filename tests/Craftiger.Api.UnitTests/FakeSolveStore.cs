using System.Collections.Concurrent;
using Craftiger.Api.Interfaces;

namespace Craftiger.Api.UnitTests;

/// <summary>An in-memory stand-in for the Valkey store, shared between cache instances the way a server is shared between replicas.</summary>
public sealed class FakeSolveStore : ISolveStore
{
    public ConcurrentDictionary<string, byte[]> Entries { get; } = new();

    public int Writes { get; private set; }

    public Task<byte[]?> GetAsync(string solveId) =>
        Task.FromResult(Entries.GetValueOrDefault(solveId));

    public Task PutAsync(string solveId, byte[] payload)
    {
        Writes++;
        Entries[solveId] = payload;
        return Task.CompletedTask;
    }
}

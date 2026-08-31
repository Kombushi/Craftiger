using System.Net.Http.Json;
using Craftiger.Api.Models;
using Craftiger.Api.Repositories;
using Microsoft.Data.Sqlite;

namespace Craftiger.Api.UnitTests;

public sealed class FactoryCacheTests : IDisposable
{
    private readonly string _dir;

    public FactoryCacheTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "craftiger-factory-cache-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        ApiFixture.WriteArtifact(
            Path.Combine(_dir, "planner.sqlite"),
            schemaVersion: PlannerArtifactRepository.SupportedSchemaVersion);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }

    private static async Task<FactoryResponse> SolveAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/factory/solve", new
        {
            garage = new { defaultTier = 3 },
            b = 4,
            targets = new[] { new { kind = "produce", itemId = "wire", rate = 1.6 } },
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FactoryResponse>())!;
    }

    [Fact]
    public async Task AReplicaServesAStoredPlanWithoutResolving()
    {
        var store = new FakeSolveStore();
        string factoryId;
        using (var first = ApiFixture.Create(_dir, store))
        {
            factoryId = (await SolveAsync(first.CreateClient())).FactoryId;
        }
        Assert.Contains("factory:" + factoryId, store.Entries.Keys);
        var writes = store.Writes;

        using var second = ApiFixture.Create(_dir, store);
        var served = await SolveAsync(second.CreateClient());

        Assert.Equal(factoryId, served.FactoryId);
        Assert.NotEmpty(served.Lines);
        // Served straight from the store: no cost solve ran, so nothing new was written.
        Assert.Equal(writes, store.Writes);
    }
}

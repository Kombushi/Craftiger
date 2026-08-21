using Craftiger.Api.Models;
using Craftiger.Api.Repositories;
using Craftiger.Api.Services;
using Craftiger.Solver.Interfaces;
using Craftiger.Solver.Models;
using Craftiger.Solver.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Craftiger.Api.UnitTests;

public sealed class SolveCacheTests : IDisposable
{
    private readonly string _dir;
    private readonly PlannerArtifact _artifact;
    private readonly CountingSolver _solver;
    private readonly FakeSolveStore _store = new();

    public SolveCacheTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "craftiger-cache-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        ApiFixture.WriteArtifact(Path.Combine(_dir, "planner.sqlite"), schemaVersion: 6);
        var rules = new GarageRulesOptions().ToRules();
        _artifact = new PlannerArtifactRepository(rules, NullLogger<PlannerArtifactRepository>.Instance).Load(_dir);
        _solver = new CountingSolver(new CostSolverService(
            new LeafWeightService(), new GarageLegalityService(rules), new SolverPreferencesOptions().ToPreferences()));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private SolveCacheService Cache(int capacity = 16) => new(
        _artifact, _solver, _store, new SolveEntryCodec(_artifact),
        Options.Create(new ApiOptions { SolveCacheSize = capacity }), NullLogger<SolveCacheService>.Instance);

    private static SolveRequest Request(int tier) =>
        new(new GarageDto(tier, new Dictionary<string, int?>(), [], new Dictionary<string, string>()), 4, null);

    [Fact]
    public async Task ConcurrentIdenticalRequestsSolveOnce()
    {
        var cache = Cache();

        var responses = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => cache.SolveAsync(Request(3)))));

        Assert.Equal(1, _solver.Calls);
        Assert.All(responses, response => Assert.Equal(responses[0].SolveId, response.SolveId));
    }

    [Fact]
    public async Task TheLeastRecentlyUsedEntryIsEvictedFirst()
    {
        var cache = Cache(capacity: 2);
        var first = (await cache.SolveAsync(Request(1))).SolveId;
        var second = (await cache.SolveAsync(Request(2))).SolveId;

        Assert.NotNull(await cache.GetAsync(first));
        var third = (await cache.SolveAsync(Request(3))).SolveId;

        // The evicted entry is still in the store, so it reads through rather than vanishing.
        _store.Entries.Clear();
        Assert.NotNull(await cache.GetAsync(first));
        Assert.Null(await cache.GetAsync(second));
        Assert.NotNull(await cache.GetAsync(third));
    }

    [Fact]
    public async Task AFailedSolveIsNotCachedAndSurfaces()
    {
        var cache = Cache();
        _solver.FailNext = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.SolveAsync(Request(3)));
        var response = await cache.SolveAsync(Request(3));

        Assert.Equal(2, _solver.Calls);
        Assert.NotNull(await cache.GetAsync(response.SolveId));
    }

    [Fact]
    public async Task ASolveIsWrittenToTheStore()
    {
        var cache = Cache();

        var response = await cache.SolveAsync(Request(3));
        await WaitForWrites(1);

        var stored = new SolveEntryCodec(_artifact).Decode(_store.Entries[response.SolveId]);
        Assert.NotNull(stored);
        Assert.Equal(response.PricedItems, stored.ReachableCount);
    }

    [Fact]
    public async Task AnotherProcessReadsASolveThroughFromTheStore()
    {
        var first = Cache();
        var response = await first.SolveAsync(Request(3));
        await WaitForWrites(1);

        var second = Cache();
        var entry = await second.GetAsync(response.SolveId);
        var again = await second.SolveAsync(Request(3));

        Assert.NotNull(entry);
        Assert.Equal(response.SolveId, again.SolveId);
        Assert.Equal(1, _solver.Calls);
    }

    [Fact]
    public async Task AStoredEntryOfAnotherArtifactIsRecomputed()
    {
        var cache = Cache();
        var solveId = (await cache.SolveAsync(Request(3))).SolveId;
        await WaitForWrites(1);
        // The same solve as another build would have stored it, then as damaged bytes.
        var otherBuild = new SolveEntryCodec(_artifact with { BuildId = "another-build" });
        _store.Entries[solveId] = otherBuild.Encode((await cache.GetAsync(solveId))!);
        var stranger = Cache();

        var fromOtherBuild = await stranger.GetAsync(solveId);
        _store.Entries[solveId][^1] ^= 0xFF;
        var damaged = await stranger.GetAsync(solveId);
        var response = await stranger.SolveAsync(Request(3));

        Assert.Null(fromOtherBuild);
        Assert.Null(damaged);
        Assert.Equal(solveId, response.SolveId);
        Assert.Equal(2, _solver.Calls);
    }

    private async Task WaitForWrites(int count)
    {
        for (var i = 0; i < 100 && _store.Writes < count; i++)
        {
            await Task.Delay(10);
        }
        Assert.Equal(count, _store.Writes);
    }

    private sealed class CountingSolver(ICostSolverService inner) : ICostSolverService
    {
        private int _calls;

        public int Calls => _calls;

        public bool FailNext { get; set; }

        public CostTable Solve(SolverGraph graph, Garage garage, WeightSettings weights)
        {
            Interlocked.Increment(ref _calls);
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException("simulated failure");
            }
            return inner.Solve(graph, garage, weights);
        }

        public double Candidate(CostTable table, int recipe, string itemId) =>
            inner.Candidate(table, recipe, itemId);
    }
}

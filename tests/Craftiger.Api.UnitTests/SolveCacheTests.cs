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

    public SolveCacheTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "craftiger-cache-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        ApiFixture.WriteArtifact(Path.Combine(_dir, "planner.sqlite"), schemaVersion: 5);
        var rules = new GarageRulesOptions().ToRules();
        _artifact = new PlannerArtifactRepository(rules, NullLogger<PlannerArtifactRepository>.Instance).Load(_dir);
        _solver = new CountingSolver(new CostSolverService(
            new LeafWeightService(), new GarageLegalityService(rules), new SolverPreferencesOptions().ToPreferences()));
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private SolveCacheService Cache(int capacity) => new(
        _artifact, _solver, Options.Create(new ApiOptions { SolveCacheSize = capacity }),
        NullLogger<SolveCacheService>.Instance);

    private static SolveRequest Request(int tier) =>
        new(new GarageDto(tier, new Dictionary<string, int?>(), [], new Dictionary<string, string>()), 4, null);

    [Fact]
    public void ConcurrentIdenticalRequestsSolveOnce()
    {
        var cache = Cache(16);

        var responses = new SolveResponse[32];
        Parallel.For(0, responses.Length, i => responses[i] = cache.Solve(Request(3)));

        Assert.Equal(1, _solver.Calls);
        Assert.All(responses, response => Assert.Equal(responses[0].SolveId, response.SolveId));
    }

    [Fact]
    public void TheLeastRecentlyUsedEntryIsEvictedFirst()
    {
        var cache = Cache(2);
        var first = cache.Solve(Request(1)).SolveId;
        var second = cache.Solve(Request(2)).SolveId;

        Assert.NotNull(cache.Get(first));
        var third = cache.Solve(Request(3)).SolveId;

        Assert.NotNull(cache.Get(first));
        Assert.Null(cache.Get(second));
        Assert.NotNull(cache.Get(third));
    }

    [Fact]
    public void AFailedSolveIsNotCached()
    {
        var cache = Cache(16);
        _solver.FailNext = true;

        Assert.Throws<InvalidOperationException>(() => cache.Solve(Request(3)));
        var response = cache.Solve(Request(3));

        Assert.Equal(2, _solver.Calls);
        Assert.NotNull(cache.Get(response.SolveId));
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

        public double Candidate(CostTable table, SolverRecipe recipe, string itemId) =>
            inner.Candidate(table, recipe, itemId);
    }
}

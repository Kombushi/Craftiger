using Craftiger.Api.Models;
using Craftiger.Api.Repositories;
using Craftiger.Api.Services;
using Craftiger.Solver.Models.Costs;
using Craftiger.Solver.Models.Options;
using Craftiger.Solver.Services.Costs;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace Craftiger.Api.UnitTests;

public sealed class SolveEntryCodecTests : IDisposable
{
    private readonly string _dir;
    private readonly PlannerArtifact _artifact;
    private readonly SolveEntry _entry;

    public SolveEntryCodecTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "craftiger-codec-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        ApiFixture.WriteArtifact(Path.Combine(_dir, "planner.sqlite"), schemaVersion: PlannerArtifactRepository.SupportedSchemaVersion);
        var rules = Options.Create(new GarageRules());
        _artifact = new PlannerArtifactRepository(new FactoryArtifactReader(), new GarageLegalityService(rules), NullLogger<PlannerArtifactRepository>.Instance).Load(_dir);
        var solver = ApiFixture.CostSolver(rules);
        var garage = new Garage(
            3, new Dictionary<string, int?> { ["Extruder"] = null, ["Wiremill"] = 2 },
            new HashSet<string> { "Electric Blast Furnace" }, new Dictionary<string, int> { ["Electric Blast Furnace"] = 2700 });
        var weights = new WeightSettings(5, new Dictionary<string, double> { ["sil"] = 7.5 });
        var table = solver.Solve(_artifact.Graph, garage, weights);
        _entry = new SolveEntry(table, garage, weights, [3, 1, 4, 0, 2, 5, 6, 7, 8, 9], 6);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void AnEntrySurvivesTheRoundTrip()
    {
        var codec = new SolveEntryCodec(_artifact);

        var decoded = codec.Decode(codec.Encode(_entry));

        Assert.NotNull(decoded);
        Assert.Equal(_entry.ReachableCount, decoded.ReachableCount);
        Assert.Equal(_entry.Sorted, decoded.Sorted);
        Assert.Equal(_entry.Table.Converged, decoded.Table.Converged);
        Assert.Equal(_entry.Table.PricedCount, decoded.Table.PricedCount);
        for (var item = 0; item < _artifact.Graph.Index.ItemCount; item++)
        {
            Assert.Equal(_entry.Table.Cost(item), decoded.Table.Cost(item));
            Assert.Equal(_entry.Table.BestRecipe(item), decoded.Table.BestRecipe(item));
            Assert.Equal(_entry.Table.Picks(item).ToArray(), decoded.Table.Picks(item).ToArray());
        }
        Assert.Equal(_entry.Garage.DefaultTier, decoded.Garage.DefaultTier);
        Assert.Equal(_entry.Garage.MachineTiers, decoded.Garage.MachineTiers);
        Assert.Equal(_entry.Garage.BuiltMultiblocks, decoded.Garage.BuiltMultiblocks);
        Assert.Equal(_entry.Garage.CoilHeat, decoded.Garage.CoilHeat);
        Assert.Equal(_entry.Weights.PriceBase, decoded.Weights.PriceBase);
        Assert.Equal(_entry.Weights.ItemWeights, decoded.Weights.ItemWeights);
    }

    [Fact]
    public void AnotherBuildsEntryIsRefused()
    {
        var codec = new SolveEntryCodec(_artifact);
        var stranger = new SolveEntryCodec(_artifact with { BuildId = "another-build" });

        Assert.Null(codec.Decode(stranger.Encode(_entry)));
        Assert.NotNull(stranger.Decode(stranger.Encode(_entry)));
    }

    [Fact]
    public void AnEarlierFormatIsRefused()
    {
        var codec = new SolveEntryCodec(_artifact);
        var payload = codec.Encode(_entry);
        // Same magic, previous format version: the body is unreadable to this reader.
        BitConverter.GetBytes(1).CopyTo(payload, sizeof(int));

        Assert.Null(codec.Decode(payload));
    }

    [Fact]
    public void TheStoredFormIsCompressed()
    {
        var codec = new SolveEntryCodec(_artifact);
        // The fixture's table is tiny; a few thousand weight overrides give the frame something
        // to squeeze, and they must come out smaller than their doubles alone.
        var weights = Enumerable.Range(0, 5000).ToDictionary(i => $"item-{i:D5}", _ => 7.5);
        var bulky = _entry with { Weights = new WeightSettings(5, weights) };

        var payload = codec.Encode(bulky);

        Assert.True(payload.Length < weights.Count * sizeof(double), $"{payload.Length} bytes for {weights.Count} weights");
        Assert.Equal(weights, codec.Decode(payload)!.Weights.ItemWeights);
    }

    [Fact]
    public void TruncatedBytesAreRefused()
    {
        var codec = new SolveEntryCodec(_artifact);
        var payload = codec.Encode(_entry);

        Assert.Null(codec.Decode(payload[..(payload.Length / 2)]));
        Assert.Null(codec.Decode([]));
    }
}

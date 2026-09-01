using Craftiger.Api.Models;
using Craftiger.Api.Repositories;
using Craftiger.Api.Services;
using Craftiger.Solver.Models.Factory;
using Craftiger.Solver.Models.Options;
using Craftiger.Solver.Services.Costs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Craftiger.Api.UnitTests;

public sealed class FactoryPlanCodecTests : IDisposable
{
    private readonly string _dir;
    private readonly PlannerArtifact _artifact;

    public FactoryPlanCodecTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "craftiger-factory-codec-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        ApiFixture.WriteArtifact(Path.Combine(_dir, "planner.sqlite"), schemaVersion: PlannerArtifactRepository.SupportedSchemaVersion);
        var rules = Options.Create(new GarageRules());
        _artifact = new PlannerArtifactRepository(
            new FactoryArtifactReader(), new GarageLegalityService(rules),
            NullLogger<PlannerArtifactRepository>.Instance).Load(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static FactoryPlan Plan() => new(
        FactoryPlanStatus.Solved,
        [new FactoryLine(
            "r_wire", "Wiremill", "wiremill-lv", 1.6, 2, 4, 0.5, false, false, 1.25, 640,
            [new FactoryLineFlow("ing", 1.6)], [new FactoryLineFlow("wire", 3.2)])],
        [new FactoryItemFlow("wire", 1.6, 0, 0.4, AutoInfinite: false)],
        [new FactoryInflow("ing", 0.8, 4, AutoInfinite: true)],
        [FactoryWarning.RoutesPruned(), FactoryWarning.InfeasibleItem("rod")],
        3.2, 640, 0, 0.5);

    [Fact]
    public void APlanSurvivesTheRoundTrip()
    {
        var codec = new FactoryPlanCodec(_artifact);

        var decoded = codec.Decode(codec.Encode(Plan()));

        Assert.NotNull(decoded);
        Assert.Equivalent(Plan(), decoded, strict: true);
        Assert.Equal(320, decoded.Lines[0].LineEuT);
    }

    [Fact]
    public void AnotherBuildsPlanIsRefused()
    {
        var codec = new FactoryPlanCodec(_artifact);
        var stranger = new FactoryPlanCodec(_artifact with { BuildId = "another-build" });

        Assert.Null(codec.Decode(stranger.Encode(Plan())));
        Assert.NotNull(stranger.Decode(stranger.Encode(Plan())));
    }

    [Fact]
    public void ADamagedValueIsRefused()
    {
        var codec = new FactoryPlanCodec(_artifact);
        var payload = codec.Encode(Plan());
        // Same magic, previous format version: the body is unreadable to this reader.
        BitConverter.GetBytes(1).CopyTo(payload, sizeof(int));

        Assert.Null(codec.Decode([1, 2, 3]));
        Assert.Null(codec.Decode(payload));
    }
}

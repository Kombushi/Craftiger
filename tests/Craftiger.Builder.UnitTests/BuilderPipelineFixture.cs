namespace Craftiger.Builder.UnitTests;

public sealed class BuilderPipelineFixture : IDisposable
{
    private readonly FixtureRun _run = new();

    public string PlannerPath => _run.PlannerPath;

    public void Dispose() => _run.Dispose();

    public T Scalar<T>(string sql) => _run.Scalar<T>(sql);
}

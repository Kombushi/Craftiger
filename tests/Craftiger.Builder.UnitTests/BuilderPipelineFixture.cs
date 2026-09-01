namespace Craftiger.Builder.UnitTests;

public sealed class BuilderPipelineFixture : IDisposable
{
    private readonly FixtureRun _run = new();

    public void Dispose() => _run.Dispose();

    public T Scalar<T>(string sql) => _run.Scalar<T>(sql);
}

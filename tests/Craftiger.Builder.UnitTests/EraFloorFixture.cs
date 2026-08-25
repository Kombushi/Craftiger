namespace Craftiger.Builder.UnitTests;

/// <summary>The same pack with an era floor under the mixer, proving a quest-anchored gate outranks the recipe graph.</summary>
public sealed class EraFloorFixture : IDisposable
{
    private readonly FixtureRun _run =
        new(new KeyValuePair<string, string?>("ErasConfiguration:MachineEraFloors:Mixer", "5"));

    public void Dispose() => _run.Dispose();

    public T Scalar<T>(string sql) => _run.Scalar<T>(sql);
}

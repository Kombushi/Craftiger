namespace Craftiger.Builder.UnitTests;

/// <summary>The same pack with the recycling exclusion off, so the fixture's widget arc loop ships and the price check must catch it.</summary>
public sealed class LeakyPipelineFixture : IDisposable
{
    private readonly FixtureRun _run =
        new(new KeyValuePair<string, string?>(
            "RecipesConfiguration:RecyclingCategorySuffixes:0", "matches-no-category"));

    public void Dispose() => _run.Dispose();

    public T Scalar<T>(string sql) => _run.Scalar<T>(sql);
}

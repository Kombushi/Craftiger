namespace Craftiger.Builder.UnitTests;

/// <summary>The same pack with one recipe condemned as a phantom registration, proving the exclusion reaches the artifact.</summary>
public sealed class PhantomRecipeFixture : IDisposable
{
    private readonly FixtureRun _run =
        new(new KeyValuePair<string, string?>(
            "RecipesConfiguration:PhantomRecipeIds:r_melt", "fixture-only condemnation"));

    public void Dispose() => _run.Dispose();

    public T Scalar<T>(string sql) => _run.Scalar<T>(sql);
}

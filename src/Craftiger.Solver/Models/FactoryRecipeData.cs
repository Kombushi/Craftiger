namespace Craftiger.Solver.Models;

/// <summary>Per-recipe rate data the cost engine never reads, aligned to recipe positions.
/// A duration of 0 marks a durationless recipe — a free, instant converter the factory flags;
/// <paramref name="EuT"/> is per-amp voltage, so draw is <c>eu_t × amps</c>.</summary>
public sealed record FactoryRecipeData(long[] DurationTicks, long[] EuT, long[] Amps)
{
    /// <summary>The fixtures' way in: unlisted recipes are durationless, zero-EU, one-amp.</summary>
    public static FactoryRecipeData Build(
        SolverIndex index,
        IReadOnlyDictionary<string, (long DurationTicks, long EuT, long Amps)>? recipes = null)
    {
        var duration = new long[index.RecipeCount];
        var euT = new long[index.RecipeCount];
        var amps = new long[index.RecipeCount];
        Array.Fill(amps, 1);

        foreach (var (recipeId, values) in recipes ?? new Dictionary<string, (long, long, long)>())
        {
            if (index.TryGetRecipe(recipeId, out var recipe))
            {
                duration[recipe] = values.DurationTicks;
                euT[recipe] = values.EuT;
                amps[recipe] = values.Amps;
            }
        }
        return new FactoryRecipeData(duration, euT, amps);
    }
}

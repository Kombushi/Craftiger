using System.Collections.Immutable;
using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Models.Factory;

/// <summary>Per-recipe rate data aligned to recipe positions: zero duration marks a free instant converter, EuT is per amp.</summary>
public sealed record FactoryRecipeData(ImmutableArray<long> DurationTicks, ImmutableArray<long> EuT, ImmutableArray<long> Amps)
{
    public bool IsDurationless(int recipe) => DurationTicks[recipe] == 0;

    /// <summary>The recipe's full draw per tick: voltage times amps.</summary>
    public long DrawPerTick(int recipe) => EuT[recipe] * Amps[recipe];

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
        return new FactoryRecipeData([.. duration], [.. euT], [.. amps]);
    }
}

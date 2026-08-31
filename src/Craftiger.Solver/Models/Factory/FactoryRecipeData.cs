using System.Collections.Immutable;
using Craftiger.Solver.Models.Graph;

namespace Craftiger.Solver.Models.Factory;

/// <summary>Per-recipe rate data aligned to recipe positions: zero duration marks a free instant converter, EuT is per amp, Overclock names the ladder a recipe climbs, the flags name the environment a recipe needs.</summary>
public sealed record FactoryRecipeData(
    ImmutableArray<long> DurationTicks,
    ImmutableArray<long> EuT,
    ImmutableArray<long> Amps,
    ImmutableArray<OverclockMode> Overclock,
    ImmutableArray<bool> Cleanroom,
    ImmutableArray<bool> LowGravity)
{
    public bool IsDurationless(int recipe) => DurationTicks[recipe] == 0;

    /// <summary>The recipe's full draw per tick: voltage times amps.</summary>
    public long DrawPerTick(int recipe) => EuT[recipe] * Amps[recipe];

    public OverclockMode OverclockOf(int recipe) => Overclock[recipe];

    public bool NeedsCleanroom(int recipe) => Cleanroom[recipe];

    public bool NeedsLowGravity(int recipe) => LowGravity[recipe];

    /// <summary>The fixtures' way in: unlisted recipes are durationless, zero-EU, one-amp, on the standard ladder.</summary>
    public static FactoryRecipeData Build(
        SolverIndex index,
        IReadOnlyDictionary<string, (long DurationTicks, long EuT, long Amps)>? recipes = null,
        IEnumerable<string>? treeFarms = null,
        IReadOnlyDictionary<string, OverclockMode>? overclocks = null,
        IEnumerable<string>? cleanroom = null,
        IEnumerable<string>? lowGravity = null)
    {
        var duration = new long[index.RecipeCount];
        var euT = new long[index.RecipeCount];
        var amps = new long[index.RecipeCount];
        var overclock = new OverclockMode[index.RecipeCount];
        var needsCleanroom = new bool[index.RecipeCount];
        var needsLowGravity = new bool[index.RecipeCount];
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
        foreach (var recipeId in treeFarms ?? [])
        {
            if (index.TryGetRecipe(recipeId, out var recipe))
            {
                overclock[recipe] = OverclockMode.TreeFarm;
            }
        }
        foreach (var (recipeId, mode) in overclocks ?? new Dictionary<string, OverclockMode>())
        {
            if (index.TryGetRecipe(recipeId, out var recipe))
            {
                overclock[recipe] = mode;
            }
        }
        foreach (var recipeId in cleanroom ?? [])
        {
            if (index.TryGetRecipe(recipeId, out var recipe))
            {
                needsCleanroom[recipe] = true;
            }
        }
        foreach (var recipeId in lowGravity ?? [])
        {
            if (index.TryGetRecipe(recipeId, out var recipe))
            {
                needsLowGravity[recipe] = true;
            }
        }
        return new FactoryRecipeData(
            [.. duration], [.. euT], [.. amps], [.. overclock], [.. needsCleanroom], [.. needsLowGravity]);
    }
}

using Craftiger.Solver.Models.Costs;

namespace Craftiger.Solver.Models.Factory;

/// <summary>The pack's environment walls: recipes flagged for a cleanroom or low gravity wait for these eras, and a hosting cleanroom draws power for as long as its lines run.</summary>
public sealed record FactoryEnvironment(string CleanroomItemId, int CleanroomEra, int LowGravityEra)
{
    /// <summary>The synthetic id of the hosting line; at most one cleanroom serves a factory.</summary>
    public const string CleanroomLineId = "cleanroom";

    /// <summary>No walls: era zero reaches every garage.</summary>
    public static readonly FactoryEnvironment None = new("", 0, 0);

    /// <summary>Whether the garage's era reaches every environment the recipe needs.</summary>
    public bool Admits(FactoryRecipeData recipes, int recipe, Garage garage) =>
        (!recipes.NeedsCleanroom(recipe) || garage.Reaches(CleanroomEra))
        && (!recipes.NeedsLowGravity(recipe) || garage.Reaches(LowGravityEra));

    /// <summary>The warm cleanroom's steady draw: a tenth of its 40 EU/t recipe overclocked by a hatch of the garage's tier.</summary>
    public long CleanroomDrawEuT(int hatchTier) => hatchTier <= 2 ? 4 : 4L << (2 * (hatchTier - 2));
}

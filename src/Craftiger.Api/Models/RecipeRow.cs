namespace Craftiger.Api.Models;

/// <summary>A <c>recipes</c> row of planner.sqlite as read at load.</summary>
internal sealed record RecipeRow(
    string Id,
    string Machine,
    long Tier,
    long? MultiTier,
    long? Heat,
    long DurationTicks,
    long EuT);

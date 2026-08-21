namespace Craftiger.Api.Models;

/// <summary>A <c>recipe_outputs</c> row of planner.sqlite as read at load.</summary>
internal sealed record OutputRow(string RecipeId, string ItemId, long Amount, double Chance);

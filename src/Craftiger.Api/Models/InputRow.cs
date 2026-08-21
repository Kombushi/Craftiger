namespace Craftiger.Api.Models;

/// <summary>A <c>recipe_inputs</c> row of planner.sqlite as read at load.</summary>
internal sealed record InputRow(string RecipeId, string ItemId, long Amount, long Slot, long Catalyst);
